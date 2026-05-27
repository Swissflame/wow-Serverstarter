param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
dotnet publish "$root\src\WowServerStarter\WowServerStarter.csproj" -c Release -r $Runtime --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true /p:IncludeAllContentForSelfExtract=true /p:DebugType=none /p:DebugSymbols=false
Write-Host "EXE: $root\src\WowServerStarter\bin\Release\net10.0\$Runtime\publish\WowServerStarter.exe"
