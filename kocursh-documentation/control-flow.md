# 🔀 Control Flow

## If / Else / End

```bash
if $x == 5
    echo X is five
end

if $name != "admin"
    echo Access denied
else
    echo Welcome, admin
end
```

### Comparison operators

| Operator | Description |
|----------|-------------|
| `==` | Equal |
| `!=` | Not equal |
| `>` | Greater than |
| `<` | Less than |
| `>=` | Greater or equal |
| `<=` | Less or equal |

Numbers are compared numerically, strings are compared as text.

### C# conditions

```bash
if @(DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
    echo It's Saturday!
end
```

### Truthy values

Any non-empty string that isn't `"0"` or `"false"` is truthy:

```bash
$flag = true
if $flag
    echo Flag is set
end
```

## For Loop

### Range

```bash
for $i in 1..10
    echo Number: $i
end

# Reverse range
for $i in 10..1
    echo Countdown: $i
end
```

### List

```bash
for $color in red,green,blue
    cprint $color This is $color!
end

for $file in readme.md,index.html,style.css
    touch $file
end
```

### Dynamic list from variable

```bash
$items = apple,banana,cherry
for $item in $items
    echo Fruit: $item
end
```

## While Loop

```bash
$i = 0
while $i < 10
    echo $i
    $i = @(int.Parse($i) + 1)
end
```

Safety limit: while loops stop after 10,000 iterations to prevent infinite loops.

## Break / Continue

```bash
for $i in 1..100
    if $i == 5
        echo Stopping at 5
        break
    end
    echo $i
end
```

```bash
for $i in 1..10
    if @(int.Parse($i) % 2 == 0)
        continue
    end
    echo Odd: $i
end
```

## Nesting

All control structures can be nested:

```bash
for $x in 1..3
    for $y in 1..3
        if $x == $y
            cprint green ($x, $y) - diagonal!
        else
            echo ($x, $y)
        end
    end
end
```
