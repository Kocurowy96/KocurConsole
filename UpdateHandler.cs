using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace KocurConsole
{
    [DataContract]
    public class VersionManifest
    {
        [DataMember(Name = "version")]
        public string Version { get; set; }

        [DataMember(Name = "releaseDate")]
        public string ReleaseDate { get; set; }

        [DataMember(Name = "downloadUrl")]
        public string DownloadUrl { get; set; }

        [DataMember(Name = "releasePage")]
        public string ReleasePage { get; set; }

        [DataMember(Name = "changelog")]
        public string Changelog { get; set; }
    }

    public static class UpdateHandler
    {
        private const string ManifestUrl = "https://raw.githubusercontent.com/Kocurowy96/KocurConsole/main/version_manifest.json";

        /// <summary>
        /// Fetch the remote version manifest from GitHub.
        /// </summary>
        public static VersionManifest CheckForUpdate()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "KocurConsole-Updater");
                    client.Headers.Add("Cache-Control", "no-cache");
                    string json = client.DownloadString(ManifestUrl);

                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(VersionManifest));
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        return (VersionManifest)serializer.ReadObject(ms);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Compare two version strings (e.g. "1.0.1" vs "1.0.0").
        /// Returns true if remote is newer than local.
        /// </summary>
        public static bool IsNewerVersion(string localVersion, string remoteVersion)
        {
            try
            {
                Version local = new Version(localVersion);
                Version remote = new Version(remoteVersion);
                return remote > local;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Download the new version exe to a temp file.
        /// Returns the temp file path, or null on failure.
        /// </summary>
        public static string DownloadUpdate(VersionManifest manifest, Action<int> progressCallback)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "KocurConsole_update.exe");

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "KocurConsole-Updater");
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        progressCallback?.Invoke(e.ProgressPercentage);
                    };

                    // Synchronous download (called from background thread via RunAsync)
                    client.DownloadFile(manifest.DownloadUrl, tempPath);
                }

                // Verify the file exists and has reasonable size
                FileInfo fi = new FileInfo(tempPath);
                if (fi.Exists && fi.Length > 10000) // At least 10KB
                {
                    return tempPath;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Apply the update by creating a batch script that:
        /// 1. Waits for the current process to exit
        /// 2. Copies the new exe over the old one
        /// 3. Launches the new exe
        /// 4. Deletes itself
        /// </summary>
        public static void ApplyUpdate(string newExePath)
        {
            string currentExe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string currentDir = Path.GetDirectoryName(currentExe);
            string batPath = Path.Combine(Path.GetTempPath(), "KocurConsole_updater.bat");

            StringBuilder bat = new StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("echo KocurConsole Updater - please wait...");
            bat.AppendLine("timeout /t 3 /nobreak >nul");
            bat.AppendLine("echo Updating...");
            bat.AppendLine("copy /Y \"" + newExePath + "\" \"" + currentExe + "\"");
            bat.AppendLine("if errorlevel 1 (");
            bat.AppendLine("  echo Update failed! Could not copy file.");
            bat.AppendLine("  echo Try running as Administrator.");
            bat.AppendLine("  pause");
            bat.AppendLine("  goto :eof");
            bat.AppendLine(")");
            bat.AppendLine("echo Update complete!");
            bat.AppendLine("start \"\" \"" + currentExe + "\"");
            bat.AppendLine("del \"" + newExePath + "\" >nul 2>&1");
            bat.AppendLine("del \"%~f0\" >nul 2>&1");

            File.WriteAllText(batPath, bat.ToString(), Encoding.ASCII);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(psi);
        }
    }
}
