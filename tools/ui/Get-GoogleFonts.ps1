# Downloads static TrueType files of a Google Fonts family (OFL) into the Studio assets.
# Google's CSS API serves static TrueType when the user agent is curl's.
param([string]$Family = 'JetBrains Mono', [int[]]$Weights = @(400, 500, 600),
      [string]$OutDir = "$PSScriptRoot/../../src/OpenGimbalCam.Studio/Assets/Fonts")
$ErrorActionPreference = 'Stop'
$q = $Family -replace ' ', '+'
$css = (Invoke-WebRequest -UseBasicParsing -UserAgent 'curl/8.0' "https://fonts.googleapis.com/css2?family=${q}:wght@$($Weights -join ';')").Content
$faces = [regex]::Matches($css, 'font-weight:\s*(\d+);\s*src:\s*url\(([^)]+)\)\s*format\(''truetype''\)')
if ($faces.Count -ne $Weights.Count) { throw "expected $($Weights.Count) TrueType faces, got $($faces.Count)" }
$names = @{ 400 = 'Regular'; 500 = 'Medium'; 600 = 'SemiBold'; 700 = 'Bold' }
New-Item -ItemType Directory -Force $OutDir | Out-Null
foreach ($m in $faces) {
  $file = Join-Path $OutDir ('{0}-{1}.ttf' -f ($Family -replace ' ', ''), $names[[int]$m.Groups[1].Value])
  Invoke-WebRequest -UseBasicParsing $m.Groups[2].Value -OutFile $file
}
