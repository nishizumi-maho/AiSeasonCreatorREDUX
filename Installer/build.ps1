<# Builds AiSeasonCreatorREDUX-<version>-win-x64.msi with WiX.
   Requires: dotnet SDK, WiX (`dotnet tool install --global wix`).
   The same UpgradeCode is reused every release so `msiexec /i` upgrades cleanly,
   which is what the in-app updater (Helpers/Updater.cs) downloads and runs.
   Usage: pwsh -File Installer/build.ps1 [-Version 26.0.2] #>
param([string]$Version = "")

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

if (-not $Version) {
    $csproj = Get-Content (Join-Path $repo "AiSeasonCreatorREDUX/AiSeasonCreatorREDUX.csproj") -Raw
    if ($csproj -notmatch "<Version>([^<]+)</Version>") { throw "Could not read <Version> from csproj" }
    $Version = $Matches[1]
}

# Stable across releases: upgrades work, side-by-side installs don't happen.
$UpgradeCode = "7E4B1E2A-3F0C-4D5A-9B1E-8C2D4A6F0E12"
$cultures = @("cs","de","es","fr","it","ja","ko","pl","pt-BR","ru","tr","zh-Hans","zh-Hant")

$stage = Join-Path ([System.IO.Path]::GetTempPath()) "opencode/msi-stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $repo "AiSeasonCreatorREDUX/AiSeasonCreatorREDUX.csproj") `
    -c Release -r win-x64 --self-contained true -o $stage --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

function SanitizeId([string]$s) {
    $s = [regex]::Replace($s, "[^A-Za-z0-9_.]", "_")
    if ($s -match "^[0-9]") { $s = "_" + $s }
    return $s
}

$script:dirN = 0; $script:fileN = 0
$dirs = New-Object System.Text.StringBuilder
$comps = New-Object System.Text.StringBuilder
function EmitDir([string]$path, [string]$dirId, [string]$dirName, [string]$indent) {
    [void]$dirs.AppendLine("$indent<Directory Id=`"$dirId`" Name=`"$dirName`">")
    foreach ($d in Get-ChildItem $path -Directory | Sort-Object Name) {
        if ($dirId -eq "INSTALLDIR" -and ($cultures -contains $d.Name)) { continue } # English-only app
        $script:dirN++
        EmitDir $d.FullName ("D_$($script:dirN)") $d.Name ($indent + "  ")
    }
    [void]$dirs.AppendLine("$indent</Directory>")
    foreach ($f in (Get-ChildItem $path -File | Sort-Object Name)) {
        $script:fileN++
        $fid = "F_$($script:fileN)_" + (SanitizeId $f.BaseName)
        if ($f.Name -ieq "AiSeasonCreatorREDUX.exe") { $fid = "MainExe" }
        $src = $f.FullName -replace "&","&amp;" -replace "<","&lt;" -replace ">","&gt;" -replace '"',"&quot;"
        [void]$comps.AppendLine("      <Component Directory=`"$dirId`" Bitness=`"always64`"><File Id=`"$fid`" Source=`"$src`" /></Component>")
    }
}

EmitDir $stage "INSTALLDIR" "AiSeasonCreatorREDUX" "        "
$tree = $dirs.ToString().TrimEnd()
$components = $comps.ToString().TrimEnd()

$outDir = Join-Path $PSScriptRoot "output"
New-Item $outDir -ItemType Directory -Force | Out-Null
$iconPath = Join-Path $outDir "app.ico"
Copy-Item (Join-Path $repo "AiSeasonCreatorREDUX/Assets/app.ico") $iconPath -Force

$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="AiSeasonCreatorREDUX" Language="1033" Version="$Version" Manufacturer="AiSeasonCreatorREDUX"
           UpgradeCode="$UpgradeCode" Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of AiSeasonCreatorREDUX is already installed." />
    <Property Id="ARPPRODUCTICON" Value="AppIcon" />
    <Media Id="1" Cabinet="product.cab" EmbedCab="yes" />
    <Feature Id="Main" Title="AiSeasonCreatorREDUX" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentRef Id="StartMenuShortcut" />
    </Feature>
  </Package>
  <Fragment>
    <StandardDirectory Id="ProgramFiles64Folder">
$tree
    </StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="AppShortcuts" Name="AiSeasonCreatorREDUX" />
    </StandardDirectory>
    <ComponentGroup Id="AppFiles">
$components
    </ComponentGroup>
    <Component Id="StartMenuShortcut" Directory="AppShortcuts" Bitness="always64">
      <Shortcut Id="AppShortcut" Name="AiSeasonCreatorREDUX" Target="[#MainExe]" Icon="AppIcon" />
      <RegistryValue Root="HKCU" Key="Software\AiSeasonCreatorREDUX" Name="installed" Value="1" Type="integer" KeyPath="yes" />
    </Component>
    <Icon Id="AppIcon" SourceFile="$iconPath" />
    <CustomAction Id="LaunchApp" Directory="INSTALLDIR" ExeCommand="[INSTALLDIR]AiSeasonCreatorREDUX.exe" Execute="immediate" Impersonate="yes" Return="asyncNoWait" />
    <InstallExecuteSequence>
      <Custom Action="LaunchApp" After="InstallFinalize" Condition="NOT Installed" />
    </InstallExecuteSequence>
  </Fragment>
</Wix>
"@

$wxsPath = Join-Path $outDir "product.wxs"
$msiPath = Join-Path $outDir "AiSeasonCreatorREDUX-$Version-win-x64.msi"
Set-Content $wxsPath $wxs -Encoding UTF8
Remove-Item $msiPath -Force -ErrorAction SilentlyContinue
wix build -acceptEula wix7 -arch x64 -o $msiPath $wxsPath
"Built: $msiPath"
