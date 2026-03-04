# 📝 Example Scripts

## 1. System Info Report

```bash
# sysinfo.kocursh — Generate a system report

cprint cyan ╔══════════════════════════════════╗
cprint cyan ║     System Information Report     ║
cprint cyan ╚══════════════════════════════════╝
echo

cprint accent User Info:
echo   User:     $USER
echo   Home:     $HOME
echo   Date:     $DATE
echo   Time:     $TIME

echo
cprint accent System:
fastfetch

echo
cprint accent Disk Usage:
df

echo
cprint accent Top Processes:
ps

# Save report
fwrite report.txt System Report - $DATE $TIME
fappend report.txt User: $USER
fappend report.txt OS: $OS

cprint green Report saved to report.txt
```

## 2. File Organizer

```bash
# organize.kocursh — Sort files by extension

cprint accent File Organizer
echo Organizing files in current directory...

flist $files .
for $file in $files
    $ext = @(Path.GetExtension("$file").TrimStart('.').ToLower())
    
    if $ext == ""
        continue
    end
    
    $length = len($ext)
    if $length == 0
        continue
    end
    
    # Create folder for extension
    fexists $dirOk $ext
    if $dirOk != "true"
        mkdir $ext
        cprint yellow Created folder: $ext/
    end
end

cprint green Done organizing!
```

## 3. Quick Timer / Pomodoro

```bash
# pomodoro.kocursh — Simple Pomodoro timer

func timer_bar($minutes, $label)
    $total = @(int.Parse($minutes) * 60)
    $step = @(int.Parse($total) / 30)
    
    cprint accent $label ($minutes min)
    
    for $i in 1..30
        progress $i 30 $label
        $wait = @(int.Parse($step) * 1000)
        sleep $wait
    end
    beep
    cprint green $label complete!
    echo
end

cprint cyan ╔═══════════════════════╗
cprint cyan ║   Pomodoro Timer 🍅  ║
cprint cyan ╚═══════════════════════╝
echo

call timer_bar(25, Work)
call timer_bar(5, Break)
call timer_bar(25, Work)
call timer_bar(5, Break)
call timer_bar(25, Work)
call timer_bar(15, Long Break)

cprint green Session complete! Great work! 🎉
```

## 4. Project Scaffolder

```bash
# scaffold.kocursh — Create a project structure

$project = $1
if $project == ""
    $project = MyProject
end

cprint accent Creating project: $project

mkdir $project

# Create directory structure
for $dir in src,tests,docs,assets
    mkdir $project/$dir
    cprint green   ✓ $project/$dir/
end

# Create files
fwrite $project/README.md # $project
fappend $project/README.md
fappend $project/README.md Created on $DATE

fwrite $project/.gitignore bin/
fappend $project/.gitignore obj/
fappend $project/.gitignore *.exe

fwrite $project/src/main.cs using System;
fappend $project/src/main.cs 
fappend $project/src/main.cs class Program
fappend $project/src/main.cs {
fappend $project/src/main.cs     static void Main() => Console.WriteLine("Hello!");
fappend $project/src/main.cs }

cprint green
cprint green Project $project created! 🚀
cprint green   cd $project to get started
```

## 5. Git Workflow Helper

```bash
# githelp.kocursh — Common git operations

func git_status()
    cprint accent Git Status:
    git status --short
    echo
end

func git_log()
    cprint accent Recent Commits:
    git log --oneline -10
    echo
end

func git_commit($msg)
    git add .
    git commit -m "$msg"
    cprint green Committed: $msg
end

# Main
call git_status()
call git_log()
```

## 6. C# Code Analyzer

```bash
# analyze.kocursh — Analyze C# source files

cprint cyan Code Analyzer
echo
$line = repeat(─, 50)
echo $line

@{
    var files = Directory.GetFiles(".", "*.cs", SearchOption.AllDirectories);
    int totalLines = 0;
    int totalClasses = 0;
    int totalMethods = 0;
    
    foreach (var file in files)
    {
        var lines = File.ReadAllLines(file);
        int lineCount = lines.Length;
        int classes = lines.Count(l => l.Contains("class "));
        int methods = lines.Count(l => l.Contains("private ") || l.Contains("public "));
        
        totalLines += lineCount;
        totalClasses += classes;
        totalMethods += methods;
        
        print($"  {Path.GetFileName(file),-30} {lineCount,6} lines  {classes,3} classes  {methods,3} methods");
    }
    
    print("");
    print($"  {"TOTAL",-30} {totalLines,6} lines  {totalClasses,3} classes  {totalMethods,3} methods");
    
    setvar("total_files", files.Length.ToString());
    setvar("total_lines", totalLines.ToString());
}

echo $line
cprint accent $total_files files, $total_lines total lines
```

## 7. Deployment Script

```bash
# deploy.kocursh — Build and deploy

$env = $1
if $env == ""
    $env = staging
end

func step($num, $total, $label)
    progress $num $total $label
    sleep 300
end

cprint cyan Deploying to: $env
echo

call step(1, 5, Cleaning)
# clean / del build artifacts

call step(2, 5, Building)
# dotnet build --configuration Release

call step(3, 5, Testing)
# dotnet test

call step(4, 5, Packaging)
# create zip / artifacts

call step(5, 5, Deploying)
# copy to server / publish

echo
beep
cprint green ✅ Deployed to $env successfully!
cprint info   Time: $TIME
```

---

*More examples? Run `kocursh example` to generate a comprehensive example script directly in your terminal!*
