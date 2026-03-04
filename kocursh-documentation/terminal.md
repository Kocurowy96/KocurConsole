# 🖥️ Terminal Integration

KocurSh has special commands for interacting with the KocurConsole terminal.

## Colored Output — `cprint`

```bash
cprint <color> <text>
```

### Available colors

| Color | Preview |
|-------|---------|
| `red` | 🔴 Error-style red |
| `green` | 🟢 Success green |
| `blue` | 🔵 Keyword blue |
| `yellow` | 🟡 Warning yellow |
| `orange` | 🟠 Orange |
| `purple` / `magenta` | 🟣 Purple |
| `cyan` | 🔵 Bright cyan |
| `pink` | 🩷 Pink |
| `white` | ⚪ White |
| `gray` / `grey` | ⚫ Gray |
| `accent` | Theme accent color |
| `info` | Theme info color |
| `warn` / `warning` | Theme warning color |
| `error` | Theme error color |

### Examples

```bash
cprint green ✅ Build successful!
cprint red ❌ Error: file not found
cprint yellow ⚠️ Warning: deprecated feature
cprint cyan ℹ️ Processing...
cprint accent === Section Header ===
```

## Progress Bar — `progress`

```bash
progress <current> <max> [label]
```

Displays a visual progress bar with percentage:

```bash
for $i in 1..10
    progress $i 10 Downloading
    sleep 200
end
```

Output:
```
  Downloading [██████░░░░░░░░░░░░░░░░░░░░░░░░] 20%
  Downloading [████████████░░░░░░░░░░░░░░░░░░] 40%
  ...
  Downloading [██████████████████████████████] 100%
```

## Beep — `beep`

Play a system beep sound:

```bash
echo Task complete!
beep
```

## Print — `print`

Print text with variable interpolation (no KocurConsole command processing):

```bash
print Hello, $USER! The time is $TIME.
```

## Sleep — `sleep`

Pause execution for milliseconds:

```bash
sleep 1000       # 1 second
sleep 500        # half a second
```

## Running KocurConsole Commands

Any line that isn't a KocurSh keyword runs as a KocurConsole command:

```bash
# These all run as terminal commands
ls
cd Desktop
cat README.md
fastfetch
theme dracula
ping google.com
calc sqrt(144)
```

## Interactive UI Example

```bash
cprint cyan ╔══════════════════════════════╗
cprint cyan ║   KocurSh Installer v1.0    ║
cprint cyan ╚══════════════════════════════╝
echo

for $step in 1..4
    if $step == 1
        $label = Checking system...
    end
    if $step == 2
        $label = Downloading files...
    end
    if $step == 3
        $label = Installing...
    end
    if $step == 4
        $label = Finishing up...
    end
    
    progress $step 4 $label
    sleep 800
end

echo
beep
cprint green ✅ Installation complete!
```
