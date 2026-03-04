using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CSharp;

namespace KocurConsole
{
    /// <summary>
    /// KocurSh — custom scripting language for KocurConsole.
    /// Hybrid between KocurConsole commands and C# code.
    /// 
    /// Features:
    ///   $var = value         Variable assignment
    ///   $var = @(C# expr)    Variable from C# expression
    ///   echo Hello, $var!    Variable interpolation in commands
    ///   @{ C# code }         Inline C# block (multi-line)
    ///   @( C# expression )   Inline C# expression (returns string)
    ///   if $x == "yes"       Conditional (string comparison)
    ///   if @(C# bool expr)   Conditional (C# expression)
    ///   else                 Else branch
    ///   end                  End if/for/func block
    ///   for $i in 1..10      For loop with range
    ///   for $item in $list   For each (comma-separated)
    ///   func name($a, $b)    Function definition
    ///   call name(args)      Function call
    ///   sleep <ms>           Pause execution
    ///   input $var "prompt"  Read user input (stores in var)
    ///   # comment            Comment line
    ///   Any other line       Executed as KocurConsole command
    /// </summary>
    public class KocurShEngine
    {
        private Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<string>> functions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private Action<string> executeCommand;
        private Action<string, System.Drawing.Color> appendText;
        private Action<string, System.Drawing.Color> replaceLastLine;
        private Func<bool> isCancelled;

        public KocurShEngine(Action<string> executeCommand, Action<string, System.Drawing.Color> appendText, Action<string, System.Drawing.Color> replaceLastLine, Func<bool> isCancelled)
        {
            this.executeCommand = executeCommand;
            this.appendText = appendText;
            this.replaceLastLine = replaceLastLine;
            this.isCancelled = isCancelled;

            // Built-in variables
            variables["USER"] = Environment.UserName;
            variables["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            variables["OS"] = Environment.OSVersion.ToString();
            variables["TIME"] = DateTime.Now.ToString("HH:mm:ss");
            variables["DATE"] = DateTime.Now.ToString("yyyy-MM-dd");
            variables["RANDOM"] = new Random().Next(0, 1000).ToString();
            variables["NEWLINE"] = "\n";
            variables["TAB"] = "\t";
            variables["TRUE"] = "true";
            variables["FALSE"] = "false";
            variables["RESULT"] = "";
            variables["ERROR"] = "";
            variables["ARGS"] = "";
            variables["ARGC"] = "0";
            variables["PI"] = Math.PI.ToString();
            variables["E"] = Math.E.ToString();
        }

        // Break/continue flags for loop control
        private bool breakRequested = false;
        private bool continueRequested = false;
        // Return flag for function returns
        private bool returnRequested = false;
        private string returnValue = "";

        /// <summary>
        /// Execute a .kocursh script from a file path.
        /// </summary>
        public void ExecuteFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                appendText("Script not found: " + filePath + "\n", System.Drawing.Color.Red);
                return;
            }

            variables["SCRIPT"] = Path.GetFileName(filePath);
            variables["SCRIPTDIR"] = Path.GetDirectoryName(Path.GetFullPath(filePath));
            string[] lines = File.ReadAllLines(filePath);
            ExecuteLines(lines, 0, lines.Length);
        }

        /// <summary>
        /// Execute file with arguments.
        /// </summary>
        public void ExecuteFile(string filePath, string[] scriptArgs)
        {
            if (scriptArgs != null)
            {
                variables["ARGS"] = string.Join(" ", scriptArgs);
                variables["ARGC"] = scriptArgs.Length.ToString();
                for (int a = 0; a < scriptArgs.Length; a++)
                    variables[(a + 1).ToString()] = scriptArgs[a];
            }
            ExecuteFile(filePath);
        }

        /// <summary>
        /// Execute a range of script lines.
        /// </summary>
        private void ExecuteLines(string[] lines, int start, int end)
        {
            int i = start;
            while (i < end && !isCancelled())
            {
                if (breakRequested || returnRequested) break;
                if (continueRequested) { continueRequested = false; break; }

                string rawLine = lines[i];
                string line = rawLine.Trim();

                // Refresh dynamic variables
                variables["TIME"] = DateTime.Now.ToString("HH:mm:ss");
                variables["DATE"] = DateTime.Now.ToString("yyyy-MM-dd");
                variables["RANDOM"] = new Random().Next(0, 1000).ToString();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    i++;
                    continue;
                }

                // String builtins FIRST: $result = upper($text) etc.
                if (HandleStringBuiltin(line)) { i++; continue; }

                // File builtins FIRST: fread, fwrite etc.
                if (HandleFileBuiltin(line)) { i++; continue; }

                // Variable assignment: $var = value
                if (line.StartsWith("$") && line.Contains("="))
                {
                    HandleAssignment(line);
                    i++;
                    continue;
                }

                // C# block: @{ ... }
                if (line.StartsWith("@{"))
                {
                    // Count ALL braces for C# blocks (not just @{/})
                    int depth = 0;
                    int blockEnd = -1;
                    for (int j = i; j < end; j++)
                    {
                        string bl = lines[j];
                        foreach (char c in bl)
                        {
                            if (c == '{') depth++;
                            else if (c == '}') depth--;
                        }
                        if (depth == 0) { blockEnd = j; break; }
                    }

                    if (blockEnd > i)
                    {
                        StringBuilder csCode = new StringBuilder();
                        string firstLine = line.Substring(2).Trim();
                        if (firstLine.Length > 0 && firstLine != "}")
                            csCode.AppendLine(firstLine);

                        for (int j = i + 1; j <= blockEnd; j++)
                        {
                            string codeLine = lines[j].TrimEnd();
                            if (j == blockEnd && codeLine.Trim() == "}") break;
                            csCode.AppendLine(codeLine);
                        }
                        i = blockEnd + 1;
                        ExecuteCSharpBlock(csCode.ToString());
                        continue;
                    }
                    i++;
                    continue;
                }

                // If/else/end
                if (line.StartsWith("if "))
                {
                    i = HandleIf(lines, i, end);
                    continue;
                }

                // While loop: while <condition> ... end
                if (line.StartsWith("while "))
                {
                    i = HandleWhile(lines, i, end);
                    continue;
                }

                // For loop: for $i in 1..10  /  for $item in a,b,c
                if (line.StartsWith("for "))
                {
                    i = HandleFor(lines, i, end);
                    continue;
                }

                // Try/catch/end
                if (line == "try")
                {
                    i = HandleTryCatch(lines, i, end);
                    continue;
                }

                // Function definition: func name($a, $b)
                if (line.StartsWith("func "))
                {
                    i = HandleFuncDef(lines, i, end);
                    continue;
                }

                // Function call: call name(args)
                if (line.StartsWith("call "))
                {
                    HandleFuncCall(line);
                    i++;
                    continue;
                }

                // Include: include <file>
                if (line.StartsWith("include "))
                {
                    string incPath = InterpolateVars(line.Substring(8).Trim());
                    if (variables.ContainsKey("SCRIPTDIR"))
                        incPath = Path.Combine(variables["SCRIPTDIR"], incPath);
                    if (File.Exists(incPath))
                    {
                        string[] incLines = File.ReadAllLines(incPath);
                        ExecuteLines(incLines, 0, incLines.Length);
                    }
                    else
                        appendText("Include not found: " + incPath + "\n", System.Drawing.Color.Red);
                    i++;
                    continue;
                }

                // Break
                if (line == "break")
                {
                    breakRequested = true;
                    break;
                }

                // Continue
                if (line == "continue")
                {
                    continueRequested = true;
                    break;
                }

                // Sleep
                if (line.StartsWith("sleep "))
                {
                    string msStr = InterpolateVars(line.Substring(6).Trim());
                    if (int.TryParse(msStr, out int ms))
                        System.Threading.Thread.Sleep(ms);
                    i++;
                    continue;
                }

                // Print (explicit, with interpolation + color support)
                if (line == "print" || line.StartsWith("print "))
                {
                    string text = line.Length > 6 ? InterpolateVars(line.Substring(6)) : "";
                    appendText(text + "\n", ThemeManager.Current.TextColor);
                    i++;
                    continue;
                }

                // Color print: cprint <color> <text>
                if (line.StartsWith("cprint "))
                {
                    HandleColorPrint(line.Substring(7));
                    i++;
                    continue;
                }

                // Progress bar: progress <current> <max> [text]
                if (line.StartsWith("progress!") || line.StartsWith("progress "))
                {
                    bool tui = line.StartsWith("progress!");
                    string progressArgs = tui ? line.Substring(9).TrimStart() : line.Substring(9);
                    HandleProgress(progressArgs, tui);
                    i++;
                    continue;
                }

                // Beep
                if (line == "beep")
                {
                    Console.Beep();
                    i++;
                    continue;
                }

                // Confirm: confirm $var "Are you sure?"
                if (line.StartsWith("confirm "))
                {
                    // Confirm is async-unfriendly, store true for scripts
                    string rest = line.Substring(8).Trim();
                    variables["RESULT"] = "true";
                    appendText(InterpolateVars(rest) + " [auto-yes in script]\n", ThemeManager.Current.WarningColor);
                    i++;
                    continue;
                }


                // Exit
                if (line == "exit" || line.StartsWith("exit "))
                {
                    string code = line.Length > 5 ? InterpolateVars(line.Substring(5).Trim()) : "0";
                    variables["EXITCODE"] = code;
                    returnRequested = true;
                    break;
                }

                // Return (from function)
                if (line == "return" || line.StartsWith("return "))
                {
                    if (line.Length > 7)
                    {
                        returnValue = InterpolateVars(line.Substring(7).Trim());
                        variables["RESULT"] = returnValue;
                    }
                    returnRequested = true;
                    break;
                }

                // Default: treat as KocurConsole command with variable interpolation
                string interpolated = InterpolateVars(line);
                executeCommand(interpolated);
                i++;
            }
        }

        /// <summary>
        /// Handle variable assignment: $name = value
        /// Supports: $x = hello, $x = @(C# expr), $x = @{ C# block }
        /// </summary>
        private void HandleAssignment(string line)
        {
            int eqIdx = line.IndexOf('=');
            string varName = line.Substring(0, eqIdx).Trim().TrimStart('$');
            string valueExpr = line.Substring(eqIdx + 1).Trim();

            if (valueExpr.StartsWith("@(") && valueExpr.EndsWith(")"))
            {
                // C# expression
                string csExpr = valueExpr.Substring(2, valueExpr.Length - 3);
                csExpr = InterpolateVarsInCSharp(csExpr);
                string result = EvaluateCSharpExpression(csExpr);
                variables[varName] = result ?? "";
            }
            else
            {
                // Simple value with interpolation
                variables[varName] = InterpolateVars(valueExpr);
            }
        }

        /// <summary>
        /// Handle if/else/end blocks.
        /// Supports: if $x == "val", if $x != "val", if @(C# bool)
        /// </summary>
        private int HandleIf(string[] lines, int ifLine, int maxEnd)
        {
            string condition = lines[ifLine].Trim().Substring(3).Trim();
            bool result = EvaluateCondition(condition);

            // Find else/end
            int depth = 0;
            int elseLine = -1;
            int endLine = -1;

            for (int j = ifLine + 1; j < maxEnd; j++)
            {
                string l = lines[j].Trim();
                if (l.StartsWith("if ") || l.StartsWith("for ") || l.StartsWith("func "))
                    depth++;
                else if (l == "end")
                {
                    if (depth == 0) { endLine = j; break; }
                    depth--;
                }
                else if (l == "else" && depth == 0)
                    elseLine = j;
            }

            if (endLine == -1)
            {
                appendText("Error: missing 'end' for 'if' on line " + (ifLine + 1) + "\n", System.Drawing.Color.Red);
                return ifLine + 1;
            }

            if (result)
            {
                int blockEnd = elseLine != -1 ? elseLine : endLine;
                ExecuteLines(lines, ifLine + 1, blockEnd);
            }
            else if (elseLine != -1)
            {
                ExecuteLines(lines, elseLine + 1, endLine);
            }

            return endLine + 1;
        }

        /// <summary>
        /// Handle for loops: for $i in 1..10  /  for $item in a,b,c
        /// </summary>
        private int HandleFor(string[] lines, int forLine, int maxEnd)
        {
            string forExpr = lines[forLine].Trim().Substring(4).Trim();
            // Parse: $var in <range|list>
            Match m = Regex.Match(forExpr, @"\$(\w+)\s+in\s+(.+)");
            if (!m.Success)
            {
                appendText("Error: invalid for syntax on line " + (forLine + 1) + "\n", System.Drawing.Color.Red);
                return forLine + 1;
            }

            string varName = m.Groups[1].Value;
            string rangeExpr = InterpolateVars(m.Groups[2].Value.Trim());

            // Find end
            int depth = 0;
            int endLine = -1;
            for (int j = forLine + 1; j < maxEnd; j++)
            {
                string l = lines[j].Trim();
                if (l.StartsWith("if ") || l.StartsWith("for ") || l.StartsWith("func "))
                    depth++;
                else if (l == "end")
                {
                    if (depth == 0) { endLine = j; break; }
                    depth--;
                }
            }

            if (endLine == -1)
            {
                appendText("Error: missing 'end' for 'for' on line " + (forLine + 1) + "\n", System.Drawing.Color.Red);
                return forLine + 1;
            }

            // Parse range: 1..10 or list: a,b,c
            List<string> values = new List<string>();
            Match rangeMatch = Regex.Match(rangeExpr, @"(\d+)\.\.(\d+)");
            if (rangeMatch.Success)
            {
                int from = int.Parse(rangeMatch.Groups[1].Value);
                int to = int.Parse(rangeMatch.Groups[2].Value);
                int step = from <= to ? 1 : -1;
                for (int v = from; step > 0 ? v <= to : v >= to; v += step)
                    values.Add(v.ToString());
            }
            else
            {
                values.AddRange(rangeExpr.Split(',').Select(s => s.Trim()));
            }

            // Execute loop body
            string oldValue = variables.ContainsKey(varName) ? variables[varName] : null;
            foreach (string val in values)
            {
                if (isCancelled()) break;
                variables[varName] = val;
                ExecuteLines(lines, forLine + 1, endLine);
            }
            if (oldValue != null) variables[varName] = oldValue;
            else variables.Remove(varName);

            return endLine + 1;
        }

        /// <summary>
        /// Handle function definition: func name($a, $b) ... end
        /// </summary>
        private int HandleFuncDef(string[] lines, int funcLine, int maxEnd)
        {
            string funcDecl = lines[funcLine].Trim().Substring(5).Trim();

            // Find end
            int depth = 0;
            int endLine = -1;
            for (int j = funcLine + 1; j < maxEnd; j++)
            {
                string l = lines[j].Trim();
                if (l.StartsWith("if ") || l.StartsWith("for ") || l.StartsWith("func "))
                    depth++;
                else if (l == "end")
                {
                    if (depth == 0) { endLine = j; break; }
                    depth--;
                }
            }

            if (endLine == -1)
            {
                appendText("Error: missing 'end' for 'func' on line " + (funcLine + 1) + "\n", System.Drawing.Color.Red);
                return funcLine + 1;
            }

            // Store the function body
            List<string> body = new List<string>();
            body.Add(funcDecl); // first line has the declaration (name + params)
            for (int j = funcLine + 1; j < endLine; j++)
                body.Add(lines[j]);

            string funcName = funcDecl.Split('(')[0].Trim();
            functions[funcName] = body;

            return endLine + 1;
        }

        /// <summary>
        /// Handle function call: call name(arg1, arg2)
        /// </summary>
        private void HandleFuncCall(string line)
        {
            string callExpr = line.Substring(5).Trim();
            string funcName;
            string[] callArgs;

            int parenStart = callExpr.IndexOf('(');
            if (parenStart > 0 && callExpr.EndsWith(")"))
            {
                funcName = callExpr.Substring(0, parenStart).Trim();
                string argsStr = callExpr.Substring(parenStart + 1, callExpr.Length - parenStart - 2);
                callArgs = argsStr.Split(',').Select(a => InterpolateVars(a.Trim())).ToArray();
            }
            else
            {
                funcName = callExpr;
                callArgs = new string[0];
            }

            if (!functions.ContainsKey(funcName))
            {
                appendText("Error: function '" + funcName + "' not found\n", System.Drawing.Color.Red);
                return;
            }

            List<string> body = functions[funcName];
            string decl = body[0]; // name($a, $b)

            // Parse parameter names
            string[] paramNames = new string[0];
            int pStart = decl.IndexOf('(');
            if (pStart > 0 && decl.Contains(")"))
            {
                string paramStr = decl.Substring(pStart + 1, decl.IndexOf(')') - pStart - 1);
                paramNames = paramStr.Split(',').Select(p => p.Trim().TrimStart('$')).ToArray();
            }

            // Save old variables, set params
            Dictionary<string, string> saved = new Dictionary<string, string>();
            for (int p = 0; p < paramNames.Length; p++)
            {
                string pName = paramNames[p];
                if (variables.ContainsKey(pName)) saved[pName] = variables[pName];
                variables[pName] = p < callArgs.Length ? callArgs[p] : "";
            }

    // Execute body (skip first line which is declaration)
    string[] bodyLines = body.Skip(1).ToArray();
    ExecuteLines(bodyLines, 0, bodyLines.Length);

    // Reset return flag (return only exits function, not entire script)
    if (returnRequested)
    {
        returnRequested = false;
        if (!string.IsNullOrEmpty(returnValue))
        {
            variables["RESULT"] = returnValue;
            returnValue = "";
        }
    }

            // Restore variables
            foreach (string pName in paramNames)
            {
                if (saved.ContainsKey(pName)) variables[pName] = saved[pName];
                else variables.Remove(pName);
            }
        }

        /// <summary>
        /// Interpolate $variables in a string.
        /// $var gets replaced with its value, ${var} also works.
        /// </summary>
        private string InterpolateVars(string text)
        {
            // Replace ${var} syntax first
            text = Regex.Replace(text, @"\$\{(\w+)\}", m =>
            {
                string name = m.Groups[1].Value;
                return variables.ContainsKey(name) ? variables[name] : m.Value;
            });

            // Replace $var syntax
            text = Regex.Replace(text, @"\$(\w+)", m =>
            {
                string name = m.Groups[1].Value;
                return variables.ContainsKey(name) ? variables[name] : m.Value;
            });

            // Replace @(expr) inline C# expressions
            text = Regex.Replace(text, @"@\(([^)]+)\)", m =>
            {
                string expr = InterpolateVarsInCSharp(m.Groups[1].Value);
                return EvaluateCSharpExpression(expr) ?? m.Value;
            });

            return text;
        }

        /// <summary>
        /// Replace $vars in C# code with their string values (always quoted).
        /// </summary>
        private string InterpolateVarsInCSharp(string code)
        {
            return Regex.Replace(code, @"\$(\w+)", m =>
            {
                string name = m.Groups[1].Value;
                if (variables.ContainsKey(name))
                {
                    string val = variables[name];
                    return "\"" + val.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                }
                return "\"\"";
            });
        }

        /// <summary>
        /// Evaluate a condition for if statements.
        /// Supports: $x == "val", $x != "val", $x > 5, @(C# bool expr)
        /// </summary>
        private bool EvaluateCondition(string condition)
        {
            condition = condition.Trim();

            // C# expression: @(expr)
            if (condition.StartsWith("@(") && condition.EndsWith(")"))
            {
                string csExpr = InterpolateVarsInCSharp(condition.Substring(2, condition.Length - 3));
                string result = EvaluateCSharpExpression(csExpr);
                return result != null && (result.ToLower() == "true" || result == "1");
            }

            // String interpolation first
            condition = InterpolateVars(condition);

            // Comparison operators
            string[] ops = { "!=", "==", ">=", "<=", ">", "<" };
            foreach (string op in ops)
            {
                int idx = condition.IndexOf(op);
                if (idx > 0)
                {
                    string left = condition.Substring(0, idx).Trim().Trim('"');
                    string right = condition.Substring(idx + op.Length).Trim().Trim('"');

                    // Try numeric comparison
                    if (double.TryParse(left, out double lNum) && double.TryParse(right, out double rNum))
                    {
                        switch (op)
                        {
                            case "==": return lNum == rNum;
                            case "!=": return lNum != rNum;
                            case ">": return lNum > rNum;
                            case "<": return lNum < rNum;
                            case ">=": return lNum >= rNum;
                            case "<=": return lNum <= rNum;
                        }
                    }

                    // String comparison
                    switch (op)
                    {
                        case "==": return left == right;
                        case "!=": return left != right;
                        default: return false;
                    }
                }
            }

            // Truthy: non-empty, non-zero
            return !string.IsNullOrEmpty(condition) && condition != "0" && condition.ToLower() != "false";
        }

        /// <summary>
        /// Execute a C# code block. Output is captured via Console.Write/WriteLine.
        /// Has access to script variables via vars dictionary.
        /// </summary>
        private void ExecuteCSharpBlock(string code)
        {
            try
            {
                // Wrap code in a class with access to variables
                string fullCode = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

public class KocurShScript
{
    public static string Execute(Dictionary<string, string> vars)
    {
        var output = new StringBuilder();
        Action<object> print = (obj) => output.AppendLine(obj != null ? obj.ToString() : """");
        Action<object> write = (obj) => output.Append(obj != null ? obj.ToString() : """");
        Func<string, string> getvar = (name) => vars.ContainsKey(name) ? vars[name] : """";
        Action<string, string> setvar = (name, val) => vars[name] = val;

" + code + @"

        return output.ToString();
    }
}";
                CSharpCodeProvider provider = new CSharpCodeProvider();
                CompilerParameters parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false
                };
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                parameters.ReferencedAssemblies.Add("System.Linq.dll");

                CompilerResults results = provider.CompileAssemblyFromSource(parameters, fullCode);

                if (results.Errors.HasErrors)
                {
                    foreach (CompilerError err in results.Errors)
                    {
                        if (!err.IsWarning)
                            appendText("  C# error: " + err.ErrorText + " (line " + (err.Line - 12) + ")\n", System.Drawing.Color.Red);
                    }
                    return;
                }

                Type type = results.CompiledAssembly.GetType("KocurShScript");
                MethodInfo method = type.GetMethod("Execute");
                string output = (string)method.Invoke(null, new object[] { variables });

                if (!string.IsNullOrEmpty(output))
                    appendText(output, ThemeManager.Current.TextColor);
            }
            catch (Exception ex)
            {
                appendText("C# error: " + (ex.InnerException?.Message ?? ex.Message) + "\n", System.Drawing.Color.Red);
            }
        }

        /// <summary>
        /// Evaluate a C# expression and return the result as a string.
        /// </summary>
        private string EvaluateCSharpExpression(string expression)
        {
            try
            {
                string code = @"
using System;
using System.Linq;
public class KocurShExpr
{
    public static string Eval()
    {
        return (" + expression + @").ToString();
    }
}";
                CSharpCodeProvider provider = new CSharpCodeProvider();
                CompilerParameters parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false
                };
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");

                CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);
                if (results.Errors.HasErrors) return null;

                Type type = results.CompiledAssembly.GetType("KocurShExpr");
                MethodInfo method = type.GetMethod("Eval");
                return (string)method.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all current variables (for display).
        /// </summary>
        public Dictionary<string, string> GetVariables() => variables;

        /// <summary>
        /// Set a variable externally.
        /// </summary>
        public void SetVariable(string name, string value) => variables[name] = value;

        #region Block Helpers

        /// <summary>
        /// Find closing brace/keyword for a block (e.g., @{ ... }, while ... end).
        /// </summary>
        private int FindBlockEnd(string[] lines, int start, string openToken, string closeToken)
        {
            int depth = 1;
            for (int j = start + 1; j < lines.Length; j++)
            {
                string l = lines[j].Trim();
                if (l.StartsWith(openToken)) depth++;
                if (l == closeToken || l.StartsWith(closeToken + " ")) depth--;
                if (depth == 0) return j;
            }
            return start; // not found
        }

        /// <summary>
        /// Find 'end' keyword matching a block start (if/for/while/func/try).
        /// </summary>
        private int FindEndBlock(string[] lines, int start, int maxEnd)
        {
            int depth = 0;
            for (int j = start + 1; j < maxEnd; j++)
            {
                string l = lines[j].Trim();
                if (l.StartsWith("if ") || l.StartsWith("for ") || l.StartsWith("while ") || l.StartsWith("func ") || l == "try")
                    depth++;
                else if (l == "end")
                {
                    if (depth == 0) return j;
                    depth--;
                }
            }
            return -1;
        }

        #endregion

        #region While Loop

        private int HandleWhile(string[] lines, int whileLine, int maxEnd)
        {
            string condition = lines[whileLine].Trim().Substring(6).Trim();

            int endLine = FindEndBlock(lines, whileLine, maxEnd);
            if (endLine == -1)
            {
                appendText("Error: missing 'end' for 'while' on line " + (whileLine + 1) + "\n", System.Drawing.Color.Red);
                return whileLine + 1;
            }

            int maxIterations = 10000; // safety limit
            int iter = 0;
            while (EvaluateCondition(condition) && !isCancelled() && iter < maxIterations)
            {
                breakRequested = false;
                ExecuteLines(lines, whileLine + 1, endLine);
                if (breakRequested)
                {
                    breakRequested = false;
                    break;
                }
                if (returnRequested) break;
                iter++;
            }

            if (iter >= maxIterations)
                appendText("Warning: while loop hit " + maxIterations + " iteration limit\n", ThemeManager.Current.WarningColor);

            return endLine + 1;
        }

        #endregion

        #region Try/Catch

        private int HandleTryCatch(string[] lines, int tryLine, int maxEnd)
        {
            // Find catch and end
            int depth = 0;
            int catchLine = -1;
            int endLine = -1;

            for (int j = tryLine + 1; j < maxEnd; j++)
            {
                string l = lines[j].Trim();
                if (l.StartsWith("if ") || l.StartsWith("for ") || l.StartsWith("while ") || l.StartsWith("func ") || l == "try")
                    depth++;
                else if (l == "end")
                {
                    if (depth == 0) { endLine = j; break; }
                    depth--;
                }
                else if (l == "catch" && depth == 0)
                    catchLine = j;
            }

            if (endLine == -1)
            {
                appendText("Error: missing 'end' for 'try' on line " + (tryLine + 1) + "\n", System.Drawing.Color.Red);
                return tryLine + 1;
            }

            try
            {
                int blockEnd = catchLine != -1 ? catchLine : endLine;
                ExecuteLines(lines, tryLine + 1, blockEnd);
            }
            catch (Exception ex)
            {
                variables["ERROR"] = ex.Message;
                if (catchLine != -1)
                    ExecuteLines(lines, catchLine + 1, endLine);
            }

            return endLine + 1;
        }

        #endregion

        #region Terminal Integrations

        /// <summary>
        /// Color print: cprint red Hello World!
        /// </summary>
        private void HandleColorPrint(string rest)
        {
            rest = rest.Trim();
            int spaceIdx = rest.IndexOf(' ');
            if (spaceIdx <= 0) { appendText(rest + "\n", ThemeManager.Current.TextColor); return; }

            string colorName = rest.Substring(0, spaceIdx).ToLower();
            string text = InterpolateVars(rest.Substring(spaceIdx + 1));

            System.Drawing.Color color;
            switch (colorName)
            {
                case "red": color = System.Drawing.Color.FromArgb(255, 85, 85); break;
                case "green": color = System.Drawing.Color.FromArgb(80, 250, 123); break;
                case "blue": color = System.Drawing.Color.FromArgb(86, 156, 214); break;
                case "yellow": color = System.Drawing.Color.FromArgb(241, 250, 140); break;
                case "orange": color = System.Drawing.Color.FromArgb(255, 184, 108); break;
                case "purple": case "magenta": color = System.Drawing.Color.FromArgb(189, 147, 249); break;
                case "cyan": color = System.Drawing.Color.FromArgb(139, 233, 253); break;
                case "pink": color = System.Drawing.Color.FromArgb(255, 121, 198); break;
                case "white": color = System.Drawing.Color.White; break;
                case "gray": case "grey": color = System.Drawing.Color.Gray; break;
                case "accent": color = ThemeManager.Current.AccentColor; break;
                case "info": color = ThemeManager.Current.InfoColor; break;
                case "warn": case "warning": color = ThemeManager.Current.WarningColor; break;
                case "error": color = ThemeManager.Current.ErrorColor; break;
                default: color = ThemeManager.Current.TextColor; break;
            }

            appendText(text + "\n", color);
        }

        /// <summary>
        /// Progress bar: progress 50 100 "Downloading..."
        /// TUI mode (progress!): overwrites previous line for in-place updates
        /// </summary>
        private void HandleProgress(string rest, bool tui = false)
        {
            rest = InterpolateVars(rest.Trim());
            string[] parts = rest.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return;

            if (int.TryParse(parts[0], out int current) && int.TryParse(parts[1], out int max) && max > 0)
            {
                int barWidth = 30;
                int filled = (int)((double)current / max * barWidth);
                filled = Math.Min(filled, barWidth);
                string bar = new string('█', filled) + new string('░', barWidth - filled);
                int percent = (int)((double)current / max * 100);
                string label = parts.Length > 2 ? parts[2] + " " : "";
                string line = "  " + label + "[" + bar + "] " + percent + "%";

                if (tui && replaceLastLine != null)
                    replaceLastLine(line + "\n", ThemeManager.Current.AccentColor);
                else
                    appendText(line + "\n", ThemeManager.Current.AccentColor);
            }
        }

        #endregion

        #region String Builtins

        /// <summary>
        /// Handle string builtins. Returns true if the line was handled.
        /// Syntax: $result = upper($text)  /  $result = len($text)  / etc.
        /// Also standalone: upper $var  /  lower $var
        /// </summary>
        private bool HandleStringBuiltin(string line)
        {
            // Match: $var = func($arg) or $var = func($arg, $arg2)
            Match m = Regex.Match(line, @"^\$(\w+)\s*=\s*(upper|lower|len|trim|replace|contains|split|substr|repeat|concat|reverse|startswith|endswith)\((.+)\)$");
            if (!m.Success) return false;

            string resultVar = m.Groups[1].Value;
            string func = m.Groups[2].Value.ToLower();
            string argsStr = m.Groups[3].Value;

            // Parse arguments (respecting commas inside quotes)
            List<string> args = ParseFuncArgs(argsStr);
            for (int a = 0; a < args.Count; a++)
                args[a] = InterpolateVars(args[a].Trim().Trim('"'));

            string result = "";
            try
            {
                switch (func)
                {
                    case "upper": result = args[0].ToUpper(); break;
                    case "lower": result = args[0].ToLower(); break;
                    case "len": result = args[0].Length.ToString(); break;
                    case "trim": result = args[0].Trim(); break;
                    case "reverse": result = new string(args[0].Reverse().ToArray()); break;
                    case "replace":
                        result = args.Count >= 3 ? args[0].Replace(args[1], args[2]) : args[0];
                        break;
                    case "contains":
                        result = args.Count >= 2 && args[0].Contains(args[1]) ? "true" : "false";
                        break;
                    case "startswith":
                        result = args.Count >= 2 && args[0].StartsWith(args[1]) ? "true" : "false";
                        break;
                    case "endswith":
                        result = args.Count >= 2 && args[0].EndsWith(args[1]) ? "true" : "false";
                        break;
                    case "split":
                        if (args.Count >= 2)
                        {
                            string[] splitParts = args[0].Split(new[] { args[1] }, StringSplitOptions.None);
                            result = string.Join(",", splitParts);
                        }
                        break;
                    case "substr":
                        if (args.Count >= 3 && int.TryParse(args[1], out int start) && int.TryParse(args[2], out int length))
                            result = args[0].Substring(Math.Min(start, args[0].Length), Math.Min(length, args[0].Length - Math.Min(start, args[0].Length)));
                        else if (args.Count >= 2 && int.TryParse(args[1], out int start2))
                            result = args[0].Substring(Math.Min(start2, args[0].Length));
                        break;
                    case "repeat":
                        if (args.Count >= 2 && int.TryParse(args[1], out int count))
                            result = string.Concat(Enumerable.Repeat(args[0], Math.Min(count, 1000)));
                        break;
                    case "concat":
                        result = string.Join("", args);
                        break;
                }
            }
            catch (Exception ex)
            {
                variables["ERROR"] = ex.Message;
                result = "";
            }

            variables[resultVar] = result;
            return true;
        }

        /// <summary>
        /// Parse function arguments, handling quoted strings with commas.
        /// </summary>
        private List<string> ParseFuncArgs(string argsStr)
        {
            List<string> result = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;
            char quoteChar = ' ';

            foreach (char c in argsStr)
            {
                if ((c == '"' || c == '\'') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (c == quoteChar && inQuotes)
                {
                    inQuotes = false;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) result.Add(current.ToString());
            return result;
        }

        #endregion

        #region File Builtins

        /// <summary>
        /// Handle file builtins. Returns true if handled.
        /// fread $var <file>  |  fwrite <file> <text>  |  fappend <file> <text>
        /// fexists $var <path>  |  fdelete <path>  |  flist $var <dir>
        /// </summary>
        private bool HandleFileBuiltin(string line)
        {
            // fread $var <file>
            if (line.StartsWith("fread "))
            {
                string rest = line.Substring(6).Trim();
                Match m = Regex.Match(rest, @"^\$(\w+)\s+(.+)$");
                if (m.Success)
                {
                    string varN = m.Groups[1].Value;
                    string path = InterpolateVars(m.Groups[2].Value.Trim());
                    try { variables[varN] = File.ReadAllText(path); }
                    catch (Exception ex) { variables["ERROR"] = ex.Message; variables[varN] = ""; }
                }
                return true;
            }

            // fwrite <file> <text>
            if (line.StartsWith("fwrite "))
            {
                string rest = line.Substring(7).Trim();
                int spaceIdx = rest.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    string path = InterpolateVars(rest.Substring(0, spaceIdx));
                    string text = InterpolateVars(rest.Substring(spaceIdx + 1));
                    try { File.WriteAllText(path, text); }
                    catch (Exception ex) { variables["ERROR"] = ex.Message; }
                }
                return true;
            }

            // fappend <file> <text>
            if (line.StartsWith("fappend "))
            {
                string rest = line.Substring(8).Trim();
                int spaceIdx = rest.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    string path = InterpolateVars(rest.Substring(0, spaceIdx));
                    string text = InterpolateVars(rest.Substring(spaceIdx + 1));
                    try { File.AppendAllText(path, text + "\n"); }
                    catch (Exception ex) { variables["ERROR"] = ex.Message; }
                }
                return true;
            }

            // fexists $var <path>
            if (line.StartsWith("fexists "))
            {
                string rest = line.Substring(8).Trim();
                Match m = Regex.Match(rest, @"^\$(\w+)\s+(.+)$");
                if (m.Success)
                {
                    string varN = m.Groups[1].Value;
                    string path = InterpolateVars(m.Groups[2].Value.Trim());
                    variables[varN] = (File.Exists(path) || Directory.Exists(path)) ? "true" : "false";
                }
                return true;
            }

            // fdelete <path>
            if (line.StartsWith("fdelete "))
            {
                string path = InterpolateVars(line.Substring(8).Trim());
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    else if (Directory.Exists(path)) Directory.Delete(path, true);
                }
                catch (Exception ex) { variables["ERROR"] = ex.Message; }
                return true;
            }

            // flist $var <dir> [pattern]
            if (line.StartsWith("flist "))
            {
                string rest = line.Substring(6).Trim();
                Match m = Regex.Match(rest, @"^\$(\w+)\s+(.+)$");
                if (m.Success)
                {
                    string varN = m.Groups[1].Value;
                    string dirPath = InterpolateVars(m.Groups[2].Value.Trim());
                    try
                    {
                        string[] files = Directory.GetFileSystemEntries(dirPath);
                        variables[varN] = string.Join(",", files.Select(f => Path.GetFileName(f)));
                    }
                    catch (Exception ex) { variables["ERROR"] = ex.Message; variables[varN] = ""; }
                }
                return true;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// Generate an example .kocursh script.
        /// </summary>
        public static string GetExampleScript()
        {
            return @"# ========================================
# Example KocurSh Script (.kocursh)
# KocurConsole's own scripting language!
# ========================================

# --- Variables & Interpolation ---
$name = KocurConsole
$version = 1.0.3

print === Welcome to $name v$version! ===

# C# expression for dynamic values
$hour = @(DateTime.Now.Hour)
print Current hour: $hour

# --- Colored output ---
cprint cyan === Terminal Integrations ===
cprint green This is green text!
cprint yellow Warning-style message
cprint red Something went wrong (not really)

# --- Conditional logic ---
if $hour < 12
    cprint yellow Good morning! ☀️  
else
    cprint blue Good evening! 🌙
end

# --- For loop with TUI progress bar (updates in place!) ---
cprint accent Counting with progress bar:
for $i in 1..10
    progress! $i 10 Loading
    sleep 150
end
cprint green Done!

# --- For loop with list ---
print My drives:
for $drive in C,D,E
    print   Drive $drive:\
end

# --- While loop ---
$counter = 0
while $counter < 3
    $counter = @(int.Parse($counter) + 1)
    print   While iteration: $counter
end

# --- String operations ---
$text = Hello World
$upper = upper($text)
$lower = lower($text)
$length = len($text)
print Original: $text
print Upper: $upper
print Lower: $lower
print Length: $length

$replaced = replace($text, World, KocurSh)
print Replaced: $replaced

$has = contains($text, World)
print Contains 'World': $has

# --- File operations ---
fwrite test_kocursh.txt Hello from KocurSh script!
fappend test_kocursh.txt Second line added by script
fexists $exists test_kocursh.txt
print File exists: $exists
fread $content test_kocursh.txt
print File content: $content
fdelete test_kocursh.txt

# --- Try/catch error handling ---
try
    print This will work fine
    $result = @(42 / 1)
    print Result: $result
catch
    print Error caught: $ERROR
end

# --- Functions ---
func greet($who)
    cprint cyan Hello, $who! Welcome to KocurSh!
    return greeting_sent
end

call greet(World)
call greet($name)
print Function result: $RESULT

# --- C# code block ---
@{
    print(""--- C# Power ---"");
    var rng = new Random();
    for (int i = 0; i < 3; i++)
    {
        print(""  Random #"" + (i+1) + "": "" + rng.Next(1,100));
    }
    print(""Pi = "" + Math.PI.ToString(""F6""));
}

# --- Built-in variables ---
print
cprint info Built-in variables:
print   USER:   $USER
print   HOME:   $HOME
print   DATE:   $DATE
print   TIME:   $TIME
print   SCRIPT: $SCRIPT

beep
cprint green === Script completed! ===
";
        }
    }
}
