using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace KocurConsole
{
    [DataContract]
    public class AppSettings
    {
        [DataMember(Name = "theme")]
        public string Theme { get; set; }

        [DataMember(Name = "fontFamily")]
        public string FontFamily { get; set; }

        [DataMember(Name = "fontSize")]
        public float FontSize { get; set; }

        [DataMember(Name = "wordWrap")]
        public bool WordWrap { get; set; }

        [DataMember(Name = "showTimestamps")]
        public bool ShowTimestamps { get; set; }

        [DataMember(Name = "autoScroll")]
        public bool AutoScroll { get; set; }

        [DataMember(Name = "shell")]
        public string Shell { get; set; }

        [DataMember(Name = "shellTimeout")]
        public int ShellTimeout { get; set; }

        public AppSettings()
        {
            Theme = "default";
            FontFamily = "Consolas";
            FontSize = 12f;
            WordWrap = true;
            ShowTimestamps = true;
            AutoScroll = true;
            Shell = "cmd";
            ShellTimeout = 30;
        }
    }

    public static class SettingsManager
    {
        private static AppSettings settings;
        private static string settingsFilePath;

        static SettingsManager()
        {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KocurConsole");

            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            settingsFilePath = Path.Combine(appDataFolder, "settings.json");
            settings = Load();
        }

        public static AppSettings Current
        {
            get { return settings; }
        }

        public static void Save()
        {
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                using (MemoryStream ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, settings);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(settingsFilePath, json, Encoding.UTF8);
                }
            }
            catch { }
        }

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath, Encoding.UTF8);
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        AppSettings loaded = (AppSettings)serializer.ReadObject(ms);
                        if (loaded != null) return loaded;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        public static void Reset()
        {
            settings = new AppSettings();
            Save();
        }

        /// <summary>
        /// Set a setting by key name. Returns true if the key was recognized.
        /// </summary>
        public static bool Set(string key, string value)
        {
            switch (key.ToLower())
            {
                case "theme":
                    if (ThemeManager.SetTheme(value))
                    {
                        settings.Theme = value;
                        Save();
                        return true;
                    }
                    return false;
                case "font":
                case "fontfamily":
                    settings.FontFamily = value;
                    Save();
                    return true;
                case "fontsize":
                    float size;
                    if (float.TryParse(value, out size) && size >= 6 && size <= 72)
                    {
                        settings.FontSize = size;
                        Save();
                        return true;
                    }
                    return false;
                case "wordwrap":
                case "wrap":
                    bool wrap;
                    if (bool.TryParse(value, out wrap))
                    {
                        settings.WordWrap = wrap;
                        Save();
                        return true;
                    }
                    return false;
                case "timestamps":
                case "showtimestamps":
                    bool ts;
                    if (bool.TryParse(value, out ts))
                    {
                        settings.ShowTimestamps = ts;
                        Save();
                        return true;
                    }
                    return false;
                case "autoscroll":
                    bool scroll;
                    if (bool.TryParse(value, out scroll))
                    {
                        settings.AutoScroll = scroll;
                        Save();
                        return true;
                    }
                    return false;
                case "shell":
                    string shellLower = value.ToLower();
                    if (shellLower == "cmd" || shellLower == "powershell")
                    {
                        settings.Shell = shellLower;
                        Save();
                        return true;
                    }
                    return false;
                case "timeout":
                case "shelltimeout":
                    int timeout;
                    if (int.TryParse(value, out timeout) && timeout >= 5 && timeout <= 300)
                    {
                        settings.ShellTimeout = timeout;
                        Save();
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns a dictionary of all current settings for display.
        /// </summary>
        public static Dictionary<string, string> GetAll()
        {
            var dict = new Dictionary<string, string>();
            dict["theme"] = settings.Theme;
            dict["fontFamily"] = settings.FontFamily;
            dict["fontSize"] = settings.FontSize.ToString();
            dict["wordWrap"] = settings.WordWrap.ToString();
            dict["showTimestamps"] = settings.ShowTimestamps.ToString();
            dict["autoScroll"] = settings.AutoScroll.ToString();
            dict["shell"] = settings.Shell;
            dict["shellTimeout"] = settings.ShellTimeout.ToString() + "s";
            return dict;
        }
    }
}
