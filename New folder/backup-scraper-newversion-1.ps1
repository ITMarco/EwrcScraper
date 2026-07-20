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
	
	$RallyCodesListbox.Items.Add($global:Config.CountryNLnumber)
	$RallyCodesListbox.Items.Add($global:Config.CountryBELnumber)
	$RallyCodesListbox.Items.Add($global:Config.CountryGERnumber)
 
	#$checkedlistbox2.Items[0].checked = $true
	&$checkboxDebugOnoff_CheckedChanged
	$checkedlistbox2.Invoke([Action]{
			$checkedlistbox2.BeginUpdate()
			$checkedlistbox2.Items.Clear()
			$checkedlistbox2.Items.AddRange($global:Config.RallySeasonSearchURLs)
			$checkedlistbox2.EndUpdate()
		})
	$checkedlistbox2.SetItemChecked(0,$true)
	$checkedlistbox2.Update()
	$global:CheckedRallySeasonSearchURLs = @($checkedlistbox2.CheckedItems)
	$datetimepicker1.Format = 'Custom'
	$datetimepicker1.CustomFormat = "yyyy"
	$datetimepicker1.ShowUpDown = $true
	$Global:RallyEvents = @(Get-RallyEvents -Year $datetimepicker1.Value.Year -CountryCode $global:Config.CountryNLnumber)
	Update-ListBox -Items ($global:RallyEvents | select -ExpandProperty Name) -ListBox $checkedlistbox1
	$buttonExportToCSV.Enabled = $false
	$buttonVergelijk.Enabled = $false
	If (Test-Path -LiteralPath "$($env:USERPROFILE)\documents\leden.csv")
	{
		$CVSFileContents = Import-Csv "$($env:USERPROFILE)\documents\leden.csv" -Delimiter ";"
		#Write-host $CVSFileContents
		$Global:RCHLeden = $CVSFileContents
		$datagridview2.DataSource = ConvertTo-DataTable -InputObject $CVSFileContents
	}
	$Global:CountryNL = $config.CountryNLnumber
	$Global:CountryBEL = $config.CountryBELnumber
	$Global:CountryGER = $config.CountryGERnumber
	
	
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
		$Year = $CurrentYear)
	$ScrapedLinks=@()
	#Foreach ($SearchURL in $Config.RallySeasonSearchURLs) {
		Foreach ($SearchURL in $global:CheckedRallySeasonSearchURLs){
		
		$URL = ((($SearchURL).replace("COUNTRYNAME", "nederland")).replace("COUNTRYNUMBER", $config.CountryNLnumber)).replace("YEAR", $Year)
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


$pictureboxSplashScreenHidden_Click = {
	#TODO: Place custom script here
	
}

$datetimepicker1_ValueChanged = {
	#TODO: Place custom script here

}

$datagrid1_Navigate = [System.Windows.Forms.NavigateEventHandler]{
	#Event Argument: $_ = [System.Windows.Forms.NavigateEventArgs]
	#TODO: Place custom script here
	
}


$checkboxNL_CheckedChanged={
	#TODO: Place custom script here
	
}

$buttonRallyInfoOphalen_Click={
	#TODO: Place custom script here
	$UrlBase = "https://ewrc-results.com/entries"
	$GetCheckedItems = $checkedlistbox1.CheckedIndices
	#Write-host $GetCheckedItems
	$AllURLS = @()
	Foreach ($CheckedRally in $GetCheckedItems) {
		Write-host "doing $(($checkedlistbox1.items[$CheckedRally]))"
		$CurrentEventObj = $Global:RallyEvents | where { $_.Name -eq ($checkedlistbox1.items[$CheckedRally])  }
		$LoopRallyURL = $CurrentEventObj.EventURL
		[string]$yearstr = $datetimepicker1.Value.Year
		$LoopRallyURL = $LoopRallyURL.replace("/final-results", "-$yearstr/entries")
		$AllURLS += $LoopRallyURL
	}
	Write-host "$AllURLS"

	$global:AllRallyResults = get-rallyresults -RallyURL $AllURLS
	$datagridview1.DataSource = ConvertTo-DataTable -InputObject $global:AllRallyResults
	$Global:RCHledenInRally = @()
	$datagridview3.DataSource = ConvertTo-DataTable -InputObject $Global:RCHledenInRally
	if (($global:RCHLeden.count -gt 0) -and ($global:AllRallyResults.count -gt 0))
		{
			$buttonExportToCSV.Enabled = $true
			$buttonVergelijk.Enabled = $true
		}
	#Update-DataGridView -DataGridView $datagridview1

}

<#
Function Get-RCHLedenfromRallyEntries($AllURLS)
{
	
	$RallyEntries = get-rallyresults -RallyURL $AllURLS
	
	$oEntryArr = @()
	
	
	ForEach ($rallyEntry in $RallyEntries)
	{
		[string]$Tstnr = $rallyEntry.number
		$oEntry = $Global:RCHLeden.EwrcNrPilot.IndexOf("$($rallyEntry.number)")
		
		if ($oEntry -ne -1)
		{
			$Global:RCHLeden[$oEntry]
			$oEntryArr += $Global:RCHLeden[$oEntry]
			
		}
		
		$oEntry = $Global:RCHLeden.EwrcNrCoPilot.IndexOf("$($rallyEntry.number)")
		
		if ($oEntry -ne -1)
		{
			$Global:RCHLeden[$oEntry]
			$oEntryArr += $Global:RCHLeden[$oEntry]
			
		}
		
	}
	
	Return, $oEntryArr
}
#>

Function get-rallyresults
{
	Param ($RallyURL)
	$AllURLS = @($RallyURL.Split(","))
	#$AllURLS[0]
	$HashTable = @()
	Foreach ($URL in $AllURLS)
	{
		Write-host "Invoking URL in function Get-RallyResults: $URL"
		$result = Invoke-WebRequest -Uri $url -UseBasicParsing
		$codrivers = ($result.Links | where { ($_ -like "*coprofile*") })
		$drivers = ($result.Links | where { ($_ -like "*/profile*") })
		[string]$yearstr = $datetimepicker1.Value.Year
		$RallyObjName = $URL.replace("-$yearstr/entries", "/final-results")
		Write-host "name should be $RallyObjName"
		$rallyName = ($RallyEvents | where { $_.EventUrl -eq $RallyObjName }).name
		Write-host "found name $rallyname"
	
		ForEach ($line in $drivers)
		{
			$DriverLine = $line.href.Split("/")[2]
			$DriverNumber = $DriverLine.split("-")[0]
			$DriverName = ($DriverLine.replace($DriverNumber, " ")).replace("-", " ")
			$oObj = New-Object pscustomobject
			$oObj | Add-Member -NotePropertyName "Name" -NotePropertyValue $Drivername.trim()
			$oObj | Add-Member -NotePropertyName "Number" -NotePropertyValue $DriverNumber.Trim()
			$oObj | Add-Member -NotePropertyName "Type" -NotePropertyValue "Driver"
			$oObj | Add-Member -NotePropertyName "RallyName" -NotePropertyValue $rallyName
			$hashtable += $oObj
		}
		ForEach ($line in $codrivers)
		{
			$DriverLine = $line.href.Split("/")[2]
			$DriverNumber = $DriverLine.split("-")[0]
			$DriverName = ($DriverLine.replace($DriverNumber, " ")).replace("-", " ")
			$oObj = New-Object pscustomobject
			$oObj | Add-Member -NotePropertyName "Name" -NotePropertyValue $Drivername.trim()
			$oObj | Add-Member -NotePropertyName "Number" -NotePropertyValue $DriverNumber.Trim()
			$oObj | Add-Member -NotePropertyName "Type" -NotePropertyValue "Codriver"
			$oObj | Add-Member -NotePropertyName "RallyName" -NotePropertyValue $rallyName
			$hashtable += $oObj
		}
		
		
	}
	Return, $HashTable
}

Function Write-host
{
	Param (
		[Parameter(Position = 0)]
		$text
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
	#TODO: Place custom script here
	$CountryCodeString = ""
	$count=0
	[string]$yearstr = $datetimepicker1.Value.Year
	Write-host "current countrynumber = $($global:Config.CountryNLnumber)"
	$Global:RallyEvents =@()
	$Global:RallyEvents = @(Get-RallyEvents -Year $yearstr -CountryCode $global:Config.CountryNLnumber)
	Update-ListBox -Items ($global:RallyEvents | select -expandproperty Name) -ListBox $checkedlistbox1
}

$datagridview1_CellContentClick=[System.Windows.Forms.DataGridViewCellEventHandler]{
#Event Argument: $_ = [System.Windows.Forms.DataGridViewCellEventArgs]
	#TODO: Place custom script here
	
}

$buttonLedenlijstInladen_Click={
	#TODO: Place custom script here
	$OpenFileDialog = New-Object System.Windows.Forms.OpenFileDialog
	$OpenFileDialog.initialDirectory = $initialDirectory
	$OpenFileDialog.filter = "Csv files (*.csv)| *.csv"
	$OpenFileDialog.ShowDialog() | Out-Null
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
	#TODO: Place custom script here
	$oEntryArr = @()

	ForEach ($rallyEntry in $global:AllRallyResults)
	{
		#Write-host $rallyEntry
		[string]$Tstnr = $rallyEntry.number
		$oEntry = $Global:RCHLeden.EwrcNrPilot.IndexOf("$($rallyEntry.number)")
		if ($oEntry -ne -1)
		{
			#$Global:RCHLeden[$oEntry]
			$oTmpObj = $Global:RCHLeden[$oEntry]
			$oTmpObj.PSobject.properties.remove('EwrcURL')
			#$oTmpObj | Add-Member -NotePropertyName "Type" -notePropertyValue $rallyEntry.Type
			$oEntryArr += $oTmpObj
			
		}
		
		$oEntry = $Global:RCHLeden.EwrcNrCoPilot.IndexOf("$($rallyEntry.number)")
		
		if ($oEntry -ne -1)
		{
			#$Global:RCHLeden[$oEntry]
			$oTmpObj = $Global:RCHLeden[$oEntry]
			$oTmpObj.PSobject.properties.remove('EwrcURL')
			#$oTmpObj | Add-Member -NotePropertyName "Type" -notePropertyValue $rallyEntry.Type
			$oEntryArr += $oTmpObj

		}
		
		
		$inschrijversUnique = @()
		$inschrijversUnique += $oEntryArr[0]
		
		$oEntryArr | %{
			
			if ($inschrijversUnique.'Leden Nr.'.IndexOf($_.'Leden Nr.') -eq -1)
			{
				$inschrijversUnique += $_
			}
			
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

$savefiledialog1_FileOk=[System.ComponentModel.CancelEventHandler]{
#Event Argument: $_ = [System.ComponentModel.CancelEventArgs]
	#TODO: Place custom script here

}

$buttonOpzoeken_Click= {
	#TODO: Place custom script here
	$picturebox1.Image = $null
	$picturebox1.Show()
	$picturebox2.Image = $null
	$picturebox2.Show()
	[string]$Zoeknaam = $($NameLookup1.Text)
	$zoeknaam = $Zoeknaam.Replace(" ", "+")
	$zoeknaam = $Zoeknaam.Replace("++", "+")
	$searchURL = "https://ewrc-results.com/search/?find=$zoeknaam&nat=0"
	Write-host "Invoking URL: $searchURL"
	$result = Invoke-WebRequest $searchURL
	$ZoeknaamFound = $false
	If ($result.Content.IndexOf("profile/") -ne -1)
	{
		$ZoeknaamFound = $true
	}
	If ($ZoeknaamFound)
	{
		$oTempresults = @()
		foreach ($tempresult in $result.links)
		{
			if ($tempresult.innerHTML -notlike "*.jpg*")
			{
				if ($tempresult.title -like "*driver*")
				{
					$oTempresults += $tempresult
				}
			}
		}
	}
	$MatchedObjects=@()
	$CopilotFound = $false
	$PilotFound = $false
	If ($ZoeknaamFound)
	{
		Foreach ($tempnr in $oTempresults)
		{
			$oAddObj = new-object pscustomobject
			$oAddObj | Add-Member -NotePropertyName "Naam" -NotePropertyValue $Zoeknaam
			
			#write-host $($tempnr.href)
			If ($tempnr.href -like "*coprofile*")
			{
				$number = ((($tempnr | where { ($_.href -like "*profile*") })[0].href).split("/")[2]).split("-")[0]
				$oAddObj | Add-Member -NotePropertyName "EWRCNummer" -NotePropertyValue $number
				$oAddObj | Add-Member -NotePropertyName "PilootinfoURL" -NotePropertyValue $tempnr.href
				$oAddObj | Add-Member -NotePropertyName "type" -NotePropertyValue "Co-driver"
				$CopilotFound = $true
			}
			Else
			{
				$number = ((($tempnr | where { ($_.href -like "*profile*") })[0].href).split("/")[2]).split("-")[0]
				$oAddObj | Add-Member -NotePropertyName "EWRCNummer" -NotePropertyValue $number
				$oAddObj | Add-Member -NotePropertyName "PilootInfoURL" -NotePropertyValue $tempnr.href
				$oAddObj | Add-Member -NotePropertyName "type" -NotePropertyValue "Driver"
				$PilotFound = $true
			}
			$MatchedObjects += $oAddObj
		}
		
	}
	$LookupResultRichTextbox1.Text=""
	Foreach ($result in $MatchedObjects)
	{
		$LookupResultRichTextbox1.Text += "$($result.type) : $($result.EWRCNummer)`r`n"
	}
	If($MatchedObjects.count -gt 0){
	$EWRCURL = "https://ewrc-results.com"
	$URL1 = $EWRCURL + $MatchedObjects[0].PilootInfoURL
	$page = Invoke-WebRequest $URL1
	Write-host "Invoking $URL1"
	$ImageURL = $page.Images.src | where { ($_ -notlike "*/images/*") -and ($_ -notlike "*/img/*") }
	$filename = ($ImageURL).Split("/")[($ImageURL).Split("/").count - 1]
	$shortfilename = "$env:TEMP\$filename"
	Write-host "Invoking $ImageURL"
	Invoke-WebRequest -Uri $ImageURL -OutFile $shortfilename
	$LoadImage1 = [System.Drawing.Image]::Fromfile((get-item $shortfilename))
	if($?){
			$picturebox1.Image = $LoadImage1
			$picturebox1.Show()
			Write-host "URL for (co)-driver: $URL1"
	}
		If ($MatchedObjects.count -gt 1)
		{
			$EWRCURL = "https://ewrc-results.com"
			$URL1 = $EWRCURL + $MatchedObjects[1].PilootInfoURL
			$page = Invoke-WebRequest $URL1
			Write-host "Invoking $URL1"
			$ImageURL = $page.Images.src | where { ($_ -notlike "*/images/*") -and ($_ -notlike "*/img/*") }
			$filename = ($ImageURL).Split("/")[($ImageURL).Split("/").count - 1]
			$shortfilename = "$env:TEMP\$filename"
			Write-host "Invoking $URL1"
			Invoke-WebRequest -Uri $ImageURL -OutFile $shortfilename
			$LoadImage2 = [System.Drawing.Image]::Fromfile((get-item $shortfilename))
			if ($?)
			{
				$picturebox2.Image = $LoadImage2
				$picturebox2.Show()
				Write-host "URL for (co)-driver: $URL1"
			}
		}
	 }else{
		#Add-Type -AssemblyName "System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
		[void][System.Windows.Forms.MessageBox]::Show('Niets gevonden', 'Unfortunately peanutbutter') # Casting the method to [void] suppresses the output. 
	}
}

$tabpage4_Click={
	#TODO: Place custom script here
	
}

$labelLandcode_Click={
	#TODO: Place custom script here
	
}

$panel1_Paint=[System.Windows.Forms.PaintEventHandler]{
#Event Argument: $_ = [System.Windows.Forms.PaintEventArgs]
	#TODO: Place custom script here
	
}

$LookupResultRichTextbox1_TextChanged={
	#TODO: Place custom script here
	
}

$NameLookup1_TextChanged={
	#TODO: Place custom script here
	
}

$linklabelFoto1_LinkClicked=[System.Windows.Forms.LinkLabelLinkClickedEventHandler]{
#Event Argument: $_ = [System.Windows.Forms.LinkLabelLinkClickedEventArgs]
	#TODO: Place custom script here
	
}

$checkboxDebugOnoff_CheckedChanged={
	#TODO: Place custom script here
	if ($checkboxDebugOnoff.Checked)
	{
		$OutputBox.Visible = $false
		$formRCHRallyScraper.Size.height =600
	}
	else
	{
		$OutputBox.Visible = $true
		$formRCHRallyScraper.Size.Height = 668
		$formRCHRallyScraper.SizeGripStyle = 'Hide'
	}
	$formRCHRallyScraper
	$formRCHRallyScraper.Update()
}

$tabcontrol1_MouseClick=[System.Windows.Forms.MouseEventHandler]{
#Event Argument: $_ = [System.Windows.Forms.MouseEventArgs]
	#TODO: Place custom script here
	if ($tabcontrol1.SelectedTab -eq $tabpage3) { &$buttonVergelijk_Click }
}

$buttonExit_Click={
	#TODO: Place custom script here
	$formRCHRallyScraper.Close()
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
	#TODO: Place custom script here
	#update de gebruikte array voor rally's ophalen
	
	if ($checkedlistbox2.CheckedItems.Count -lt 1) {
		$checkedlistbox2.SetItemChecked(0, $true)
	}
	$global:CheckedRallySeasonSearchURLs = $checkedlistbox2.CheckedItems
	$checkedlistbox2.Update()
	Write-host "checked now are: $global:CheckedRallySeasonSearchURLs"
	
}

$FindAllRallyButton_Click={
	#TODO: Place custom script here
	#https://ewrc-results.com/calendar?nat=0
	#https://ewrc-results.com/calendar?s=2025&week=2&month=12&nat=24
	$nat = $NameLookup1.Text
	$year= $datetimepicker1.Value.Year
	if($nat -eq ""){$nat="0"}
	For ($Month = 1; $Month -le 12; $Month++) {
		Write-Host $Month -ForegroundColor Yellow
		$myurl = "https://ewrc-results.com/calendar?s=dddd&nat=nnnn&month=mmmm&week=wwww"
		For ($week = 1; $week -le 4; $week++) {
			$url = $myurl.Replace("dddd", $year)
			$url = $url.Replace("nnnn", $nat)
			$url = $url.Replace("mmmm", $Month)
			$url = $url.Replace("wwww", $week)
			Write-host "Query = $url"
		}
	}
	
}