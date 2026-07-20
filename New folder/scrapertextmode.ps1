# =============================================================================
# EWRC Scraper - Debug / Text-Only Version
# No WinForms / GUI dependencies. Run in any PowerShell 5.1+ or PS7+ terminal.
# =============================================================================
# HOW TO USE:
#   1. Set the variables in the CONFIG section below
#   2. Run the whole script, or dot-source it:  . .\ewrc-scraper-debug.ps1
#   3. Then call individual functions manually, e.g.:
#        $events = Find-RallyEvents -Year 2025
#        $events | Format-Table
#        Get-RallyEntries -EventUrl "https://ewrc-results.com/event/12345-some-rally/final-results"
# =============================================================================

#region --- LOAD WINFORMS (needed only for type-casting, not for GUI) ---------
# This prevents the "Unable to find type" errors without opening any windows
Add-Type -AssemblyName System.Windows.Forms | Out-Null
Add-Type -AssemblyName System.Drawing       | Out-Null
#endregion

#region --- CONFIG ------------------------------------------------------------
$ConfigFile = "$PSScriptRoot\ewrc-scraper.json"

$DefaultConfig = @{
    EWRCBaseURL          = "https://ewrc-results.com/"
    CountryNLnumber      = "24"
    CountryBELnumber     = "25"
    CountryGERnumber     = "10"
    RallyEventUrlBase    = "https://ewrc-results.com/event/"
    RallySeasonSearchURLs = @(
        "https://ewrc-results.com/season/YEAR/25-nederland?nat=24",
        "https://ewrc-results.com/season/YEAR/1363-nederland-historic?nat=24",
        "https://ewrc-results.com/season/YEAR/86-nederland-others?nat=24"
    )
}

# Load config from JSON if it exists, otherwise use defaults
if (Test-Path -LiteralPath $ConfigFile) {
    try {
        $Config = Get-Content -LiteralPath $ConfigFile -Raw -Encoding utf8 | ConvertFrom-Json
        Write-Host "[CONFIG] Loaded from $ConfigFile" -ForegroundColor Green
    } catch {
        Write-Warning "[CONFIG] Failed to parse JSON, using defaults. Error: $_"
        $Config = [pscustomobject]$DefaultConfig
    }
} else {
    Write-Warning "[CONFIG] No config file found at $ConfigFile, using defaults."
    $Config = [pscustomobject]$DefaultConfig
}

# Active season search URLs (can be changed interactively)
$global:ActiveSeasonURLs = @($Config.RallySeasonSearchURLs)
#endregion

#region --- HELPER: JSON file functions ---------------------------------------
function Write-JsonFile {
    param (
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$InputObject,
        [int]$Depth = 10
    )
    try {
        $dir = Split-Path $Path -Parent
        if ($dir -and -not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
        $InputObject | ConvertTo-Json -Depth $Depth | Set-Content -LiteralPath $Path -Encoding utf8
        return $true
    } catch { return $_ }
}

function Read-JsonFile {
    param (
        [Parameter(Mandatory)][string]$Path,
        [switch]$AsHashtable
    )
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    try {
        $content = Get-Content -LiteralPath $Path -Raw -Encoding utf8
        if ($AsHashtable) { return $content | ConvertFrom-Json -AsHashtable }
        else               { return $content | ConvertFrom-Json }
    } catch { return $false }
}
#endregion

#region --- HELPER: HTTP with error handling ----------------------------------
function Invoke-EWRCRequest {
    <#
    .SYNOPSIS
        Wrapper around Invoke-WebRequest with error handling and debug output.
    .PARAMETER Url
        The URL to fetch.
    .PARAMETER Label
        Short label shown in debug output so you know which call is running.
    #>
    param(
        [Parameter(Mandatory)][string]$Url,
        [string]$Label = "Request"
    )
    Write-Host "[HTTP] $Label => $Url" -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -ErrorAction Stop
        Write-Host "[HTTP] OK  Status=$($response.StatusCode)  ContentLength=$($response.RawContentLength)" -ForegroundColor Green
        return $response
    } catch {
        Write-Warning "[HTTP] FAILED: $_"
        return $null
    }
}
#endregion

#region --- FUNCTION: Find-RallyEvents ----------------------------------------
function Find-RallyEvents {
    <#
    .SYNOPSIS
        Scrapes the EWRC season page(s) and returns a list of rally event objects.
    .PARAMETER Year
        The season year (default: current year).
    .PARAMETER SeasonURLs
        Array of season search URL templates. Uses $global:ActiveSeasonURLs if omitted.
        Use YEAR as placeholder, e.g. "https://ewrc-results.com/season/YEAR/25-nederland?nat=24"
    .EXAMPLE
        $events = Find-RallyEvents -Year 2025
        $events | Format-Table -AutoSize
    #>
    param(
        [int]$Year = (Get-Date).Year,
        [string[]]$SeasonURLs = $global:ActiveSeasonURLs
    )

    Write-Host "`n=== Find-RallyEvents (Year=$Year) ===" -ForegroundColor Yellow

    $allLinks = @()
    foreach ($template in $SeasonURLs) {
        $url = $template.Replace("YEAR", $Year)
        $response = Invoke-EWRCRequest -Url $url -Label "SeasonPage"
        if (-not $response) { continue }

        # Grab all /event/ and /events/ hrefs
        $eventLinks = $response.Links.href | Where-Object { $_ -like "*event*" }
        Write-Host "[PARSE] Found $($eventLinks.Count) event-related links on this page" -ForegroundColor DarkGray

        # Debug: show raw links so you can see what the site currently returns
        if ($eventLinks.Count -gt 0) {
            Write-Host "[RAW LINKS from $url]" -ForegroundColor DarkYellow
            $eventLinks | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkYellow }
        }

        $allLinks += $eventLinks
    }

    $rallyObjects = ConvertTo-RallyInfo -Url $allLinks -BaseUrl $Config.EWRCBaseURL

    Write-Host "`n[RESULT] Found $($rallyObjects.Count) rally events:" -ForegroundColor Green
    $rallyObjects | Format-Table -AutoSize

    return $rallyObjects
}
#endregion

#region --- FUNCTION: ConvertTo-RallyInfo -------------------------------------
function ConvertTo-RallyInfo {
    <#
    .SYNOPSIS
        Converts raw scraped href links into rally info objects.
        Matches /event/NNN-slug/final-results and /events/NNN-slug patterns.
    .NOTES
        If the site changes its URL structure, this is the function to update.
        Enable -Verbose to see every URL being evaluated.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string[]]$Url,
        [string]$BaseUrl
    )
    begin   { $rallies = @{} }
    process {
        foreach ($u in $Url) {
            $u = $u.Trim()
            if (-not $u) { continue }

            Write-Verbose "  Evaluating: $u"

            # Pattern 1: /event/NNN-slug/final-results
            if ($u -match '^/event/\d+-(.+?)/final-results$') {
                $slug = $Matches[1]
                if (-not $rallies.Contains($slug)) {
                    $rallies[$slug] = [ordered]@{
                        Name      = ($slug -replace '-', ' ')
                        EventUrl  = $null
                        EventsUrl = $null
                    }
                }
                $rallies[$slug].EventUrl = if ($BaseUrl) { $BaseUrl.TrimEnd('/') + $u } else { $u }
                continue
            }

            # Pattern 2: /events/NNN-slug
            if ($u -match '^/events/\d+-(.+)$') {
                $slug = $Matches[1]
                if (-not $rallies.Contains($slug)) {
                    $rallies[$slug] = [ordered]@{
                        Name      = ($slug -replace '-', ' ')
                        EventUrl  = $null
                        EventsUrl = $null
                    }
                }
                $rallies[$slug].EventsUrl = if ($BaseUrl) { $BaseUrl.TrimEnd('/') + $u } else { $u }
                continue
            }

            Write-Verbose "  -> No pattern matched for: $u"
        }
    }
    end {
        $result = foreach ($item in $rallies.Values) {
            if ($null -ne $item.EventUrl) { [pscustomobject]$item }
        }
        return $result
    }
}
#endregion

#region --- FUNCTION: Get-RallyEntries ----------------------------------------
function Get-RallyEntries {
    <#
    .SYNOPSIS
        Fetches the entry list (drivers + co-drivers) for one or more rally events.
    .PARAMETER EventUrl
        The final-results URL for the event, e.g. from Find-RallyEvents.
        Accepts multiple URLs as an array or comma-separated string.
    .PARAMETER Year
        The season year - used to build the /entries URL. Default: current year.
    .EXAMPLE
        $events = Find-RallyEvents -Year 2025
        $entries = Get-RallyEntries -EventUrl $events[0].EventUrl -Year 2025
        $entries | Format-Table -AutoSize
    #>
    param(
        [Parameter(Mandatory)][string[]]$EventUrl,
        [int]$Year = (Get-Date).Year
    )

    Write-Host "`n=== Get-RallyEntries (Year=$Year) ===" -ForegroundColor Yellow

    # Normalise: accept comma-separated string too
    $urls = @($EventUrl) -join "," | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ }

    $allResults = @()

    foreach ($url in $urls) {
        # Convert final-results URL -> entries URL
        $entriesUrl = $url -replace "/final-results", "-$Year/entries"
        $response = Invoke-EWRCRequest -Url $entriesUrl -Label "EntriesPage"
        if (-not $response) { continue }

        # Debug: dump all links so you can see what changed on the site
        Write-Host "`n[RAW LINKS from entries page]" -ForegroundColor DarkYellow
        $response.Links | Where-Object { $_.href -like "*profile*" } |
            ForEach-Object { Write-Host "  href=$($_.href)  innerHTML=$($_.innerHTML)" -ForegroundColor DarkYellow }

        $codrivers = @($response.Links | Where-Object { $_.href -like "*coprofile*" })
        $drivers   = @($response.Links | Where-Object { $_.href -like "*/profile*" -and $_.href -notlike "*coprofile*" })

        Write-Host "[PARSE] Drivers=$($drivers.Count)  Co-drivers=$($codrivers.Count)" -ForegroundColor DarkGray

        # Derive rally name from the URL slug
        $rallyName = "Unknown"
        if ($url -match '/event/\d+-(.+?)/') { $rallyName = ($Matches[1] -replace '-', ' ') }

        foreach ($line in $drivers) {
            $parts        = $line.href.Split("/")
            $driverSegment = if ($parts.Count -gt 2) { $parts[2] } else { $line.href }
            $number       = $driverSegment.Split("-")[0]
            $name         = ($driverSegment -replace "^$number-", "") -replace '-', ' '
            $allResults += [pscustomobject]@{
                Name      = $name.Trim()
                Number    = $number.Trim()
                Type      = "Driver"
                RallyName = $rallyName
                ProfileUrl= $Config.EWRCBaseURL.TrimEnd('/') + $line.href
            }
        }

        foreach ($line in $codrivers) {
            $parts         = $line.href.Split("/")
            $driverSegment = if ($parts.Count -gt 2) { $parts[2] } else { $line.href }
            $number        = $driverSegment.Split("-")[0]
            $name          = ($driverSegment -replace "^$number-", "") -replace '-', ' '
            $allResults += [pscustomobject]@{
                Name      = $name.Trim()
                Number    = $number.Trim()
                Type      = "Co-driver"
                RallyName = $rallyName
                ProfileUrl= $Config.EWRCBaseURL.TrimEnd('/') + $line.href
            }
        }
    }

    Write-Host "`n[RESULT] Total entries: $($allResults.Count)" -ForegroundColor Green
    $allResults | Format-Table -AutoSize

    return $allResults
}
#endregion

#region --- FUNCTION: Find-AllRalliesCalendar ---------------------------------
function Find-AllRalliesCalendar {
    <#
    .SYNOPSIS
        Replicates FindAllRallyButton_Click - scans the EWRC calendar week by week
        and dumps all URLs so you can see what the site currently returns.
    .PARAMETER Year
        Season year. Default: current year.
    .PARAMETER NatCode
        Country code filter (0 = all countries). Default: "0"
    .EXAMPLE
        Find-AllRalliesCalendar -Year 2025 -NatCode 24
    #>
    param(
        [int]$Year    = (Get-Date).Year,
        [string]$NatCode = "0"
    )

    Write-Host "`n=== Find-AllRalliesCalendar (Year=$Year, NatCode=$NatCode) ===" -ForegroundColor Yellow

    $allLinks = @()

    for ($month = 1; $month -le 12; $month++) {
        Write-Host "`n--- Month $month ---" -ForegroundColor Cyan
        for ($week = 1; $week -le 4; $week++) {
            $url = "https://ewrc-results.com/calendar?s=$Year&nat=$NatCode&month=$month&week=$week"
            $response = Invoke-EWRCRequest -Url $url -Label "Calendar M$month W$week"
            if (-not $response) { continue }

            $eventLinks = $response.Links.href | Where-Object { $_ -like "*event*" -or $_ -like "*/events/*" }
            if ($eventLinks) {
                $eventLinks | ForEach-Object { Write-Host "  LINK: $_" -ForegroundColor DarkYellow }
                $allLinks += $eventLinks
            } else {
                Write-Host "  (no event links this week)" -ForegroundColor DarkGray
            }
        }
    }

    $unique = $allLinks | Sort-Object -Unique
    Write-Host "`n[RESULT] $($unique.Count) unique event links found total:" -ForegroundColor Green
    $unique | ForEach-Object { Write-Host "  $_" }

    return $unique
}
#endregion

#region --- FUNCTION: Search-Driver ------------------------------------------
function Search-Driver {
    <#
    .SYNOPSIS
        Searches EWRC for a driver or co-driver by name.
    .PARAMETER Name
        Full or partial name to search.
    .EXAMPLE
        Search-Driver -Name "Jan Jansen"
    #>
    param([Parameter(Mandatory)][string]$Name)

    Write-Host "`n=== Search-Driver '$Name' ===" -ForegroundColor Yellow

    $searchName = $Name.Trim() -replace '\s+', '+'
    $url = "https://ewrc-results.com/search/?find=$searchName&nat=0"
    $response = Invoke-EWRCRequest -Url $url -Label "DriverSearch"
    if (-not $response) { return }

    Write-Host "`n[RAW PROFILE LINKS]" -ForegroundColor DarkYellow
    $response.Links | Where-Object { $_.href -like "*profile*" } |
        ForEach-Object { Write-Host "  href=$($_.href)  title=$($_.title)  innerHTML=$($_.innerHTML)" -ForegroundColor DarkYellow }

    $results = @()
    foreach ($link in $response.Links) {
        if ($link.href -notlike "*profile*") { continue }
        if ($link.innerHTML -like "*.jpg*")  { continue }
        if ($link.title -notlike "*driver*") { continue }

        $parts  = $link.href.Split("/")
        $seg    = if ($parts.Count -gt 2) { $parts[2] } else { $link.href }
        $number = $seg.Split("-")[0]
        $type   = if ($link.href -like "*coprofile*") { "Co-driver" } else { "Driver" }

        $results += [pscustomobject]@{
            Name       = $Name
            EWRCNumber = $number
            Type       = $type
            ProfileUrl = "https://ewrc-results.com" + $link.href
        }
    }

    Write-Host "`n[RESULT] $($results.Count) match(es):" -ForegroundColor Green
    $results | Format-Table -AutoSize

    return $results
}
#endregion

#region --- FUNCTION: Compare-MembersToEntries --------------------------------
function Compare-MembersToEntries {
    <#
    .SYNOPSIS
        Compares a club member CSV against rally entries and returns matches.
    .PARAMETER MembersCsvPath
        Path to members CSV (semicolon-delimited). 
        Expected columns: EwrcNrPilot, EwrcNrCoPilot, and whatever else you want in the output.
        Default: ~/Documents/leden.csv
    .PARAMETER RallyEntries
        Output from Get-RallyEntries. If omitted, prompts you to run that first.
    .EXAMPLE
        $events  = Find-RallyEvents -Year 2025
        $entries = Get-RallyEntries -EventUrl $events.EventUrl -Year 2025
        Compare-MembersToEntries -RallyEntries $entries
    #>
    param(
        [string]$MembersCsvPath = "$env:USERPROFILE\Documents\leden.csv",
        [Parameter(Mandatory)][object[]]$RallyEntries
    )

    Write-Host "`n=== Compare-MembersToEntries ===" -ForegroundColor Yellow

    if (-not (Test-Path -LiteralPath $MembersCsvPath)) {
        Write-Warning "Members CSV not found at: $MembersCsvPath"
        Write-Warning "Provide the correct path with -MembersCsvPath"
        return
    }

    $members = Import-Csv $MembersCsvPath -Delimiter ";"
    Write-Host "[CSV] Loaded $($members.Count) members from $MembersCsvPath" -ForegroundColor Green

    $matches = @()
    $seen    = @{}

    foreach ($entry in $RallyEntries) {
        foreach ($field in @('EwrcNrPilot', 'EwrcNrCoPilot')) {
            $idx = @($members.$field).IndexOf($entry.Number)
            if ($idx -ne -1) {
                $member = $members[$idx]
                $key    = $member.'Leden Nr.'
                if (-not $seen[$key]) {
                    $seen[$key] = $true
                    $obj = $member | Select-Object * -ExcludeProperty EwrcURL
                    $matches += $obj
                    Write-Host "[MATCH] $($entry.Type) $($entry.Name) (#$($entry.Number)) -> $($member.'Leden Nr.')" -ForegroundColor Magenta
                }
            }
        }
    }

    Write-Host "`n[RESULT] $($matches.Count) unique member match(es):" -ForegroundColor Green
    $matches | Format-Table -AutoSize

    return $matches
}
#endregion

#region --- FUNCTION: Show-PageLinks (debug helper) ---------------------------
function Show-PageLinks {
    <#
    .SYNOPSIS
        Fetches any URL and dumps all links - useful for debugging when the site changes.
    .PARAMETER Url
        The URL to inspect.
    .PARAMETER Filter
        Optional wildcard filter for href (e.g. "*event*"). Shows all if omitted.
    .EXAMPLE
        Show-PageLinks -Url "https://ewrc-results.com/season/2025/25-nederland?nat=24" -Filter "*event*"
    #>
    param(
        [Parameter(Mandatory)][string]$Url,
        [string]$Filter = "*"
    )

    Write-Host "`n=== Show-PageLinks ===" -ForegroundColor Yellow
    $response = Invoke-EWRCRequest -Url $Url -Label "DebugDump"
    if (-not $response) { return }

    $links = $response.Links | Where-Object { $_.href -like $Filter }
    Write-Host "[RESULT] $($links.Count) link(s) matching '$Filter':" -ForegroundColor Green
    $links | Select-Object href, innerHTML, title | Format-Table -AutoSize
    return $links
}
#endregion

#region --- QUICK-START MENU --------------------------------------------------
Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║          EWRC Scraper - Debug / Text-Only Mode               ║
╠══════════════════════════════════════════════════════════════╣
║  Available functions:                                        ║
║                                                              ║
║  Find-RallyEvents [-Year 2025]                               ║
║    -> Returns rally list for NL (uses season URLs in config) ║
║                                                              ║
║  Find-AllRalliesCalendar [-Year 2025] [-NatCode 24]          ║
║    -> Scans calendar week-by-week, dumps all raw links       ║
║                                                              ║
║  Get-RallyEntries -EventUrl <url> [-Year 2025]               ║
║    -> Returns drivers + co-drivers for an event              ║
║                                                              ║
║  Search-Driver -Name "Jan Jansen"                            ║
║    -> Searches EWRC for a driver/co-driver by name           ║
║                                                              ║
║  Compare-MembersToEntries -RallyEntries `$entries             ║
║    -> Finds club members in a rally entry list               ║
║                                                              ║
║  Show-PageLinks -Url <url> [-Filter "*event*"]               ║
║    -> Dumps all links from any page (great for debugging)    ║
║                                                              ║
║  TIP: use -Verbose on any function for extra detail          ║
╚══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor White

# Uncomment to auto-run on load:
# $events = Find-RallyEvents -Year (Get-Date).Year
#endregion