# ⚡ C# Integration

KocurSh's superpower is seamless C# integration. You can use full .NET Framework power from within scripts.

## Inline Expressions — `@()`

Evaluate a C# expression and use the result:

```bash
$result = @(2 + 2)                           # → 4
$pi = @(Math.PI)                             # → 3.14159...
$hour = @(DateTime.Now.Hour)                 # → 14
$name = @("hello".ToUpper())                 # → HELLO
$guid = @(Guid.NewGuid().ToString("N"))      # → abc123...
$rand = @(new Random().Next(1, 100))         # → 42
```

### Using script variables in C#

Script variables (`$var`) are automatically converted:
- Numbers stay as numbers
- Strings get quoted

```bash
$x = 10
$doubled = @($x * 2)        # → 20
$name = World
$greeting = @("Hello " + $name)  # → Hello World
```

## Code Blocks — `@{ }`

For multi-line C# code:

```bash
@{
    print("Hello from C#!");
    
    var now = DateTime.Now;
    print($"Date: {now:yyyy-MM-dd}");
    print($"Time: {now:HH:mm:ss}");
    
    for (int i = 1; i <= 5; i++)
        print($"  {i} × {i} = {i*i}");
}
```

### Available functions in C# blocks

| Function | Description |
|----------|-------------|
| `print(object)` | Print a line to output |
| `write(object)` | Print without newline |
| `getvar("name")` | Get a script variable |
| `setvar("name", "val")` | Set a script variable |

### Available namespaces

- `System`
- `System.Collections.Generic`
- `System.Linq`
- `System.IO`
- `System.Text`

### Accessing and modifying script variables

```bash
$mydata = some value

@{
    // Read script variable
    string data = getvar("mydata");
    print("From script: " + data);
    
    // Set script variable (available after block)
    setvar("computed", (2 + 2).ToString());
}

echo Computed in C#: $computed    # → 4
```

### Complex C# examples

**Fibonacci:**
```bash
@{
    int Fib(int n) => n <= 1 ? n : Fib(n-1) + Fib(n-2);
    
    for (int i = 0; i < 10; i++)
        print($"  Fib({i}) = {Fib(i)}");
}
```

**File analysis:**
```bash
@{
    var files = Directory.GetFiles(".", "*.cs");
    int totalLines = 0;
    
    foreach (var f in files)
    {
        int lines = File.ReadAllLines(f).Length;
        totalLines += lines;
        print($"  {Path.GetFileName(f)}: {lines} lines");
    }
    
    print($"\n  Total: {totalLines} lines across {files.Length} files");
}
```

**JSON-like data processing:**
```bash
@{
    var items = new[] { "Apple", "Banana", "Cherry", "Date" };
    
    print("Shopping list:");
    for (int i = 0; i < items.Length; i++)
        print($"  {i+1}. {items[i]}");
    
    setvar("count", items.Length.ToString());
}

echo Total items: $count
```

## C# in Conditions

Use `@()` in if statements for complex conditions:

```bash
if @(DateTime.Now.DayOfWeek == DayOfWeek.Friday)
    cprint green It's Friday! 🎉
end

if @(Environment.ProcessorCount > 4)
    echo Multi-core system detected
end
```

## Error Handling

C# errors are caught and displayed:

```bash
@{
    // This will show a compile error
    print(undefined_variable);
}
# Output: C# error: The name 'undefined_variable' does not exist... (line 2)
```

Use try/catch in KocurSh:

```bash
try
    $result = @(int.Parse("not a number"))
catch
    echo C# error caught: $ERROR
end
```
