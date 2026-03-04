# 🐱 KocurSh — KocurConsole Scripting Language

**KocurSh** is the proprietary scripting language built into KocurConsole. It's a hybrid between shell scripting and C# — combining the simplicity of shell commands with the power of .NET runtime compilation.

## 📖 Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](getting-started.md) | First script, syntax basics, how to run |
| [Variables](variables.md) | Variables, interpolation, built-in vars |
| [Control Flow](control-flow.md) | if/else, for, while, break/continue |
| [Functions](functions.md) | func/call, return values, scope |
| [String Operations](strings.md) | upper, lower, len, replace, split, etc. |
| [File Operations](files.md) | fread, fwrite, fappend, fexists, flist |
| [Terminal Integration](terminal.md) | cprint, progress, beep, confirm |
| [C# Integration](csharp.md) | @{} blocks, @() expressions, vars bridge |
| [Error Handling](errors.md) | try/catch, $ERROR, exit codes |
| [Advanced Features](advanced.md) | include, args, plugins, .kocurrc |
| [Examples](examples.md) | Real-world script examples |

## Quick Example

```bash
# hello.kocursh
$name = World
$hour = @(DateTime.Now.Hour)

if $hour < 12
    cprint yellow Good morning, $name! ☀️
else
    cprint blue Good evening, $name! 🌙
end

for $i in 1..5
    progress $i 5 Loading
    sleep 200
end

cprint green Done!
```

Run with: `kocursh run hello.kocursh` or just type `hello.kocursh`

## File Extension

All KocurSh scripts use the `.kocursh` extension.

---

*Part of KocurConsole v1.0.3 — Made by [Kocurowy96](https://github.com/Kocurowy96)*
