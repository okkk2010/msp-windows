using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

public static class SettingsManager
{
    private static readonly string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlaySettings.json");
    private static readonly string paletteFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "colorPalette.json");

    public static Color SelectedOverlayColor { get; set; } = Color.FromArgb(255, 192, 0, 0);

    public static Color LoadOverlayColor()
    {
        if (File.Exists(settingsFile)) {
            try {
                string json = File.ReadAllText(settingsFile);
                int argb;
                if (TryExtractInt(json, "ColorArgb", out argb) || TryExtractInt(json, "SelectedOverlayColorArgb", out argb)) {
                    SelectedOverlayColor = Color.FromArgb(argb);
                    return SelectedOverlayColor;
                }
            } catch { }
        }

        SelectedOverlayColor = Color.FromArgb(255, 192, 0, 0);
        return SelectedOverlayColor;
    }

    public static void SaveOverlayColor(Color color)
    {
        SelectedOverlayColor = color;
        string json = "{\n  \"ColorArgb\": " + color.ToArgb() + "\n}";
        File.WriteAllText(settingsFile, json);
    }

    public static List<Color> LoadColorPalette()
    {
        if (File.Exists(paletteFile)) {
            try {
                string json = File.ReadAllText(paletteFile);
                string[] values;
                if (TryExtractIntArray(json, "Colors", out values) || TryExtractIntArray(json, "SavedColors", out values)) {
                    List<Color> palette = new List<Color>();
                    foreach (string value in values) {
                        int argb;
                        if (int.TryParse(value.Trim(), out argb)) {
                            palette.Add(Color.FromArgb(argb));
                        }
                    }

                    return palette;
                }
            } catch { }
        }
        return new List<Color>();
    }

    public static void SaveColorPalette(List<Color> palette)
    {
        List<string> argbValues = new List<string>();
        foreach (Color color in palette) {
            argbValues.Add(color.ToArgb().ToString());
        }

        string json = "{\n  \"Colors\": [" + string.Join(", ", argbValues.ToArray()) + "]\n}";
        File.WriteAllText(paletteFile, json);
    }

    private static bool TryExtractInt(string json, string key, out int value)
    {
        Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?\\d+)");
        if (match.Success) {
            return int.TryParse(match.Groups[1].Value, out value);
        }

        value = 0;
        return false;
    }

    private static bool TryExtractIntArray(string json, string key, out string[] values)
    {
        Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (match.Success) {
            string raw = match.Groups[1].Value.Trim();
            if (raw.Length == 0) {
                values = new string[0];
                return true;
            }

            values = raw.Split(',');
            return true;
        }

        values = null;
        return false;
    }
}
