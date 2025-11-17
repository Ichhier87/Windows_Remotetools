# Windows Remote Tools - Watchdog-Architektur

## Übersicht

Das Windows Remote Tools System verwendet jetzt eine **Zwei-Prozess-Architektur**, um sicherzustellen, dass die UI nicht von Benutzern mit eingeschränkten Rechten beendet werden kann.

## Architektur

### 1. **WindowsRemoteTools** (Service)
- **Läuft als**: Windows-Dienst (SYSTEM-Konto)
- **Kann gestoppt werden von**: Nur Administratoren
- **Funktionen**:
  - WebSocket-Verbindung zum CentralServer
  - Volume-Steuerung
  - Display-Steuerung
  - Named Pipe Server für Kommunikation mit UI
  - **Watchdog**: Überwacht und startet UI-Prozess automatisch neu

### 2. **WindowsRemoteToolsUI** (UI-Anwendung)
- **Läuft als**: Benutzer-Anwendung (im Kontext des angemeldeten Benutzers)
- **Kann beendet werden von**: Jedem Benutzer
- **Wird automatisch neugestartet**: Ja, vom Service-Watchdog
- **Funktionen**:
  - System Tray Icon
  - OverlayWindow-Anzeige
  - Named Pipe Client für Kommunikation mit Service

## Kommunikation

Die beiden Prozesse kommunizieren über **Named Pipes**:

```
Service (Named Pipe Server)  ←→  UI (Named Pipe Client)
        │                              │
        ├─ Sendet Overlay-Befehle →    │
        │                              │
        │    ← Sendet Heartbeat ────────┤
```

### Message Types

**Service → UI:**
- `show_overlay`: Zeige Overlay-Fenster
- `hide_overlay`: Verstecke Overlay
- `show_notification`: Zeige Benachrichtigung
- `show_warning`: Zeige Warnung
- `show_error`: Zeige Fehler
- `show_blocking_screen`: Zeige Sperrbildschirm
- `shutdown`: Beende UI-Prozess

**UI → Service:**
- `heartbeat`: "Ich lebe noch"
- `ready`: UI ist bereit
- `closed`: UI wurde geschlossen

## Prozess-Schutz

### Wie funktioniert der Schutz?

1. **Service läuft als SYSTEM**
   - Nur Administratoren können den Dienst stoppen
   - Service-Control-Manager verhindert unbefugten Zugriff

2. **Watchdog überwacht UI-Prozess**
   - Prüft alle 5 Sekunden, ob UI-Prozess läuft
   - Startet UI automatisch neu, wenn beendet

3. **Benutzer kann UI beenden**
   - UI beendet sich (Task-Manager, Alt+F4, etc.)
   - Watchdog erkennt Beendigung innerhalb von 5 Sekunden
   - Watchdog startet UI sofort neu

### Ergebnis

✅ **Deine Tochter kann die UI beenden** - aber sie startet sofort wieder
✅ **Service kann nur von Admins gestoppt werden**
✅ **Alle Features funktionieren** (OverlayWindow, Volume, Display)

## Installation

### 1. Beide Projekte kompilieren

```bash
cd C:\Programming\Windows_Remotetools\WindowsRemoteTools
dotnet build -c Release

cd C:\Programming\Windows_Remotetools\WindowsRemoteToolsUI
dotnet build -c Release
```

### 2. Dateien zusammenführen

Kopiere beide ausführbare Dateien **in dasselbe Verzeichnis**:

```
C:\Program Files\WindowsRemoteTools\
├── WindowsRemoteTools.exe       (Service)
├── WindowsRemoteTools.dll
├── WindowsRemoteToolsUI.exe     (UI)
├── WindowsRemoteToolsUI.dll
├── config.json
└── ... (weitere DLLs)
```

**WICHTIG**: Beide EXEs müssen im selben Verzeichnis liegen!

### 3. Service installieren

Als Administrator:

```powershell
# Service installieren
sc create WindowsRemoteToolsService binPath= "C:\Program Files\WindowsRemoteTools\WindowsRemoteTools.exe --service" start= auto

# Service starten
sc start WindowsRemoteToolsService

# Service-Status prüfen
sc query WindowsRemoteToolsService
```

### 4. Autostart für UI (Optional)

Der Service startet die UI automatisch. Aber du kannst auch einen Autostart-Eintrag erstellen:

**Windows Autostart:**
```
Win + R → shell:startup → Enter
Verknüpfung erstellen zu: C:\Program Files\WindowsRemoteTools\WindowsRemoteToolsUI.exe
```

## Verwendung

### Service-Modus (Standard)

```bash
# Als Service installiert
sc start WindowsRemoteToolsService
```

Der Service startet automatisch die UI im Benutzer-Kontext.

### GUI-Modus (zum Testen)

```bash
WindowsRemoteTools.exe
```

Startet mit direktem OverlayWindow (kein Watchdog).

### Console-Modus (zum Debuggen)

```bash
WindowsRemoteTools.exe --console
```

Läuft in der Konsole, verwendet direktes OverlayWindow.

## Troubleshooting

### UI startet nicht

**Problem**: Service läuft, aber UI erscheint nicht.

**Lösung**:
1. Prüfe Event Log:
   ```powershell
   Get-EventLog -LogName Application -Source WindowsRemoteToolsService -Newest 10
   ```

2. Prüfe, ob WindowsRemoteToolsUI.exe existiert:
   ```powershell
   Test-Path "C:\Program Files\WindowsRemoteTools\WindowsRemoteToolsUI.exe"
   ```

### Named Pipe Verbindung schlägt fehl

**Problem**: UI kann nicht mit Service kommunizieren.

**Lösung**:
1. Beide Prozesse müssen laufen
2. Prüfe Firewall (Named Pipes sind lokal, sollte kein Problem sein)
3. Prüfe Logs in der Konsole

### UI wird immer wieder neugestartet (Loop)

**Problem**: UI crasht sofort nach Start.

**Lösung**:
1. Starte UI manuell zum Testen:
   ```bash
   C:\Program Files\WindowsRemoteTools\WindowsRemoteToolsUI.exe
   ```
2. Prüfe Fehlerausgabe in Konsole
3. Prüfe, ob Newtonsoft.Json.dll vorhanden ist

## Service deinstallieren

```powershell
# Service stoppen
sc stop WindowsRemoteToolsService

# Service deinstallieren
sc delete WindowsRemoteToolsService
```

## Code-Struktur

### RemoteToolsCore

```csharp
// GUI-Modus: Verwendet OverlayWindow direkt
var core = new RemoteToolsCore(useOverlayDirectly: true);

// Service-Modus: Verwendet ServicePipeServer
var core = new RemoteToolsCore(useOverlayDirectly: false);
```

### ProcessWatchdog

```csharp
var watchdog = new ProcessWatchdog("C:\\Path\\To\\WindowsRemoteToolsUI.exe");
watchdog.ProcessStarted += (s, e) => Console.WriteLine("UI started");
watchdog.ProcessStopped += (s, e) => Console.WriteLine("UI stopped");
watchdog.Start();
```

### Named Pipe Server (Service)

```csharp
var pipeServer = new ServicePipeServer();
pipeServer.MessageReceived += OnMessageReceived;
pipeServer.ClientConnected += (s, e) => Console.WriteLine("UI connected");
pipeServer.Start();

// Sende Overlay-Befehl
await pipeServer.SendMessageAsync(PipeMessage.Create(
    PipeMessage.MessageTypes.ShowOverlay,
    new OverlayData { Message = "Hello", Opacity = 0.9 }
));
```

### Named Pipe Client (UI)

```csharp
var pipeClient = new UIPipeClient();
pipeClient.MessageReceived += OnMessageReceived;
await pipeClient.ConnectAsync();

// Sende Heartbeat
await pipeClient.SendMessageAsync(PipeMessage.Create(
    PipeMessage.MessageTypes.Heartbeat
));
```

## Sicherheitshinweise

### Was ist geschützt?

✅ **Service kann nur von Admins gestoppt werden**
✅ **Volume-Limits können nicht umgangen werden**
✅ **Display-Steuerung funktioniert immer**
✅ **UI startet automatisch neu**

### Was ist NICHT geschützt?

❌ **PC kann ausgeschaltet werden** (Boot Guard/BIOS-Passwort verwenden)
❌ **Netzwerkkabel kann gezogen werden** (physischer Zugriff)
❌ **Benutzer kann im abgesicherten Modus booten** (BIOS-Passwort verwenden)

## Weitere Schutzmaßnahmen

### BIOS-Passwort setzen
Verhindert Boot in abgesicherten Modus.

### Benutzerrechte einschränken
```powershell
# Entferne Benutzer aus der Gruppe "Hauptbenutzer"
net localgroup Users Tochter /delete
net localgroup Guests Tochter /add
```

### Autostart in Registry sperren
Verhindert Deaktivierung des Autostart-Eintrags.

```reg
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"DisableLocalUserRun"=dword:00000001
```

## Support

Bei Problemen:
1. Prüfe Event Log
2. Starte UI manuell zum Testen
3. Prüfe Named Pipe Verbindung
4. Prüfe Dateipfade

## Zusammenfassung

**Vorher:**
- Eine Anwendung
- Kann von jedem Benutzer beendet werden

**Nachher:**
- Service (geschützt) + UI (kann beendet werden, startet aber neu)
- Service überwacht UI permanent
- Named Pipe Kommunikation
- Alle Features funktionieren weiterhin
