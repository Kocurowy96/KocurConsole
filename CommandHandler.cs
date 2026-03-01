using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KocurConsole
{
    public class CommandHandler
    {
        private Process currentProcess;
        private readonly object processLock = new object();

        /// <summary>
        /// Event fired when output is received from the external process.
        /// </summary>
        public event Action<string, bool> OutputReceived; // text, isError

        /// <summary>
        /// Event fired when the external process completes.
        /// </summary>
        public event Action<int> ProcessCompleted; // exit code

        /// <summary>
        /// Execute a command in cmd.exe or powershell.exe asynchronously.
        /// </summary>
        public void ExecuteAsync(string command, string workingDirectory, string shell, int timeoutSeconds)
        {
            Task.Run(() =>
            {
                try
                {
                    string fileName;
                    string arguments;

                    if (shell.Equals("powershell", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = "powershell.exe";
                        arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"";
                    }
                    else
                    {
                        fileName = "cmd.exe";
                        arguments = "/c " + command;
                    }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = workingDirectory,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    Process process = new Process();
                    process.StartInfo = psi;
                    process.EnableRaisingEvents = true;

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            OutputReceived?.Invoke(e.Data + "\n", false);
                        }
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            OutputReceived?.Invoke(e.Data + "\n", true);
                        }
                    };

                    lock (processLock)
                    {
                        currentProcess = process;
                    }

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool exited = process.WaitForExit(timeoutSeconds * 1000);

                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        OutputReceived?.Invoke("[Timeout: process killed after " + timeoutSeconds + "s]\n", true);
                    }

                    // Ensure all async output is flushed
                    process.WaitForExit();

                    int exitCode = process.ExitCode;

                    lock (processLock)
                    {
                        currentProcess = null;
                    }

                    ProcessCompleted?.Invoke(exitCode);
                }
                catch (Exception ex)
                {
                    OutputReceived?.Invoke("Error executing command: " + ex.Message + "\n", true);
                    ProcessCompleted?.Invoke(-1);
                }
            });
        }

        /// <summary>
        /// Cancel the currently running process.
        /// </summary>
        public void Cancel()
        {
            lock (processLock)
            {
                if (currentProcess != null && !currentProcess.HasExited)
                {
                    try
                    {
                        currentProcess.Kill();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Returns true if a process is currently running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (processLock)
                {
                    return currentProcess != null && !currentProcess.HasExited;
                }
            }
        }
    }
}
