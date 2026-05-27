# Build

## Voraussetzungen

- .NET SDK 10 oder neuer
- Windows 10/11 für Windows-Builds
- macOS für `.app`-Bundles

## Entwicklung starten

```bash
dotnet restore
dotnet run --project src/WowServerStarter/WowServerStarter.csproj
```

## Windows Single-EXE

```powershell
.\scripts\build-windows.ps1
```

Die EXE ist self-contained und benötigt keine installierte .NET Runtime.

## macOS APP

```bash
chmod +x scripts/build-macos-app.sh
./scripts/build-macos-app.sh osx-arm64
```

Alternativ:

```bash
./scripts/build-macos-app.sh osx-x64
```

Das Skript veröffentlicht die App self-contained und erstellt ein `.app`-Bundle unter `artifacts/`.

## Direkte Publish-Befehle

```bash
dotnet publish src/WowServerStarter/WowServerStarter.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
dotnet publish src/WowServerStarter/WowServerStarter.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
```
