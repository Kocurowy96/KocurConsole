# 📁 File Operations

KocurSh has built-in file operations that don't require C# blocks.

## Commands

| Command | Description |
|---------|-------------|
| `fread $var <file>` | Read file contents into variable |
| `fwrite <file> <text>` | Write text to file (overwrites) |
| `fappend <file> <text>` | Append text to file |
| `fexists $var <path>` | Check if file/dir exists (true/false) |
| `fdelete <path>` | Delete file or directory |
| `flist $var <dir>` | List directory entries (comma-separated) |

## Examples

### Reading and writing

```bash
# Write a file
fwrite notes.txt Hello from KocurSh!
fappend notes.txt This is the second line.
fappend notes.txt Written at $TIME on $DATE

# Read it back
fread $content notes.txt
echo File content: $content
```

### Check existence

```bash
fexists $exists config.json
if $exists == "true"
    echo Config found!
    fread $config config.json
else
    echo No config found, creating default...
    fwrite config.json {"theme": "dracula"}
end
```

### List directory

```bash
flist $files .
echo Files: $files

# Iterate over files
for $file in $files
    echo - $file
end
```

### Delete

```bash
fwrite temp.txt temporary data
fdelete temp.txt
```

### Combining with KocurConsole commands

Remember, any non-KocurSh line is a KocurConsole command:

```bash
# These use KocurConsole's built-in commands
cat myfile.txt             # display with syntax highlighting
cat myfile.txt -n          # with line numbers
preview myfile.txt         # paginated viewer
write myfile.txt new line  # KocurConsole's write command

# These use KocurSh builtins (more control)
fread $data myfile.txt     # read into variable
fwrite output.txt $data    # write from variable
```

### Error handling

File operations set `$ERROR` on failure:

```bash
try
    fread $data nonexistent.txt
catch
    echo File error: $ERROR
end
```

### Building a log file

```bash
$logfile = build.log
fwrite $logfile Build started: $DATE $TIME

for $step in compile,test,package
    echo Running: $step
    fappend $logfile [$TIME] $step - started
    sleep 500
    fappend $logfile [$TIME] $step - completed
end

fappend $logfile Build completed: $TIME
cprint green Build done! Log saved to $logfile
```
