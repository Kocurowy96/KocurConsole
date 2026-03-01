using System;
using System.Collections.Generic;
using System.Drawing;

namespace KocurConsole
{
    public class ConsoleTheme
    {
        public string Name { get; set; }
        public Color BackgroundColor { get; set; }
        public Color TextColor { get; set; }
        public Color AccentColor { get; set; }
        public Color PromptColor { get; set; }
        public Color ErrorColor { get; set; }
        public Color WarningColor { get; set; }
        public Color InfoColor { get; set; }

        public ConsoleTheme(string name, string bg, string text, string accent, string prompt, string error, string warning, string info)
        {
            Name = name;
            BackgroundColor = ColorTranslator.FromHtml(bg);
            TextColor = ColorTranslator.FromHtml(text);
            AccentColor = ColorTranslator.FromHtml(accent);
            PromptColor = ColorTranslator.FromHtml(prompt);
            ErrorColor = ColorTranslator.FromHtml(error);
            WarningColor = ColorTranslator.FromHtml(warning);
            InfoColor = ColorTranslator.FromHtml(info);
        }
    }

    public static class ThemeManager
    {
        private static Dictionary<string, ConsoleTheme> themes = new Dictionary<string, ConsoleTheme>(StringComparer.OrdinalIgnoreCase);
        private static ConsoleTheme currentTheme;

        static ThemeManager()
        {
            // Default — dark terminal
            Register(new ConsoleTheme("default",    "#0C0C0C", "#CCCCCC", "#00BFFF", "#00FF00", "#FF4444", "#FFD700", "#00CED1"));
            // Dracula
            Register(new ConsoleTheme("dracula",    "#282A36", "#F8F8F2", "#BD93F9", "#50FA7B", "#FF5555", "#F1FA8C", "#8BE9FD"));
            // Monokai
            Register(new ConsoleTheme("monokai",    "#272822", "#F8F8F2", "#F92672", "#A6E22E", "#F92672", "#E6DB74", "#66D9EF"));
            // Nord
            Register(new ConsoleTheme("nord",       "#2E3440", "#D8DEE9", "#88C0D0", "#A3BE8C", "#BF616A", "#EBCB8B", "#88C0D0"));
            // Gruvbox
            Register(new ConsoleTheme("gruvbox",    "#282828", "#EBDBB2", "#FE8019", "#B8BB26", "#CC241D", "#FABD2F", "#83A598"));
            // Solarized Dark
            Register(new ConsoleTheme("solarized",  "#002B36", "#839496", "#268BD2", "#859900", "#DC322F", "#B58900", "#2AA198"));
            // Matrix
            Register(new ConsoleTheme("matrix",     "#000000", "#00FF00", "#00FF00", "#00FF00", "#FF0000", "#FFFF00", "#00FF00"));
            // Catppuccin Mocha
            Register(new ConsoleTheme("catppuccin", "#1E1E2E", "#CDD6F4", "#CBA6F7", "#A6E3A1", "#F38BA8", "#F9E2AF", "#89DCEB"));

            currentTheme = themes["default"];
        }

        private static void Register(ConsoleTheme theme)
        {
            themes[theme.Name] = theme;
        }

        public static ConsoleTheme Current
        {
            get { return currentTheme; }
        }

        public static bool SetTheme(string name)
        {
            ConsoleTheme theme;
            if (themes.TryGetValue(name, out theme))
            {
                currentTheme = theme;
                return true;
            }
            return false;
        }

        public static List<string> GetThemeNames()
        {
            return new List<string>(themes.Keys);
        }

        public static ConsoleTheme GetTheme(string name)
        {
            ConsoleTheme theme;
            themes.TryGetValue(name, out theme);
            return theme;
        }
    }
}
