using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;

namespace KocurConsole
{
    public static class PluginManager
    {
        private static Dictionary<string, MethodInfo> loadedPlugins = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        private static string pluginDir;

        public static void Initialize()
        {
            pluginDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KocurConsole", "plugins");
            if (!Directory.Exists(pluginDir))
                Directory.CreateDirectory(pluginDir);
        }

        public static string PluginDirectory => pluginDir;

        /// <summary>
        /// Load all .cs plugins from the plugins directory.
        /// Each plugin must have a public static string Execute(string[] args) method.
        /// The filename (without .cs) becomes the command name.
        /// </summary>
        public static int LoadAll()
        {
            loadedPlugins.Clear();
            if (!Directory.Exists(pluginDir)) return 0;

            int count = 0;
            foreach (string file in Directory.GetFiles(pluginDir, "*.cs"))
            {
                try
                {
                    string code = File.ReadAllText(file);
                    string cmdName = Path.GetFileNameWithoutExtension(file).ToLower();

                    CSharpCodeProvider provider = new CSharpCodeProvider();
                    CompilerParameters parameters = new CompilerParameters
                    {
                        GenerateInMemory = true,
                        GenerateExecutable = false
                    };
                    parameters.ReferencedAssemblies.Add("System.dll");
                    parameters.ReferencedAssemblies.Add("System.Core.dll");
                    parameters.ReferencedAssemblies.Add("System.Linq.dll");
                    parameters.ReferencedAssemblies.Add("System.IO.dll");

                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);

                    if (results.Errors.HasErrors)
                        continue;

                    // Find the Execute method
                    foreach (Type type in results.CompiledAssembly.GetExportedTypes())
                    {
                        MethodInfo method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                        if (method != null && method.ReturnType == typeof(string))
                        {
                            loadedPlugins[cmdName] = method;
                            count++;
                            break;
                        }
                    }
                }
                catch { }
            }
            return count;
        }

        public static bool HasPlugin(string name) => loadedPlugins.ContainsKey(name);

        public static string ExecutePlugin(string name, string[] args)
        {
            if (!loadedPlugins.ContainsKey(name)) return null;
            try
            {
                return (string)loadedPlugins[name].Invoke(null, new object[] { args });
            }
            catch (Exception ex)
            {
                return "Plugin error: " + ex.InnerException?.Message ?? ex.Message;
            }
        }

        public static string[] GetPluginNames() => loadedPlugins.Keys.ToArray();

        /// <summary>
        /// Create an example plugin file.
        /// </summary>
        public static void CreateExample()
        {
            string example = @"using System;
using System.Linq;

// Plugin: hello
// Place this file in %APPDATA%/KocurConsole/plugins/
// Command name = filename without .cs
// Must have: public static string Execute(string[] args)

public class HelloPlugin
{
    public static string Execute(string[] args)
    {
        if (args.Length > 0)
            return ""Hello, "" + string.Join("" "", args) + ""!"";
        return ""Hello from KocurConsole plugin!"";
    }
}";
            string path = Path.Combine(pluginDir, "hello.cs");
            if (!File.Exists(path))
                File.WriteAllText(path, example);
        }
    }
}
