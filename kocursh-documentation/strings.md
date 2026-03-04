# 🔤 String Operations

String builtins use function syntax: `$result = function(args)`

## Available Functions

| Function | Description | Example |
|----------|-------------|---------|
| `upper($text)` | Uppercase | `HELLO WORLD` |
| `lower($text)` | Lowercase | `hello world` |
| `len($text)` | String length | `11` |
| `trim($text)` | Remove whitespace | `hello` |
| `reverse($text)` | Reverse string | `dlroW olleH` |
| `replace($text, old, new)` | Replace substring | `Hello KocurSh` |
| `contains($text, sub)` | Contains check (true/false) | `true` |
| `startswith($text, pre)` | Starts with check | `true` |
| `endswith($text, suf)` | Ends with check | `false` |
| `split($text, delim)` | Split into comma-list | `a,b,c` |
| `substr($text, start, len)` | Substring | `World` |
| `substr($text, start)` | Substring from index | `World` |
| `repeat($text, count)` | Repeat N times | `hahaha` |
| `concat($a, $b, $c)` | Concatenate | `abc` |

## Examples

### Basic operations

```bash
$text = Hello World
$upper = upper($text)
$lower = lower($text)
$length = len($text)

echo Upper: $upper           # → HELLO WORLD
echo Lower: $lower           # → hello world
echo Length: $length          # → 11
```

### Search and replace

```bash
$msg = Hello World
$replaced = replace($msg, World, KocurSh)
echo $replaced               # → Hello KocurSh

$has = contains($msg, World)
echo Has World: $has         # → true

$starts = startswith($msg, Hello)
echo Starts with Hello: $starts  # → true
```

### Splitting and iterating

```bash
$csv = apple,banana,cherry
for $fruit in $csv
    echo Fruit: $fruit
end

# Or split by custom delimiter
$data = one;two;three
$split = split($data, ;)
for $item in $split
    echo Item: $item
end
```

### Building strings

```bash
$name = World
$greeting = concat(Hello , $name, !)
echo $greeting               # → Hello World!

$line = repeat(=, 30)
echo $line                   # → ==============================
```

### Substring

```bash
$text = Hello World
$sub = substr($text, 6)
echo $sub                    # → World

$sub2 = substr($text, 0, 5)
echo $sub2                   # → Hello
```

### Using with C# for more power

```bash
$text = Hello World
$result = @("Hello World".Replace("World", "KocurSh").ToUpper())
echo $result                 # → HELLO KOCURSH
```
