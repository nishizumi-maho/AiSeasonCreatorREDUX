<# Builds AiSeasonCreator-<version>-win-x64.msi with WiX.
   Requires: dotnet SDK, WiX (`dotnet tool install --global wix`).
   The same UpgradeCode is reused every release so `msiexec /i` upgrades cleanly,
   which is what the in-app updater (Helpers/Updater.cs) downloads and runs.
   Usage: pwsh -File Installer/build.ps1 [-Version 26.0.2] #>
param([string]$Version = "")

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

if (-not $Version) {
    $csproj = Get-Content (Join-Path $repo "AiSeasonCreator/AiSeasonCreator.csproj") -Raw
    if ($csproj -notmatch "<Version>([^<]+)</Version>") { throw "Could not read <Version> from csproj" }
    $Version = $Matches[1]
}

# Stable across releases: upgrades work, side-by-side installs don't happen.
$UpgradeCode = "7E4B1E2A-3F0C-4D5A-9B1E-8C2D4A6F0E12"
$cultures = @("cs","de","es","fr","it","ja","ko","pl","pt-BR","ru","tr","zh-Hans","zh-Hant")

$stage = Join-Path ([System.IO.Path]::GetTempPath()) "opencode/msi-stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $repo "AiSeasonCreator/AiSeasonCreator.csproj") `
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
        if ($f.Name -ieq "AiSeasonCreator.exe") { $fid = "MainExe" }
        $src = $f.FullName -replace "&","&amp;" -replace "<","&lt;" -replace ">","&gt;" -replace '"',"&quot;"
        [void]$comps.AppendLine("      <Component Directory=`"$dirId`" Bitness=`"always64`"><File Id=`"$fid`" Source=`"$src`" /></Component>")
    }
}

EmitDir $stage "INSTALLDIR" "AiSeasonCreator" "        "
$tree = $dirs.ToString().TrimEnd()
$components = $comps.ToString().TrimEnd()

$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="AiSeasonCreator" Language="1033" Version="$Version" Manufacturer="AiSeasonCreator"
           UpgradeCode="$UpgradeCode" Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of AiSeasonCreator is already installed." />
    <Media Id="1" Cabinet="product.cab" EmbedCab="yes" />
    <Feature Id="Main" Title="AiSeasonCreator" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentRef Id="StartMenuShortcut" />
    </Feature>
  </Package>
  <Fragment>
    <StandardDirectory Id="ProgramFiles64Folder">
$tree
    </StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="AppShortcuts" Name="AiSeasonCreator" />
    </StandardDirectory>
    <ComponentGroup Id="AppFiles">
$components
    </ComponentGroup>
    <Component Id="StartMenuShortcut" Directory="AppShortcuts" Bitness="always64">
      <Shortcut Id="AppShortcut" Name="AiSeasonCreator" Target="[#MainExe]" />
      <RegistryValue Root="HKCU" Key="Software\AiSeasonCreator" Name="installed" Value="1" Type="integer" KeyPath="yes" />
    </Component>
  </Fragment>
</Wix>
"@

$outDir = Join-Path $PSScriptRoot "output"
New-Item $outDir -ItemType Directory -Force | Out-Null
$wxsPath = Join-Path $outDir "product.wxs"
$msiPath = Join-Path $outDir "AiSeasonCreator-$Version-win-x64.msi"
Set-Content $wxsPath $wxs -Encoding UTF8
Remove-Item $msiPath -Force -ErrorAction SilentlyContinue
wix build -acceptEula wix7 -arch x64 -o $msiPath $wxsPath
"Built: $msiPath"
