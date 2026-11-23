using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NoActivateClick
{
    internal sealed class AppConfig
    {
        public bool Enabled;
        public bool WheelRedirect;
        public bool RealDragHandover = true;
        public bool BorrowOnTextClick = true;
        public bool KeepFront = true;
        public bool AutoReleaseIdle = true;
        public bool Sound = true;
        public bool DpiScaling = true;
        public HotkeyBinding Hotkey = HotkeyBinding.Default();

        public static string FilePath
        {
            get { return Path.Combine(Log.DataDirectory, "config.ini"); }
        }

        public static AppConfig Load(out bool firstRun)
        {
            AppConfig config = new AppConfig();
            string path = FilePath;
            firstRun = !File.Exists(path);
            if (firstRun) return config;

            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim().ToLowerInvariant();
                    string value = line.Substring(split + 1).Trim();
                    switch (key)
                    {
                        case "enabled": config.Enabled = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                        case "wheel": config.WheelRedirect = value == "1"; break;
                        case "realdrag": config.RealDragHandover = value != "0"; break;
                        case "borrowtext": config.BorrowOnTextClick = value != "0"; break;
                        case "keepfront": config.KeepFront = value == "1"; break;
                        case "idle": config.AutoReleaseIdle = value != "0"; break;
                        case "sound": config.Sound = value == "1"; break;
                        case "dpi": config.DpiScaling = value != "0"; break;
                        case "hotkey":
                            HotkeyBinding binding;
                            if (HotkeyBinding.TryParse(value, out binding) && binding.IsValid) config.Hotkey = binding;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("config load", ex);
            }
            return config;
        }

        public void Save()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("# NoActivateClick settings");
                lines.Add("enabled=" + (Enabled ? "1" : "0"));
                lines.Add("wheel=" + (WheelRedirect ? "1" : "0"));
                lines.Add("realdrag=" + (RealDragHandover ? "1" : "0"));
                lines.Add("borrowtext=" + (BorrowOnTextClick ? "1" : "0"));
                lines.Add("keepfront=" + (KeepFront ? "1" : "0"));
                lines.Add("idle=" + (AutoReleaseIdle ? "1" : "0"));
                lines.Add("sound=" + (Sound ? "1" : "0"));
                lines.Add("dpi=" + (DpiScaling ? "1" : "0"));
                lines.Add("hotkey=" + Hotkey.ToText());
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("config save", ex);
            }
        }
    }
}