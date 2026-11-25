using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FreeCrosshair
{
    public class Profile
    {
        public string Name;
        public CrosshairSettings Settings;
        public Profile(string name, CrosshairSettings s) { Name = name; Settings = s; }
        public override string ToString() { return Name; }
    }

    /// <summary>App-wide settings + all profiles. Stored in %APPDATA%\FreeCrosshair\config.ini.</summary>
    public class AppConfig
    {
        public List<Profile> Profiles = new List<Profile>();
        public string ActiveProfile = "Default";
        public Keys ToggleKey = Keys.F8;
        public Keys NextProfileKey = Keys.F9;
        public Keys SettingsKey = Keys.F10;
        public int MonitorIndex = 0; // index into Screen.AllScreens, 0 = primary
        public bool Visible = true;
        public bool FirstRun = true;

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FreeCrosshair"); }
        }
        public static string FilePath { get { return Path.Combine(Folder, "config.ini"); } }

        public Profile Active
        {
            get
            {
                var p = Profiles.FirstOrDefault(x => x.Name == ActiveProfile);
                if (p == null) { p = Profiles[0]; ActiveProfile = p.Name; }
                return p;
            }
        }

        public Screen TargetScreen
        {
            get
            {
                var all = OrderedScreens();
                if (MonitorIndex < 0 || MonitorIndex >= all.Length) return Screen.PrimaryScreen;
                return all[MonitorIndex];
            }
        }

        /// <summary>Primary screen first, then the rest left-to-right.</summary>
        public static Screen[] OrderedScreens()
        {
            return Screen.AllScreens.OrderBy(s => s.Primary ? 0 : 1).ThenBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();
        }

        public bool HasProfile(string name)
        {
            return Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public string UniqueName(string baseName)
        {
            if (!HasProfile(baseName)) return baseName;
            for (int i = 2; ; i++)
            {
                string n = baseName + " " + i;
                if (!HasProfile(n)) return n;
            }
        }

        // ---------- defaults ----------

        public static AppConfig CreateDefault()
        {
            var c = new AppConfig();
            c.Profiles.Add(new Profile("Default", new CrosshairSettings()));
            c.Profiles.Add(new Profile("Cyan dot", new CrosshairSettings
            {
                Style = CrosshairStyle.Dot, Color = Color.FromArgb(0, 255, 255), DotSize = 4, RoundDot = true
            }));
            c.Profiles.Add(new Profile("Red circle", new CrosshairSettings
            {
                Style = CrosshairStyle.Circle, Color = Color.FromArgb(255, 40, 40), CircleRadius = 12, Thickness = 2,
                CenterDot = true, DotSize = 2
            }));
            c.Profiles.Add(new Profile("Big white cross", new CrosshairSettings
            {
                Style = CrosshairStyle.Cross, Color = Color.White, Length = 10, Thickness = 2, Gap = 5, CenterDot = true, DotSize = 2
            }));
            c.ActiveProfile = "Default";
            return c;
        }

        // ---------- load / save ----------

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return CreateDefault();
                var c = new AppConfig();
                Profile current = null;
                bool inApp = false;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string section = line.Substring(1, line.Length - 2);
                        inApp = section == "App";
                        current = null;
                        if (section.StartsWith("Profile:"))
                        {
                            string name = section.Substring("Profile:".Length).Trim();
                            if (name.Length > 0 && !c.HasProfile(name))
                            {
                                current = new Profile(name, new CrosshairSettings());
                                c.Profiles.Add(current);
                            }
                        }
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (current != null) current.Settings.Set(key, val);
                    else if (inApp) c.SetApp(key, val);
                }
                if (c.Profiles.Count == 0) return CreateDefault();
                foreach (var p in c.Profiles) p.Settings.Clamp();
                var _ = c.Active; // fixes ActiveProfile if it points nowhere
                return c;
            }
            catch
            {
                return CreateDefault();
            }
        }

        void SetApp(string key, string val)
        {
            try
            {
                switch (key)
                {
                    case "ActiveProfile": ActiveProfile = val; break;
                    case "ToggleKey": ToggleKey = (Keys)Enum.Parse(typeof(Keys), val, true); break;
                    case "NextProfileKey": NextProfileKey = (Keys)Enum.Parse(typeof(Keys), val, true); break;
                    case "SettingsKey": SettingsKey = (Keys)Enum.Parse(typeof(Keys), val, true); break;
                    case "MonitorIndex": MonitorIndex = int.Parse(val); break;
                    case "Visible": Visible = val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case "FirstRun": FirstRun = val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var sb = new StringBuilder();
                sb.AppendLine("; Free Crosshair settings. You can edit this while the app is closed.");
                sb.AppendLine("[App]");
                sb.AppendLine("ActiveProfile=" + ActiveProfile);
                sb.AppendLine("ToggleKey=" + ToggleKey);
                sb.AppendLine("NextProfileKey=" + NextProfileKey);
                sb.AppendLine("SettingsKey=" + SettingsKey);
                sb.AppendLine("MonitorIndex=" + MonitorIndex);
                sb.AppendLine("Visible=" + (Visible ? "true" : "false"));
                sb.AppendLine("FirstRun=" + (FirstRun ? "true" : "false"));
                foreach (var p in Profiles)
                {
                    sb.AppendLine();
                    sb.AppendLine("[Profile:" + p.Name + "]");
                    foreach (var kv in p.Settings.ToPairs()) sb.AppendLine(kv.Key + "=" + kv.Value);
                }
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch { }
        }
    }
}
