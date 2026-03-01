# 🐱 KocurConsole

A modern, feature-rich terminal emulator built with C# and .NET Framework 4.8.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Version](https://img.shields.io/badge/version-1.0.2-green.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

---

## ✨ Features

- **50+ Built-in Commands** — file system, network, processes, utilities
- **8 Color Themes** — dracula, monokai, nord, gruvbox, solarized, matrix, catppuccin
- **Settings GUI** — graphical settings window (`settings gui`)
- **Tab Autocomplete** — commands, sub-commands, and file paths
- **CMD/PowerShell Fallback** — unrecognized commands run via your default shell
- **Auto-Updater** — checks for updates on startup + manual `checkupdate` / `update`
- **Command Chaining** — `mkdir test && cd test`
- **Output Redirection** — `ls > output.txt`, `echo hello >> file.txt`
- **Custom Aliases** — `alias gs git status`
- **Directory Bookmarks** — `bookmark add home`
- **Session Logging** — `log start session.log`
- **Command History** — navigate with ↑/↓ arrows
- **Dark Mode UI** — dark scrollbars and title bar
- **Persistent Settings** — saved to `%APPDATA%/KocurConsole/settings.json`

---

## 📦 Installation

### Quick Install (recommended)

1. Download `install.bat` from the [latest release](https://github.com/Kocurowy96/KocurConsole/releases/latest)
2. Right-click → **Run as Administrator**
3. Done! Launch from Desktop shortcut or type `KocurConsole` in any terminal

### Manual Install

1. Download `KocurConsole.exe` from [Releases](https://github.com/Kocurowy96/KocurConsole/releases/latest)
2. Place it anywhere you want
3. Double-click to run

### Uninstall

Run `uninstall.bat` in `C:\Program Files\KocurConsole\` as Administrator.

---

## 🖥️ Commands

### General

| Command | Description |
| ------- | ----------- |
| `help` | Show all commands |
| `clear` / `cls` | Clear the console |
| `echo <text>` | Echo text |
| `title <text>` | Change window title |
| `history` | Command history |
| `about` | About KocurConsole |
| `exit` / `quit` | Close |

### System

| Command | Description |
| ------- | ----------- |
| `fastfetch` / `neofetch` | System overview with colors |
| `systeminfo` | Detailed system info |
| `whoami` | Current user |
| `hostname` | Computer name |
| `date` | Date and time |
| `uptime` | System uptime |
| `ps` / `tasklist` | List processes (top 30 by RAM) |
| `kill <pid>` | Kill a process |
| `df` | Disk usage for all drives |

### File System

| Command | Description |
| ------- | ----------- |
| `ls` / `dir` | List directory |
| `cd <path>` | Change directory (`cd ~` / `cd -` / `cd ..`) |
| `pwd` | Working directory |
| `mkdir <name>` | Create directory |
| `rmdir <name>` | Remove directory |
| `rm` / `del <file>` | Delete file |
| `cp` / `copy <src> <dst>` | Copy file |
| `mv` / `move <src> <dst>` | Move or rename |
| `cat` / `type <file>` | Show file contents |
| `touch <file>` | Create empty file |
| `tree` | Directory tree |
| `find <pattern>` | Search files by name |
| `grep <pattern> <file>` | Search text in file (with highlighting) |
| `head <file> [n]` | First N lines (default 10) |
| `tail <file> [n]` | Last N lines (default 10) |
| `wc <file>` | Word, line, char count |
| `size <path>` | File or directory size |
| `md5 <file>` | MD5 hash |
| `sha256 <file>` | SHA256 hash |
| `write <file> <text>` | Append text to file |

### Network

| Command | Description |
| ------- | ----------- |
| `ping <host>` | Ping (4 packets) |
| `ip` | Show all IP addresses |
| `dns <host>` | DNS lookup |
| `wget <url> [file]` | Download file |
| `curl <url>` | Fetch URL content |

### Utility

| Command | Description |
| ------- | ----------- |
| `env [name]` | Environment variables |
| `calc <expr>` | Calculator |
| `base64 <encode/decode> <text>` | Base64 |
| `hash <md5/sha256> <text>` | Hash text (not file) |
| `random [min] [max]` | Random number |
| `clipboard` | Show clipboard text |
| `open [path]` | Open in Explorer |
| `start <program>` | Launch program |
| `stopwatch` | Start/stop/lap stopwatch |
| `timer <seconds>` | Countdown timer with beep |
| `bookmark` | Manage saved directories |
| `alias <name> <cmd>` | Create command alias |
| `log start [file]` | Start session logging |
| `log stop` | Stop session logging |

### Themes and Settings

| Command | Description |
| ------- | ----------- |
| `theme list` | List available themes |
| `theme <name>` | Apply theme |
| `settings` | Show current settings |
| `settings set <key> <value>` | Change a setting |
| `settings reset` | Reset to defaults |
| `settings gui` | Open settings window |

### Updates

| Command | Description |
| ------- | ----------- |
| `checkupdate` | Check for new version |
| `update` | Download and install update |

### Operators

| Operator | Description |
| -------- | ----------- |
| `cmd1 && cmd2` | Command chaining |
| `cmd > file` | Redirect output (overwrite) |
| `cmd >> file` | Redirect output (append) |

> **💡 Tip:** Any command not listed above is automatically passed to your default shell (cmd or PowerShell).

---

## 🎨 Themes

Available themes: `default`, `dracula`, `monokai`, `nord`, `gruvbox`, `solarized`, `matrix`, `catppuccin`

```
theme dracula
```

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| -------- | ------ |
| `Tab` | Autocomplete command, sub-command, or file path |
| `Ctrl+C` | Cancel running process or clear input |
| `Ctrl+L` | Clear screen |
| `Ctrl+V` | Paste |
| `Esc` | Clear current input |
| `↑` / `↓` | Navigate command history |

---

## 🔧 Building from Source

See [Compiling.md](Compiling.md) for build instructions.

## 📄 License

[MIT License](LICENSE.txt)

---

**Made with ❤️ by [Kocurowy96](https://github.com/Kocurowy96)**