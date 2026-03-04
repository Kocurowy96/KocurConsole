# 🔬 Advanced Features

## Include — Import Other Scripts

Split code into modules:

```bash
# utils.kocursh
func banner($title)
    $line = repeat(=, 40)
    cprint accent $line
    cprint accent   $title
    cprint accent $line
end

func log($msg)
    fappend log.txt [$TIME] $msg
    echo [$TIME] $msg
end
```

```bash
# main.kocursh
include utils.kocursh

call banner(My Application)
call log(Application started)
```

Include paths are relative to the script's directory.

## Script Arguments

Pass arguments when running scripts:

```bash
# deploy.kocursh
echo Deploying to: $1
echo Branch: $2
echo All args: $ARGS
echo Count: $ARGC
```

```
kocursh run deploy.kocursh production main
```

## .kocurrc — Startup Script

KocurConsole runs `.kocurrc` on startup. It's essentially a KocurSh script:

```bash
# ~/.kocurrc
theme dracula
echo Welcome back, $USER!
alias gs git status
alias gp git push
pin F1 ls
pin F2 git status
```

Manage with: `rc edit` | `rc run` | `rc`

## Combining with KocurConsole Features

### With aliases

```bash
# Set up aliases in a script
alias build dotnet build
alias test dotnet test
alias run dotnet run

echo Aliases configured!
```

### With bookmarks

```bash
# Navigate with bookmarks
bookmark add project
cd src
bookmark add src

echo Bookmarks set up!
```

### With pinned commands

```bash
# Set up F-keys
pin F1 ls
pin F2 git status
pin F3 dotnet build
pin F4 dotnet run
pin F5 cd $HOME

echo F-keys configured!
```

### With plugins

```bash
# Create and use plugins from a script
plugin create
plugin reload
```

## Patterns and Idioms

### Configuration loader

```bash
func load_config($file)
    fexists $ok $file
    if $ok != "true"
        cprint yellow Config not found: $file
        return
    end
    
    fread $data $file
    $lines = split($data, $NEWLINE)
    for $line in $lines
        # Skip comments and empty lines
        $starts = startswith($line, #)
        if $starts == "true"
            continue
        end
        $length = len($line)
        if $length == 0
            continue
        end
        echo Config: $line
    end
end
```

### Build system

```bash
# build.kocursh
call banner(Building Project)

$steps = clean,compile,test,package
$total = 4
$current = 0

for $step in $steps
    $current = @(int.Parse($current) + 1)
    progress $current $total $step
    
    try
        call run_$step()
    catch
        cprint red Failed at step: $step
        cprint red Error: $ERROR
        exit 1
    end
    
    sleep 300
end

beep
cprint green Build successful!
```

### Menu system

```bash
cprint cyan [1] Build
cprint cyan [2] Test
cprint cyan [3] Deploy
cprint cyan [4] Exit
echo
echo Select option (scripted: running all)

for $opt in 1,2,3
    if $opt == 1
        call do_build()
    end
    if $opt == 2
        call do_test()
    end
    if $opt == 3
        call do_deploy()
    end
end
```

## Performance Tips

1. **Minimize C# blocks** — each `@{}` and `@()` compiles a new assembly
2. **Cache computed values** — don't repeat `@()` in loops if the value doesn't change
3. **Use KocurSh builtins** — string functions are faster than C# compilation
4. **Use `break`** — exit loops early when you find what you need
5. **Ctrl+C** — always works to stop a runaway script
