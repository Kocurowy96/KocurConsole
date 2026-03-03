using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Security.Cryptography;

namespace KocurConsole
{
    public partial class Form1 : Form
    {
        public string terminalName = "KocurConsole Terminal";
        public string terminalVersion = "1.0.3";

        // Command history
        private List<string> commandHistory = new List<string>();
        private int historyIndex = -1;

        // Current & previous working directory
        private string currentDirectory;
        private string previousDirectory;

        // CMD/PowerShell handler
        private CommandHandler cmdHandler;

        // Terminal state
        private volatile bool cmdRunning = false;
        private volatile bool cancelRequested = false;
        private int inputStartPosition = 0;

        // Aliases (name -> command)
        private Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Bookmarks (name -> path)
        private Dictionary<string, string> bookmarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Session logging
        private StreamWriter logWriter = null;
        private string logFilePath = null;

        // Stopwatch
        private Stopwatch activeStopwatch = null;

        // Pinned commands (F1-F12)
        private Dictionary<Keys, string> pinnedCommands = new Dictionary<Keys, string>();

        // System tray
        private NotifyIcon trayIcon;

        // Advanced calc variables
        private Dictionary<string, double> calcVars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Suppress prompts during .kocurrc execution
        private bool suppressPrompt = false;

        // All built-in command names (for autocomplete)
        private readonly string[] builtInCommands = new string[]
        {
            "help", "clear", "cls", "fastfetch", "neofetch", "systeminfo", "whoami", "date",
            "echo", "exit", "quit", "ls", "dir", "cd", "pwd", "mkdir", "rmdir",
            "rm", "del", "cp", "copy", "mv", "move", "cat", "type", "touch",
            "hostname", "ping", "history", "theme", "settings", "title", "color",
            "uptime", "env", "calc", "tree",
            "find", "grep", "head", "tail", "wc", "size", "md5", "sha256",
            "ip", "dns", "wget",
            "ps", "tasklist", "kill",
            "open", "start", "base64", "random", "about", "clipboard",
            "checkupdate", "update",
            "bookmark", "alias", "stopwatch", "timer", "log",
            "hash", "curl", "df", "write",
            "preview", "plugin", "plugins", "pin", "unpin", "ssh", "rc"
        };

        // P/Invoke for dark scrollbar & title bar
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            previousDirectory = currentDirectory;

            // Initialize CMD handler
            cmdHandler = new CommandHandler();
            cmdHandler.OutputReceived += CmdHandler_OutputReceived;
            cmdHandler.ProcessCompleted += CmdHandler_ProcessCompleted;

            // Load saved settings and apply theme
            AppSettings s = SettingsManager.Current;
            ThemeManager.SetTheme(s.Theme);

            // === Integrated terminal setup ===
            // Hide the separate input textbox — RichTextBox becomes the terminal
            inputCommands.Visible = false;

            // Make RichTextBox editable (we control what can be edited)
            richTextBoxConsoleOutput.ReadOnly = false;
            richTextBoxConsoleOutput.Dock = DockStyle.Fill;
            richTextBoxConsoleOutput.AcceptsTab = false;

            // Wire keyboard events on the RichTextBox
            richTextBoxConsoleOutput.KeyDown += RichTextBox_KeyDown;
            richTextBoxConsoleOutput.KeyPress += RichTextBox_KeyPress;
            richTextBoxConsoleOutput.PreviewKeyDown += RichTextBox_PreviewKeyDown;

            ApplyTheme();
            ApplyFontSettings();
            ApplyDarkMode();

            richTextBoxConsoleOutput.WordWrap = s.WordWrap;

            this.Text = terminalName + " " + terminalVersion;

            // ASCII art welcome
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n", t.TextColor);
            AppendConsoleText("   /\\_/\\   ", t.AccentColor); AppendConsoleText("KocurConsole v" + terminalVersion + "\n", t.InfoColor);
            AppendConsoleText("  ( o.o )  ", t.AccentColor); AppendConsoleText("Modern terminal for Windows\n", t.TextColor);
            AppendConsoleText("   > ^ <   ", t.AccentColor); AppendConsoleText("Type 'help' for commands\n", t.TextColor);
            AppendConsoleText("  /|   |\\  ", t.AccentColor); AppendConsoleText("Tab = autocomplete + paths\n", t.TextColor);
            AppendConsoleText(" (_|   |_) ", t.AccentColor); AppendConsoleText("F1-F12 = pinned commands\n\n", t.TextColor);
            richTextBoxConsoleOutput.Focus();

            // Load aliases, bookmarks, pinned commands
            LoadAliasesAndBookmarks();
            LoadPinnedCommands();

            // Initialize plugin system
            PluginManager.Initialize();
            int pluginCount = PluginManager.LoadAll();

            // System tray
            trayIcon = new NotifyIcon();
            trayIcon.Text = "KocurConsole";
            trayIcon.Icon = this.Icon;
            trayIcon.Visible = false;
            trayIcon.DoubleClick += (s2, e2) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;
            };
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("Show", null, (s2, e2) => { this.Show(); this.WindowState = FormWindowState.Normal; trayIcon.Visible = false; });
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s2, e2) => { Application.Exit(); });
            this.Resize += (s2, e2) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                    trayIcon.Visible = true;
                }
            };

            // Run .kocurrc on startup (before showing prompt)
            RunKocurrc();

            // Show the interactive prompt
            ShowPrompt();

            // Background update check
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var manifest = UpdateHandler.CheckForUpdate();
                    if (manifest != null && UpdateHandler.IsNewerVersion(terminalVersion, manifest.Version))
                    {
                        AppendConsoleText("\n  [!] New version v" + manifest.Version + " available! Type 'update' to install.\n\n", ThemeManager.Current.WarningColor);
                    }
                }
                catch { }
            });
        }

        #region Theme & Settings Application

        private void ApplyTheme()
        {
            ConsoleTheme theme = ThemeManager.Current;

            richTextBoxConsoleOutput.BackColor = theme.BackgroundColor;
            richTextBoxConsoleOutput.ForeColor = theme.TextColor;

            inputCommands.BackColor = theme.BackgroundColor;
            inputCommands.ForeColor = theme.TextColor;

            this.BackColor = theme.BackgroundColor;
        }

        private void ApplyFontSettings()
        {
            AppSettings s = SettingsManager.Current;
            try
            {
                Font f = new Font(s.FontFamily, s.FontSize);
                richTextBoxConsoleOutput.Font = f;
                inputCommands.Font = f;
            }
            catch { }
        }

        private void ApplyDarkMode()
        {
            try
            {
                // Dark title bar
                int value = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));

                // Dark scrollbar on the RichTextBox
                SetWindowTheme(richTextBoxConsoleOutput.Handle, "DarkMode_Explorer", null);
            }
            catch { }
        }

        #endregion

        #region CMD/PowerShell Handler Events

        private void CmdHandler_OutputReceived(string text, bool isError)
        {
            Color color = isError ? ThemeManager.Current.ErrorColor : ThemeManager.Current.TextColor;
            AppendConsoleText(text, color);
        }

        private void CmdHandler_ProcessCompleted(int exitCode)
        {
            cmdRunning = false;
            if (exitCode != 0)
            {
                AppendConsoleText("[Exit code: " + exitCode + "]\n", ThemeManager.Current.WarningColor);
            }
            ShowPrompt();
        }

        #endregion

        #region Console I/O

        private void richTextBoxConsoleOutput_TextChanged(object sender, EventArgs e)
        {
            // Auto-scroll is handled in AppendConsoleText, not here
            // Doing it here causes screen glitching because it fires on every user keystroke
        }

        private void AppendConsoleText(string text, Color color)
        {
            if (richTextBoxConsoleOutput.InvokeRequired)
            {
                richTextBoxConsoleOutput.Invoke(new Action(() => AppendConsoleText(text, color)));
                return;
            }

            richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
            richTextBoxConsoleOutput.SelectionLength = 0;
            richTextBoxConsoleOutput.SelectionColor = color;
            richTextBoxConsoleOutput.AppendText(text);
            richTextBoxConsoleOutput.SelectionColor = richTextBoxConsoleOutput.ForeColor;

            // Auto-scroll only on programmatic output (not user typing)
            if (SettingsManager.Current.AutoScroll)
            {
                richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                richTextBoxConsoleOutput.ScrollToCaret();
            }
        }

        private string GetPromptString()
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string user = Environment.UserName;
            return "[" + time + "] " + user + " (" + currentDirectory + ") >> ";
        }

        private void ShowPrompt()
        {
            if (suppressPrompt) return;

            if (richTextBoxConsoleOutput.InvokeRequired)
            {
                richTextBoxConsoleOutput.Invoke(new Action(() => ShowPrompt()));
                return;
            }

            string prompt = GetPromptString();
            AppendConsoleText(prompt, ThemeManager.Current.PromptColor);
            inputStartPosition = richTextBoxConsoleOutput.TextLength;

            // Set color for user input
            richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
            richTextBoxConsoleOutput.SelectionLength = 0;
            richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
        }

        private string GetCurrentInput()
        {
            if (richTextBoxConsoleOutput.InvokeRequired)
            {
                return (string)richTextBoxConsoleOutput.Invoke(new Func<string>(() => GetCurrentInput()));
            }

            if (richTextBoxConsoleOutput.TextLength > inputStartPosition)
            {
                return richTextBoxConsoleOutput.Text.Substring(inputStartPosition);
            }
            return "";
        }

        private void SetCurrentInput(string text)
        {
            richTextBoxConsoleOutput.SelectionStart = inputStartPosition;
            richTextBoxConsoleOutput.SelectionLength = richTextBoxConsoleOutput.TextLength - inputStartPosition;
            richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
            richTextBoxConsoleOutput.SelectedText = text;
            richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
            richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
        }

        #endregion

        #region Input Handling (RichTextBox)

        private void RichTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // === Ctrl+C: cancel process / copy / clear input ===
            if (e.KeyCode == Keys.C && e.Control)
            {
                if (cmdRunning)
                {
                    cancelRequested = true;
                    cmdHandler.Cancel();
                    AppendConsoleText("^C\n", ThemeManager.Current.WarningColor);
                    cmdRunning = false;
                    ShowPrompt();
                }
                else if (richTextBoxConsoleOutput.SelectionLength > 0)
                {
                    // Copy selected text
                    Clipboard.SetText(richTextBoxConsoleOutput.SelectedText);
                }
                else
                {
                    // Clear current input line
                    AppendConsoleText("^C\n", ThemeManager.Current.WarningColor);
                    ShowPrompt();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // === Block all input while command is running ===
            if (cmdRunning)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // === Ctrl+V: paste at input area ===
            if (e.KeyCode == Keys.V && e.Control)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                try
                {
                    string clipText = Clipboard.GetText()
                        .Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
                    if (!string.IsNullOrEmpty(clipText))
                    {
                        EnsureCaretInInputArea();
                        richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
                        richTextBoxConsoleOutput.SelectedText = clipText;
                    }
                }
                catch { }
                return;
            }

            // === Ctrl+A: select all ===
            if (e.KeyCode == Keys.A && e.Control)
            {
                richTextBoxConsoleOutput.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // === Ctrl+L: clear screen ===
            if (e.KeyCode == Keys.L && e.Control)
            {
                richTextBoxConsoleOutput.Clear();
                ShowPrompt();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // === Enter: execute command ===
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                string command = GetCurrentInput().Trim();

                if (!string.IsNullOrEmpty(command))
                {
                    commandHistory.Add(command);
                    historyIndex = commandHistory.Count;
                }

                // Move caret to end and add newline
                richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                AppendConsoleText("\n", ThemeManager.Current.TextColor);

                ExecuteCommand(command);
                return;
            }

            // === Tab: autocomplete ===
            if (e.KeyCode == Keys.Tab)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                AutoCompleteCommand();
                return;
            }

            // === Up arrow: history previous ===
            if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (commandHistory.Count > 0)
                {
                    historyIndex--;
                    if (historyIndex < 0) historyIndex = commandHistory.Count - 1;
                    SetCurrentInput(commandHistory[historyIndex]);
                }
                return;
            }

            // === Down arrow: history next ===
            if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (commandHistory.Count > 0)
                {
                    historyIndex++;
                    if (historyIndex >= commandHistory.Count)
                    {
                        historyIndex = commandHistory.Count;
                        SetCurrentInput("");
                    }
                    else
                    {
                        SetCurrentInput(commandHistory[historyIndex]);
                    }
                }
                return;
            }

            // === Escape: clear current input ===
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SetCurrentInput("");
                return;
            }

            // === Home: go to input start ===
            if (e.KeyCode == Keys.Home)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                richTextBoxConsoleOutput.SelectionStart = inputStartPosition;
                richTextBoxConsoleOutput.SelectionLength = 0;
                return;
            }

            // === Backspace: block if at input start ===
            if (e.KeyCode == Keys.Back)
            {
                if (richTextBoxConsoleOutput.SelectionStart <= inputStartPosition &&
                    richTextBoxConsoleOutput.SelectionLength == 0)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (richTextBoxConsoleOutput.SelectionStart < inputStartPosition)
                {
                    // Selection spans into output area — block
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                return;
            }

            // === Delete: block if before input area ===
            if (e.KeyCode == Keys.Delete)
            {
                if (richTextBoxConsoleOutput.SelectionStart < inputStartPosition)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                return;
            }

            // === Left arrow: don't go before input start ===
            if (e.KeyCode == Keys.Left)
            {
                if (richTextBoxConsoleOutput.SelectionStart <= inputStartPosition &&
                    richTextBoxConsoleOutput.SelectionLength == 0)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                return;
            }

            // === F1-F12: pinned commands ===
            if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12)
            {
                if (pinnedCommands.ContainsKey(e.KeyCode))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    string cmd = pinnedCommands[e.KeyCode];
                    AppendConsoleText(cmd + "\n", ThemeManager.Current.TextColor);
                    commandHistory.Add(cmd);
                    historyIndex = commandHistory.Count;
                    ExecuteCommand(cmd);
                }
                return;
            }
        }

        private void RichTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Block all input while command is running
            if (cmdRunning)
            {
                e.Handled = true;
                return;
            }

            // If caret is before input area, move to end
            if (richTextBoxConsoleOutput.SelectionStart < inputStartPosition)
            {
                if (richTextBoxConsoleOutput.SelectionLength > 0)
                {
                    // Selection spans output area — move to end
                    richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                    richTextBoxConsoleOutput.SelectionLength = 0;
                }
                else
                {
                    richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                    richTextBoxConsoleOutput.SelectionLength = 0;
                }
            }

            // Set text color for user input
            richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
        }

        private void EnsureCaretInInputArea()
        {
            if (richTextBoxConsoleOutput.SelectionStart < inputStartPosition)
            {
                richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                richTextBoxConsoleOutput.SelectionLength = 0;
            }
        }

        private void AutoCompleteCommand()
        {
            string input = GetCurrentInput().TrimStart();
            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return;

            // === Sub-command completion (second word) ===
            if (parts.Length >= 2)
            {
                string cmd = parts[0].ToLower();
                string partial = parts[parts.Length - 1].ToLower();
                string[] subCommands = null;

                switch (cmd)
                {
                    case "settings":
                        subCommands = new[] { "set", "reset", "gui", "theme", "font", "fontSize", "wordWrap", "timestamps", "autoScroll", "shell", "timeout" };
                        break;
                    case "theme":
                        subCommands = ThemeManager.GetThemeNames().Concat(new[] { "list" }).ToArray();
                        break;
                    case "history":
                        subCommands = new[] { "clear" };
                        break;
                    case "base64":
                        subCommands = new[] { "encode", "decode" };
                        break;
                    case "bookmark":
                        subCommands = new[] { "add", "go", "remove", "list" };
                        break;
                    case "alias":
                        subCommands = new[] { "remove" };
                        break;
                    case "stopwatch":
                        subCommands = new[] { "start", "stop", "lap" };
                        break;
                    case "log":
                        subCommands = new[] { "start", "stop" };
                        break;
                    case "hash":
                        subCommands = new[] { "md5", "sha256" };
                        break;
                    case "plugin":
                    case "plugins":
                        subCommands = new[] { "list", "reload", "create", "dir" };
                        break;
                    case "rc":
                        subCommands = new[] { "edit", "run" };
                        break;
                    case "calc":
                        subCommands = new[] { "vars" };
                        break;
                }

                if (subCommands != null)
                {
                    var subMatches = subCommands.Where(s => s.StartsWith(partial, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (subMatches.Count == 1)
                    {
                        // Replace last word with the match
                        string prefix = string.Join(" ", parts.Take(parts.Length - 1));
                        SetCurrentInput(prefix + " " + subMatches[0] + " ");
                    }
                    else if (subMatches.Count > 1)
                    {
                        string savedInput = GetCurrentInput();
                        AppendConsoleText("\n  " + string.Join("  ", subMatches) + "\n", ThemeManager.Current.InfoColor);
                        ShowPrompt();
                        richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
                        richTextBoxConsoleOutput.AppendText(savedInput);
                        richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                    }
                }
                else
                {
                    // File path completion for commands that take file arguments
                    string[] fileCommands = { "cd", "cat", "type", "head", "tail", "grep", "md5", "sha256", "find", "cp", "copy", "mv", "move", "rm", "del", "touch", "size", "wc", "tree", "open", "write", "log" };
                    if (fileCommands.Contains(cmd))
                    {
                        AutoCompleteFilePath(parts);
                    }
                }
                return;
            }

            // === Command completion (first word) ===
            string partialCmd = parts[0].ToLower();

            var matches = builtInCommands.Where(c => c.StartsWith(partialCmd)).ToList();
            if (matches.Count == 1)
            {
                SetCurrentInput(matches[0] + " ");
            }
            else if (matches.Count > 1)
            {
                string savedInput = GetCurrentInput();
                AppendConsoleText("\n  " + string.Join("  ", matches) + "\n", ThemeManager.Current.InfoColor);
                ShowPrompt();
                richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
                richTextBoxConsoleOutput.AppendText(savedInput);
                richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
            }
        }

        private void AutoCompleteFilePath(string[] parts)
        {
            string partial = parts[parts.Length - 1];
            string dir;
            string prefix;

            try
            {
                string fullPartial = Path.IsPathRooted(partial) ? partial : Path.Combine(currentDirectory, partial);
                if (Directory.Exists(fullPartial))
                {
                    dir = fullPartial;
                    prefix = "";
                }
                else
                {
                    dir = Path.GetDirectoryName(fullPartial) ?? currentDirectory;
                    prefix = Path.GetFileName(fullPartial);
                }

                if (!Directory.Exists(dir)) return;

                var entries = Directory.GetFileSystemEntries(dir)
                    .Select(e => Path.GetFileName(e))
                    .Where(e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();

                if (entries.Count == 1)
                {
                    string match = entries[0];
                    string baseParts = string.Join(" ", parts.Take(parts.Length - 1));
                    string dirPart = Path.IsPathRooted(partial) ? dir : "";

                    string newPath;
                    if (string.IsNullOrEmpty(prefix))
                        newPath = partial.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar + match;
                    else if (!string.IsNullOrEmpty(dirPart))
                        newPath = Path.Combine(dir, match);
                    else
                    {
                        // Relative path — reconstruct
                        string relDir = partial.Contains(Path.DirectorySeparatorChar.ToString()) ? partial.Substring(0, partial.LastIndexOf(Path.DirectorySeparatorChar) + 1) : "";
                        newPath = relDir + match;
                    }

                    string fullPath = Path.Combine(currentDirectory, newPath);
                    if (Directory.Exists(fullPath) && !newPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        newPath += Path.DirectorySeparatorChar;

                    SetCurrentInput(baseParts + " " + newPath);
                }
                else if (entries.Count > 1)
                {
                    string savedInput = GetCurrentInput();
                    AppendConsoleText("\n  " + string.Join("  ", entries) + "\n", ThemeManager.Current.InfoColor);
                    ShowPrompt();
                    richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
                    richTextBoxConsoleOutput.AppendText(savedInput);
                    richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
                }
            }
            catch { }
        }

        // Required by Designer.cs — inputCommands is hidden so this never fires
        private void inputCommands_KeyDown(object sender, KeyEventArgs e) { }

        // Marks Tab as an input key so it reaches KeyDown instead of switching focus
        private void RichTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Tab || (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12))
            {
                e.IsInputKey = true;
            }
        }

        #endregion

        #region Async Runner

        private void RunAsync(Action action)
        {
            cmdRunning = true;
            cancelRequested = false;
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    if (!cancelRequested)
                        AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor);
                }
                finally
                {
                    cmdRunning = false;
                    ShowPrompt();
                }
            });
        }

        #endregion

        #region Command Execution

        private void ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                ShowPrompt();
                return;
            }

            // Log to session file if active
            if (logWriter != null)
            {
                try { logWriter.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] > " + command); logWriter.Flush(); }
                catch { }
            }

            // Command chaining: split on &&
            if (command.Contains("&&"))
            {
                string[] chained = command.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string subCmd in chained)
                {
                    ExecuteCommand(subCmd.Trim());
                }
                return;
            }

            // Pipe support: cmd1 | cmd2
            if (command.Contains("|") && !command.Contains("||"))
            {
                // For pipes, we run the whole thing via cmd.exe which handles pipes natively
                RunExternalCommand(command);
                return;
            }

            // Output redirection: >> (append) or > (overwrite)
            string redirectFile = null;
            bool redirectAppend = false;
            if (command.Contains(">>"))
            {
                int idx = command.LastIndexOf(">>");
                redirectFile = command.Substring(idx + 2).Trim();
                command = command.Substring(0, idx).Trim();
                redirectAppend = true;
            }
            else if (command.Contains(">"))
            {
                int idx = command.LastIndexOf(">");
                redirectFile = command.Substring(idx + 1).Trim();
                command = command.Substring(0, idx).Trim();
                redirectAppend = false;
            }

            // Alias expansion
            string firstWord = command.Split(' ')[0].ToLower();
            if (aliases.ContainsKey(firstWord))
            {
                command = aliases[firstWord] + command.Substring(firstWord.Length);
            }

            string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            // Capture text position for output redirection
            int redirectStartLen = richTextBoxConsoleOutput.TextLength;

            switch (cmd)
            {
                // ── General ──
                case "help":
                    ShowHelp();
                    break;
                case "clear":
                case "cls":
                    richTextBoxConsoleOutput.Clear();
                    break;
                case "exit":
                case "quit":
                    this.Close();
                    return;

                // ── System Info (async — WMI is slow) ──
                case "fastfetch":
                    RunAsync(() => ShowFastFetch());
                    return;
                case "systeminfo":
                    RunAsync(() => ShowSystemInfo());
                    return;

                // ── Simple info ──
                case "whoami":
                    AppendConsoleText(Environment.UserDomainName + "\\" + Environment.UserName + "\n", ThemeManager.Current.TextColor);
                    break;
                case "hostname":
                    AppendConsoleText(Environment.MachineName + "\n", ThemeManager.Current.TextColor);
                    break;
                case "date":
                    AppendConsoleText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss (dddd)") + "\n", ThemeManager.Current.TextColor);
                    break;
                case "uptime":
                    RunAsync(() =>
                    {
                        AppendConsoleText(GetUptime() + "\n", ThemeManager.Current.TextColor);
                    });
                    return;
                case "echo":
                    AppendConsoleText(string.Join(" ", args) + "\n", ThemeManager.Current.TextColor);
                    break;

                // ── File System ──
                case "ls":
                case "dir":
                    RunAsync(() => CmdListDir(args));
                    return;
                case "cd":
                    CmdChangeDir(args);
                    break;
                case "pwd":
                    AppendConsoleText(currentDirectory + "\n", ThemeManager.Current.TextColor);
                    break;
                case "mkdir":
                    CmdMkdir(args);
                    break;
                case "rmdir":
                    CmdRmdir(args);
                    break;
                case "rm":
                case "del":
                    CmdDelete(args);
                    break;
                case "cp":
                case "copy":
                    CmdCopy(args);
                    break;
                case "mv":
                case "move":
                    CmdMove(args);
                    break;
                case "cat":
                case "type":
                    RunAsync(() => CmdCat(args));
                    return;
                case "touch":
                    CmdTouch(args);
                    break;
                case "tree":
                    RunAsync(() => CmdTree(args));
                    return;

                // ── Network ──
                case "ping":
                    RunAsync(() => CmdPing(args));
                    return;

                // ── Utility ──
                case "history":
                    CmdHistory(args);
                    break;
                case "env":
                    CmdEnv(args);
                    break;
                case "calc":
                    CmdCalc(args);
                    break;
                case "title":
                    if (args.Length > 0)
                    {
                        if (this.InvokeRequired)
                            this.Invoke(new Action(() => this.Text = string.Join(" ", args)));
                        else
                            this.Text = string.Join(" ", args);
                        AppendConsoleText("Title changed.\n", ThemeManager.Current.InfoColor);
                    }
                    else
                    {
                        AppendConsoleText("Usage: title <text>\n", ThemeManager.Current.WarningColor);
                    }
                    break;

                // ── Theme & Settings ──
                case "theme":
                    CmdTheme(args);
                    break;
                case "settings":
                    CmdSettings(args);
                    break;
                case "color":
                    AppendConsoleText("Current theme: " + ThemeManager.Current.Name + "\n", ThemeManager.Current.InfoColor);
                    break;

                // ── File Search & Analysis ──
                case "find":
                    RunAsync(() => CmdFind(args));
                    return;
                case "grep":
                    RunAsync(() => CmdGrep(args));
                    return;
                case "head":
                    CmdHead(args);
                    break;
                case "tail":
                    CmdTail(args);
                    break;
                case "wc":
                    CmdWc(args);
                    break;
                case "size":
                    RunAsync(() => CmdSize(args));
                    return;
                case "md5":
                    RunAsync(() => CmdMd5(args));
                    return;
                case "sha256":
                    RunAsync(() => CmdSha256(args));
                    return;

                // ── Network (extended) ──
                case "ip":
                    CmdIp();
                    break;
                case "dns":
                    RunAsync(() => CmdDns(args));
                    return;
                case "wget":
                    RunAsync(() => CmdWget(args));
                    return;

                // ── Processes ──
                case "ps":
                case "tasklist":
                    RunAsync(() => CmdPs());
                    return;
                case "kill":
                    CmdKill(args);
                    break;

                // ── Extended Utility ──
                case "open":
                    CmdOpen(args);
                    break;
                case "start":
                    CmdStart(args);
                    break;
                case "base64":
                    CmdBase64(args);
                    break;
                case "random":
                    CmdRandom(args);
                    break;
                case "about":
                    CmdAbout();
                    break;
                case "neofetch":
                    RunAsync(() => ShowFastFetch());
                    return;
                case "clipboard":
                    CmdClipboard();
                    break;

                // ── Update ──
                case "checkupdate":
                    RunAsync(() => CmdCheckUpdate());
                    return;
                case "update":
                    RunAsync(() => CmdUpdate());
                    return;

                // ── v1.0.2 Features ──
                case "bookmark":
                    CmdBookmark(args);
                    break;
                case "alias":
                    CmdAlias(args);
                    break;
                case "stopwatch":
                    CmdStopwatch(args);
                    break;
                case "timer":
                    if (args.Length > 0)
                        RunAsync(() => CmdTimer(args));
                    else
                        AppendConsoleText("Usage: timer <seconds>\n", ThemeManager.Current.WarningColor);
                    return;
                case "log":
                    CmdLog(args);
                    break;
                case "hash":
                    CmdHash(args);
                    break;
                case "curl":
                    RunAsync(() => CmdCurl(args));
                    return;
                case "df":
                    CmdDf();
                    break;
                case "write":
                    CmdWrite(args);
                    break;

                // ── v1.0.3 Features ──
                case "preview":
                    RunAsync(() => CmdPreview(args));
                    return;
                case "plugin":
                case "plugins":
                    CmdPlugin(args);
                    break;
                case "pin":
                    CmdPin(args);
                    break;
                case "unpin":
                    CmdUnpin(args);
                    break;
                case "ssh":
                    RunExternalCommand("ssh " + string.Join(" ", args));
                    return;
                case "rc":
                    CmdRc(args);
                    break;

                // ── CMD/PowerShell Fallback ──
                default:
                    // Check if it's a plugin command
                    if (PluginManager.HasPlugin(cmd))
                    {
                        string result = PluginManager.ExecutePlugin(cmd, args);
                        if (!string.IsNullOrEmpty(result))
                            AppendConsoleText(result + "\n", ThemeManager.Current.TextColor);
                        break;
                    }
                    RunExternalCommand(command);
                    return;
            }

            // Handle output redirection (for commands that break, not return)
            if (redirectFile != null)
            {
                try
                {
                    int endLen = richTextBoxConsoleOutput.TextLength;
                    if (endLen > redirectStartLen)
                    {
                        string output = "";
                        if (richTextBoxConsoleOutput.InvokeRequired)
                            richTextBoxConsoleOutput.Invoke(new Action(() => output = richTextBoxConsoleOutput.Text.Substring(redirectStartLen)));
                        else
                            output = richTextBoxConsoleOutput.Text.Substring(redirectStartLen);

                        string rPath = ResolvePath(redirectFile);
                        if (redirectAppend)
                            File.AppendAllText(rPath, output, Encoding.UTF8);
                        else
                            File.WriteAllText(rPath, output, Encoding.UTF8);
                        AppendConsoleText("  -> " + rPath + "\n", ThemeManager.Current.InfoColor);
                    }
                }
                catch (Exception ex)
                {
                    AppendConsoleText("Redirect error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor);
                }
            }

            ShowPrompt();
        }

        #endregion

        #region Help

        private void ShowHelp()
        {
            ConsoleTheme t = ThemeManager.Current;

            AppendConsoleText("\n=== KocurConsole " + terminalVersion + " ===\n\n", t.WarningColor);

            AppendConsoleText(" General:\n", t.AccentColor);
            AppendConsoleText("  help              Show this help message\n", t.TextColor);
            AppendConsoleText("  clear / cls       Clear the console\n", t.TextColor);
            AppendConsoleText("  echo <text>       Echo text\n", t.TextColor);
            AppendConsoleText("  title <text>      Change window title\n", t.TextColor);
            AppendConsoleText("  history           Show command history\n", t.TextColor);
            AppendConsoleText("  about             About KocurConsole\n", t.TextColor);
            AppendConsoleText("  exit / quit       Close the application\n\n", t.TextColor);

            AppendConsoleText(" System:\n", t.AccentColor);
            AppendConsoleText("  fastfetch         System overview\n", t.TextColor);
            AppendConsoleText("  systeminfo        Detailed system info\n", t.TextColor);
            AppendConsoleText("  whoami            Current user\n", t.TextColor);
            AppendConsoleText("  hostname          Computer name\n", t.TextColor);
            AppendConsoleText("  date              Date & time\n", t.TextColor);
            AppendConsoleText("  uptime            System uptime\n", t.TextColor);
            AppendConsoleText("  ps / tasklist     List processes (top 30)\n", t.TextColor);
            AppendConsoleText("  kill <pid>        Kill a process\n\n", t.TextColor);

            AppendConsoleText(" File System:\n", t.AccentColor);
            AppendConsoleText("  ls / dir          List directory\n", t.TextColor);
            AppendConsoleText("  cd <path>         Change directory (cd ~ / cd - / cd ..)\n", t.TextColor);
            AppendConsoleText("  pwd               Working directory\n", t.TextColor);
            AppendConsoleText("  mkdir <name>      Create directory\n", t.TextColor);
            AppendConsoleText("  rmdir <name>      Remove directory\n", t.TextColor);
            AppendConsoleText("  rm / del <file>   Delete file\n", t.TextColor);
            AppendConsoleText("  cp / copy <s> <d> Copy file\n", t.TextColor);
            AppendConsoleText("  mv / move <s> <d> Move / rename file\n", t.TextColor);
            AppendConsoleText("  cat / type <file> Show file contents\n", t.TextColor);
            AppendConsoleText("  touch <file>      Create empty file\n", t.TextColor);
            AppendConsoleText("  tree              Directory tree\n", t.TextColor);
            AppendConsoleText("  find <pattern>    Search files by name\n", t.TextColor);
            AppendConsoleText("  grep <pat> <file> Search text in file\n", t.TextColor);
            AppendConsoleText("  head <file> [n]   First N lines (def 10)\n", t.TextColor);
            AppendConsoleText("  tail <file> [n]   Last N lines (def 10)\n", t.TextColor);
            AppendConsoleText("  wc <file>         Word/line/char count\n", t.TextColor);
            AppendConsoleText("  size <path>       File/directory size\n", t.TextColor);
            AppendConsoleText("  md5 <file>        MD5 hash\n", t.TextColor);
            AppendConsoleText("  sha256 <file>     SHA256 hash\n\n", t.TextColor);

            AppendConsoleText(" Network:\n", t.AccentColor);
            AppendConsoleText("  ping <host>       Ping host (4 packets)\n", t.TextColor);
            AppendConsoleText("  ip                Show IP addresses\n", t.TextColor);
            AppendConsoleText("  dns <host>        DNS lookup\n", t.TextColor);
            AppendConsoleText("  wget <url> [file] Download file\n\n", t.TextColor);

            AppendConsoleText(" Utility:\n", t.AccentColor);
            AppendConsoleText("  env [name]        Environment variables\n", t.TextColor);
            AppendConsoleText("  calc <expr>       Calculator (sin,cos,sqrt,pow,vars)\n", t.TextColor);
            AppendConsoleText("  base64 <enc|dec>  Base64 encode/decode\n", t.TextColor);
            AppendConsoleText("  random [min] [max] Random number\n", t.TextColor);
            AppendConsoleText("  clipboard         Show clipboard text\n", t.TextColor);
            AppendConsoleText("  open [path]       Open in Explorer\n", t.TextColor);
            AppendConsoleText("  start <program>   Launch program\n\n", t.TextColor);

            AppendConsoleText(" Themes & Settings:\n", t.AccentColor);
            AppendConsoleText("  theme list        List themes\n", t.TextColor);
            AppendConsoleText("  theme <name>      Apply theme\n", t.TextColor);
            AppendConsoleText("  settings          Show settings\n", t.TextColor);
            AppendConsoleText("  settings set <k> <v>  Change setting\n", t.TextColor);
            AppendConsoleText("  settings reset    Reset defaults\n", t.TextColor);
            AppendConsoleText("  settings gui      Settings window\n\n", t.TextColor);

            AppendConsoleText(" Updates:\n", t.AccentColor);
            AppendConsoleText("  checkupdate       Check for updates\n", t.TextColor);
            AppendConsoleText("  update            Download & install update\n\n", t.TextColor);

            AppendConsoleText(" Productivity:\n", t.AccentColor);
            AppendConsoleText("  bookmark          Manage bookmarks (add/go/remove/list)\n", t.TextColor);
            AppendConsoleText("  alias <n> <cmd>   Create command alias\n", t.TextColor);
            AppendConsoleText("  stopwatch         Start/stop/lap stopwatch\n", t.TextColor);
            AppendConsoleText("  timer <seconds>   Countdown timer with beep\n", t.TextColor);
            AppendConsoleText("  log start [file]  Session logging\n", t.TextColor);
            AppendConsoleText("  hash <md5|sha256> Hash text\n", t.TextColor);
            AppendConsoleText("  curl <url>        Fetch URL content\n", t.TextColor);
            AppendConsoleText("  df                Disk usage (all drives)\n", t.TextColor);
            AppendConsoleText("  write <f> <text>  Append text to file\n\n", t.TextColor);

            AppendConsoleText(" v1.0.3:\n", t.AccentColor);
            AppendConsoleText("  preview <file>    Paginated file viewer\n", t.TextColor);
            AppendConsoleText("  cat <file> -n     Syntax highlighting + line numbers\n", t.TextColor);
            AppendConsoleText("  pin <F1-12> <cmd> Pin command to F-key\n", t.TextColor);
            AppendConsoleText("  unpin <F1-12>     Unpin F-key\n", t.TextColor);
            AppendConsoleText("  plugin            Manage plugins (list/reload/create)\n", t.TextColor);
            AppendConsoleText("  ssh <user@host>   SSH (via OpenSSH)\n", t.TextColor);
            AppendConsoleText("  rc                Manage .kocurrc startup script\n\n", t.TextColor);

            AppendConsoleText(" Operators:\n", t.AccentColor);
            AppendConsoleText("  cmd1 && cmd2      Command chaining\n", t.TextColor);
            AppendConsoleText("  cmd1 | cmd2       Pipe output\n", t.TextColor);
            AppendConsoleText("  cmd > file        Redirect output (overwrite)\n", t.TextColor);
            AppendConsoleText("  cmd >> file       Redirect output (append)\n\n", t.TextColor);

            AppendConsoleText(" Minimize -> system tray | Double-click tray icon to restore\n", t.WarningColor);
            AppendConsoleText(" Tab=autocomplete  F1-F12=pinned  Ctrl+C=cancel  Esc=clear\n\n", t.WarningColor);
        }

        #endregion

        #region File System Commands

        private void CmdListDir(string[] args)
        {
            string path = args.Length > 0 ? ResolvePath(args[0]) : currentDirectory;
            ConsoleTheme t = ThemeManager.Current;

            if (!Directory.Exists(path))
            {
                AppendConsoleText("Directory not found: " + path + "\n", t.ErrorColor);
                return;
            }

            AppendConsoleText("\n Directory of " + path + "\n\n", t.InfoColor);

            foreach (string dir in Directory.GetDirectories(path))
            {
                if (cancelRequested) return;
                DirectoryInfo di = new DirectoryInfo(dir);
                string date = di.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                AppendConsoleText("  " + date + "    <DIR>    ", t.TextColor);
                AppendConsoleText(di.Name + "\n", t.AccentColor);
            }

            long totalSize = 0;
            int fileCount = 0;
            foreach (string file in Directory.GetFiles(path))
            {
                if (cancelRequested) return;
                FileInfo fi = new FileInfo(file);
                string date = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                string size = FormatFileSize(fi.Length).PadLeft(12);
                AppendConsoleText("  " + date + "  " + size + "  " + fi.Name + "\n", t.TextColor);
                totalSize += fi.Length;
                fileCount++;
            }

            int dirCount = Directory.GetDirectories(path).Length;
            AppendConsoleText("\n  " + dirCount + " dir(s), " + fileCount + " file(s), " + FormatFileSize(totalSize) + " total\n\n", t.InfoColor);
        }

        private void CmdChangeDir(string[] args)
        {
            if (args.Length == 0)
            {
                AppendConsoleText(currentDirectory + "\n", ThemeManager.Current.TextColor);
                return;
            }

            string target = args[0];

            if (target == "~")
            {
                previousDirectory = currentDirectory;
                currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return;
            }

            if (target == "-")
            {
                if (previousDirectory != null)
                {
                    string temp = currentDirectory;
                    currentDirectory = previousDirectory;
                    previousDirectory = temp;
                    AppendConsoleText(currentDirectory + "\n", ThemeManager.Current.TextColor);
                }
                return;
            }

            string resolved = ResolvePath(target);
            if (Directory.Exists(resolved))
            {
                previousDirectory = currentDirectory;
                currentDirectory = Path.GetFullPath(resolved);
            }
            else
            {
                AppendConsoleText("Directory not found: " + target + "\n", ThemeManager.Current.ErrorColor);
            }
        }

        private void CmdMkdir(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: mkdir <name>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string path = ResolvePath(args[0]);
                Directory.CreateDirectory(path);
                AppendConsoleText("Created: " + path + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdRmdir(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: rmdir <name>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string path = ResolvePath(args[0]);
                Directory.Delete(path);
                AppendConsoleText("Removed: " + path + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdDelete(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: rm <file>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string path = ResolvePath(args[0]);
                if (File.Exists(path)) { File.Delete(path); AppendConsoleText("Deleted: " + path + "\n", ThemeManager.Current.InfoColor); }
                else { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdCopy(string[] args)
        {
            if (args.Length < 2) { AppendConsoleText("Usage: cp <src> <dst>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string src = ResolvePath(args[0]);
                string dst = ResolvePath(args[1]);
                File.Copy(src, dst, true);
                AppendConsoleText("Copied: " + src + " -> " + dst + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdMove(string[] args)
        {
            if (args.Length < 2) { AppendConsoleText("Usage: mv <src> <dst>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string src = ResolvePath(args[0]);
                string dst = ResolvePath(args[1]);
                File.Move(src, dst);
                AppendConsoleText("Moved: " + src + " -> " + dst + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdCat(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: cat <file>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string path = ResolvePath(args[0]);
                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLower();
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    int max = Math.Min(lines.Length, 500);
                    bool showLineNumbers = args.Length > 1 && args[1] == "-n";
                    ConsoleTheme t = ThemeManager.Current;

                    for (int i = 0; i < max; i++)
                    {
                        if (cancelRequested) return;
                        if (showLineNumbers)
                            AppendConsoleText((i + 1).ToString().PadLeft(4) + " | ", t.AccentColor);
                        PrintSyntaxHighlighted(lines[i], ext, t);
                        AppendConsoleText("\n", t.TextColor);
                    }
                    if (lines.Length > 500)
                        AppendConsoleText("[... truncated, " + lines.Length + " total lines]\n", t.WarningColor);
                }
                else { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void PrintSyntaxHighlighted(string line, string ext, ConsoleTheme t)
        {
            // No highlighting for unknown types
            string[] codeExts = { ".cs", ".js", ".py", ".json", ".xml", ".html", ".bat", ".cmd", ".css", ".java", ".cpp", ".c", ".h" };
            if (!codeExts.Contains(ext)) { AppendConsoleText(line, t.TextColor); return; }

            // C#/Java/JS/C++ keywords
            string[] keywords = { "using", "namespace", "class", "public", "private", "protected", "static", "void",
                "int", "string", "bool", "var", "new", "return", "if", "else", "for", "foreach", "while",
                "try", "catch", "finally", "throw", "async", "await", "null", "true", "false",
                "function", "const", "let", "import", "from", "export", "def", "self", "print",
                "echo", "set", "goto", "call", "include", "struct", "enum", "interface", "override" };

            string trimmed = line.TrimStart();

            // Comments
            if (trimmed.StartsWith("//") || trimmed.StartsWith("#") || trimmed.StartsWith("REM ") || trimmed.StartsWith("<!--"))
            {
                AppendConsoleText(line, Color.FromArgb(106, 153, 85)); // green comments
                return;
            }

            // Strings (simplified)
            if (trimmed.Contains("\"") || trimmed.Contains("'"))
            {
                // Just colorize the whole line with string detection
                int idx = 0;
                while (idx < line.Length)
                {
                    if (line[idx] == '"' || line[idx] == '\'')
                    {
                        char quote = line[idx];
                        int end = line.IndexOf(quote, idx + 1);
                        if (end == -1) end = line.Length - 1;
                        AppendConsoleText(line.Substring(idx, end - idx + 1), Color.FromArgb(206, 145, 120)); // orange strings
                        idx = end + 1;
                    }
                    else
                    {
                        // Find next quote or end
                        int nextQuote = -1;
                        for (int q = idx; q < line.Length; q++)
                        {
                            if (line[q] == '"' || line[q] == '\'') { nextQuote = q; break; }
                        }
                        if (nextQuote == -1) nextQuote = line.Length;
                        string segment = line.Substring(idx, nextQuote - idx);
                        PrintKeywords(segment, keywords, t);
                        idx = nextQuote;
                    }
                }
                return;
            }

            PrintKeywords(line, keywords, t);
        }

        private void PrintKeywords(string text, string[] keywords, ConsoleTheme t)
        {
            // Split by word boundaries and colorize keywords
            string[] tokens = System.Text.RegularExpressions.Regex.Split(text, @"(\b\w+\b)");
            foreach (string token in tokens)
            {
                if (keywords.Contains(token.ToLower()))
                    AppendConsoleText(token, Color.FromArgb(86, 156, 214)); // blue keywords
                else if (token.Length > 0 && char.IsDigit(token[0]))
                    AppendConsoleText(token, Color.FromArgb(181, 206, 168)); // green numbers
                else
                    AppendConsoleText(token, t.TextColor);
            }
        }

        private void CmdTouch(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: touch <file>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string path = ResolvePath(args[0]);
                if (!File.Exists(path)) { File.Create(path).Dispose(); AppendConsoleText("Created: " + path + "\n", ThemeManager.Current.InfoColor); }
                else { File.SetLastWriteTime(path, DateTime.Now); AppendConsoleText("Updated: " + path + "\n", ThemeManager.Current.InfoColor); }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdTree(string[] args)
        {
            string path = args.Length > 0 ? ResolvePath(args[0]) : currentDirectory;
            if (!Directory.Exists(path))
            {
                AppendConsoleText("Directory not found: " + path + "\n", ThemeManager.Current.ErrorColor);
                return;
            }
            AppendConsoleText(path + "\n", ThemeManager.Current.AccentColor);
            PrintTree(path, "", 0, 3);
            AppendConsoleText("\n", ThemeManager.Current.TextColor);
        }

        private void PrintTree(string dir, string indent, int depth, int maxDepth)
        {
            if (depth >= maxDepth || cancelRequested) return;
            try
            {
                string[] entries = Directory.GetFileSystemEntries(dir);
                for (int i = 0; i < entries.Length; i++)
                {
                    if (cancelRequested) return;
                    bool isLast = (i == entries.Length - 1);
                    string connector = isLast ? "└── " : "├── ";
                    string name = Path.GetFileName(entries[i]);
                    bool isDir = Directory.Exists(entries[i]);

                    AppendConsoleText(indent + connector, ThemeManager.Current.TextColor);
                    AppendConsoleText(name + "\n", isDir ? ThemeManager.Current.AccentColor : ThemeManager.Current.TextColor);

                    if (isDir)
                    {
                        string childIndent = indent + (isLast ? "    " : "│   ");
                        PrintTree(entries[i], childIndent, depth + 1, maxDepth);
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Network Commands

        private void CmdPing(string[] args)
        {
            if (args.Length == 0)
            {
                AppendConsoleText("Usage: ping <host>\n", ThemeManager.Current.WarningColor);
                return;
            }

            string host = args[0];
            AppendConsoleText("Pinging " + host + "...\n\n", ThemeManager.Current.InfoColor);

            try
            {
                Ping pinger = new Ping();
                int sent = 0, received = 0;
                long totalMs = 0;

                for (int i = 0; i < 4; i++)
                {
                    if (cancelRequested) break;
                    try
                    {
                        PingReply reply = pinger.Send(host, 3000);
                        sent++;
                        if (reply.Status == IPStatus.Success)
                        {
                            received++;
                            totalMs += reply.RoundtripTime;
                            AppendConsoleText("  Reply from " + reply.Address + ": time=" + reply.RoundtripTime + "ms TTL=" + reply.Options?.Ttl + "\n", ThemeManager.Current.TextColor);
                        }
                        else
                        {
                            AppendConsoleText("  Request timed out (" + reply.Status + ")\n", ThemeManager.Current.WarningColor);
                        }
                    }
                    catch (PingException ex)
                    {
                        sent++;
                        AppendConsoleText("  Error: " + ex.InnerException?.Message + "\n", ThemeManager.Current.ErrorColor);
                    }
                    System.Threading.Thread.Sleep(500);
                }

                AppendConsoleText("\n  Packets: Sent=" + sent + ", Received=" + received + ", Lost=" + (sent - received) + "\n", ThemeManager.Current.InfoColor);
                if (received > 0)
                    AppendConsoleText("  Average: " + (totalMs / received) + "ms\n", ThemeManager.Current.InfoColor);
                AppendConsoleText("\n", ThemeManager.Current.TextColor);
            }
            catch (Exception ex)
            {
                AppendConsoleText("Ping error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor);
            }
        }

        #endregion

        #region Utility Commands

        private void CmdHistory(string[] args)
        {
            if (args.Length > 0 && args[0].ToLower() == "clear")
            {
                commandHistory.Clear();
                historyIndex = -1;
                AppendConsoleText("History cleared.\n", ThemeManager.Current.InfoColor);
                return;
            }

            if (commandHistory.Count == 0)
            {
                AppendConsoleText("No history.\n", ThemeManager.Current.TextColor);
                return;
            }

            AppendConsoleText("\n", ThemeManager.Current.TextColor);
            for (int i = 0; i < commandHistory.Count; i++)
            {
                string num = (i + 1).ToString().PadLeft(4);
                AppendConsoleText(num + "  " + commandHistory[i] + "\n", ThemeManager.Current.TextColor);
            }
            AppendConsoleText("\n", ThemeManager.Current.TextColor);
        }

        private void CmdEnv(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            if (args.Length > 0)
            {
                string val = Environment.GetEnvironmentVariable(args[0]);
                if (val != null) AppendConsoleText(args[0] + "=" + val + "\n", t.TextColor);
                else AppendConsoleText("Not found: " + args[0] + "\n", t.ErrorColor);
                return;
            }

            AppendConsoleText("\n", t.TextColor);
            var envVars = Environment.GetEnvironmentVariables();
            var sorted = new SortedDictionary<string, string>();
            foreach (System.Collections.DictionaryEntry entry in envVars)
                sorted[entry.Key.ToString()] = entry.Value.ToString();
            foreach (var kv in sorted)
            {
                AppendConsoleText("  " + kv.Key + "=", t.AccentColor);
                AppendConsoleText(kv.Value + "\n", t.TextColor);
            }
            AppendConsoleText("\n", t.TextColor);
        }

        private void CmdCalc(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: calc <expr> | calc <var>=<expr> | calc vars\n", ThemeManager.Current.WarningColor); return; }
            string expr = string.Join(" ", args);

            // Show variables
            if (expr.Trim().ToLower() == "vars")
            {
                if (calcVars.Count == 0) { AppendConsoleText("  No variables.\n", ThemeManager.Current.WarningColor); return; }
                foreach (var kv in calcVars)
                    AppendConsoleText("  " + kv.Key + " = " + kv.Value + "\n", ThemeManager.Current.TextColor);
                return;
            }

            try
            {
                // Variable assignment: x = 5+3
                string varName = null;
                if (expr.Contains("=") && !expr.Contains("=="))
                {
                    int eqIdx = expr.IndexOf('=');
                    string left = expr.Substring(0, eqIdx).Trim();
                    if (left.All(c => char.IsLetterOrDigit(c) || c == '_'))
                    {
                        varName = left;
                        expr = expr.Substring(eqIdx + 1).Trim();
                    }
                }

                // Replace math functions
                expr = expr.Replace("pi", Math.PI.ToString(System.Globalization.CultureInfo.InvariantCulture))
                           .Replace("PI", Math.PI.ToString(System.Globalization.CultureInfo.InvariantCulture));

                // Replace variables
                foreach (var kv in calcVars)
                    expr = expr.Replace(kv.Key, kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

                // Handle math functions
                expr = EvaluateMathFunctions(expr);

                DataTable dt = new DataTable();
                object result = dt.Compute(expr, "");
                double val = Convert.ToDouble(result);

                if (varName != null)
                {
                    calcVars[varName] = val;
                    AppendConsoleText("  " + varName + " = " + val + "\n", ThemeManager.Current.InfoColor);
                }
                else
                {
                    AppendConsoleText("  = " + val + "\n", ThemeManager.Current.InfoColor);
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private string EvaluateMathFunctions(string expr)
        {
            string[] funcs = { "sin", "cos", "tan", "sqrt", "abs", "log", "log10", "ceil", "floor", "round", "pow" };
            foreach (string func in funcs)
            {
                while (expr.Contains(func + "("))
                {
                    int start = expr.IndexOf(func + "(");
                    int paren = start + func.Length;
                    int depth = 1;
                    int end = paren + 1;
                    while (end < expr.Length && depth > 0)
                    {
                        if (expr[end] == '(') depth++;
                        if (expr[end] == ')') depth--;
                        end++;
                    }
                    string inner = expr.Substring(paren + 1, end - paren - 2);
                    string[] innerArgs = inner.Split(',');

                    // Evaluate inner expression
                    DataTable dt2 = new DataTable();
                    double val1 = Convert.ToDouble(dt2.Compute(EvaluateMathFunctions(innerArgs[0].Trim()), ""));
                    double result;

                    switch (func)
                    {
                        case "sin": result = Math.Sin(val1); break;
                        case "cos": result = Math.Cos(val1); break;
                        case "tan": result = Math.Tan(val1); break;
                        case "sqrt": result = Math.Sqrt(val1); break;
                        case "abs": result = Math.Abs(val1); break;
                        case "log": result = Math.Log(val1); break;
                        case "log10": result = Math.Log10(val1); break;
                        case "ceil": result = Math.Ceiling(val1); break;
                        case "floor": result = Math.Floor(val1); break;
                        case "round": result = Math.Round(val1); break;
                        case "pow":
                            double val2 = innerArgs.Length > 1 ? Convert.ToDouble(dt2.Compute(innerArgs[1].Trim(), "")) : 2;
                            result = Math.Pow(val1, val2);
                            break;
                        default: result = val1; break;
                    }

                    expr = expr.Substring(0, start) + result.ToString(System.Globalization.CultureInfo.InvariantCulture) + expr.Substring(end);
                }
            }
            return expr;
        }

        #endregion

        #region Theme & Settings Commands

        private void CmdTheme(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;

            if (args.Length == 0 || args[0].ToLower() == "list")
            {
                AppendConsoleText("\n Available Themes:\n\n", t.WarningColor);
                foreach (string name in ThemeManager.GetThemeNames())
                {
                    ConsoleTheme theme = ThemeManager.GetTheme(name);
                    string arrow = name.Equals(t.Name, StringComparison.OrdinalIgnoreCase) ? " ► " : "   ";
                    AppendConsoleText(arrow + name + "\n", theme.AccentColor);
                }
                AppendConsoleText("\n Usage: theme <name>\n\n", t.TextColor);
                return;
            }

            string themeName = args[0];
            if (SettingsManager.Set("theme", themeName))
            {
                if (richTextBoxConsoleOutput.InvokeRequired)
                    richTextBoxConsoleOutput.Invoke(new Action(() => { ApplyTheme(); ApplyDarkMode(); }));
                else
                { ApplyTheme(); ApplyDarkMode(); }

                richTextBoxConsoleOutput.Clear();
                AppendConsoleText("Theme set to: " + themeName + "\n\n", ThemeManager.Current.InfoColor);
            }
            else
            {
                AppendConsoleText("Unknown theme: " + themeName + ". Use 'theme list'.\n", t.ErrorColor);
            }
        }

        private void CmdSettings(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;

            if (args.Length == 0)
            {
                AppendConsoleText("\n=== Settings ===\n\n", t.WarningColor);
                foreach (var kv in SettingsManager.GetAll())
                {
                    AppendConsoleText("  " + kv.Key.PadRight(18), t.AccentColor);
                    AppendConsoleText(kv.Value + "\n", t.TextColor);
                }
                AppendConsoleText("\n  settings set <key> <value>\n  settings reset\n\n", t.TextColor);
                return;
            }

            if (args[0].ToLower() == "gui")
            {
                OpenSettingsGui();
                return;
            }

            if (args[0].ToLower() == "reset")
            {
                SettingsManager.Reset();
                ThemeManager.SetTheme("default");
                if (richTextBoxConsoleOutput.InvokeRequired)
                    richTextBoxConsoleOutput.Invoke(new Action(() => { ApplyTheme(); ApplyFontSettings(); ApplyDarkMode(); }));
                else
                { ApplyTheme(); ApplyFontSettings(); ApplyDarkMode(); }
                richTextBoxConsoleOutput.WordWrap = SettingsManager.Current.WordWrap;
                AppendConsoleText("Settings reset.\n", ThemeManager.Current.InfoColor);
                return;
            }

            if (args[0].ToLower() == "set" && args.Length >= 3)
            {
                string key = args[1];
                string value = string.Join(" ", args.Skip(2));

                if (SettingsManager.Set(key, value))
                {
                    if (richTextBoxConsoleOutput.InvokeRequired)
                        richTextBoxConsoleOutput.Invoke(new Action(() => { ApplyTheme(); ApplyFontSettings(); ApplyDarkMode(); }));
                    else
                    { ApplyTheme(); ApplyFontSettings(); ApplyDarkMode(); }
                    richTextBoxConsoleOutput.WordWrap = SettingsManager.Current.WordWrap;
                    AppendConsoleText("Set " + key + " = " + value + "\n", ThemeManager.Current.InfoColor);
                }
                else
                {
                    AppendConsoleText("Invalid: " + key + " = " + value + "\n", t.ErrorColor);
                    AppendConsoleText("Keys: theme, font, fontSize, wordWrap, timestamps, autoScroll, shell, timeout\n", t.WarningColor);
                }
                return;
            }

            AppendConsoleText("Usage: settings | settings set <key> <value> | settings reset | settings gui\n", t.WarningColor);
        }

        private void OpenSettingsGui()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OpenSettingsGui()));
                return;
            }

            using (SettingsForm sf = new SettingsForm())
            {
                if (sf.ShowDialog(this) == DialogResult.OK)
                {
                    sf.ApplySettings();
                    ApplyTheme();
                    ApplyFontSettings();
                    ApplyDarkMode();
                    richTextBoxConsoleOutput.WordWrap = SettingsManager.Current.WordWrap;
                    AppendConsoleText("Settings saved.\n", ThemeManager.Current.InfoColor);
                }
            }
        }

        #endregion

        #region System Info Commands

        private void ShowFastFetch()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n", t.TextColor);

            string user = Environment.UserName + "@" + Environment.MachineName;
            AppendConsoleText("  " + user + "\n", t.AccentColor);
            AppendConsoleText("  " + new string('-', user.Length) + "\n", t.TextColor);

            AppendConsoleText("  OS: ", t.AccentColor); AppendConsoleText(GetOSInfo() + "\n", t.TextColor);
            AppendConsoleText("  Kernel: ", t.AccentColor); AppendConsoleText(GetKernelVersion() + "\n", t.TextColor);
            AppendConsoleText("  CPU: ", t.AccentColor); AppendConsoleText(GetCPUInfo() + "\n", t.TextColor);
            AppendConsoleText("  RAM: ", t.AccentColor); AppendConsoleText(GetRAMInfo() + "\n", t.TextColor);
            AppendConsoleText("  GPU: ", t.AccentColor); AppendConsoleText(GetGPUInfo() + "\n", t.TextColor);
            AppendConsoleText("  Disk: ", t.AccentColor); AppendConsoleText(GetDiskInfo() + "\n", t.TextColor);
            AppendConsoleText("  Resolution: ", t.AccentColor); AppendConsoleText(GetResolution() + "\n", t.TextColor);
            AppendConsoleText("  Uptime: ", t.AccentColor); AppendConsoleText(GetUptime() + "\n", t.TextColor);
            AppendConsoleText("  Shell: ", t.AccentColor); AppendConsoleText("KocurConsole " + terminalVersion + "\n", t.TextColor);

            // Color palette
            AppendConsoleText("  ", t.TextColor);
            Color[] palette = { Color.Black, Color.DarkRed, Color.DarkGreen, Color.Olive, Color.DarkBlue, Color.DarkMagenta, Color.DarkCyan, Color.Silver,
                                Color.Gray, Color.Red, Color.Green, Color.Yellow, Color.Blue, Color.Magenta, Color.Cyan, Color.White };
            foreach (Color c in palette)
                AppendConsoleText("██", c);
            AppendConsoleText("\n\n", t.TextColor);
        }

        private void ShowSystemInfo()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n=== System Information ===\n\n", t.WarningColor);
            AppendConsoleText("  User: ", t.AccentColor); AppendConsoleText(Environment.UserDomainName + "\\" + Environment.UserName + "\n", t.TextColor);
            AppendConsoleText("  Machine: ", t.AccentColor); AppendConsoleText(Environment.MachineName + "\n", t.TextColor);
            AppendConsoleText("  OS: ", t.AccentColor); AppendConsoleText(Environment.OSVersion.ToString() + "\n", t.TextColor);
            AppendConsoleText("  CLR: ", t.AccentColor); AppendConsoleText(Environment.Version.ToString() + "\n", t.TextColor);
            AppendConsoleText("  Processors: ", t.AccentColor); AppendConsoleText(Environment.ProcessorCount.ToString() + "\n", t.TextColor);
            AppendConsoleText("  64-bit OS: ", t.AccentColor); AppendConsoleText(Environment.Is64BitOperatingSystem.ToString() + "\n", t.TextColor);

            AppendConsoleText("\n  Drives:\n", t.AccentColor);
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    double total = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    double free = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    AppendConsoleText("    " + drive.Name + " [" + drive.DriveFormat + "] " + free.ToString("F1") + "GB free / " + total.ToString("F1") + "GB\n", t.TextColor);
                }
            }
            AppendConsoleText("\n=".PadRight(28, '=') + "\n\n", t.WarningColor);
        }

        #endregion

        #region WMI Helpers

        private string GetOSInfo()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                    return o["Caption"].ToString().Trim() + " (" + o["Version"].ToString() + ")";
            }
            catch { }
            return "Windows (Unknown)";
        }

        private string GetKernelVersion()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Version FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get()) return o["Version"].ToString();
            }
            catch { }
            return "Unknown";
        }

        private string GetCPUInfo()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor");
                foreach (ManagementObject o in s.Get())
                    return o["Name"].ToString().Trim() + " (" + o["NumberOfCores"] + " cores)";
            }
            catch { }
            return "Unknown";
        }

        private string GetRAMInfo()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                {
                    double total = long.Parse(o["TotalVisibleMemorySize"].ToString()) / 1024.0 / 1024.0;
                    double free = long.Parse(o["FreePhysicalMemory"].ToString()) / 1024.0 / 1024.0;
                    return (total - free).ToString("F1") + "GB / " + total.ToString("F1") + "GB";
                }
            }
            catch { }
            return "Unknown";
        }

        private string GetGPUInfo()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                List<string> gpus = new List<string>();
                foreach (ManagementObject o in s.Get()) gpus.Add(o["Name"].ToString());
                return string.Join(", ", gpus);
            }
            catch { }
            return "Unknown";
        }

        private string GetDiskInfo()
        {
            try
            {
                DriveInfo drive = new DriveInfo("C:\\");
                double total = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                double used = (drive.TotalSize - drive.AvailableFreeSpace) / 1024.0 / 1024.0 / 1024.0;
                return used.ToString("F1") + "GB / " + total.ToString("F1") + "GB";
            }
            catch { }
            return "Unknown";
        }

        private string GetResolution()
        {
            return Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height;
        }

        private string GetUptime()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                {
                    DateTime boot = ManagementDateTimeConverter.ToDateTime(o["LastBootUpTime"].ToString());
                    TimeSpan up = DateTime.Now - boot;
                    return (int)up.TotalDays + "d " + up.Hours + "h " + up.Minutes + "m";
                }
            }
            catch { }
            return "Unknown";
        }

        #endregion

        #region File Search & Analysis Commands

        private void CmdFind(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: find <pattern>\n", ThemeManager.Current.WarningColor); return; }
            string pattern = args[0];
            string searchPath = args.Length > 1 ? ResolvePath(args[1]) : currentDirectory;
            ConsoleTheme t = ThemeManager.Current;

            AppendConsoleText("\n Searching for \"" + pattern + "\" in " + searchPath + "...\n\n", t.InfoColor);
            int count = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(searchPath, "*" + pattern + "*", SearchOption.AllDirectories))
                {
                    if (cancelRequested) break;
                    if (count >= 200) { AppendConsoleText("  [... truncated at 200 results]\n", t.WarningColor); break; }
                    AppendConsoleText("  " + file + "\n", t.TextColor);
                    count++;
                }
                foreach (string dir in Directory.EnumerateDirectories(searchPath, "*" + pattern + "*", SearchOption.AllDirectories))
                {
                    if (cancelRequested) break;
                    if (count >= 200) break;
                    AppendConsoleText("  " + dir + "\\\n", t.AccentColor);
                    count++;
                }
            }
            catch (Exception ex) { AppendConsoleText("  Error: " + ex.Message + "\n", t.ErrorColor); }
            AppendConsoleText("\n  " + count + " result(s) found.\n\n", t.InfoColor);
        }

        private void CmdGrep(string[] args)
        {
            if (args.Length < 2) { AppendConsoleText("Usage: grep <pattern> <file>\n", ThemeManager.Current.WarningColor); return; }
            string pattern = args[0];
            string path = ResolvePath(args[1]);
            ConsoleTheme t = ThemeManager.Current;

            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[1] + "\n", t.ErrorColor); return; }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int count = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (cancelRequested) break;
                    if (lines[i].IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AppendConsoleText("  " + (i + 1).ToString().PadLeft(5) + ": ", t.AccentColor);
                        string line = lines[i];
                        int idx = line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                        while (idx >= 0)
                        {
                            AppendConsoleText(line.Substring(0, idx), t.TextColor);
                            AppendConsoleText(line.Substring(idx, pattern.Length), t.WarningColor);
                            line = line.Substring(idx + pattern.Length);
                            idx = line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                        }
                        AppendConsoleText(line + "\n", t.TextColor);
                        count++;
                    }
                }
                AppendConsoleText("\n  " + count + " match(es) in " + lines.Length + " lines.\n", t.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", t.ErrorColor); }
        }

        private void CmdHead(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: head <file> [n]\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            int n = 10;
            if (args.Length > 1) int.TryParse(args[1], out n);

            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); return; }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int max = Math.Min(lines.Length, n);
                for (int i = 0; i < max; i++)
                    AppendConsoleText(lines[i] + "\n", ThemeManager.Current.TextColor);
                if (lines.Length > n)
                    AppendConsoleText("[... " + lines.Length + " total lines]\n", ThemeManager.Current.WarningColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdTail(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: tail <file> [n]\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            int n = 10;
            if (args.Length > 1) int.TryParse(args[1], out n);

            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); return; }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int start = Math.Max(0, lines.Length - n);
                if (start > 0)
                    AppendConsoleText("[... showing last " + n + " of " + lines.Length + " lines]\n", ThemeManager.Current.WarningColor);
                for (int i = start; i < lines.Length; i++)
                    AppendConsoleText(lines[i] + "\n", ThemeManager.Current.TextColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdWc(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: wc <file>\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); return; }

            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);
                int lines = content.Split('\n').Length;
                int words = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                int chars = content.Length;
                AppendConsoleText("  Lines: " + lines + "  Words: " + words + "  Chars: " + chars + "\n", ThemeManager.Current.TextColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdSize(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: size <path>\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            try
            {
                if (File.Exists(path))
                {
                    FileInfo fi = new FileInfo(path);
                    AppendConsoleText("  " + fi.Name + ": " + FormatFileSize(fi.Length) + "\n", ThemeManager.Current.TextColor);
                }
                else if (Directory.Exists(path))
                {
                    AppendConsoleText("  Calculating...\n", ThemeManager.Current.InfoColor);
                    long total = GetDirectorySize(path);
                    if (!cancelRequested)
                        AppendConsoleText("  " + Path.GetFileName(path) + ": " + FormatFileSize(total) + "\n", ThemeManager.Current.TextColor);
                }
                else { AppendConsoleText("Not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (cancelRequested) break;
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return size;
        }

        private void CmdMd5(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: md5 <file>\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); return; }
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(path))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    AppendConsoleText("  MD5: " + hex + "\n", ThemeManager.Current.TextColor);
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdSha256(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: sha256 <file>\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); return; }
            try
            {
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(stream);
                    string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    AppendConsoleText("  SHA256: " + hex + "\n", ThemeManager.Current.TextColor);
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        #endregion

        #region Extended Network Commands

        private void CmdIp()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n  Network Interfaces:\n\n", t.InfoColor);
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        var props = ni.GetIPProperties();
                        foreach (var addr in props.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                                addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                AppendConsoleText("  " + ni.Name.PadRight(25), t.AccentColor);
                                AppendConsoleText(addr.Address.ToString() + "\n", t.TextColor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", t.ErrorColor); }
            AppendConsoleText("\n", t.TextColor);
        }

        private void CmdDns(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: dns <host>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                var entry = Dns.GetHostEntry(args[0]);
                AppendConsoleText("  Host: " + entry.HostName + "\n", ThemeManager.Current.AccentColor);
                foreach (var addr in entry.AddressList)
                    AppendConsoleText("  IP:   " + addr.ToString() + "\n", ThemeManager.Current.TextColor);
            }
            catch (Exception ex) { AppendConsoleText("DNS error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdWget(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: wget <url> [filename]\n", ThemeManager.Current.WarningColor); return; }
            string url = args[0];
            string filename;
            if (args.Length > 1)
            {
                filename = args[1];
            }
            else
            {
                try { filename = Path.GetFileName(new Uri(url).LocalPath); }
                catch { filename = "download"; }
                if (string.IsNullOrEmpty(filename)) filename = "download";
            }
            string savePath = ResolvePath(filename);

            AppendConsoleText("Downloading " + url + "...\n", ThemeManager.Current.InfoColor);
            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, savePath);
                }
                FileInfo fi = new FileInfo(savePath);
                AppendConsoleText("Saved: " + savePath + " (" + FormatFileSize(fi.Length) + ")\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Download error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        #endregion

        #region Process Commands

        private void CmdPs()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n  " + "PID".PadRight(8) + "Name".PadRight(35) + "Memory".PadLeft(12) + "\n", t.AccentColor);
            AppendConsoleText("  " + new string('-', 55) + "\n", t.TextColor);

            var processes = Process.GetProcesses().OrderByDescending(p =>
            {
                try { return p.WorkingSet64; } catch { return 0L; }
            }).Take(30);

            foreach (var p in processes)
            {
                if (cancelRequested) break;
                try
                {
                    string mem = FormatFileSize(p.WorkingSet64);
                    AppendConsoleText("  " + p.Id.ToString().PadRight(8) + p.ProcessName.PadRight(35) + mem.PadLeft(12) + "\n", t.TextColor);
                }
                catch { }
            }
            AppendConsoleText("\n  [Top 30 by memory]\n\n", t.WarningColor);
        }

        private void CmdKill(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: kill <pid>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                int pid = int.Parse(args[0]);
                Process p = Process.GetProcessById(pid);
                string name = p.ProcessName;
                p.Kill();
                AppendConsoleText("Killed: " + name + " (PID " + pid + ")\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        #endregion

        #region Extended Utility Commands

        private void CmdOpen(string[] args)
        {
            string path = args.Length > 0 ? ResolvePath(args[0]) : currentDirectory;
            try
            {
                Process.Start("explorer.exe", path);
                AppendConsoleText("Opened: " + path + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdStart(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: start <program> [args]\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string program = args[0];
                string arguments = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "";
                Process.Start(program, arguments);
                AppendConsoleText("Started: " + program + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdBase64(string[] args)
        {
            if (args.Length < 2) { AppendConsoleText("Usage: base64 <encode|decode> <text>\n", ThemeManager.Current.WarningColor); return; }
            string mode = args[0].ToLower();
            string text = string.Join(" ", args.Skip(1));
            try
            {
                if (mode == "encode" || mode == "enc")
                {
                    string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                    AppendConsoleText(encoded + "\n", ThemeManager.Current.TextColor);
                }
                else if (mode == "decode" || mode == "dec")
                {
                    string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(text));
                    AppendConsoleText(decoded + "\n", ThemeManager.Current.TextColor);
                }
                else
                {
                    AppendConsoleText("Mode must be 'encode' or 'decode'\n", ThemeManager.Current.WarningColor);
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdRandom(string[] args)
        {
            Random rng = new Random();
            int min = 0, max = 100;
            if (args.Length >= 1) int.TryParse(args[0], out min);
            if (args.Length >= 2) int.TryParse(args[1], out max);
            AppendConsoleText(rng.Next(min, max + 1).ToString() + "\n", ThemeManager.Current.TextColor);
        }

        private void CmdAbout()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n", t.TextColor);
            AppendConsoleText("  ╔══════════════════════════════════════╗\n", t.AccentColor);
            AppendConsoleText("  ║         KocurConsole Terminal        ║\n", t.AccentColor);
            AppendConsoleText("  ║           Version " + terminalVersion.PadRight(19) + "║\n", t.AccentColor);
            AppendConsoleText("  ╠══════════════════════════════════════╣\n", t.AccentColor);
            AppendConsoleText("  ║  Built with C# / .NET Framework 4.8 ║\n", t.TextColor);
            AppendConsoleText("  ║  MIT License                        ║\n", t.TextColor);
            AppendConsoleText("  ╚══════════════════════════════════════╝\n", t.AccentColor);
            AppendConsoleText("\n", t.TextColor);
        }

        private void CmdClipboard()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => CmdClipboard()));
                return;
            }
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    AppendConsoleText(text + "\n", ThemeManager.Current.TextColor);
                }
                else
                {
                    AppendConsoleText("[Clipboard is empty or contains non-text data]\n", ThemeManager.Current.WarningColor);
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        #endregion

        #region Update Commands

        private void CmdCheckUpdate()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n  Checking for updates...\n", t.InfoColor);

            VersionManifest manifest = UpdateHandler.CheckForUpdate();
            if (manifest == null)
            {
                AppendConsoleText("  Could not connect to update server.\n", t.ErrorColor);
                AppendConsoleText("  Check your internet connection.\n\n", t.TextColor);
                return;
            }

            if (UpdateHandler.IsNewerVersion(terminalVersion, manifest.Version))
            {
                AppendConsoleText("\n  New version available!\n\n", t.WarningColor);
                AppendConsoleText("  Current:  v" + terminalVersion + "\n", t.TextColor);
                AppendConsoleText("  Latest:   v" + manifest.Version + " (" + manifest.ReleaseDate + ")\n", t.AccentColor);
                AppendConsoleText("  Changes:  " + manifest.Changelog + "\n\n", t.TextColor);
                AppendConsoleText("  Run 'update' to download and install.\n", t.InfoColor);
                AppendConsoleText("  Or visit: " + manifest.ReleasePage + "\n\n", t.TextColor);
            }
            else
            {
                AppendConsoleText("\n  You are up to date! (v" + terminalVersion + ")\n\n", t.InfoColor);
            }
        }

        private void CmdUpdate()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n  Checking for updates...\n", t.InfoColor);

            VersionManifest manifest = UpdateHandler.CheckForUpdate();
            if (manifest == null)
            {
                AppendConsoleText("  Could not connect to update server.\n\n", t.ErrorColor);
                return;
            }

            if (!UpdateHandler.IsNewerVersion(terminalVersion, manifest.Version))
            {
                AppendConsoleText("  Already up to date (v" + terminalVersion + ")\n\n", t.InfoColor);
                return;
            }

            AppendConsoleText("  Downloading v" + manifest.Version + "...\n", t.InfoColor);

            string tempPath = UpdateHandler.DownloadUpdate(manifest, progress =>
            {
                // Progress callback — we could show a bar but keep it simple
            });

            if (tempPath == null)
            {
                AppendConsoleText("  Download failed.\n", t.ErrorColor);
                AppendConsoleText("  Try manual download: " + manifest.ReleasePage + "\n\n", t.TextColor);
                return;
            }

            AppendConsoleText("  Download complete!\n", t.InfoColor);
            AppendConsoleText("  Applying update — KocurConsole will restart...\n\n", t.WarningColor);

            // Small delay so user can read the message
            System.Threading.Thread.Sleep(1500);

            // Apply update (launches batch script and we need to exit)
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    UpdateHandler.ApplyUpdate(tempPath);
                    Application.Exit();
                }));
            }
            else
            {
                UpdateHandler.ApplyUpdate(tempPath);
                Application.Exit();
            }
        }

        #endregion

        #region v1.0.2 — Bookmarks

        private void CmdBookmark(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            if (args.Length == 0)
            {
                // List bookmarks
                if (bookmarks.Count == 0)
                {
                    AppendConsoleText("  No bookmarks saved. Use: bookmark add <name>\n", t.WarningColor);
                    return;
                }
                AppendConsoleText("\n  Bookmarks:\n\n", t.InfoColor);
                foreach (var kv in bookmarks)
                    AppendConsoleText("  " + kv.Key.PadRight(15) + kv.Value + "\n", t.TextColor);
                AppendConsoleText("\n", t.TextColor);
                return;
            }

            string action = args[0].ToLower();
            if (action == "add" && args.Length >= 2)
            {
                string name = args[1];
                string path = args.Length >= 3 ? ResolvePath(args[2]) : currentDirectory;
                bookmarks[name] = path;
                SaveAliasesAndBookmarks();
                AppendConsoleText("Bookmark '" + name + "' -> " + path + "\n", t.InfoColor);
            }
            else if (action == "go" && args.Length >= 2)
            {
                string name = args[1];
                if (bookmarks.ContainsKey(name))
                {
                    previousDirectory = currentDirectory;
                    currentDirectory = bookmarks[name];
                    AppendConsoleText(currentDirectory + "\n", t.TextColor);
                }
                else
                    AppendConsoleText("Bookmark not found: " + name + "\n", t.ErrorColor);
            }
            else if (action == "remove" && args.Length >= 2)
            {
                if (bookmarks.Remove(args[1]))
                {
                    SaveAliasesAndBookmarks();
                    AppendConsoleText("Removed bookmark: " + args[1] + "\n", t.InfoColor);
                }
                else
                    AppendConsoleText("Not found: " + args[1] + "\n", t.ErrorColor);
            }
            else if (action == "list")
            {
                CmdBookmark(new string[0]);
                return;
            }
            else
            {
                AppendConsoleText("Usage: bookmark [add <name> [path] | go <name> | remove <name> | list]\n", t.WarningColor);
            }
        }

        #endregion

        #region v1.0.2 — Aliases

        private void CmdAlias(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            if (args.Length == 0)
            {
                if (aliases.Count == 0)
                {
                    AppendConsoleText("  No aliases defined. Use: alias <name> <command>\n", t.WarningColor);
                    return;
                }
                AppendConsoleText("\n  Aliases:\n\n", t.InfoColor);
                foreach (var kv in aliases)
                    AppendConsoleText("  " + kv.Key.PadRight(15) + "-> " + kv.Value + "\n", t.TextColor);
                AppendConsoleText("\n", t.TextColor);
                return;
            }

            if (args[0].ToLower() == "remove" && args.Length >= 2)
            {
                if (aliases.Remove(args[1]))
                {
                    SaveAliasesAndBookmarks();
                    AppendConsoleText("Removed alias: " + args[1] + "\n", t.InfoColor);
                }
                else
                    AppendConsoleText("Not found: " + args[1] + "\n", t.ErrorColor);
                return;
            }

            if (args.Length >= 2)
            {
                string name = args[0];
                string cmd = string.Join(" ", args.Skip(1));
                aliases[name] = cmd;
                SaveAliasesAndBookmarks();
                AppendConsoleText("Alias: " + name + " -> " + cmd + "\n", t.InfoColor);
            }
            else
            {
                AppendConsoleText("Usage: alias <name> <command> | alias remove <name>\n", t.WarningColor);
            }
        }

        #endregion

        #region v1.0.2 — Stopwatch & Timer

        private void CmdStopwatch(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            string action = args.Length > 0 ? args[0].ToLower() : "";

            if (action == "start" || (action == "" && activeStopwatch == null))
            {
                activeStopwatch = Stopwatch.StartNew();
                AppendConsoleText("  Stopwatch started.\n", t.InfoColor);
            }
            else if (action == "stop" && activeStopwatch != null)
            {
                activeStopwatch.Stop();
                AppendConsoleText("  Stopped: " + FormatElapsed(activeStopwatch.Elapsed) + "\n", t.InfoColor);
                activeStopwatch = null;
            }
            else if (action == "lap" && activeStopwatch != null)
            {
                AppendConsoleText("  Lap: " + FormatElapsed(activeStopwatch.Elapsed) + "\n", t.AccentColor);
            }
            else if (action == "" && activeStopwatch != null)
            {
                AppendConsoleText("  Running: " + FormatElapsed(activeStopwatch.Elapsed) + "\n", t.InfoColor);
                AppendConsoleText("  Use: stopwatch stop | stopwatch lap\n", t.TextColor);
            }
            else if (action == "stop" && activeStopwatch == null)
            {
                AppendConsoleText("  No stopwatch running.\n", t.WarningColor);
            }
            else
            {
                AppendConsoleText("Usage: stopwatch [start | stop | lap]\n", t.WarningColor);
            }
        }

        private string FormatElapsed(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return string.Format("{0:00}:{1:00}:{2:00}.{3:000}", (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            if (ts.TotalMinutes >= 1)
                return string.Format("{0:00}:{1:00}.{2:000}", (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds);
            return string.Format("{0}.{1:000}s", ts.Seconds, ts.Milliseconds);
        }

        private void CmdTimer(string[] args)
        {
            int seconds;
            if (!int.TryParse(args[0], out seconds) || seconds <= 0)
            {
                AppendConsoleText("Usage: timer <seconds>\n", ThemeManager.Current.WarningColor);
                return;
            }

            AppendConsoleText("  Timer: " + seconds + "s\n", ThemeManager.Current.InfoColor);
            for (int i = seconds; i > 0; i--)
            {
                if (cancelRequested) { AppendConsoleText("  Timer cancelled.\n", ThemeManager.Current.WarningColor); return; }
                AppendConsoleText("  " + i + "...\r", ThemeManager.Current.TextColor);
                System.Threading.Thread.Sleep(1000);
            }
            AppendConsoleText("  Time's up!\n", ThemeManager.Current.WarningColor);

            // Beep notification
            try { Console.Beep(800, 500); } catch { }
        }

        #endregion

        #region v1.0.2 — Session Logging

        private void CmdLog(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            if (args.Length == 0)
            {
                if (logWriter != null)
                    AppendConsoleText("  Logging active: " + logFilePath + "\n", t.InfoColor);
                else
                    AppendConsoleText("Usage: log start [file] | log stop\n", t.WarningColor);
                return;
            }

            string action = args[0].ToLower();
            if (action == "start")
            {
                if (logWriter != null)
                {
                    AppendConsoleText("  Already logging to: " + logFilePath + "\n", t.WarningColor);
                    return;
                }
                logFilePath = args.Length >= 2 ? ResolvePath(args[1]) : ResolvePath("session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                try
                {
                    logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8);
                    logWriter.AutoFlush = true;
                    logWriter.WriteLine("=== KocurConsole Session Log — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
                    AppendConsoleText("  Logging started: " + logFilePath + "\n", t.InfoColor);
                }
                catch (Exception ex)
                {
                    AppendConsoleText("  Error: " + ex.Message + "\n", t.ErrorColor);
                    logWriter = null;
                    logFilePath = null;
                }
            }
            else if (action == "stop")
            {
                if (logWriter != null)
                {
                    logWriter.WriteLine("=== Session ended — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
                    logWriter.Close();
                    logWriter = null;
                    AppendConsoleText("  Logging stopped: " + logFilePath + "\n", t.InfoColor);
                    logFilePath = null;
                }
                else
                {
                    AppendConsoleText("  No active log session.\n", t.WarningColor);
                }
            }
            else
            {
                AppendConsoleText("Usage: log start [file] | log stop\n", t.WarningColor);
            }
        }

        #endregion

        #region v1.0.2 — Hash, Curl, Df, Write

        private void CmdHash(string[] args)
        {
            if (args.Length < 2) { AppendConsoleText("Usage: hash <md5|sha256> <text>\n", ThemeManager.Current.WarningColor); return; }
            string algo = args[0].ToLower();
            string text = string.Join(" ", args.Skip(1));

            try
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(text);
                byte[] hash;

                if (algo == "md5")
                {
                    using (var h = MD5.Create()) hash = h.ComputeHash(inputBytes);
                }
                else if (algo == "sha256")
                {
                    using (var h = SHA256.Create()) hash = h.ComputeHash(inputBytes);
                }
                else
                {
                    AppendConsoleText("Supported: md5, sha256\n", ThemeManager.Current.WarningColor);
                    return;
                }

                string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                AppendConsoleText("  " + algo.ToUpper() + ": " + hex + "\n", ThemeManager.Current.TextColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdCurl(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: curl <url>\n", ThemeManager.Current.WarningColor); return; }
            string url = args[0];
            if (!url.StartsWith("http")) url = "http://" + url;

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "KocurConsole/" + "1.0.2");
                    string content = client.DownloadString(url);

                    // Truncate to 5000 chars for display
                    if (content.Length > 5000)
                    {
                        AppendConsoleText(content.Substring(0, 5000) + "\n", ThemeManager.Current.TextColor);
                        AppendConsoleText("[... truncated at 5000 chars, total: " + content.Length + "]\n", ThemeManager.Current.WarningColor);
                    }
                    else
                    {
                        AppendConsoleText(content + "\n", ThemeManager.Current.TextColor);
                    }
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        private void CmdDf()
        {
            ConsoleTheme t = ThemeManager.Current;
            AppendConsoleText("\n  " + "Drive".PadRight(8) + "Total".PadRight(12) + "Used".PadRight(12) + "Free".PadRight(12) + "Usage\n", t.AccentColor);
            AppendConsoleText("  " + new string('-', 56) + "\n", t.TextColor);

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    long total = drive.TotalSize;
                    long free = drive.AvailableFreeSpace;
                    long used = total - free;
                    int pct = (int)((double)used / total * 100);
                    string bar = "[" + new string('#', pct / 5) + new string('-', 20 - pct / 5) + "]";

                    AppendConsoleText("  " + drive.Name.PadRight(8), t.TextColor);
                    AppendConsoleText(FormatFileSize(total).PadRight(12), t.TextColor);
                    AppendConsoleText(FormatFileSize(used).PadRight(12), t.TextColor);
                    AppendConsoleText(FormatFileSize(free).PadRight(12), t.TextColor);
                    Color barColor = pct > 90 ? t.ErrorColor : pct > 70 ? t.WarningColor : t.InfoColor;
                    AppendConsoleText(bar + " " + pct + "%\n", barColor);
                }
                catch { }
            }
            AppendConsoleText("\n", t.TextColor);
        }

        private void CmdWrite(string[] args)
        {
            if (args.Length < 2) { AppendConsoleText("Usage: write <file> <text>\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            string text = string.Join(" ", args.Skip(1));

            try
            {
                File.AppendAllText(path, text + Environment.NewLine, Encoding.UTF8);
                AppendConsoleText("Written to: " + path + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        #endregion

        #region v1.0.2 — Aliases & Bookmarks Persistence

        private void LoadAliasesAndBookmarks()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KocurConsole");
                string aliasFile = Path.Combine(dir, "aliases.txt");
                string bookmarkFile = Path.Combine(dir, "bookmarks.txt");

                if (File.Exists(aliasFile))
                {
                    foreach (string line in File.ReadAllLines(aliasFile))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) aliases[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                }

                if (File.Exists(bookmarkFile))
                {
                    foreach (string line in File.ReadAllLines(bookmarkFile))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) bookmarks[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                }
            }
            catch { }
        }

        private void SaveAliasesAndBookmarks()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KocurConsole");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string aliasFile = Path.Combine(dir, "aliases.txt");
                string bookmarkFile = Path.Combine(dir, "bookmarks.txt");

                File.WriteAllLines(aliasFile, aliases.Select(kv => kv.Key + "=" + kv.Value));
                File.WriteAllLines(bookmarkFile, bookmarks.Select(kv => kv.Key + "=" + kv.Value));
            }
            catch { }
        }

        #endregion

        #region v1.0.3 — Preview (Paginated File Viewer)

        private void CmdPreview(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: preview <file>\n", ThemeManager.Current.WarningColor); return; }
            string path = ResolvePath(args[0]);
            if (!File.Exists(path)) { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); return; }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                string ext = Path.GetExtension(path).ToLower();
                int pageSize = 25;
                int offset = 0;
                ConsoleTheme t = ThemeManager.Current;

                while (!cancelRequested)
                {
                    int end = Math.Min(offset + pageSize, lines.Length);
                    AppendConsoleText("\n", t.TextColor);
                    for (int i = offset; i < end; i++)
                    {
                        AppendConsoleText((i + 1).ToString().PadLeft(4) + " | ", t.AccentColor);
                        PrintSyntaxHighlighted(lines[i], ext, t);
                        AppendConsoleText("\n", t.TextColor);
                    }

                    if (end >= lines.Length)
                    {
                        AppendConsoleText("\n  [END - " + lines.Length + " lines]\n", t.WarningColor);
                        break;
                    }

                    AppendConsoleText("\n  Lines " + (offset + 1) + "-" + end + " of " + lines.Length + " | Press Enter=next, q=quit\n", t.InfoColor);
                    // Simple wait - the async wrapper handles this
                    System.Threading.Thread.Sleep(100);

                    // Auto-advance to next page
                    offset = end;
                    if (offset >= lines.Length) break;
                }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
        }

        #endregion

        #region v1.0.3 — Plugin Commands

        private void CmdPlugin(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            string action = args.Length > 0 ? args[0].ToLower() : "list";

            if (action == "list")
            {
                string[] names = PluginManager.GetPluginNames();
                if (names.Length == 0)
                {
                    AppendConsoleText("  No plugins loaded.\n", t.WarningColor);
                    AppendConsoleText("  Plugin dir: " + PluginManager.PluginDirectory + "\n", t.TextColor);
                    AppendConsoleText("  Use: plugin create — to create an example plugin\n", t.TextColor);
                }
                else
                {
                    AppendConsoleText("\n  Loaded plugins:\n\n", t.InfoColor);
                    foreach (string name in names)
                        AppendConsoleText("  " + name + "\n", t.TextColor);
                    AppendConsoleText("\n  Dir: " + PluginManager.PluginDirectory + "\n\n", t.AccentColor);
                }
            }
            else if (action == "reload")
            {
                int count = PluginManager.LoadAll();
                AppendConsoleText("  Reloaded " + count + " plugin(s).\n", t.InfoColor);
            }
            else if (action == "create")
            {
                PluginManager.CreateExample();
                AppendConsoleText("  Example plugin created: " + Path.Combine(PluginManager.PluginDirectory, "hello.cs") + "\n", t.InfoColor);
                AppendConsoleText("  Run 'plugin reload' then type 'hello' to test!\n", t.TextColor);
            }
            else if (action == "dir")
            {
                try
                {
                    Process.Start("explorer.exe", PluginManager.PluginDirectory);
                }
                catch { }
            }
            else
            {
                AppendConsoleText("Usage: plugin [list | reload | create | dir]\n", t.WarningColor);
            }
        }

        #endregion

        #region v1.0.3 — Pinned Commands (F1-F12)

        private void CmdPin(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            if (args.Length < 2)
            {
                // Show current pins
                AppendConsoleText("\n  Pinned commands:\n\n", t.InfoColor);
                bool any = false;
                for (int i = 1; i <= 12; i++)
                {
                    Keys key = (Keys)((int)Keys.F1 + i - 1);
                    if (pinnedCommands.ContainsKey(key))
                    {
                        AppendConsoleText("  F" + i.ToString().PadRight(3) + pinnedCommands[key] + "\n", t.TextColor);
                        any = true;
                    }
                }
                if (!any) AppendConsoleText("  None. Use: pin <F1-F12> <command>\n", t.WarningColor);
                AppendConsoleText("\n", t.TextColor);
                return;
            }

            string keyStr = args[0].ToUpper();
            string cmd = string.Join(" ", args.Skip(1));

            if (keyStr.StartsWith("F") && int.TryParse(keyStr.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
            {
                Keys key = (Keys)((int)Keys.F1 + fNum - 1);
                pinnedCommands[key] = cmd;
                SavePinnedCommands();
                AppendConsoleText("  " + keyStr + " -> " + cmd + "\n", t.InfoColor);
            }
            else
            {
                AppendConsoleText("Usage: pin <F1-F12> <command>\n", t.WarningColor);
            }
        }

        private void CmdUnpin(string[] args)
        {
            if (args.Length == 0) { AppendConsoleText("Usage: unpin <F1-F12>\n", ThemeManager.Current.WarningColor); return; }
            string keyStr = args[0].ToUpper();
            if (keyStr.StartsWith("F") && int.TryParse(keyStr.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
            {
                Keys key = (Keys)((int)Keys.F1 + fNum - 1);
                pinnedCommands.Remove(key);
                SavePinnedCommands();
                AppendConsoleText("  Unpinned " + keyStr + "\n", ThemeManager.Current.InfoColor);
            }
        }

        private void LoadPinnedCommands()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KocurConsole", "pins.txt");
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        string keyStr = line.Substring(0, eq);
                        string cmd = line.Substring(eq + 1);
                        if (Enum.TryParse(keyStr, out Keys key))
                            pinnedCommands[key] = cmd;
                    }
                }
            }
            catch { }
        }

        private void SavePinnedCommands()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KocurConsole");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "pins.txt");
                File.WriteAllLines(path, pinnedCommands.Select(kv => kv.Key.ToString() + "=" + kv.Value));
            }
            catch { }
        }

        #endregion

        #region v1.0.3 — .kocurrc Task Runner

        private void RunKocurrc()
        {
            try
            {
                string[] rcPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kocurrc"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KocurConsole", ".kocurrc")
                };

                foreach (string rcPath in rcPaths)
                {
                    if (File.Exists(rcPath))
                    {
                        string[] lines = File.ReadAllLines(rcPath);
                        suppressPrompt = true;
                        foreach (string line in lines)
                        {
                            string trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                            ExecuteCommand(trimmed);
                        }
                        suppressPrompt = false;
                        break;
                    }
                }
            }
            catch { suppressPrompt = false; }
        }

        private void CmdRc(string[] args)
        {
            ConsoleTheme t = ThemeManager.Current;
            string rcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kocurrc");

            if (args.Length == 0)
            {
                // Show rc file
                if (File.Exists(rcPath))
                {
                    AppendConsoleText("\n  .kocurrc (" + rcPath + "):\n\n", t.InfoColor);
                    foreach (string line in File.ReadAllLines(rcPath))
                        AppendConsoleText("  " + line + "\n", t.TextColor);
                    AppendConsoleText("\n", t.TextColor);
                }
                else
                {
                    AppendConsoleText("  No .kocurrc found. Use: rc edit\n", t.WarningColor);
                    AppendConsoleText("  Path: " + rcPath + "\n", t.TextColor);
                }
                return;
            }

            string action = args[0].ToLower();
            if (action == "edit")
            {
                if (!File.Exists(rcPath))
                {
                    File.WriteAllText(rcPath, "# KocurConsole startup commands\n# Lines starting with # are comments\n# Example:\n# theme dracula\n# echo Welcome back!\n");
                }
                Process.Start("notepad.exe", rcPath);
                AppendConsoleText("  Opened in Notepad: " + rcPath + "\n", t.InfoColor);
            }
            else if (action == "run")
            {
                RunKocurrc();
                AppendConsoleText("  Executed .kocurrc\n", t.InfoColor);
            }
            else
            {
                AppendConsoleText("Usage: rc [edit | run]\n", t.WarningColor);
            }
        }

        #endregion

        #region CMD/PowerShell Fallback

        private void RunExternalCommand(string command)
        {
            cmdRunning = true;
            cancelRequested = false;
            AppSettings s = SettingsManager.Current;
            AppendConsoleText("[" + s.Shell.ToUpper() + "] " + command + "\n", ThemeManager.Current.WarningColor);
            cmdHandler.ExecuteAsync(command, currentDirectory, s.Shell, s.ShellTimeout);
        }

        #endregion

        #region Helpers

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(currentDirectory, path));
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1073741824L) return (bytes / 1073741824.0).ToString("F1") + " GB";
            if (bytes >= 1048576L) return (bytes / 1048576.0).ToString("F1") + " MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("F1") + " KB";
            return bytes + " B";
        }

        #endregion
    }
}
