# KocurConsole — Compiling

## Requirements

- **Visual Studio 2022** (Community, Professional, or Enterprise)
- **.NET Framework 4.8** (included with Windows 10/11)
- **Windows 10/11**

## Build Steps

### Visual Studio (recommended)

1. Open `KocurConsole.sln` in Visual Studio 2022
2. Set configuration to **Release** (toolbar dropdown)
3. Build → Build Solution (`Ctrl+Shift+B`)
4. Output: `bin\Release\KocurConsole.exe`

### Command Line (MSBuild)

```cmd
cd C:\Users\wikto\Desktop\Projekty\KocurConsole
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" KocurConsole.csproj /p:Configuration=Release
```

Or if `msbuild` is in PATH:
```cmd
msbuild KocurConsole.csproj /p:Configuration=Release
```

## Project Structure

```
KocurConsole/
├── Form1.cs                 # Main terminal logic + all commands
├── Form1.Designer.cs        # GUI layout (VS Designer)
├── ThemeManager.cs           # 8 color themes
├── SettingsManager.cs        # Persistent JSON settings
├── SettingsForm.cs           # Settings GUI dialog
├── CommandHandler.cs         # External cmd/PowerShell execution
├── UpdateHandler.cs          # Auto-update from GitHub
├── Program.cs                # Application entry point
├── version_manifest.json     # Version info for updater
├── install.bat               # Installer script
└── KocurConsole.csproj       # Project file
```

## Dependencies

All built-in .NET Framework 4.8 assemblies (no NuGet packages):
- `System.Management` — WMI queries (CPU, RAM, GPU info)
- `System.Runtime.Serialization` — JSON serialization
- `System.Net.Http` — HTTP requests

## Creating a Release

1. Build in **Release** mode
2. Test the `bin\Release\KocurConsole.exe`
3. Update `version_manifest.json` with new version
4. Create GitHub Release and upload the `.exe`
5. See [UpdatingProcess.md](UpdatingProcess.md) for details
