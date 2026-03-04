# 📦 Variables

## Assignment

```bash
$name = World
$count = 42
$path = C:\Users\me\Desktop
```

## Variable Interpolation

Variables are replaced with their values in all commands and strings:

```bash
$name = KocurSh
echo Hello, $name!              # → Hello, KocurSh!
echo Version is $VERSION        # → Version is 1.0.3
```

### Brace syntax for disambiguation

```bash
$item = cat
echo ${item}s are cute           # → cats are cute
```

## C# Expressions

Assign values from C# expressions:

```bash
$hour = @(DateTime.Now.Hour)
$pi = @(Math.PI)
$sum = @(2 + 2)
$rand = @(new Random().Next(1, 100))
$upper = @("hello".ToUpper())
```

## Built-in Variables

These are automatically set and available in every script:

| Variable | Description | Example Value |
|----------|-------------|---------------|
| `$USER` | Current Windows user | `Kocurowy96` |
| `$HOME` | User home directory | `C:\Users\Kocurowy96` |
| `$OS` | Operating system | `Microsoft Windows NT 10.0...` |
| `$DATE` | Current date (auto-refreshes) | `2026-03-03` |
| `$TIME` | Current time (auto-refreshes) | `14:30:25` |
| `$RANDOM` | Random 0-999 (auto-refreshes) | `42` |
| `$PI` | Math.PI | `3.14159265358979` |
| `$E` | Math.E | `2.71828182845905` |
| `$NEWLINE` | Newline character | `\n` |
| `$TAB` | Tab character | `\t` |
| `$TRUE` | String "true" | `true` |
| `$FALSE` | String "false" | `false` |
| `$RESULT` | Last function return value | varies |
| `$ERROR` | Last error message | varies |
| `$SCRIPT` | Current script filename | `myscript.kocursh` |
| `$SCRIPTDIR` | Script directory path | `C:\Scripts` |
| `$ARGS` | Script arguments (space-separated) | `arg1 arg2` |
| `$ARGC` | Number of arguments | `2` |
| `$1`, `$2`... | Individual arguments | `arg1` |
| `$CWD` | Current working directory | `C:\Users\me` |
| `$VERSION` | KocurConsole version | `1.0.3` |
| `$EXITCODE` | Exit code (set by `exit`) | `0` |

## Variable Scope

Variables are **global** within a script. Functions receive copies of their parameters but share the global scope for everything else.

```bash
$x = 10

func test()
    echo $x           # → 10 (sees global)
    $x = 20           # modifies global!
end

call test()
echo $x               # → 20
```
