$formSplashScreen_Load = {
	<#
	Use the -PassTru parameter to update the splash screen text:
	.EXAMPLE
	$splashForm = Show-SplashScreen ... -PassThru
	#Update the splash screen text
	$splashForm.Text = 'Loading Modules...'
	#>
	$global:AllRallyResults = @()
	$Global:RCHledenInRally=@()
	$Global:CurrentYear = (get-date).Year
	
	$JsonFile = "ewrc-scraper.json"
	$global:Config = Read-JsonFile -Path "$PSScriptRoot\$JsonFile"
	#Write-host $config
	If ($Config -eq $false){
		Write-host "cannot find .json file, created a new one."
		$global:Config = @{
			CountryNLnumber	     = "24"
			CountryBELnumber	 = "25"
			CountryGERnumber	 = "10"
			RallySeasonPrefixURL = "https://ewrc-results.com/season/YEAR/25-COUNTRYNAME?nat=COUNTRYNUMBER"
			RallyEventUrlBase    = "https://ewrc-results.com/event/"
			EWRCBaseURL		     = "https://ewrc-results.com/"
			RallySeasonSearchURLs = @(
				"https://ewrc-results.com/season/YEAR/25-nederland?nat=24",
				"https://ewrc-results.com/season/YEAR/1363-nederland-historic?nat=24",
				"https://ewrc-results.com/season/YEAR/86-nederland-others?nat=24"
			)
		}
		#season example: https://ewrc-results.com/season/2025/25-nederland?nat=24
		Write-JsonFile -Path "$PSScriptRoot\$JsonFile" -InputObject $Config
	}else {
		#Write-host "DEBUG: $config"
		}

	# Set the ledenlijst initial directory from the saved config; fall back to the script folder.
	if ($global:Config.LedenlijstPath -and (Test-Path -LiteralPath $global:Config.LedenlijstPath)) {
		$global:initialDirectory = $global:Config.LedenlijstPath
	} else {
		$global:initialDirectory = $PSScriptRoot
	}

	&$checkboxDebugOnoff_CheckedChanged

	# Populate country selector from API (IDs 24=NL, 25=BE, 10=DE pre-checked)
	$global:AllCountries = @(Get-eWRCCountries -Year $Global:CurrentYear)
	$defaultIds = @(24, 25, 10)
	$checkedlistbox2.Invoke([Action]{
		$checkedlistbox2.BeginUpdate()
		$checkedlistbox2.Items.Clear()
		foreach ($c in $global:AllCountries) {
			[void]$checkedlistbox2.Items.Add($c.Name)
		}
		$checkedlistbox2.EndUpdate()
	})
	for ($i = 0; $i -lt $global:AllCountries.Count; $i++) {
		if ($global:AllCountries[$i].Id -in $defaultIds) {
			$checkedlistbox2.SetItemChecked($i, $true)
		}
	}
	$checkedlistbox2.Update()

	# Initial calendar load for default countries and current year
	$selectedIds = $defaultIds | Where-Object { $_ -in ($global:AllCountries | Select-Object -ExpandProperty Id) }
	$Global:RallyEvents = @(Get-eWRCCalendar -Year $datetimepicker1.Value.Year -Countries $selectedIds)
	$datagridview1.DataSource = $null
	$datagridview1.Columns.Clear()
	$nameTable = New-Object System.Data.DataTable
	[void]$nameTable.Columns.Add('Name', [string])
	foreach ($ev in $Global:RallyEvents) { [void]$nameTable.Rows.Add($ev.Name) }
	$datagridview1.DataSource = $nameTable
	Update-ListBox -Items ($Global:RallyEvents | Select-Object -ExpandProperty Name) -ListBox $checkedlistbox1

	$buttonExportToCSV.Enabled = $false
	$buttonVergelijk.Enabled = $false
	If (Test-Path -LiteralPath "$($env:USERPROFILE)\documents\leden.csv")
	{
		$CVSFileContents = Import-Csv "$($env:USERPROFILE)\documents\leden.csv" -Delimiter ";"
		$Global:RCHLeden = $CVSFileContents
		$datagridview2.DataSource = ConvertTo-DataTable -InputObject $CVSFileContents
	}
	
	
	$paramShowSplashScreen = @{
		Image = $pictureboxSplashScreenHidden.Image
		Title = 'Loading...'
		PassThru = $false
	}
	
	#Show-SplashScreen @paramShowSplashScreen
	
	#TODO: Place initialization script here:
	
}



Function Get-RallyEvents{
	Param (
		$CountryCode = $global:CountryNL,
		$Year = $global:CurrentYear)
	$ScrapedLinks=@()
	Foreach ($SearchURL in $global:Config.RallySeasonSearchURLs){
		$URL = ($SearchURL).replace("YEAR", $Year)
		Write-host "Invoking URL in function Get-RallyEvents: $URL"
		$result = Invoke-WebRequest -Uri $url
		$ScrapedLinks += @($result.links.href | where { $_ -like "*event*" })
	}
	$rallyObjects = @(ConvertTo-RallyInfo -url $ScrapedLinks -BaseUrl $global:Config.EWRCBaseURL)
	Return $rallyObjects
}

function ConvertTo-RallyInfo{
	[CmdletBinding()]
	param (
		[Parameter(Mandatory, ValueFromPipeline)]
		[string[]]$Url,
		[Parameter()]
		[string]$BaseUrl
	)
	
	begin
	{
		# key = slug, value = ordered hashtable
		$rallies = @{ }
	}
	
	process
	{
		foreach ($u in $Url) {
			
			$u = $u.Trim()
			if (-not $u) { continue }
			
			if ($u -match '^/event/\d+-(.+?)/final-results$')
			{
				$slug = $matches[1]
				
				if (-not $rallies.Contains($slug))
				{
					$rallies[$slug] = [ordered]@{
						Name	  = ($slug -replace '-', ' ')
						EventUrl  = $null
						EventsUrl = $null
					}
				}
				
				$rallies[$slug].EventUrl =
				if ($BaseUrl) { $BaseUrl.TrimEnd('/') + $u }
				else { $u }
				
				continue
			}
			
			if ($u -match '^/events/\d+-(.+)$')
			{
				$slug = $matches[1]
				
				if (-not $rallies.Contains($slug))
				{
					$rallies[$slug] = [ordered]@{
						Name	  = ($slug -replace '-', ' ')
						EventUrl  = $null
						EventsUrl = $null
					}
				}
				
				$rallies[$slug].EventsUrl =
				if ($BaseUrl) { $BaseUrl.TrimEnd('/') + $u }
				else { $u }
				
				continue
			}
		}
	}
	
	end
	{
		$result = foreach ($item in $rallies.Values) {
			if ($null -ne $item.EventUrl)
			{
				[pscustomobject]$item
			}
		}
		
		return $result
	}
}



#region Splash Screen Helper Function
function Show-SplashScreen
{
	<#
	.SYNOPSIS
		Displays a splash screen using the specified image.
	
	.PARAMETER Image
		Mandatory Image object that is displayed in the splash screen.
	
	.PARAMETER Title
		(Optional) Sets a title for the splash screen window. 
	
	.PARAMETER Timeout
		The amount of seconds before the splash screen is closed.
		Set to 0 to leave the splash screen open indefinitely.
		Default: 2
	
	.PARAMETER ImageLocation
		The file path or url to the image.

	.PARAMETER PassThru
		Returns the splash screen form control. Use to manually close the form.
	
	.PARAMETER Modal
		The splash screen will hold up the pipeline until it closes.

	.EXAMPLE
		PS C:\> Show-SplashScreen -Image $Image -Title 'Loading...' -Timeout 3

	.EXAMPLE
		PS C:\> Show-SplashScreen -ImageLocation 'C:\Image\MyImage.png' -Title 'Loading...' -Timeout 3

	.EXAMPLE
		PS C:\> $splashScreen = Show-SplashScreen -Image $Image -Title 'Loading...' -PassThru
				#close the splash screen
				$splashScreen.Close()
	.OUTPUTS
		System.Windows.Forms.Form
	
	.NOTES
		Created by SAPIEN Technologies, Inc.

		The size of the splash screen is dependent on the image.
		The required assemblies to use this function outside of a WinForms script:
		Add-Type -AssemblyName System.Windows.Forms
		Add-Type -AssemblyName System.Drawing
#>
	[OutputType([System.Windows.Forms.Form])]
	param
	(
		[Parameter(ParameterSetName = 'Image',
				   Mandatory = $true,
				   Position = 1)]
		[ValidateNotNull()]
		[System.Drawing.Image]$Image,
		[Parameter(Mandatory = $false)]
		[string]$Title,
		[int]$Timeout = 2,
		[Parameter(ParameterSetName = 'ImageLocation',
				   Mandatory = $true,
				   Position = 1)]
		[ValidateNotNullOrEmpty()]
		[string]$ImageLocation,
		[switch]$PassThru,
		[switch]$Modal
	)
	
	#Create a splash screen form to display the image.
	$splashForm = New-Object System.Windows.Forms.Form
	
	#Create a picture box for the image
	$pict = New-Object System.Windows.Forms.PictureBox
	
	if ($Image)
	{
		$pict.Image = $Image;
	}
	else
	{
		$pict.Load($ImageLocation)
	}
	
	$pict.AutoSize = $true
	$pict.Dock = 'Fill'
	$splashForm.Controls.Add($pict)
	
	#Display a title if defined.
	if ($Title)
	{
		$splashForm.Text = $Title
		$splashForm.FormBorderStyle = 'FixedDialog'
	}
	else
	{
		$splashForm.FormBorderStyle = 'None'
	}
	
	#Set a timer
	if ($Timeout -gt 0)
	{
		$timer = New-Object System.Windows.Forms.Timer
		$timer.Interval = $Timeout * 1000
		$timer.Tag = $splashForm
		$timer.add_Tick({
				$this.Tag.Close();
				$this.Stop()
			})
		$timer.Start()
	}
	
	#Show the form
	$splashForm.AutoSize = $true
	$splashForm.AutoSizeMode = 'GrowAndShrink'
	$splashForm.ControlBox = $false
	$splashForm.StartPosition = 'CenterScreen'
	$splashForm.TopMost = $true
	
	if ($Modal) { $splashForm.ShowDialog() }
	else { $splashForm.Show() }
	
	if ($PassThru)
	{
		return $splashForm
	}
}
#endregion
#region Control Helper Functions
<#
	.SYNOPSIS
		Sets the emulation of the WebBrowser control for the application.
	
	.DESCRIPTION
		Sets the emulation of the WebBrowser control for the application using the installed version of IE.
		This improves the WebBrowser control compatibility with newer html features.
	
	.PARAMETER ExecutableName
		The name of the executable E.g. PowerShellStudio.exe.
		Default Value: The running executable name.
	
	.EXAMPLE
		PS C:\> Set-WebBrowserEmulation

	.EXAMPLE
		PS C:\> Set-WebBrowserEmulation PowerShell.exe
#>
function Set-WebBrowserEmulation
{
	param
	(
		[ValidateNotNullOrEmpty()]
		[string]
		$ExecutableName = [System.IO.Path]::GetFileName([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
	)
	
	#region Get IE Version
	$valueNames = 'svcVersion', 'svcUpdateVersion', 'Version', 'W2kVersion'
	
	$version = 0;
	for ($i = 0; $i -lt $valueNames.Length; $i++)
	{
		$objVal = [Microsoft.Win32.Registry]::GetValue('HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer', $valueNames[$i], '0')
		$strVal = [System.Convert]::ToString($objVal)
		if ($strVal)
		{
			$iPos = $strVal.IndexOf('.')
			if ($iPos -gt 0)
			{
				$strVal = $strVal.Substring(0, $iPos)
			}
			
			$res = 0;
			if ([int]::TryParse($strVal, [ref]$res))
			{
				$version = [Math]::Max($version, $res)
			}
		}
	}
	
	if ($version -lt 7)
	{
		$version = 7000
	}
	else
	{
		$version = $version * 1000
	}
	#endregion
	
	[Microsoft.Win32.Registry]::SetValue('HKEY_CURRENT_USER\SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION', $ExecutableName, $version)
}


function Update-DataGridView
{
	<#
	.SYNOPSIS
		This functions helps you load items into a DataGridView.

	.DESCRIPTION
		Use this function to dynamically load items into the DataGridView control.

	.PARAMETER  DataGridView
		The DataGridView control you want to add items to.

	.PARAMETER  Item
		The object or objects you wish to load into the DataGridView's items collection.
	
	.PARAMETER  DataMember
		Sets the name of the list or table in the data source for which the DataGridView is displaying data.

	.PARAMETER AutoSizeColumns
	    Resizes DataGridView control's columns after loading the items.
	#>
	Param (
		[ValidateNotNull()]
		[Parameter(Mandatory=$true)]
		[System.Windows.Forms.DataGridView]$DataGridView,
		[ValidateNotNull()]
		[Parameter(Mandatory=$true)]
		$Item,
	    [Parameter(Mandatory=$false)]
		[string]$DataMember,
		[System.Windows.Forms.DataGridViewAutoSizeColumnsMode]$AutoSizeColumns = 'None'
	)
	$DataGridView.SuspendLayout()
	$DataGridView.DataMember = $DataMember
	
	if ($null -eq $Item)
	{
		$DataGridView.DataSource = $null
	}
	elseif ($Item -is [System.Data.DataSet] -and $Item.Tables.Count -gt 0)
	{
		$DataGridView.DataSource = $Item.Tables[0]
	}
	elseif ($Item -is [System.ComponentModel.IListSource]`
	-or $Item -is [System.ComponentModel.IBindingList] -or $Item -is [System.ComponentModel.IBindingListView] )
	{
		$DataGridView.DataSource = $Item
	}
	else
	{
		$array = New-Object System.Collections.ArrayList
		
		if ($Item -is [System.Collections.IList])
		{
			$array.AddRange($Item)
		}
		else
		{
			$array.Add($Item)
		}
		$DataGridView.DataSource = $array
	}
	
	if ($AutoSizeColumns -ne 'None')
	{
		$DataGridView.AutoResizeColumns($AutoSizeColumns)
	}
	
	$DataGridView.ResumeLayout()
}

function ConvertTo-DataTable
{
	<#
		.SYNOPSIS
			Converts objects into a DataTable.
	
		.DESCRIPTION
			Converts objects into a DataTable, which are used for DataBinding.
	
		.PARAMETER  InputObject
			The input to convert into a DataTable.
	
		.PARAMETER  Table
			The DataTable you wish to load the input into.
	
		.PARAMETER RetainColumns
			This switch tells the function to keep the DataTable's existing columns.
		
		.PARAMETER FilterWMIProperties
			This switch removes WMI properties that start with an underline.
	
		.EXAMPLE
			$DataTable = ConvertTo-DataTable -InputObject (Get-Process)
	#>
	[OutputType([System.Data.DataTable])]
	param(
	$InputObject, 
	[ValidateNotNull()]
	[System.Data.DataTable]$Table,
	[switch]$RetainColumns,
	[switch]$FilterWMIProperties)
	
	if($null -eq $Table)
	{
		$Table = New-Object System.Data.DataTable
	}
	
	if ($null -eq $InputObject)
	{
		$Table.Clear()
		return @( ,$Table)
	}
	
	if ($InputObject -is [System.Data.DataTable])
	{
		$Table = $InputObject
	}
	elseif ($InputObject -is [System.Data.DataSet] -and $InputObject.Tables.Count -gt 0)
	{
		$Table = $InputObject.Tables[0]
	}
	else
	{
		if (-not $RetainColumns -or $Table.Columns.Count -eq 0)
		{
			#Clear out the Table Contents
			$Table.Clear()
			
			if ($null -eq $InputObject) { return } #Empty Data
			
			$object = $null
			#find the first non null value
			foreach ($item in $InputObject)
			{
				if ($null -ne $item)
				{
					$object = $item
					break
				}
			}
			
			if ($null -eq $object) { return } #All null then empty
			
			#Get all the properties in order to create the columns
			foreach ($prop in $object.PSObject.Get_Properties())
			{
				if (-not $FilterWMIProperties -or -not $prop.Name.StartsWith('__')) #filter out WMI properties
				{
					#Get the type from the Definition string
					$type = $null
					
					if ($null -ne $prop.Value)
					{
						try { $type = $prop.Value.GetType() }
						catch { Out-Null }
					}
					
					if ($null -ne $type) # -and [System.Type]::GetTypeCode($type) -ne 'Object')
					{
						[void]$table.Columns.Add($prop.Name, $type)
					}
					else #Type info not found
					{
						[void]$table.Columns.Add($prop.Name)
					}
				}
			}
			
			if ($object -is [System.Data.DataRow])
			{
				foreach ($item in $InputObject)
				{
					$Table.Rows.Add($item)
				}
				return @( ,$Table)
			}
		}
		else
		{
			$Table.Rows.Clear()
		}
		
		foreach ($item in $InputObject)
		{
			$row = $table.NewRow()
			
			if ($item)
			{
				foreach ($prop in $item.PSObject.Get_Properties())
				{
					if ($table.Columns.Contains($prop.Name))
					{
						$row.Item($prop.Name) = $prop.Value
					}
				}
			}
			[void]$table.Rows.Add($row)
		}
	}
	
	return @(,$Table)
}

function Update-ComboBox
{
<#
	.SYNOPSIS
		This functions helps you load items into a ComboBox.
	
	.DESCRIPTION
		Use this function to dynamically load items into the ComboBox control.
	
	.PARAMETER ComboBox
		The ComboBox control you want to add items to.
	
	.PARAMETER Items
		The object or objects you wish to load into the ComboBox's Items collection.
	
	.PARAMETER DisplayMember
		Indicates the property to display for the items in this control.
		
	.PARAMETER ValueMember
		Indicates the property to use for the value of the control.
	
	.PARAMETER Append
		Adds the item(s) to the ComboBox without clearing the Items collection.
	
	.EXAMPLE
		Update-ComboBox $combobox1 "Red", "White", "Blue"
	
	.EXAMPLE
		Update-ComboBox $combobox1 "Red" -Append
		Update-ComboBox $combobox1 "White" -Append
		Update-ComboBox $combobox1 "Blue" -Append
	
	.EXAMPLE
		Update-ComboBox $combobox1 (Get-Process) "ProcessName"
	
	.NOTES
		Additional information about the function.
#>
	
	param
	(
		[Parameter(Mandatory = $true)]
		[ValidateNotNull()]
		[System.Windows.Forms.ComboBox]$ComboBox,
		[Parameter(Mandatory = $true)]
		[ValidateNotNull()]
		$Items,
		[Parameter(Mandatory = $false)]
		[string]$DisplayMember,
		[Parameter(Mandatory = $false)]
		[string]$ValueMember,
		[switch]$Append
	)
	
	if (-not $Append)
	{
		$ComboBox.Items.Clear()
	}
	
	if ($Items -is [Object[]])
	{
		$ComboBox.Items.AddRange($Items)
	}
	elseif ($Items -is [System.Collections.IEnumerable])
	{
		$ComboBox.BeginUpdate()
		foreach ($obj in $Items)
		{
			$ComboBox.Items.Add($obj)
		}
		$ComboBox.EndUpdate()
	}
	else
	{
		$ComboBox.Items.Add($Items)
	}
	
	$ComboBox.DisplayMember = $DisplayMember
	$ComboBox.ValueMember = $ValueMember
}

function Update-ListBox
{
<#
	.SYNOPSIS
		This functions helps you load items into a ListBox or CheckedListBox.
	
	.DESCRIPTION
		Use this function to dynamically load items into the ListBox control.
	
	.PARAMETER ListBox
		The ListBox control you want to add items to.
	
	.PARAMETER Items
		The object or objects you wish to load into the ListBox's Items collection.
	
	.PARAMETER DisplayMember
		Indicates the property to display for the items in this control.
		
	.PARAMETER ValueMember
		Indicates the property to use for the value of the control.
	
	.PARAMETER Append
		Adds the item(s) to the ListBox without clearing the Items collection.
	
	.EXAMPLE
		Update-ListBox $ListBox1 "Red", "White", "Blue"
	
	.EXAMPLE
		Update-ListBox $listBox1 "Red" -Append
		Update-ListBox $listBox1 "White" -Append
		Update-ListBox $listBox1 "Blue" -Append
	
	.EXAMPLE
		Update-ListBox $listBox1 (Get-Process) "ProcessName"
	
	.NOTES
		Additional information about the function.
#>
	
	param
	(
		[Parameter(Mandatory = $true)]
		[ValidateNotNull()]
		[System.Windows.Forms.ListBox]$ListBox,
		[Parameter(Mandatory = $true)]
		[ValidateNotNull()]
		$Items,
		[Parameter(Mandatory = $false)]
		[string]$DisplayMember,
		[Parameter(Mandatory = $false)]
		[string]$ValueMember,
		[switch]$Append
	)
	
	if (-not $Append)
	{
		$ListBox.Items.Clear()
	}
	
	if ($Items -is [System.Windows.Forms.ListBox+ObjectCollection] -or $Items -is [System.Collections.ICollection])
	{
		$ListBox.Items.AddRange($Items)
	}
	elseif ($Items -is [System.Collections.IEnumerable])
	{
		$ListBox.BeginUpdate()
		foreach ($obj in $Items)
		{
			$ListBox.Items.Add($obj)
		}
		$ListBox.EndUpdate()
	}
	else
	{
		$ListBox.Items.Add($Items)
	}
	
	$ListBox.DisplayMember = $DisplayMember
	$ListBox.ValueMember = $ValueMember
}


$buttonRallyInfoOphalen_Click={
	$GetCheckedItems = $checkedlistbox1.CheckedIndices
	if ($GetCheckedItems.Count -eq 0) {
		Write-Host "Geen rally's aangevinkt — vink er minstens een aan."
		return
	}

	$selectedEvents = @()
	Foreach ($CheckedRally in $GetCheckedItems) {
		$rallyItemName = $checkedlistbox1.items[$CheckedRally]
		$CurrentEventObj = $Global:RallyEvents | Where-Object { $_.Name -eq $rallyItemName }
		if (-not $CurrentEventObj) {
			Write-Host "WAARSCHUWING: rally-object niet gevonden voor '$rallyItemName'"
			continue
		}
		Write-Host "Rally geselecteerd: $($CurrentEventObj.Name) (ID: $($CurrentEventObj.Id))"
		$selectedEvents += $CurrentEventObj
	}

	if ($selectedEvents.Count -eq 0) {
		Write-Host "Geen geldige rally's gevonden."
		return
	}

	Write-Host "--- Start ophalen van $($selectedEvents.Count) rally('s) via API ---"
	try {
		$global:AllRallyResults = Get-RallyResults -Events $selectedEvents
		Write-Host "--- Klaar: $($global:AllRallyResults.Count) deelnemers gevonden ---"
	} catch {
		Write-Host "FOUT in Get-RallyResults: $_"
		return
	}

	$datagridview1.DataSource = ConvertTo-DataTable -InputObject $global:AllRallyResults
	$Global:RCHledenInRally = @()
	$datagridview3.DataSource = ConvertTo-DataTable -InputObject $Global:RCHledenInRally
	if (($global:RCHLeden.count -gt 0) -and ($global:AllRallyResults.count -gt 0)) {
		$buttonExportToCSV.Enabled = $true
		$buttonVergelijk.Enabled = $true
	}
}

Function Get-RallyResults {
    Param (
        [object[]]$Events,
        [string[]]$RallyURL   # legacy parameter, ignored — kept so existing callers don't break
    )

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $HashTable = @()

    foreach ($ev in $Events) {
        $rallyName = $ev.Name
        $eventId   = $ev.Id
        Write-Host "API-ophalen: $rallyName (event ID $eventId)"

        # Try the event-scoped endpoint first (most reliable), fall back to flat entries
        $apiUrl      = "https://api-next.ewrc-results.com/event/$eventId/entries"
        $apiUrlAlt   = "https://api-next.ewrc-results.com/entries/$eventId"

        $response = $null
        foreach ($url in @($apiUrl, $apiUrlAlt)) {
            try {
                Write-Host "  Probeer: $url"
                $response = Invoke-RestMethod -Uri $url
                Write-Host "  OK — antwoord ontvangen"
                break
            } catch {
                Write-Host "  Mislukt: $_"
            }
        }

        if ($null -eq $response) {
            Write-Host "  SKIP: beide API-endpoints mislukten voor '$rallyName'"
            continue
        }

        # Log the top-level property names so we can see the structure
        $topProps = if ($response -is [System.Collections.IEnumerable] -and $response -isnot [string]) {
            $first = $response | Select-Object -First 1
            "array[$( ($response | Measure-Object).Count )] — eerste item: $($first.PSObject.Properties.Name -join ', ')"
        } else {
            "object — properties: $($response.PSObject.Properties.Name -join ', ')"
        }
        Write-Host "  Structuur: $topProps"

        # ── Parse: flat array of entry objects ──────────────────────────────────
        $entries = if ($response -is [System.Collections.IEnumerable] -and $response -isnot [string]) {
            @($response)
        } elseif ($response.entries) {
            @($response.entries)
        } else {
            @()
        }

        if ($entries.Count -eq 0 -and $response.PSObject.Properties.Name -notcontains 'entries') {
            # Unknown structure — dump first item raw so the debug box shows it
            Write-Host "  Onbekende structuur. Raw eerste item:"
            Write-Host "  $($response | ConvertTo-Json -Depth 3 -Compress | Select-Object -First 1)"
        }

        Write-Host "  Inschrijvingen gevonden: $($entries.Count)"

        foreach ($entry in $entries) {
            # ── Driver ──────────────────────────────────────────────────────────
            $drv = if ($entry.driver)    { $entry.driver }
                   elseif ($entry.pilot) { $entry.pilot }
                   else                  { $null }
            if ($drv) {
                $name   = "$($drv.firstname) $($drv.lastname)".Trim()
                $number = if ($drv.id) { "$($drv.id)" } else { "" }
                $HashTable += [PSCustomObject]@{
                    Name      = $name
                    Number    = $number
                    Type      = "Driver"
                    RallyName = $rallyName
                }
            }

            # ── Codriver ─────────────────────────────────────────────────────────
            $codrv = if ($entry.codriver)   { $entry.codriver }
                     elseif ($entry.copilot) { $entry.copilot }
                     else                    { $null }
            if ($codrv) {
                $name   = "$($codrv.firstname) $($codrv.lastname)".Trim()
                $number = if ($codrv.id) { "$($codrv.id)" } else { "" }
                $HashTable += [PSCustomObject]@{
                    Name      = $name
                    Number    = $number
                    Type      = "Codriver"
                    RallyName = $rallyName
                }
            }
        }

        # ── Fallback: separate drivers / codrivers arrays at top level ───────────
        if ($HashTable.Count -eq 0 -and $response.drivers) {
            foreach ($drv in $response.drivers) {
                $HashTable += [PSCustomObject]@{
                    Name      = "$($drv.firstname) $($drv.lastname)".Trim()
                    Number    = "$($drv.id)"
                    Type      = "Driver"
                    RallyName = $rallyName
                }
            }
            foreach ($codrv in $response.codrivers) {
                $HashTable += [PSCustomObject]@{
                    Name      = "$($codrv.firstname) $($codrv.lastname)".Trim()
                    Number    = "$($codrv.id)"
                    Type      = "Codriver"
                    RallyName = $rallyName
                }
            }
        }

        Write-Host "  Rijen toegevoegd voor '$rallyName': $($HashTable.Count) totaal"
    }

    Return $HashTable
}
Function Write-host
{
	Param (
		[Parameter(Position = 0)]
		$text,
		$ForegroundColor,
		$BackgroundColor
	)
	$NewLine = [System.Environment]::NewLine
	If ($text -is [object]){
		foreach ($entry in $text) {
			$OutputBox.AppendText($entry)
			$OutputBox.AppendText($NewLine)
		}
	}else{
		$OutputBox.AppendText($text)
		$OutputBox.AppendText($NewLine)
	}
	
}

$checkedlistbox2_SelectedIndexChanged={
	#TODO: Place custom script here
	
}

$buttonUpdateRallyLijst_Click={
	$year = $datetimepicker1.Value.Year

	# Collect IDs of checked countries from the country selector
	$selectedIds = @()
	for ($i = 0; $i -lt $checkedlistbox2.Items.Count; $i++) {
		if ($checkedlistbox2.GetItemChecked($i)) {
			$selectedIds += $global:AllCountries[$i].Id
		}
	}
	if ($selectedIds.Count -eq 0) {
		Write-host "No countries selected — check at least one country."
		return
	}

	Write-host "Fetching calendar for year $year, country IDs: $($selectedIds -join ', ')"
	$Global:RallyEvents = @(Get-eWRCCalendar -Year $year -Countries $selectedIds)
	Write-host "Found $($Global:RallyEvents.Count) rally events."

	# Bind Name column only to datagridview1
	$datagridview1.DataSource = $null
	$datagridview1.Columns.Clear()
	$nameTable = New-Object System.Data.DataTable
	[void]$nameTable.Columns.Add('Name', [string])
	foreach ($ev in $Global:RallyEvents) { [void]$nameTable.Rows.Add($ev.Name) }
	$datagridview1.DataSource = $nameTable

	# Keep checkedlistbox1 in sync for Rally info ophalen
	Update-ListBox -Items ($Global:RallyEvents | Select-Object -ExpandProperty Name) -ListBox $checkedlistbox1
}

$buttonLedenlijstInladen_Click={
	#TODO: Place custom script here
	$OpenFileDialog = New-Object System.Windows.Forms.OpenFileDialog
	$OpenFileDialog.initialDirectory = $global:initialDirectory
	$OpenFileDialog.filter = "Csv files (*.csv)| *.csv"
	if ($OpenFileDialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
	# Remember the chosen folder for next time.
	$selectedDir = [System.IO.Path]::GetDirectoryName($OpenFileDialog.filename)
	$global:initialDirectory = $selectedDir
	$global:Config | Add-Member -NotePropertyName 'LedenlijstPath' -NotePropertyValue $selectedDir -Force
	Write-JsonFile -Path "$PSScriptRoot\ewrc-scraper.json" -InputObject $global:Config
	$CVSFileContents = Import-Csv $OpenFileDialog.filename -Delimiter ";"
	$Global:RCHLeden = $CVSFileContents
	$datagridview2.DataSource = ConvertTo-DataTable -InputObject $CVSFileContents
	if (($global:RCHLeden.count -gt 0) -and ($global:AllRallyResults.count -gt 0))
	{
		$buttonExportToCSV.Enabled = $true
		$buttonVergelijk.Enabled = $true
	}
}

$buttonVergelijk_Click={
	if (-not $Global:RCHLeden -or -not $global:AllRallyResults) { return }
	$oEntryArr = @()

	ForEach ($rallyEntry in $global:AllRallyResults)
	{
		$oEntry = $Global:RCHLeden.EwrcNrPilot.IndexOf("$($rallyEntry.number)")
		if ($oEntry -ne -1)
		{
			$oTmpObj = $Global:RCHLeden[$oEntry]
			$oTmpObj.PSobject.properties.remove('EwrcURL')
			$oEntryArr += $oTmpObj
		}

		$oEntry = $Global:RCHLeden.EwrcNrCoPilot.IndexOf("$($rallyEntry.number)")
		if ($oEntry -ne -1)
		{
			$oTmpObj = $Global:RCHLeden[$oEntry]
			$oTmpObj.PSobject.properties.remove('EwrcURL')
			$oEntryArr += $oTmpObj
		}
	}

	$inschrijversUnique = @()
	foreach ($entry in $oEntryArr) {
		if ($inschrijversUnique.'Leden Nr.' -notcontains $entry.'Leden Nr.') {
			$inschrijversUnique += $entry
		}
	}

	$labelTotaalAantal.Text = "Totaal aantal matches: $($inschrijversUnique.count)"
	$global:RCHledenInRally = $inschrijversUnique
	$datagridview3.DataSource = ConvertTo-DataTable -InputObject $inschrijversUnique
	#$sortLidNr = New-Object System.ComponentModel.SortDescription('Leden nr.', 'Ascending')
	#$datagridview3.Sort($sortLidNr, 'Ascending')
	#$datagridview3.Update()
	
}


$ToClipBoard_Click={
	#TODO: Place custom script here
	
	[string]$Emailadressen = ""
	
	foreach ($adres in $global:RCHledenInRally.'E-mail adres')
	{
		#write-host $adres
		#write-host "---"
		$Emailadressen += "$adres;"
	}
	Set-Clipboard -Value $Emailadressen
	[System.Windows.Forms.MessageBox]::Show('Email addresses copied to clipboard', 'Ctrl+v!')
}

$buttonExportToCSV_Click={
	#TODO: Place custom script here
	#$result = $savefiledialog1.ShowDialog()
	if ($savefiledialog1.ShowDialog() -eq 'OK')
	{
		
		if (($savefiledialog1.FileName).EndsWith(".csv"))
			{
				$csv = $savefiledialog1.FileName
			}
			else
			{
				$csv = "$($savefiledialog1.FileName).csv"
			}
	$global:RCHledenInRally |ConvertTo-Csv -Delimiter ";" -NoTypeInformation | Out-file $csv
		if ($?) { Write-host "File saved succesfully" }
		else { Write-host "Error saving file" }
		
	}
}


function Search-eWRC {
    <#
    .SYNOPSIS
        Search eWRC-results.com for rally events, drivers, co-drivers, or cars.

    .PARAMETER Query
        The search term (e.g. a driver name, rally name, or plate number)

    .PARAMETER Limit
        Maximum number of results to return. Defaults to 10.

    .OUTPUTS
        PSCustomObject with Type, Id, Name, Flag, Slug and Url properties

    .EXAMPLE
        Search-eWRC -Query "Ogier"

    .EXAMPLE
        Search-eWRC -Query "Thies Stegeman" | Format-Table

    .EXAMPLE
        Search-eWRC -Query "Monte Carlo" | Where-Object Type -eq "event"
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Query,

        [Parameter(Mandatory = $false)]
        [int]$Limit = 10
    )

    begin {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $baseUrl = "https://api-next.ewrc-results.com/search"
    }

    process {
        $encodedQuery = [System.Uri]::EscapeDataString($Query)
        $searchUrl = "${baseUrl}?query=${encodedQuery}&limit=${Limit}"

        Write-Verbose "Querying: $searchUrl"

        try {
            $response = Invoke-RestMethod -Uri $searchUrl

            # Each category in the response
            $categories = @("events", "drivers", "codrivers", "plates")

            foreach ($category in $categories) {
                $section = $response.$category
                if ($section.total -eq 0) { continue }

                foreach ($item in $section.items) {

                    # Build full name depending on category
                    $name = if ($category -eq "events") {
                        $item.name
                    } else {
                        "$($item.firstname) $($item.lastname)".Trim()
                    }

                    # Build profile URL
                    $urlPrefix = switch ($item.type) {
                        "driver"   { "profile" }
                        "codriver" { "coprofile" }
                        "event"    { "event" }
                        "plate"    { "cars" }
                        default    { "profile" }
                    }
                    $url = "https://www.ewrc-results.com/$urlPrefix/$($item.id)-$($item.slug)/"

                    [PSCustomObject]@{
                        Type = $item.type
                        Id   = $item.id
                        Name = $name
                        Flag = $item.flag
                        Slug = $item.slug
                        Url  = $url
                    }
                }
            }

        } catch {
            Write-Error "Search failed for '$Query': $_"
        }
    }
}


$buttonOpzoeken_Click = {
    $picturebox1.Image = $null
    $picturebox2.Image = $null

    [string]$Zoeknaam = $NameLookup1.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($Zoeknaam)) { return }

    Write-Host "Searching eWRC for: $Zoeknaam"
    $results = @(Search-eWRC -Query $Zoeknaam)

    $rtb = $LookupResultRichTextbox1
    $rtb.Clear()

    # Prune unchecked entries from the comparison list; keep only what is checked
    $checkedKeys = @($compareCheckedList.CheckedItems | ForEach-Object { $_.ToString() })
    $toRemove    = @($compareCheckedList.Items        | Where-Object  { $checkedKeys -notcontains $_.ToString() })
    foreach ($item in $toRemove) {
        $compareCheckedList.Items.Remove($item)
        $script:compareDriverData.Remove($item.ToString())
    }

    if ($results.Count -eq 0) {
        [void][System.Windows.Forms.MessageBox]::Show('Niets gevonden', 'Zoekresultaat')
        return
    }

    # Fonts
    $fNormal = $rtb.Font
    $fBold   = New-Object System.Drawing.Font($fNormal, [System.Drawing.FontStyle]::Bold)
    $fSmall  = New-Object System.Drawing.Font($fNormal.FontFamily, [float]($fNormal.Size - 1))

    # Colours
    $cBlack     = [System.Drawing.Color]::Black
    $cGray      = [System.Drawing.Color]::Gray
    $cDimGray   = [System.Drawing.Color]::DimGray
    $cSteelBlue = [System.Drawing.Color]::SteelBlue
    $cSilver    = [System.Drawing.Color]::Silver

    # Type → accent colour
    $typeColors = @{
        driver   = [System.Drawing.Color]::FromArgb(0, 100, 200)
        codriver = [System.Drawing.Color]::FromArgb(130, 50, 180)
        event    = [System.Drawing.Color]::FromArgb(0, 140, 80)
        plate    = [System.Drawing.Color]::FromArgb(180, 100, 0)
    }

    # Inline helper: move cursor to end, apply colour+font, append text
    $w = {
        param($color, $font, $text)
        $rtb.SelectionStart  = $rtb.TextLength
        $rtb.SelectionLength = 0
        $rtb.SelectionColor  = if ($color) { $color } else { [System.Drawing.Color]::Black }
        $rtb.SelectionFont   = if ($font)  { $font }  else { $rtb.Font }
        $rtb.AppendText($text)
    }

    $driverCount = 0   # photo slots filled (0-1 = picturebox1, 2 = picturebox2 ... cap at 2)

    foreach ($r in $results) {
        # Skip incomplete results where the API returned no Type
        if (-not $r.Type) { continue }

        $accent = if ($typeColors.ContainsKey($r.Type)) { $typeColors[$r.Type] } else { $cDimGray }

        # ── Row 1: type badge  +  name  +  flag
        & $w $accent     $fBold   "[$($r.Type.ToUpper())]  "
        & $w $cBlack     $fBold   $(if ($r.Name) { $r.Name } else { '(no name)' })
        if ($r.Flag) { & $w $cGray $fSmall "  ($($r.Flag))" }
        & $w $cBlack     $fNormal "`n"

        # ── Row 2: ID  +  URL
        & $w $cGray      $fSmall  "   ID $($r.Id)   "
        & $w $cSteelBlue $fSmall  $r.Url
        & $w $cBlack     $fNormal "`n"

        # ── Stats block (drivers and codrivers only)
        if ($r.Type -in @('driver', 'codriver')) {
            # Register in the comparison checklist
            $displayKey = "[$($r.Type.ToUpper())] $(if ($r.Name) { $r.Name } else { '?' }) (ID $($r.Id))"
            $script:compareDriverData[$displayKey] = $r
            if (-not $compareCheckedList.Items.Contains($displayKey)) {
                [void]$compareCheckedList.Items.Add($displayKey)
            }

            try {
                Write-Host "Getting stats for $($r.Name) (ID $($r.Id))..."
                $statsList = @(Get-eWRCDriverStats -DriverId $r.Id -Type $r.Type)
                $script:compareStatsCache["$($r.Type)-$($r.Id)"] = $statsList   # cache for compare view

                # Profile metadata — same across all rows, show once
                $meta = $statsList | Select-Object -First 1
                if ($meta) {
                    $metaParts = @()
                    if ($meta.DateOfBirth) { $metaParts += "Born: $($meta.DateOfBirth)" }
                    if ($meta.Country)     { $metaParts += "Country: $($meta.Country)" }
                    if ($metaParts) { & $w $cDimGray $fSmall "   $($metaParts -join '   ')`n" }
                }

                foreach ($s in $statsList) {
                    # Category header line
                    & $w $accent   $fBold  "   $($s.RaceType.ToUpper())"
                    & $w $cDimGray $fSmall "  — $($s.Sections)`n"

                    # Key stats on one line, dot-separated
                    $parts = @()
                    if ($null -ne $s.rally)              { $parts += "Starts: $($s.rally)" }
                    if ($null -ne $s.countretirement)    { $parts += "DNF: $($s.countretirement) ($($s.countretirement_pct)%)" }
                    if ($null -ne $s.class_wins)         { $parts += "Class wins: $($s.class_wins) ($($s.class_wins_pct)%)" }
                    if ($s.winsU -gt 0)                  { $parts += "Overall wins: $($s.winsU)" }
                    if ($null -ne $s.registered_mileage) { $parts += "Mileage: $($s.registered_mileage) km" }
                    & $w $cBlack $fSmall "   $($parts -join '  ·  ')`n"
                }

                # Photo from PhotoUrl — fill picturebox1 then picturebox2
                $photoUrl = ($statsList | Where-Object { $null -ne $_.PhotoUrl } | Select-Object -First 1).PhotoUrl
                if ($photoUrl -and $driverCount -lt 2) {
                    try {
                        $dest = "$env:TEMP\$($photoUrl.Split('/')[-1])"
                        Invoke-WebRequest -Uri $photoUrl -OutFile $dest
                        $bmp = [System.Drawing.Image]::FromFile((Get-Item $dest))
                        if ($driverCount -eq 0) { $picturebox1.Image = $bmp } else { $picturebox2.Image = $bmp }
                    } catch { Write-Host "Could not load photo: $_" }
                    $driverCount++   # move to next slot regardless of download success
                }
            } catch {
                & $w $cGray $fSmall "   (stats unavailable)`n"
            }
        }

        # ── Separator
        & $w $cSilver $fSmall ("─" * 60 + "`n")
    }

    # Scroll back to top so first result is visible
    $rtb.SelectionStart = 0
    $rtb.ScrollToCaret()
}


# ─────────────────────────────────────────────────────────────────────────────
# TAB 4 — COMPARISON CONTROLS  (created at dot-source time)
# ─────────────────────────────────────────────────────────────────────────────
$script:compareDriverData = @{}   # displayKey → result object
$script:compareStatsCache = @{}   # "type-id"   → statsList array

$labelCompare          = New-Object System.Windows.Forms.Label
$labelCompare.Text     = 'Vergelijken (max 4):'
$labelCompare.Location = [System.Drawing.Point]::new(4, 178)
$labelCompare.Size     = [System.Drawing.Size]::new(342, 16)
$labelCompare.Font     = New-Object System.Drawing.Font('Segoe UI', 8)
$panelTab4Photos.Controls.Add($labelCompare)

$compareCheckedList              = New-Object System.Windows.Forms.CheckedListBox
$compareCheckedList.Location     = [System.Drawing.Point]::new(4, 196)
$compareCheckedList.Size         = [System.Drawing.Size]::new(342, 120)
$compareCheckedList.CheckOnClick = $true
$compareCheckedList.Font         = New-Object System.Drawing.Font('Segoe UI', 9)
$panelTab4Photos.Controls.Add($compareCheckedList)

# Enforce max 4 selections; keep compare button in sync
$compareCheckedList.add_ItemCheck({
    param($sender, $e)
    if ($e.NewValue -eq [System.Windows.Forms.CheckState]::Checked -and
        $sender.CheckedItems.Count -ge 4) {
        $e.NewValue = [System.Windows.Forms.CheckState]::Unchecked
    }
    # ItemCheck fires BEFORE the state is applied, so compute the future count
    $futureCount = $sender.CheckedItems.Count
    if ($e.NewValue -eq [System.Windows.Forms.CheckState]::Checked) {
        $futureCount++
    } elseif ($e.NewValue -eq [System.Windows.Forms.CheckState]::Unchecked -and
              $e.CurrentValue -eq [System.Windows.Forms.CheckState]::Checked) {
        $futureCount--
    }
    $buttonVergelijkRijders.Enabled = $futureCount -gt 0
})

$buttonVergelijkRijders          = New-Object System.Windows.Forms.Button
$buttonVergelijkRijders.Text     = 'Vergelijk geselecteerden'
$buttonVergelijkRijders.Location = [System.Drawing.Point]::new(4, 322)
$buttonVergelijkRijders.Size     = [System.Drawing.Size]::new(342, 28)
$buttonVergelijkRijders.Enabled  = $false   # enabled by ItemCheck when ≥1 item is checked
$panelTab4Photos.Controls.Add($buttonVergelijkRijders)

$buttonVergelijkRijders_Click = {
    $checkedItems = @($compareCheckedList.CheckedItems)
    if ($checkedItems.Count -eq 0) {
        [void][System.Windows.Forms.MessageBox]::Show(
            'Selecteer minimaal 1 driver/codriver om te vergelijken.', 'Vergelijking')
        return
    }

    $rtb = $LookupResultRichTextbox1
    $rtb.Clear()

    $fMono     = New-Object System.Drawing.Font('Consolas', 9)
    $fMonoBold = New-Object System.Drawing.Font('Consolas', 9, [System.Drawing.FontStyle]::Bold)
    $fNormal   = $rtb.Font
    $fBold     = New-Object System.Drawing.Font($fNormal, [System.Drawing.FontStyle]::Bold)
    $fSmall    = New-Object System.Drawing.Font($fNormal.FontFamily, [float]($fNormal.Size - 1))

    $cBlack   = [System.Drawing.Color]::Black
    $cGray    = [System.Drawing.Color]::Gray
    $cDimGray = [System.Drawing.Color]::DimGray
    $cSilver  = [System.Drawing.Color]::Silver
    $typeColors = @{
        driver   = [System.Drawing.Color]::FromArgb(0, 100, 200)
        codriver = [System.Drawing.Color]::FromArgb(130, 50, 180)
    }

    $w = {
        param($color, $font, $text)
        $rtb.SelectionStart  = $rtb.TextLength
        $rtb.SelectionLength = 0
        $rtb.SelectionColor  = if ($color) { $color } else { [System.Drawing.Color]::Black }
        $rtb.SelectionFont   = if ($font)  { $font  } else { $rtb.Font }
        $rtb.AppendText($text)
    }

    # Resolve checked items → driver result objects
    $selected = [System.Collections.Generic.List[object]]::new()
    foreach ($item in $checkedItems) {
        $r = $script:compareDriverData[$item.ToString()]
        if ($r) { $selected.Add($r) }
    }
    if ($selected.Count -eq 0) { return }

    # Fetch or use cached stats for each selected driver
    $allStats = @{}
    foreach ($r in $selected) {
        $cacheKey = "$($r.Type)-$($r.Id)"
        if (-not $script:compareStatsCache.ContainsKey($cacheKey)) {
            try {
                $script:compareStatsCache[$cacheKey] = @(Get-eWRCDriverStats -DriverId $r.Id -Type $r.Type)
            } catch {
                $script:compareStatsCache[$cacheKey] = @()
            }
        }
        $allStats[$cacheKey] = $script:compareStatsCache[$cacheKey]
    }

    # ── Column layout (monospace, fixed width) ─────────────────────────────
    $labelW = 16
    $colW   = [Math]::Max(18, [Math]::Floor((90 - $labelW) / $selected.Count))
    $totalW = $labelW + $colW * $selected.Count

    $padStr = {
        param([string]$text, [int]$width)
        if ($text.Length -ge $width) { $text.Substring(0, $width - 1) + ' ' }
        else { $text.PadRight($width) }
    }

    # ── Title + back hint ──────────────────────────────────────────────────
    & $w $cGray  $fSmall "← Klik 'Opzoeken' om terug te gaan naar zoekresultaten`n"
    & $w $cBlack $fBold  "VERGELIJKING`n"
    & $w $cSilver $fMono ("═" * $totalW + "`n")

    # ── Names header ───────────────────────────────────────────────────────
    & $w $cDimGray $fMono (& $padStr '' $labelW)
    foreach ($r in $selected) {
        $accent = if ($r.Type -and $typeColors.ContainsKey($r.Type)) { $typeColors[$r.Type] } else { $cDimGray }
        & $w $accent $fMonoBold (& $padStr $(if ($r.Name) { $r.Name } else { '?' }) $colW)
    }
    & $w $cBlack $fMono "`n"

    # ── Type ───────────────────────────────────────────────────────────────
    & $w $cDimGray $fMono (& $padStr 'Type' $labelW)
    foreach ($r in $selected) {
        $tv = if ($r.Type) { $r.Type.ToUpper() } else { '-' }
        & $w $cGray $fMono (& $padStr $tv $colW)
    }
    & $w $cBlack $fMono "`n"

    # ── Born ───────────────────────────────────────────────────────────────
    & $w $cDimGray $fMono (& $padStr 'Born' $labelW)
    foreach ($r in $selected) {
        $ck   = "$($r.Type)-$($r.Id)"
        $meta = $allStats[$ck] | Select-Object -First 1
        $v    = if ($meta -and $meta.DateOfBirth) { $meta.DateOfBirth } else { '-' }
        & $w $cBlack $fMono (& $padStr $v $colW)
    }
    & $w $cBlack $fMono "`n"

    # ── Country ────────────────────────────────────────────────────────────
    & $w $cDimGray $fMono (& $padStr 'Country' $labelW)
    foreach ($r in $selected) {
        $ck   = "$($r.Type)-$($r.Id)"
        $meta = $allStats[$ck] | Select-Object -First 1
        $v    = if ($meta -and $meta.Country) { $meta.Country } else { '-' }
        & $w $cBlack $fMono (& $padStr $v $colW)
    }
    & $w $cBlack $fMono "`n"

    & $w $cSilver $fMono ("─" * $totalW + "`n")

    # ── Stats per RaceType ─────────────────────────────────────────────────
    $allRaceTypes = [System.Collections.Generic.List[string]]::new()
    foreach ($r in $selected) {
        $ck = "$($r.Type)-$($r.Id)"
        foreach ($s in $allStats[$ck]) {
            if ($s.RaceType -and -not $allRaceTypes.Contains($s.RaceType)) {
                $allRaceTypes.Add($s.RaceType)
            }
        }
    }

    $statDefs = [ordered]@{
        rally              = 'Starts'
        countretirement    = 'DNF'
        class_wins         = 'Class wins'
        winsU              = 'Overall wins'
        registered_mileage = 'Mileage (km)'
    }

    foreach ($raceType in $allRaceTypes) {
        $hdFill = [Math]::Max(2, $totalW - $raceType.Length - 4)
        & $w $cDimGray $fMonoBold "── $raceType "
        & $w $cSilver  $fMono     ("─" * $hdFill + "`n")

        foreach ($statKey in $statDefs.Keys) {
            $rowVals = @(foreach ($dr in $selected) {
                $ck   = "$($dr.Type)-$($dr.Id)"
                $sRow = $allStats[$ck] | Where-Object { $_.RaceType -eq $raceType } | Select-Object -First 1
                if (-not $sRow)              { '-'; continue }
                $v = $sRow.$statKey
                if ($null -eq $v)            { '-'; continue }
                $pct = $sRow."${statKey}_pct"
                if ($null -ne $pct) { "$v ($pct%)" } else { "$v" }
            })

            # Only show row if at least one driver actually has data
            if (($rowVals | Where-Object { $_ -ne '-' }).Count -gt 0) {
                & $w $cDimGray $fMono (& $padStr $statDefs[$statKey] $labelW)
                foreach ($cv in $rowVals) {
                    & $w $cBlack $fMono (& $padStr $cv $colW)
                }
                & $w $cBlack $fMono "`n"
            }
        }
    }

    & $w $cSilver $fMono ("═" * $totalW + "`n")

    $rtb.SelectionStart = 0
    $rtb.ScrollToCaret()
}
$buttonVergelijkRijders.add_Click($buttonVergelijkRijders_Click)


$tabcontrol1_MouseClick=[System.Windows.Forms.MouseEventHandler]{
    # Tab 3 comparison is triggered by SelectedIndexChanged
}

$checkboxDebugOnoff_CheckedChanged={
	#TODO: Place custom script here
	if ($checkboxDebugOnoff.Checked)
	{
		$OutputBox.Visible = $false
		$formRCHRallyScraper.Height = 600
	}
	else
	{
		$OutputBox.Visible = $true
		$formRCHRallyScraper.Height = 668
		$formRCHRallyScraper.SizeGripStyle = 'Hide'
	}
	$formRCHRallyScraper.Update()
}


# PowerShell 7+ compatible

function Write-JsonFile{
	[CmdletBinding()]
	param (
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Path,
		[Parameter(Mandatory)]
		[ValidateNotNull()]
		[object]$InputObject,
		[Parameter()]
		[ValidateRange(1, 100)]
		[int]$Depth = 10
	)
	
	try
	{
		$dir = Split-Path -Path $Path -Parent
		if ($dir -and -not (Test-Path -LiteralPath $dir))
		{
			New-Item -ItemType Directory -Path $dir -Force | Out-Null
		}
		
		$json = $InputObject | ConvertTo-Json -Depth $Depth
		Set-Content -LiteralPath $Path -Value $json -Encoding utf8
		return $true
	}
	catch
	{
		return $_
	}
}

function Read-JsonFile{
	[CmdletBinding()]
	param (
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Path,
		# PS 6+ feature (so PS7+ OK): returns hashtables instead of PSCustomObject

		[Parameter()]
		[switch]$AsHashtable
	)
	
	if (-not (Test-Path -LiteralPath $Path))
	{
		return $false
	}
	
	try
	{
		$content = Get-Content -LiteralPath $Path -Raw -Encoding utf8
		if ($AsHashtable)
		{
			return $content | ConvertFrom-Json -AsHashtable
		}
		else
		{
			return $content | ConvertFrom-Json
		}
	}
	catch
	{
		return $false
	}
}

$checkedlistbox2_SelectedValueChanged={
	# Country selection changed — click "Update rally lijst" to reload the calendar.
}

$FindAllRallyButton_Click={
	# Query every URL in RallySeasonSearchURLs and merge the results into the rally list.
	$year = $datetimepicker1.Value.Year
	Write-host "Fetching all rallies for $year from all configured URLs..."
	$allLinks = @()
	foreach ($SearchURL in $global:Config.RallySeasonSearchURLs) {
		$url = $SearchURL.Replace("YEAR", $year)
		Write-host "Querying: $url"
		try {
			$result   = Invoke-WebRequest -Uri $url
			$allLinks += @($result.links.href | Where-Object { $_ -like "*event*" })
		} catch {
			Write-host "Error querying $url : $_"
		}
	}
	$allLinks = $allLinks | Sort-Object -Unique
	$Global:RallyEvents = @(ConvertTo-RallyInfo -url $allLinks -BaseUrl $global:Config.EWRCBaseURL)
	Update-ListBox -Items ($Global:RallyEvents | Select-Object -ExpandProperty Name) -ListBox $checkedlistbox1
	Write-host "Found $($Global:RallyEvents.Count) rallies total."
}


function Get-eWRCCountries {
    <#
    .SYNOPSIS
        Retrieve all available countries from the eWRC calendar.

    .PARAMETER Year
        The season year. Defaults to the current year.

    .OUTPUTS
        PSCustomObject with Id, Name, Shortcut and Flag properties

    .EXAMPLE
        Get-eWRCCountries

    .EXAMPLE
        Get-eWRCCountries | Where-Object { $_.Name -like "*Belgium*" }

    .EXAMPLE
        Get-eWRCCountries -Year 2025
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$Year = (Get-Date).Year
    )

    begin {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    }

    process {
        $url = "https://api-next.ewrc-results.com/calendar/$Year/natall"

        Write-Verbose "Querying: $url"

        try {
            $response = Invoke-RestMethod -Uri $url

            foreach ($country in $response) {
                [PSCustomObject]@{
                    Id       = $country.id
                    Name     = $country.name.en
                    Shortcut = $country.shortcut
                    Flag     = $country.flag
                }
            }
        } catch {
            Write-Error "Failed to retrieve countries: $_"
        }
    }
}


function Get-eWRCCalendar {
    <#
    .SYNOPSIS
        Retrieve rally calendar from eWRC-results.com by year and country.

    .PARAMETER Year
        The season year. Defaults to the current year.

    .PARAMETER Countries
        One or more country IDs. Defaults to Netherlands (24), Belgium (25), Germany (10).
        Use Get-eWRCCountries to find IDs.

    .PARAMETER CountryNames
        One or more country names (full or partial). Case-insensitive.
        Examples: "Netherlands", "Belgium", "Ger"
        Cannot be combined with -Countries.

    .OUTPUTS
        PSCustomObject with Id, Name, Season, From, Until, Days, Country, Flag, Slug, Cancelled and Url properties

    .EXAMPLE
        Get-eWRCCalendar

    .EXAMPLE
        Get-eWRCCalendar -Countries 24, 25

    .EXAMPLE
        Get-eWRCCalendar -CountryNames "Netherlands", "Belgium", "Germany"

    .EXAMPLE
        Get-eWRCCalendar -Year 2025 -CountryNames "Finland", "Sweden", "Norway"

    .EXAMPLE
        Get-eWRCCalendar | Where-Object Cancelled -eq 0 | Sort-Object From | Format-Table
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$Year = (Get-Date).Year,

        [Parameter(Mandatory = $false)]
        [int[]]$Countries,

        [Parameter(Mandatory = $false)]
        [string[]]$CountryNames
    )

    begin {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    }

    process {

        # Validate that both aren't supplied at the same time
        if ($Countries -and $CountryNames) {
            Write-Error "Please use either -Countries or -CountryNames, not both."
            return
        }

        # Resolve country names to IDs if -CountryNames was used
        if ($CountryNames) {
            $allCountries = Get-eWRCCountries -Year $Year

            $resolvedIds = @()
            foreach ($name in $CountryNames) {
                $match = $allCountries | Where-Object { $_.Name -like "*$name*" }

                if (-not $match) {
                    Write-Warning "No country found matching '$name'. Skipping."
                    continue
                }

                if ($match.Count -gt 1) {
                    Write-Warning "Multiple countries matched '$name': $($match.Name -join ', '). Using first match: $($match[0].Name) (Id=$($match[0].Id))."
                    $resolvedIds += $match[0].Id
                } else {
                    Write-Verbose "Resolved '$name' to '$($match.Name)' (Id=$($match.Id))"
                    $resolvedIds += $match.Id
                }
            }

            if ($resolvedIds.Count -eq 0) {
                Write-Error "No valid countries could be resolved from the provided names."
                return
            }

            $Countries = $resolvedIds
        }

        # Fall back to default countries if neither parameter was supplied
        if (-not $Countries) {
            $Countries = @(24, 25, 10)
        }

        # Build query string: first is nat=, then nat2=, nat3=, etc.
        $natParams = @()
        for ($i = 0; $i -lt $Countries.Count; $i++) {
            $paramName = if ($i -eq 0) { "nat" } else { "nat$($i + 1)" }
            $natParams += "$paramName=$($Countries[$i])"
        }
        $queryString = $natParams -join "&"
        $url = "https://api-next.ewrc-results.com/calendar/$Year/list?$queryString"

        Write-Verbose "Querying: $url"

        try {
            $response = Invoke-RestMethod -Uri $url

            foreach ($week in $response) {
                if ($week.events.Count -eq 0) { continue }

                foreach ($event in $week.events) {
                    [PSCustomObject]@{
                        Id        = $event.id
                        Name      = $event.name
                        Season    = $event.season
                        From      = $event.from
                        Until     = $event.until
                        Days      = $event.days
                        Country   = $event.shortcut
                        Flag      = $event.flag
                        Slug      = $event.slug
                        Cancelled = $event.cancelled
                        Url       = "https://www.ewrc-results.com/event/$($event.id)-$($event.slug)/"
                    }
                }
            }
        } catch {
            Write-Error "Calendar request failed: $_"
        }
    }
}

function Get-eWRCEntries {
    <#
    .SYNOPSIS
        Retrieve the entry list for a rally event from eWRC-results.com.

    .PARAMETER EventId
        The numeric event ID (e.g. 91954)

    .PARAMETER EventSlug
        The event slug (e.g. "zuiderzee-rally-2025")

    .PARAMETER Url
        Full event URL from Get-eWRCCalendar. Can be used instead of EventId + EventSlug.

    .PARAMETER RallyName
        Optional. Rally name to tag each result with. When piping from Get-eWRCCalendar
        this is filled automatically from the Name property.

    .OUTPUTS
        PSCustomObject with RallyName, Driver, DriverId, CoDriver, CoDriverId properties

    .EXAMPLE
        Get-eWRCEntries -EventId 91954 -EventSlug "zuiderzee-rally-2025"

    .EXAMPLE
        Get-eWRCCalendar | Where-Object Name -like "*Zuiderzee*" | Get-eWRCEntries

    .EXAMPLE
        Get-eWRCCalendar -CountryNames "Netherlands" | Get-eWRCEntries | Where-Object DriverId -eq 154487
    #>

    [CmdletBinding(DefaultParameterSetName = 'ByParts')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'ByParts')]
        [int]$EventId,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByParts')]
        [string]$EventSlug,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByUrl', ValueFromPipelineByPropertyName = $true)]
        [string]$Url,

        [Parameter(Mandatory = $false, ValueFromPipelineByPropertyName = $true)]
        [string]$Name
    )

    begin {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

        $headers = @{
            "User-Agent"      = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            "Accept"          = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
            "Accept-Language" = "en-US,en;q=0.5"
            "Referer"         = "https://www.ewrc-results.com/"
        }
    }

    process {

        # Build entries URL
        if ($PSCmdlet.ParameterSetName -eq 'ByUrl') {
            $entriesUrl = $Url.TrimEnd('/') + "/entries"
        } else {
            $entriesUrl = "https://www.ewrc-results.com/event/$EventId-$EventSlug/entries"
        }

        # Use pipeline Name property as rally name, or fall back to slug
        $rallyName = if ($Name) { 
            $Name 
        } elseif ($PSCmdlet.ParameterSetName -eq 'ByParts') { 
            $EventSlug 
        } else { 
            $Url.TrimEnd('/').Split('/')[-1] -replace '^\d+-', ''
        }

        Write-Verbose "Fetching: $entriesUrl"

        try {
            $result = Invoke-WebRequest -Uri $entriesUrl -Headers $headers -UseBasicParsing

            # Use Links collection - much more reliable than regex on raw HTML
            $driverLinks   = $result.Links | Where-Object { $_.href -like "*/profile/*" }
            $coDriverLinks = $result.Links | Where-Object { $_.href -like "*/coprofile/*" }

            # Build a lookup of codriver IDs so we can pair them with drivers
            # Both collections are in the same order as the entry list
            $coDriverList = foreach ($link in $coDriverLinks) {
                $segment    = $link.href.Split("/")[2]
                $coDriverId = $segment.Split("-")[0]
                $coDriverName = ($segment.Replace($coDriverId, "")).Replace("-", " ").Trim()
                [PSCustomObject]@{
                    CoDriverId   = $coDriverId
                    CoDriver     = $coDriverName
                }
            }

            $index = 0
            foreach ($link in $driverLinks) {
                $segment  = $link.href.Split("/")[2]
                $driverId = $segment.Split("-")[0]
                $driver   = ($segment.Replace($driverId, "")).Replace("-", " ").Trim()

                # Pair with codriver at same index if available
                $coDriver   = if ($index -lt $coDriverList.Count) { $coDriverList[$index].CoDriver } else { "" }
                $coDriverId = if ($index -lt $coDriverList.Count) { $coDriverList[$index].CoDriverId } else { "" }

                [PSCustomObject]@{
                    RallyName  = $rallyName
                    Driver     = $driver
                    DriverId   = $driverId
                    CoDriver   = $coDriver
                    CoDriverId = $coDriverId
                }

                $index++
            }

        } catch {
            Write-Error "Failed to retrieve entries for '$entriesUrl': $_"
        }
    }
}

function Get-eWRCDriverStats {
    <#
    .SYNOPSIS
        Retrieve statistics for a driver or codriver from eWRC-results.com.

    .PARAMETER DriverId
        The numeric driver or codriver ID (e.g. 42820)

    .PARAMETER Id
        Alias for DriverId. Allows piping directly from Search-eWRC.

    .PARAMETER Type
        "driver" (default) or "codriver". Must match the type of the ID supplied.

    .OUTPUTS
        PSCustomObject with DriverId, Type, RaceType, Sections, DateOfBirth, Country,
        PhotoUrl and per-category stat properties.

    .EXAMPLE
        Get-eWRCDriverStats -DriverId 42820

    .EXAMPLE
        Get-eWRCDriverStats -DriverId 99999 -Type codriver

    .EXAMPLE
        # Pipe from Search-eWRC
        Search-eWRC -Query "Martijn van Hoek" | Where-Object Type -eq "driver" | Get-eWRCDriverStats
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [Alias("Id")]
        [int]$DriverId,

        [Parameter(Mandatory = $false)]
        [ValidateSet("driver", "codriver")]
        [string]$Type = "driver"
    )

    begin {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    }

    process {
        $endpoint = $Type   # "driver" or "codriver"

        # ── Profile fetch (photo, date of birth, country) — non-fatal if missing
        $photoUrl    = $null
        $dateOfBirth = $null
        $country     = $null
        try {
            $profileData = Invoke-RestMethod -Uri "https://api-next.ewrc-results.com/$endpoint/$DriverId"
            $photoUrl    = $profileData.photo
            $dateOfBirth = $profileData.dateOfBirth
            $country     = if ($profileData.nationality) { $profileData.nationality }
                           elseif ($profileData.country)  { $profileData.country }
                           else                           { $profileData.flag }
        } catch {
            Write-Verbose "Profile fetch failed for $endpoint $DriverId : $_"
        }

        # ── Categories / stats fetch
        try {
            $response = Invoke-RestMethod -Uri "https://api-next.ewrc-results.com/$endpoint/$DriverId/categories?all=true"

            $sections = $response.sections | ForEach-Object {
                [PSCustomObject]@{
                    SectionId   = $_.id
                    SectionName = $_.name.en
                }
            }

            foreach ($category in $response.categories) {
                $statsHash = [ordered]@{
                    DriverId    = $DriverId
                    Type        = $Type
                    RaceType    = $category.raceType
                    Sections    = ($sections.SectionName -join ", ")
                    DateOfBirth = $dateOfBirth
                    Country     = $country
                    PhotoUrl    = $photoUrl
                }

                foreach ($stat in $category.stats) {
                    $statName = $stat.key -replace "^lg_", ""
                    $statsHash[$statName] = $stat.value
                    if ($null -ne $stat.percentage) {
                        $statsHash["${statName}_pct"] = $stat.percentage
                    }
                }

                [PSCustomObject]$statsHash
            }

        } catch {
            Write-Error "Failed to retrieve stats for $Type ID '$DriverId': $_"
        }
    }
}
