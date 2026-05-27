# wow serverstarter

Kleines Cross-Plattform-Desktopprogramm zum Erkennen, Anzeigen, Starten, Stoppen und Rebooten von AzerothCore/WoW-Serverprozessen per SSH.

## Eigenschaften

- Avalonia UI und .NET
- Windows und macOS
- keine Web-App, kein Electron, kein Docker
- lokale Konfiguration
- asynchrone SSH-Ausführung, die UI bleibt bedienbar
- erkennt `/opt/azeroth-server` und `/opt/azeroth-playerbots-server`
- erkennt `authserver` und ein oder mehrere `worldserver`

## Start

```powershell
dotnet restore
dotnet run --project src/WowServerStarter/WowServerStarter.csproj
```

Beim ersten Start sind IP `192.168.1.118`, SSH-Port `22`, Benutzer `klaus` und das Testpasswort vorbelegt. Änderungen werden im Configfenster vorgenommen und lokal gespeichert.

## SSH-Konfiguration

Im Configfenster:

- IP
- SSH-Port
- Benutzername
- Passwort
- Ping-Sound an/aus
- Prüfintervall in Sekunden

Die Konfiguration liegt lokal unter dem Benutzerprofil:

- Windows: `%APPDATA%\wow-serverstarter\config.json`
- macOS: `~/Library/Application Support/wow-serverstarter/config.json`

## Bekannte Serverpfade

Das Programm sucht automatisch:

- `/opt/azeroth-server/bin/authserver`
- `/opt/azeroth-server/bin/worldserver`
- `/opt/azeroth-playerbots-server/bin/worldserver`
- optional `/opt/azeroth-playerbots-server/bin/authserver`

Ports:

- Authserver: `3724`
- Realm 1 worldserver: `8085`
- Playerbot worldserver: `8086`

## Aktionen

Start:

```bash
cd /opt/azeroth-server/bin && nohup ./authserver > ../logs/authserver_launcher.log 2>&1 &
cd /opt/azeroth-server/bin && nohup ./worldserver > ../logs/worldserver_launcher.log 2>&1 &
cd /opt/azeroth-playerbots-server/bin && nohup ./worldserver > ../logs/worldserver_launcher.log 2>&1 &
```

Stop:

- zuerst `SIGTERM` per `pkill -TERM -f`
- falls der Prozess weiter läuft, `SIGKILL` per `pkill -KILL -f`

Es werden keine Konfigurationen, Datenbanken oder Systemdateien auf dem Server verändert.

## Build Windows

```powershell
.\scripts\build-windows.ps1
```

Ausgabe:

```text
src\WowServerStarter\bin\Release\net10.0\win-x64\publish\WowServerStarter.exe
```

## Build macOS

Auf einem Mac:

```bash
chmod +x scripts/build-macos-app.sh
./scripts/build-macos-app.sh osx-arm64
```

Für Intel-Macs:

```bash
./scripts/build-macos-app.sh osx-x64
```

Ausgabe:

```text
artifacts/wow serverstarter-osx-arm64.app
```

## Hinweise

- Der SSH-Benutzer muss die Serverprozesse starten und beenden dürfen.
- `pgrep`, `ps` sowie `ss` oder `netstat` werden zur Erkennung genutzt.
- Das Tool ist bewusst auf diese eine Aufgabe begrenzt.
