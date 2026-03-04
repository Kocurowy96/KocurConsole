# 🚀 Getting Started with KocurSh

## What is KocurSh?

KocurSh is KocurConsole's built-in scripting language. It lets you:
- Automate KocurConsole commands
- Use variables, loops, and conditions
- Mix in C# code for complex logic
- Interact with files, strings, and the terminal

## Your First Script

### 1. Create a script

```
kocursh new myscript.kocursh
```

This creates the file and opens it in Notepad. Or manually create any `.kocursh` file.

### 2. Write some code

```bash
# myscript.kocursh
echo Hello from KocurSh!

$name = KocurConsole
echo I'm running inside $name

date
whoami
```

### 3. Run it

Three ways to run:

```bash
# Method 1: kocursh run command
kocursh run myscript.kocursh

# Method 2: just type the filename
myscript.kocursh

# Method 3: from kocursh command
kocursh myscript.kocursh
```

### 4. Try the example script

```bash
kocursh example
kocursh run example.kocursh
```

## Syntax Overview

| Syntax | What it does |
|--------|-------------|
| `# comment` | Comment (ignored) |
| `$var = value` | Set a variable |
| `echo $var` | Run command with variable interpolation |
| `print text` | Print text directly |
| `cprint red text` | Print colored text |
| `if ... else ... end` | Conditional |
| `for $i in 1..5 ... end` | Loop |
| `while condition ... end` | While loop |
| `func name($arg) ... end` | Function definition |
| `call name(value)` | Function call |
| `@{ C# code }` | C# code block |
| `@(C# expression)` | Inline C# expression |
| `sleep 1000` | Pause (milliseconds) |
| `include file.kocursh` | Import another script |

## Key Principle

**Any line that isn't a KocurSh keyword is executed as a KocurConsole command.** This means you can mix terminal commands freely:

```bash
$project = MyApp
mkdir $project
cd $project
echo Creating project...
touch README.md
ls
```

## Cancel a Running Script

Press `Ctrl+C` to stop a running script at any time.
