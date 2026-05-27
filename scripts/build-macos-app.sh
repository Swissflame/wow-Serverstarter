#!/usr/bin/env bash
set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/WowServerStarter/WowServerStarter.csproj"
PUBLISH="$ROOT/src/WowServerStarter/bin/Release/net10.0/$RID/publish"
APP="$ROOT/artifacts/wow serverstarter-$RID.app"
CONTENTS="$APP/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"

dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true /p:IncludeAllContentForSelfExtract=true /p:DebugType=none /p:DebugSymbols=false

rm -rf "$APP"
mkdir -p "$MACOS" "$RESOURCES"
cp "$PUBLISH/WowServerStarter" "$MACOS/wow-serverstarter"
chmod +x "$MACOS/wow-serverstarter"

cat > "$CONTENTS/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>wow serverstarter</string>
  <key>CFBundleDisplayName</key>
  <string>wow serverstarter</string>
  <key>CFBundleIdentifier</key>
  <string>local.wow.serverstarter</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleExecutable</key>
  <string>wow-serverstarter</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

echo "APP: $APP"
