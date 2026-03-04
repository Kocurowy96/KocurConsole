# ⚠️ Error Handling

## Try / Catch / End

```bash
try
    # risky operations
    fread $data important_file.txt
    echo Data loaded: $data
catch
    # handle error
    cprint red Error: $ERROR
    echo Using default data instead
end
```

## The `$ERROR` Variable

When an error occurs in file operations, string operations, or C# code, the error message is stored in `$ERROR`:

```bash
fread $data nonexistent.txt
echo Last error: $ERROR
```

## Exit Codes

Exit a script with a code:

```bash
fexists $ok required_file.txt
if $ok != "true"
    cprint red Missing required file!
    exit 1
end

echo All good!
exit 0
```

The exit code is stored in `$EXITCODE`.

## Return from Functions

```bash
func validate($input)
    if $input == ""
        return error
    end
    return ok
end

call validate()
if $RESULT == "error"
    cprint red Validation failed!
end
```

## Nested Try/Catch

```bash
try
    try
        $data = @(int.Parse("abc"))
    catch
        echo Inner error: $ERROR
        echo Trying fallback...
        $data = 0
    end
    echo Data: $data
catch
    echo Outer error: $ERROR
end
```

## Common Error Patterns

### File operation safety

```bash
func safe_read($file)
    fexists $ok $file
    if $ok == "true"
        fread $content $file
        return $content
    else
        return FILE_NOT_FOUND
    end
end

call safe_read(config.txt)
if $RESULT == "FILE_NOT_FOUND"
    cprint yellow Config not found, using defaults
end
```

### Retry pattern

```bash
$attempts = 0
$success = false

while $success == "false"
    $attempts = @(int.Parse($attempts) + 1)
    if $attempts > 3
        cprint red Failed after 3 attempts
        break
    end
    
    try
        curl https://example.com
        $success = true
    catch
        cprint yellow Attempt $attempts failed, retrying...
        sleep 1000
    end
end
```
