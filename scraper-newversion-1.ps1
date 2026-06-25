#Requires -Version 5.0
<#
.SYNOPSIS
    RCH Rally Scraper — Standalone WinForms launcher.
.DESCRIPTION
    Builds and shows the RCH Rally Scraper form.
    All event handlers and helper functions live in scraper-functions.ps1,
    which is dot-sourced after the controls are created.
#>

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$global:initialDirectory = $PSScriptRoot   # overridden at Load time from JSON (LedenlijstPath)

# ─────────────────────────────────────────────────────────────────────────────
# FORM
# ─────────────────────────────────────────────────────────────────────────────
$formRCHRallyScraper               = New-Object System.Windows.Forms.Form
$formRCHRallyScraper.Text          = 'RCH Rally Scraper'
$formRCHRallyScraper.Width         = 1400
$formRCHRallyScraper.Height        = 668
$formRCHRallyScraper.StartPosition = 'CenterScreen'
$formRCHRallyScraper.MinimumSize   = [System.Drawing.Size]::new(900, 500)
$formRCHRallyScraper.SizeGripStyle = 'Show'

# ── Restore saved window position / size from ewrc-scraper.json ───────────────
$global:configPath = "$PSScriptRoot\ewrc-scraper.json"
if (Test-Path $global:configPath) {
    try {
        $ws = Get-Content $global:configPath -Raw | ConvertFrom-Json
        if ($null -ne $ws.WindowX) {
            $formRCHRallyScraper.StartPosition = 'Manual'
            $formRCHRallyScraper.Location      = [System.Drawing.Point]::new($ws.WindowX, $ws.WindowY)
            $formRCHRallyScraper.Size          = [System.Drawing.Size]::new($ws.WindowWidth, $ws.WindowHeight)
        }
    } catch { }
}

# ─────────────────────────────────────────────────────────────────────────────
# NON-VISUAL COMPONENTS
# ─────────────────────────────────────────────────────────────────────────────
$savefiledialog1            = New-Object System.Windows.Forms.SaveFileDialog
$savefiledialog1.Filter     = 'CSV files (*.csv)|*.csv'
$savefiledialog1.DefaultExt = 'csv'

# Hidden PictureBox — holds the splash image ref used in $formSplashScreen_Load
$pictureboxSplashScreenHidden         = New-Object System.Windows.Forms.PictureBox
$pictureboxSplashScreenHidden.Visible = $false
$pictureboxSplashScreenHidden.Size    = [System.Drawing.Size]::new(1, 1)
$formRCHRallyScraper.Controls.Add($pictureboxSplashScreenHidden)

# ─────────────────────────────────────────────────────────────────────────────
# OUTPUT BOX  (hidden by default; no Dock when hidden so it takes NO layout space.
#              Dock=Bottom is set dynamically when the debug checkbox is checked.)
# ─────────────────────────────────────────────────────────────────────────────
$OutputBox           = New-Object System.Windows.Forms.RichTextBox
$OutputBox.Dock      = [System.Windows.Forms.DockStyle]::None   # set to Bottom only when shown
$OutputBox.Height    = 120
$OutputBox.Visible   = $false
$OutputBox.ReadOnly  = $true
$OutputBox.BackColor = [System.Drawing.Color]::White
$OutputBox.Font      = [System.Drawing.Font]::new('Consolas', 9)

# Drag handle at the top edge of the debug area.
# A plain Panel instead of Splitter — Splitter requires the Fill panel to be directly adjacent.
$debugResizeHandle           = New-Object System.Windows.Forms.Panel
$debugResizeHandle.Dock      = [System.Windows.Forms.DockStyle]::None
$debugResizeHandle.Height    = 5
$debugResizeHandle.Visible   = $false
$debugResizeHandle.BackColor = [System.Drawing.SystemColors]::ControlDark
$debugResizeHandle.Cursor    = [System.Windows.Forms.Cursors]::HSplit

$script:_dbgDrag   = $false
$script:_dbgStartY = 0
$script:_dbgStartH = 0

$debugResizeHandle.add_MouseDown({
    if ($_.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
        $script:_dbgDrag           = $true
        $script:_dbgStartY         = [System.Windows.Forms.Cursor]::Position.Y
        $script:_dbgStartH         = $OutputBox.Height
        $debugResizeHandle.Capture = $true
    }
})
$debugResizeHandle.add_MouseMove({
    if (-not $script:_dbgDrag) { return }
    $delta = [System.Windows.Forms.Cursor]::Position.Y - $script:_dbgStartY
    $minH  = 40
    $maxH  = $formRCHRallyScraper.ClientSize.Height - $panelBottom.Height - $debugResizeHandle.Height - 100
    $OutputBox.Height = [Math]::Max($minH, [Math]::Min($script:_dbgStartH - $delta, $maxH))
})
$debugResizeHandle.add_MouseUp({
    $script:_dbgDrag           = $false
    $debugResizeHandle.Capture = $false
})

# ─────────────────────────────────────────────────────────────────────────────
# BOTTOM BAR — docking order matters (highest index = very bottom):
#   Add order: panelBottom → debugResizeHandle → OutputBox
#   Result (bottom to top): OutputBox | debugResizeHandle | panelBottom | panelMain
# ─────────────────────────────────────────────────────────────────────────────
$panelBottom           = New-Object System.Windows.Forms.Panel
$panelBottom.Dock      = [System.Windows.Forms.DockStyle]::Bottom
$panelBottom.Height    = 60
$panelBottom.BackColor = [System.Drawing.SystemColors]::Control
$formRCHRallyScraper.Controls.Add($panelBottom)
$formRCHRallyScraper.Controls.Add($debugResizeHandle)   # top edge of debug area
$formRCHRallyScraper.Controls.Add($OutputBox)           # highest index → very bottom when shown

$checkboxDebugOnoff            = New-Object System.Windows.Forms.CheckBox
$checkboxDebugOnoff.Text       = "Debug`non/off"
$checkboxDebugOnoff.Appearance = [System.Windows.Forms.Appearance]::Button
$checkboxDebugOnoff.FlatStyle  = [System.Windows.Forms.FlatStyle]::Flat
$checkboxDebugOnoff.Width      = 68
$checkboxDebugOnoff.Height     = 34
$checkboxDebugOnoff.Location   = [System.Drawing.Point]::new(2, 13)
$checkboxDebugOnoff.TextAlign  = [System.Drawing.ContentAlignment]::MiddleCenter
$panelBottom.Controls.Add($checkboxDebugOnoff)

$panelBottomRight        = New-Object System.Windows.Forms.Panel
$panelBottomRight.Dock   = [System.Windows.Forms.DockStyle]::Right
$panelBottomRight.Width  = 600
$panelBottom.Controls.Add($panelBottomRight)

$buttonLedenlijstInladen          = New-Object System.Windows.Forms.Button
$buttonLedenlijstInladen.Text     = 'Ledenlijst inladen'
$buttonLedenlijstInladen.Width    = 180
$buttonLedenlijstInladen.Height   = 28
$buttonLedenlijstInladen.Visible  = $false   # hidden until Tab 2 is active
$buttonLedenlijstInladen.Location = [System.Drawing.Point]::new(4, 16)
$panelBottomRight.Controls.Add($buttonLedenlijstInladen)

$buttonExit          = New-Object System.Windows.Forms.Button
$buttonExit.Text     = 'Exit'
$buttonExit.Width    = 90
$buttonExit.Height   = 28
$buttonExit.Location = [System.Drawing.Point]::new(500, 16)
$panelBottomRight.Controls.Add($buttonExit)

# ─────────────────────────────────────────────────────────────────────────────
# MAIN PANEL  (fills remaining space between title and bottom bar + output box)
# ─────────────────────────────────────────────────────────────────────────────
$panelMain      = New-Object System.Windows.Forms.Panel
$panelMain.Dock = [System.Windows.Forms.DockStyle]::Fill
$formRCHRallyScraper.Controls.Add($panelMain)

# ─────────────────────────────────────────────────────────────────────────────
# RIGHT SIDE: TAB CONTROL
# Added first so Dock=Fill gets whatever space the left panel leaves behind.
# ─────────────────────────────────────────────────────────────────────────────
$tabcontrol1      = New-Object System.Windows.Forms.TabControl
$tabcontrol1.Dock = [System.Windows.Forms.DockStyle]::Fill
$panelMain.Controls.Add($tabcontrol1)

#region Tab 1 — Stap1: Selecteer rally
$tabpage1      = New-Object System.Windows.Forms.TabPage
$tabpage1.Text = 'Stap1:Selecteer rally'

$datagridview1                        = New-Object System.Windows.Forms.DataGridView
$datagridview1.Dock                   = [System.Windows.Forms.DockStyle]::Fill
$datagridview1.AutoSizeColumnsMode    = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::Fill
$datagridview1.SelectionMode          = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
$datagridview1.MultiSelect            = $true
$datagridview1.ReadOnly               = $true
$datagridview1.AllowUserToAddRows     = $false
$tabpage1.Controls.Add($datagridview1)         # Fill first (processed last by layout)

# Small spacer so the grid doesn't sink behind the tab/form bottom edge.
$dgv1BottomSpacer        = New-Object System.Windows.Forms.Panel
$dgv1BottomSpacer.Dock   = [System.Windows.Forms.DockStyle]::Bottom
$dgv1BottomSpacer.Height = 6
$tabpage1.Controls.Add($dgv1BottomSpacer)      # Bottom second (processed first, reserves space)

$tabcontrol1.TabPages.Add($tabpage1)
#endregion

#region Tab 2 — Stap2: RCH Ledenlijst laden
$tabpage2      = New-Object System.Windows.Forms.TabPage
$tabpage2.Text = 'Stap2:RCH Ledenlijst laden'

$datagridview2                     = New-Object System.Windows.Forms.DataGridView
$datagridview2.Dock                = [System.Windows.Forms.DockStyle]::Fill
$datagridview2.AutoSizeColumnsMode = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::Fill
$datagridview2.SelectionMode       = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
$datagridview2.MultiSelect         = $true
$datagridview2.ReadOnly            = $true
$datagridview2.AllowUserToAddRows  = $false

# "Ledenlijst inladen" button is in the bottom bar (next to Exit) — always visible.
$tabpage2.Controls.Add($datagridview2)

$tabcontrol1.TabPages.Add($tabpage2)
#endregion

#region Tab 3 — Stap3: Vergelijken
$tabpage3      = New-Object System.Windows.Forms.TabPage
$tabpage3.Text = 'Stap3:Vergelijken'

$datagridview3                     = New-Object System.Windows.Forms.DataGridView
$datagridview3.Dock                = [System.Windows.Forms.DockStyle]::Fill
$datagridview3.AutoSizeColumnsMode = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::Fill
$datagridview3.SelectionMode       = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
$datagridview3.MultiSelect         = $true
$datagridview3.ReadOnly            = $true
$datagridview3.AllowUserToAddRows  = $false
$tabpage3.Controls.Add($datagridview3)

# Tab 3 controls live in the bottom bar — shown only when Tab 3 is active.
# Layout inside $panelBottomRight (Width=600):
#   [Ledenlijst inladen (Tab2)] OR [label | ToClipBoard | ExportToCSV (Tab3)] ... [Exit]
$labelTotaalAantal          = New-Object System.Windows.Forms.Label
$labelTotaalAantal.Text     = 'Totaal aantal matches: 0'
$labelTotaalAantal.Location = [System.Drawing.Point]::new(4, 44)
$labelTotaalAantal.AutoSize = $true
$labelTotaalAantal.Visible  = $false
$panelBottomRight.Controls.Add($labelTotaalAantal)

$buttonVergelijk          = New-Object System.Windows.Forms.Button
$buttonVergelijk.Text     = 'Vergelijk'
$buttonVergelijk.Width    = 90
$buttonVergelijk.Height   = 28
$buttonVergelijk.Location = [System.Drawing.Point]::new(4, 16)
$buttonVergelijk.Visible  = $false   # shown only when Tab 3 is active
$buttonVergelijk.Enabled  = $true
$panelBottomRight.Controls.Add($buttonVergelijk)

$ToClipBoard          = New-Object System.Windows.Forms.Button
$ToClipBoard.Text     = 'Copy e-mail adressen'
$ToClipBoard.Width    = 160
$ToClipBoard.Height   = 28
$ToClipBoard.Location = [System.Drawing.Point]::new(200, 16)
$ToClipBoard.Visible  = $false
$panelBottomRight.Controls.Add($ToClipBoard)

$buttonExportToCSV          = New-Object System.Windows.Forms.Button
$buttonExportToCSV.Text     = 'Export to CSV'
$buttonExportToCSV.Width    = 115
$buttonExportToCSV.Height   = 28
$buttonExportToCSV.Location = [System.Drawing.Point]::new(368, 16)
$buttonExportToCSV.Enabled  = $false
$buttonExportToCSV.Visible  = $false
$panelBottomRight.Controls.Add($buttonExportToCSV)

$tabcontrol1.TabPages.Add($tabpage3)
#endregion

#region Tab 4 — Ewrc nummer zoeken
$tabpage4      = New-Object System.Windows.Forms.TabPage
$tabpage4.Text = 'Ewrc nummer zoeken'

$panelTab4Top        = New-Object System.Windows.Forms.Panel
$panelTab4Top.Dock   = [System.Windows.Forms.DockStyle]::Top
$panelTab4Top.Height = 36

$NameLookup1          = New-Object System.Windows.Forms.TextBox
$NameLookup1.Location = [System.Drawing.Point]::new(4, 8)
$NameLookup1.Width    = 300
$panelTab4Top.Controls.Add($NameLookup1)

$buttonOpzoeken          = New-Object System.Windows.Forms.Button
$buttonOpzoeken.Text     = 'Opzoeken'
$buttonOpzoeken.Width    = 90
$buttonOpzoeken.Height   = 26
$buttonOpzoeken.Location = [System.Drawing.Point]::new(310, 5)
$panelTab4Top.Controls.Add($buttonOpzoeken)

$FindAllRallyButton          = New-Object System.Windows.Forms.Button
$FindAllRallyButton.Text     = 'Find All Rallies'
$FindAllRallyButton.Width    = 120
$FindAllRallyButton.Height   = 26
$FindAllRallyButton.Location = [System.Drawing.Point]::new(410, 5)
$panelTab4Top.Controls.Add($FindAllRallyButton)

# Photos panel (right side of tab 4)
$panelTab4Photos       = New-Object System.Windows.Forms.Panel
$panelTab4Photos.Dock  = [System.Windows.Forms.DockStyle]::Right
$panelTab4Photos.Width = 350

$picturebox1             = New-Object System.Windows.Forms.PictureBox
$picturebox1.Location    = [System.Drawing.Point]::new(4, 4)
$picturebox1.Size        = [System.Drawing.Size]::new(166, 166)
$picturebox1.SizeMode    = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
$picturebox1.BackColor   = [System.Drawing.Color]::LightGray
$picturebox1.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$panelTab4Photos.Controls.Add($picturebox1)

$picturebox2             = New-Object System.Windows.Forms.PictureBox
$picturebox2.Location    = [System.Drawing.Point]::new(178, 4)
$picturebox2.Size        = [System.Drawing.Size]::new(166, 166)
$picturebox2.SizeMode    = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
$picturebox2.BackColor   = [System.Drawing.Color]::LightGray
$picturebox2.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$panelTab4Photos.Controls.Add($picturebox2)

$LookupResultRichTextbox1          = New-Object System.Windows.Forms.RichTextBox
$LookupResultRichTextbox1.Dock     = [System.Windows.Forms.DockStyle]::Fill
$LookupResultRichTextbox1.ReadOnly = $true

# Docking order: Fill first, then Right, then Top (last added = processed first by layout)
$tabpage4.Controls.Add($LookupResultRichTextbox1) # Fill first
$tabpage4.Controls.Add($panelTab4Photos)           # Right second
$tabpage4.Controls.Add($panelTab4Top)              # Top last → processed first by layout

$tabcontrol1.TabPages.Add($tabpage4)
#endregion

# ─────────────────────────────────────────────────────────────────────────────
# LEFT PANEL
# Added after the tab control so Dock=Left steals 415px from its left edge.
# ─────────────────────────────────────────────────────────────────────────────
$panelLeft       = New-Object System.Windows.Forms.Panel
$panelLeft.Dock  = [System.Windows.Forms.DockStyle]::Left
$panelLeft.Width = 415
$panelMain.Controls.Add($panelLeft)

# GroupBox: Rally Selector
$groupboxRallySelector          = New-Object System.Windows.Forms.GroupBox
$groupboxRallySelector.Text     = 'Rally Selector'
$groupboxRallySelector.Location = [System.Drawing.Point]::new(5, 5)
$groupboxRallySelector.Width    = 225
$groupboxRallySelector.Height   = 310
$panelLeft.Controls.Add($groupboxRallySelector)

# Country multi-select — populated on Load from Get-eWRCCountries
$checkedlistbox2          = New-Object System.Windows.Forms.CheckedListBox
$checkedlistbox2.Location = [System.Drawing.Point]::new(10, 20)
$checkedlistbox2.Size     = [System.Drawing.Size]::new(200, 200)
$groupboxRallySelector.Controls.Add($checkedlistbox2)

$datetimepicker1              = New-Object System.Windows.Forms.DateTimePicker
$datetimepicker1.Format       = [System.Windows.Forms.DateTimePickerFormat]::Custom
$datetimepicker1.CustomFormat = 'yyyy'
$datetimepicker1.ShowUpDown   = $true
$datetimepicker1.Width        = 100
$datetimepicker1.Location     = [System.Drawing.Point]::new(10, 232)
$groupboxRallySelector.Controls.Add($datetimepicker1)

$buttonUpdateRallyLijst          = New-Object System.Windows.Forms.Button
$buttonUpdateRallyLijst.Text     = 'Update rally lijst'
$buttonUpdateRallyLijst.Width    = 140
$buttonUpdateRallyLijst.Height   = 26
$buttonUpdateRallyLijst.Location = [System.Drawing.Point]::new(10, 272)
$groupboxRallySelector.Controls.Add($buttonUpdateRallyLijst)

# "Rally lijst" label
$labelRallyLijst          = New-Object System.Windows.Forms.Label
$labelRallyLijst.Text     = 'Rally lijst'
$labelRallyLijst.Location = [System.Drawing.Point]::new(8, 320)
$labelRallyLijst.AutoSize = $true
$panelLeft.Controls.Add($labelRallyLijst)

# Filter TextBox — type to filter the rally list below
$labelRallyFilter          = New-Object System.Windows.Forms.Label
$labelRallyFilter.Text     = 'Filter:'
$labelRallyFilter.Location = [System.Drawing.Point]::new(8, 339)
$labelRallyFilter.AutoSize = $true
$panelLeft.Controls.Add($labelRallyFilter)

$textboxRallyFilter          = New-Object System.Windows.Forms.TextBox
$textboxRallyFilter.Location = [System.Drawing.Point]::new(50, 336)
$textboxRallyFilter.Width    = 355
$textboxRallyFilter.Height   = 22
$panelLeft.Controls.Add($textboxRallyFilter)

# Rally list (user selects which rallies to process for Rally info ophalen)
$checkedlistbox1          = New-Object System.Windows.Forms.CheckedListBox
$checkedlistbox1.Location = [System.Drawing.Point]::new(5, 363)
$checkedlistbox1.Width    = 400
$checkedlistbox1.Height   = 300
$checkedlistbox1.Anchor   = [System.Windows.Forms.AnchorStyles]'Top, Bottom, Left, Right'
$panelLeft.Controls.Add($checkedlistbox1)

# Hidden country-code store — kept for backward compatibility with any remaining references
$RallyCodesListbox          = New-Object System.Windows.Forms.ListBox
$RallyCodesListbox.Visible  = $false
$RallyCodesListbox.Location = [System.Drawing.Point]::new(0, 0)
$RallyCodesListbox.Size     = [System.Drawing.Size]::new(1, 1)
$panelLeft.Controls.Add($RallyCodesListbox)

# "Rally info ophalen" wide button anchored to bottom of left panel
$buttonRallyInfoOphalen          = New-Object System.Windows.Forms.Button
$buttonRallyInfoOphalen.Text     = 'Rally info ophalen'
$buttonRallyInfoOphalen.Width    = 400
$buttonRallyInfoOphalen.Height   = 35
$ButtonRallyInfoPlekje = $checkedlistbox1.Location.Y
$ButtonRallyInfoPlekje = $ButtonRallyInfoPlekje + 300
#$buttonRallyInfoOphalen.Location = [System.Drawing.Point]::new(5, 480)
$buttonRallyInfoOphalen.Location = [System.Drawing.Point]::new(5, $ButtonRallyInfoPlekje)
$buttonRallyInfoOphalen.Anchor   = [System.Windows.Forms.AnchorStyles]'Bottom, Left, Right'
$panelLeft.Controls.Add($buttonRallyInfoOphalen)

# ─────────────────────────────────────────────────────────────────────────────
# DOT-SOURCE FUNCTIONS & EVENT HANDLER LIBRARY
# ─────────────────────────────────────────────────────────────────────────────
. "$PSScriptRoot\scraper-functions.ps1"

# ─────────────────────────────────────────────────────────────────────────────
# FIX INVERTED DEBUG HANDLER (scraper-functions.ps1 has the if/else backwards:
# Checked=true hides output, Checked=false shows it.  Override it here so that
# Checked=true → debug ON → OutputBox visible, and no hard-coded form height.)
# ─────────────────────────────────────────────────────────────────────────────
$checkboxDebugOnoff_CheckedChanged = {
    if ($checkboxDebugOnoff.Checked) {
        $OutputBox.Dock            = [System.Windows.Forms.DockStyle]::Bottom
        $OutputBox.Visible         = $true
        $debugResizeHandle.Dock    = [System.Windows.Forms.DockStyle]::Bottom
        $debugResizeHandle.Visible = $true
        if ($formRCHRallyScraper.WindowState -eq [System.Windows.Forms.FormWindowState]::Normal) {
            $formRCHRallyScraper.Height += $OutputBox.Height + $debugResizeHandle.Height
        }
    } else {
        if ($OutputBox.Visible -and $formRCHRallyScraper.WindowState -eq [System.Windows.Forms.FormWindowState]::Normal) {
            $formRCHRallyScraper.Height -= ($OutputBox.Height + $debugResizeHandle.Height)
        }
        $debugResizeHandle.Visible = $false
        $debugResizeHandle.Dock    = [System.Windows.Forms.DockStyle]::None
        $OutputBox.Visible         = $false
        $OutputBox.Dock            = [System.Windows.Forms.DockStyle]::None
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# WIRE EVENTS
# ─────────────────────────────────────────────────────────────────────────────
$formRCHRallyScraper.add_Load({
    & $formSplashScreen_Load
})

$formRCHRallyScraper.add_FormClosing({
    # Save window position/size into ewrc-scraper.json (merged with existing config)
    if ($formRCHRallyScraper.WindowState -eq [System.Windows.Forms.FormWindowState]::Normal) {
        try {
            $cfg = if (Test-Path $global:configPath) {
                Get-Content $global:configPath -Raw | ConvertFrom-Json
            } else { [PSCustomObject]@{} }
            $cfg | Add-Member -NotePropertyName 'WindowX'      -NotePropertyValue $formRCHRallyScraper.Left   -Force
            $cfg | Add-Member -NotePropertyName 'WindowY'      -NotePropertyValue $formRCHRallyScraper.Top    -Force
            $cfg | Add-Member -NotePropertyName 'WindowWidth'  -NotePropertyValue $formRCHRallyScraper.Width  -Force
            $cfg | Add-Member -NotePropertyName 'WindowHeight' -NotePropertyValue $formRCHRallyScraper.Height -Force
            $cfg | ConvertTo-Json -Depth 10 | Set-Content $global:configPath -Encoding utf8
        } catch { }
    }
})

$buttonUpdateRallyLijst.add_Click($buttonUpdateRallyLijst_Click)
$buttonRallyInfoOphalen.add_Click($buttonRallyInfoOphalen_Click)
$buttonLedenlijstInladen.add_Click($buttonLedenlijstInladen_Click)
$buttonVergelijk.add_Click($buttonVergelijk_Click)
$buttonExportToCSV.add_Click($buttonExportToCSV_Click)
$ToClipBoard.add_Click($ToClipBoard_Click)
$buttonExit.add_Click($buttonExit_Click)
$buttonOpzoeken.add_Click($buttonOpzoeken_Click)
$FindAllRallyButton.add_Click($FindAllRallyButton_Click)
$checkboxDebugOnoff.add_CheckedChanged($checkboxDebugOnoff_CheckedChanged)
$tabcontrol1.add_MouseClick($tabcontrol1_MouseClick)
$checkedlistbox2.add_ItemCheck($checkedlistbox2_SelectedValueChanged)

$textboxRallyFilter.add_TextChanged({
    $filter = $textboxRallyFilter.Text.Trim()
    $names = if ($filter) {
        @($Global:RallyEvents | Where-Object { $_.Name -like "*$filter*" } | Select-Object -ExpandProperty Name)
    } else {
        @($Global:RallyEvents | Select-Object -ExpandProperty Name)
    }
    $checkedNames = @($checkedlistbox1.CheckedItems)
    $checkedlistbox1.BeginUpdate()
    $checkedlistbox1.Items.Clear()
    foreach ($n in $names) { [void]$checkedlistbox1.Items.Add($n) }
    for ($i = 0; $i -lt $checkedlistbox1.Items.Count; $i++) {
        if ($checkedlistbox1.Items[$i] -in $checkedNames) {
            $checkedlistbox1.SetItemChecked($i, $true)
        }
    }
    $checkedlistbox1.EndUpdate()
})

$tabcontrol1.add_SelectedIndexChanged({
    $onTab2 = $tabcontrol1.SelectedIndex -eq 1
    $onTab3 = $tabcontrol1.SelectedIndex -eq 2
    $buttonLedenlijstInladen.Visible = $onTab2
    $buttonVergelijk.Visible         = $onTab3
    $labelTotaalAantal.Visible       = $onTab3
    $ToClipBoard.Visible             = $onTab3
    $buttonExportToCSV.Visible       = $onTab3
    if ($onTab3) { & $buttonVergelijk_Click }
})

# ─────────────────────────────────────────────────────────────────────────────
# SHOW FORM
# ─────────────────────────────────────────────────────────────────────────────
[void]$formRCHRallyScraper.ShowDialog()
