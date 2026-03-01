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

namespace KocurConsole
{
    public partial class Form1 : Form
    {
        public string terminalName = "KocurConsole Terminal";
        public string terminalVersion = "1.0.0";

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

        // All built-in command names (for autocomplete)
        private readonly string[] builtInCommands = new string[]
        {
            "help", "clear", "cls", "fastfetch", "systeminfo", "whoami", "date",
            "echo", "exit", "quit", "ls", "dir", "cd", "pwd", "mkdir", "rmdir",
            "rm", "del", "cp", "copy", "mv", "move", "cat", "type", "touch",
            "hostname", "ping", "history", "theme", "settings", "title", "color",
            "uptime", "env", "calc", "tree"
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

            AppendConsoleText(terminalName + " " + terminalVersion + " - Type 'help' for available commands\n\n", ThemeManager.Current.InfoColor);
            ShowPrompt();
            richTextBoxConsoleOutput.Focus();
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
            string[] parts = input.Split(' ');
            if (parts.Length > 1) return; // Don't autocomplete arguments

            string partial = parts[0].ToLower();
            if (string.IsNullOrEmpty(partial)) return;

            var matches = builtInCommands.Where(c => c.StartsWith(partial)).ToList();
            if (matches.Count == 1)
            {
                SetCurrentInput(matches[0] + " ");
            }
            else if (matches.Count > 1)
            {
                string savedInput = GetCurrentInput();
                AppendConsoleText("\n  " + string.Join("  ", matches) + "\n", ThemeManager.Current.InfoColor);
                ShowPrompt();
                // Restore the partial input
                richTextBoxConsoleOutput.SelectionColor = ThemeManager.Current.TextColor;
                richTextBoxConsoleOutput.AppendText(savedInput);
                richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.TextLength;
            }
        }

        // Required by Designer.cs — inputCommands is hidden so this never fires
        private void inputCommands_KeyDown(object sender, KeyEventArgs e) { }

        // Marks Tab as an input key so it reaches KeyDown instead of switching focus
        private void RichTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
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

            string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

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

                // ── CMD/PowerShell Fallback ──
                default:
                    RunExternalCommand(command);
                    return;
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
            AppendConsoleText("  exit / quit       Close the application\n\n", t.TextColor);

            AppendConsoleText(" System:\n", t.AccentColor);
            AppendConsoleText("  fastfetch         System overview\n", t.TextColor);
            AppendConsoleText("  systeminfo        Detailed system info\n", t.TextColor);
            AppendConsoleText("  whoami            Current user\n", t.TextColor);
            AppendConsoleText("  hostname          Computer name\n", t.TextColor);
            AppendConsoleText("  date              Date & time\n", t.TextColor);
            AppendConsoleText("  uptime            System uptime\n\n", t.TextColor);

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
            AppendConsoleText("  tree              Directory tree\n\n", t.TextColor);

            AppendConsoleText(" Network:\n", t.AccentColor);
            AppendConsoleText("  ping <host>       Ping host (4 packets)\n\n", t.TextColor);

            AppendConsoleText(" Utility:\n", t.AccentColor);
            AppendConsoleText("  env [name]        Environment variables\n", t.TextColor);
            AppendConsoleText("  calc <expr>       Calculator\n\n", t.TextColor);

            AppendConsoleText(" Themes:\n", t.AccentColor);
            AppendConsoleText("  theme list        List themes\n", t.TextColor);
            AppendConsoleText("  theme <name>      Apply theme\n", t.TextColor);
            AppendConsoleText("  settings          Show settings\n", t.TextColor);
            AppendConsoleText("  settings set <k> <v>  Change setting\n", t.TextColor);
            AppendConsoleText("  settings reset    Reset defaults\n\n", t.TextColor);

            AppendConsoleText(" Unknown commands -> " + SettingsManager.Current.Shell + ".exe\n", t.WarningColor);
            AppendConsoleText(" Tab=autocomplete  Ctrl+C=cancel  Ctrl+L=clear  Esc=clear input\n\n", t.WarningColor);
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
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    int max = Math.Min(lines.Length, 500);
                    for (int i = 0; i < max; i++)
                    {
                        if (cancelRequested) return;
                        AppendConsoleText(lines[i] + "\n", ThemeManager.Current.TextColor);
                    }
                    if (lines.Length > 500)
                        AppendConsoleText("[... truncated, " + lines.Length + " total lines]\n", ThemeManager.Current.WarningColor);
                }
                else { AppendConsoleText("File not found: " + args[0] + "\n", ThemeManager.Current.ErrorColor); }
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
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
            if (args.Length == 0) { AppendConsoleText("Usage: calc <expr>\n", ThemeManager.Current.WarningColor); return; }
            try
            {
                string expr = string.Join(" ", args);
                DataTable dt = new DataTable();
                object result = dt.Compute(expr, "");
                AppendConsoleText("= " + result.ToString() + "\n", ThemeManager.Current.InfoColor);
            }
            catch (Exception ex) { AppendConsoleText("Error: " + ex.Message + "\n", ThemeManager.Current.ErrorColor); }
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

            AppendConsoleText("Usage: settings | settings set <key> <value> | settings reset\n", t.WarningColor);
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
