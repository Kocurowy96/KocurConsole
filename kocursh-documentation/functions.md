# 🔧 Functions

## Defining Functions

```bash
func greet($name)
    cprint cyan Hello, $name!
end
```

## Calling Functions

```bash
call greet(World)
call greet($USER)
```

## Parameters

Functions can have multiple parameters:

```bash
func info($label, $value)
    cprint accent $label: $value
end

call info(User, $USER)
call info(Date, $DATE)
```

## Return Values

Functions can return values. The return value is stored in `$RESULT`:

```bash
func double($n)
    $result = @(int.Parse($n) * 2)
    return $result
end

call double(21)
echo The answer is: $RESULT     # → The answer is: 42
```

## Scope

- Parameters are **local** — they're restored after the function returns
- Other variables are **global** — changes inside a function affect the global state

```bash
$x = original

func modify($param)
    echo param = $param        # → param = test
    $x = modified              # changes global $x!
end

call modify(test)
echo $x                        # → modified
```

## Recursion

Functions can call themselves (with stack limits):

```bash
func countdown($n)
    if $n <= 0
        cprint green Liftoff!
        return
    end
    echo $n...
    sleep 500
    $next = @(int.Parse($n) - 1)
    call countdown($next)
end

call countdown(5)
```

## Functions as Builders

Combine functions with KocurConsole commands:

```bash
func create_project($name)
    mkdir $name
    cd $name
    touch README.md
    fwrite README.md # $name
    mkdir src
    touch src/main.cs
    cprint green Project $name created!
end

call create_project(MyApp)
```
