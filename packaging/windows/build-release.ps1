#requires -Version 7
<#
.SYNOPSIS
  Builds the win-x64 release assets: portable zip, Inno Setup installer and SHA256SUMS.txt.
.PARAMETER OutDir
  Output folder, relative to the repository root. It is deleted and recreated.
.PARAMETER SkipInstaller
  Skips the Inno Setup step (use when ISCC.exe is not installed).
#>
param([string]$OutDir = 'artifacts', [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = @($props.Project.PropertyGroup.Version)[0]; if (-not $version) { throw 'Version not found' }
$name = "OpenGimbalCam-$version-win-x64"
$out = Join-Path $root $OutDir; $stage = Join-Path $out $name; $tmp = Join-Path $out 'publish'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage, $tmp | Out-Null
$publish = '-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true',
  '-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-p:DebugType=none','-p:DebugSymbols=false'
foreach ($proj in 'src/OpenGimbalCam.Studio/OpenGimbalCam.Studio.csproj', 'src/OpenGimbalCam.Cli/OpenGimbalCam.Cli.csproj') {
  $dest = Join-Path $tmp ([IO.Path]::GetFileNameWithoutExtension($proj))
  dotnet publish (Join-Path $root $proj) @publish -o $dest; if ($LASTEXITCODE) { throw "publish failed: $proj" }
  $extra = Get-ChildItem $dest -File | Where-Object Extension -ne '.exe'
  if ($extra) { throw "unexpected files next to the exe: $($extra.Name -join ', ')" }
  Copy-Item (Join-Path $dest '*.exe') $stage
}
'LICENSE','TRADEMARKS.md','THIRD-PARTY-NOTICES.md','README.md','packaging/windows/LEIA-ME.txt' |
  ForEach-Object { Copy-Item (Join-Path $root $_) $stage }
Copy-Item (Join-Path $root 'licenses') (Join-Path $stage 'licenses') -Recurse
$assets = @(Join-Path $out "$name.zip"); Compress-Archive -Path $stage -DestinationPath $assets[0]
if (-not $SkipInstaller) {
  $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
  if (-not (Test-Path $iscc)) { throw "Inno Setup 6 not found ($iscc); use -SkipInstaller" }
  & $iscc "/DAppVersion=$version" "/DStageDir=$stage" "/DOutputDir=$out" (Join-Path $root 'packaging/windows/OpenGimbalCam.iss')
  if ($LASTEXITCODE) { throw 'ISCC failed' }
  $assets += Join-Path $out "$name-setup.exe"
}
$assets | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $_ -Leaf) } |
  Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
Remove-Item $tmp -Recurse -Force
Write-Host "Done: $out"
