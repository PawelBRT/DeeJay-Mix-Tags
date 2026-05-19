param(
	[string]$Configuration = 'Release',
	[string]$RuntimeIdentifier = 'win-x64',
	[string]$ProjectPath = 'DeeJayMixTags\DeeJayMixTags.csproj',
	[string]$OutputRoot = 'artifacts\portable'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRelative = Join-Path $OutputRoot $RuntimeIdentifier
$publishDir = Join-Path $repoRoot $publishRelative
$zipRelative = Join-Path $OutputRoot "$RuntimeIdentifier.zip"
$projectFullPath = Join-Path $repoRoot $ProjectPath
$publishFullPath = $publishDir
$zipFullPath = Join-Path $repoRoot $zipRelative

if (-not (Test-Path $projectFullPath)) {
	throw "Nie znaleziono projektu: $projectFullPath"
}

if (Test-Path $publishFullPath) {
	Remove-Item $publishFullPath -Recurse -Force
}

New-Item -ItemType Directory -Path $publishFullPath -Force | Out-Null

Write-Host "Publishing $projectFullPath -> $publishFullPath"

dotnet publish $projectFullPath `
	-c $Configuration `
	-r $RuntimeIdentifier `
	--self-contained true `
	-p:PublishSingleFile=true `
	-p:IncludeNativeLibrariesForSelfExtract=true `
	-p:PublishTrimmed=false `
	-p:PublishReadyToRun=false `
	-p:PublishAot=false `
	-o $publishFullPath

if (Test-Path $zipFullPath) {
	Remove-Item $zipFullPath -Force
}

$zipParent = Split-Path -Parent $zipFullPath
if (-not (Test-Path $zipParent)) {
	New-Item -ItemType Directory -Path $zipParent -Force | Out-Null
}

Compress-Archive -Path (Join-Path $publishFullPath '*') -DestinationPath $zipFullPath -Force

Write-Host "Publish completed: $publishFullPath"
Write-Host "Zip created: $zipFullPath"
