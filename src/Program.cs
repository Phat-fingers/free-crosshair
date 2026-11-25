using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FreeCrosshair
{
    static class Program
    {
        const string MutexName = "FreeCrosshair_SingleInstance_7f3c";
        const string ShowEventName = "FreeCrosshair_ShowSettings_7f3c";

        [STAThread]
        static int Main(string[] args)
        {
            try { NativeMethods.SetProcessDPIAware(); } catch { } // manifest already does per-monitor; this is a fallback
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Test hooks used by the GitHub build to check the app really draws on Windows.
            if (args.Length >= 2 && args[0] == "--render-test") return SelfTest.RenderAll(args[1]);
            if (args.Length >= 2 && args[0] == "--screenshot-test") return SelfTest.Screenshot(args[1]);
            if (args.Length >= 2 && args[0] == "--settings-test") return SelfTest.SettingsWindow(args[1]);

            bool minimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

            using (var mutex = new Mutex(true, MutexName, out bool first))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
            {
                if (!first)
                {
                    // Already running: ask the running copy to open its settings window instead of starting a second one.
                    showEvent.Set();
                    return 0;
                }
                var ctx = new CrosshairApp(AppConfig.Load(), showEvent, !minimized);
                Application.Run(ctx);
                GC.KeepAlive(mutex);
            }
            return 0;
        }
    }

    /// <summary>The running app: overlay + tray icon + hotkeys + settings window.</summary>
    public class CrosshairApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "FreeCrosshair";

        public readonly AppConfig Config;
        readonly OverlayForm overlay;
        readonly NotifyIcon tray;
        readonly Hotkeys hotkeys;
        readonly System.Windows.Forms.Timer saveTimer;
        readonly Control invoker;
        SettingsForm settingsForm;
        ToolStripMenuItem showItem, profilesMenu, startupItem;

        public event Action Changed; // fired when visibility / active profile changes from outside the settings window

        public CrosshairApp(AppConfig config, EventWaitHandle showEvent, bool openSettings)
        {
            Config = config;

            invoker = new Control();
            invoker.CreateControl();

            overlay = new OverlayForm();
            overlay.Apply(Config.Active.Settings, Config.TargetScreen);

            tray = new NotifyIcon
            {
                Icon = AppIcon.Create(),
                Text = "Free Crosshair",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
            tray.DoubleClick += (s, e) => ShowSettings();

            hotkeys = new Hotkeys();
            RegisterHotkeys();

            saveTimer = new System.Windows.Forms.Timer { Interval = 600 };
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); Config.Save(); };

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;

            // Second launch of the exe -> open settings in this one.
            var t = new Thread(() =>
            {
                while (true)
                {
                    showEvent.WaitOne();
                    try { invoker.BeginInvoke((Action)ShowSettings); } catch { return; }
                }
            }) { IsBackground = true };
            t.Start();

            if (Config.Visible) { overlay.Show(); overlay.Redraw(); }

            if (Config.FirstRun)
            {
                Config.FirstRun = false;
                Config.Save();
                openSettings = true;
            }
            if (openSettings) ShowSettings();
        }

        // ---------- actions ----------

        public void ApplyCurrent()
        {
            overlay.Apply(Config.Active.Settings, Config.TargetScreen);
            UpdateMenu();
            SaveSoon();
        }

        public void SaveSoon()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        public void SetVisible(bool visible)
        {
            Config.Visible = visible;
            if (visible) { overlay.Show(); overlay.Redraw(); }
            else overlay.Hide();
            UpdateMenu();
            SaveSoon();
            Changed?.Invoke();
        }

        public void ToggleVisible() { SetVisible(!Config.Visible); }

        public void SelectProfile(string name)
        {
            Config.ActiveProfile = name;
            ApplyCurrent();
            Changed?.Invoke();
        }

        public void NextProfile()
        {
            int i = Config.Profiles.IndexOf(Config.Active);
            var next = Config.Profiles[(i + 1) % Config.Profiles.Count];
            SelectProfile(next.Name);
            if (!Config.Visible) SetVisible(true);
            tray.ShowBalloonTip(800, "Free Crosshair", "Profile: " + next.Name, ToolTipIcon.None);
        }

        public void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this);
            }
            settingsForm.Show();
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
            settingsForm.BringToFront();
        }

        /// <summary>Returns a message listing keys that couldn't be registered, or null if all worked.</summary>
        public string RegisterHotkeys()
        {
            hotkeys.UnregisterAll();
            var failed = new System.Collections.Generic.List<string>();
            if (!hotkeys.Register(Config.ToggleKey, ToggleVisible)) failed.Add(Config.ToggleKey.ToString());
            if (!hotkeys.Register(Config.NextProfileKey, NextProfile)) failed.Add(Config.NextProfileKey.ToString());
            if (!hotkeys.Register(Config.SettingsKey, ShowSettings)) failed.Add(Config.SettingsKey.ToString());
            UpdateMenu();
            return failed.Count == 0 ? null : string.Join(", ", failed) + " is already used by another program. Pick a different key.";
        }

        public bool StartWithWindows
        {
            get
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k?.GetValue(RunValue) != null; }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" --minimized");
                        else k.DeleteValue(RunValue, false);
                    }
                }
                catch { }
                UpdateMenu();
            }
        }

        public void Exit()
        {
            saveTimer.Stop();
            Config.Save();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            tray.Visible = false;
            tray.Dispose();
            hotkeys.Dispose();
            overlay.Close();
            ExitThread();
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            // Resolution / monitor changed: re-center.
            try { invoker.BeginInvoke((Action)(() => overlay.Apply(Config.Active.Settings, Config.TargetScreen))); } catch { }
        }

        // ---------- tray menu ----------

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            var settings = new ToolStripMenuItem("Settings...", null, (s, e) => ShowSettings());
            settings.Font = new Font(settings.Font, FontStyle.Bold);
            showItem = new ToolStripMenuItem("Show crosshair", null, (s, e) => ToggleVisible());
            profilesMenu = new ToolStripMenuItem("Profile");
            startupItem = new ToolStripMenuItem("Start with Windows", null, (s, e) => StartWithWindows = !StartWithWindows);
            m.Items.Add(settings);
            m.Items.Add(showItem);
            m.Items.Add(profilesMenu);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(startupItem);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => Exit()));
            m.Opening += (s, e) => UpdateMenu();
            return m;
        }

        void UpdateMenu()
        {
            if (showItem == null) return;
            showItem.Checked = Config.Visible;
            showItem.Text = "Show crosshair" + (Config.ToggleKey != Keys.None ? "  (" + Config.ToggleKey + ")" : "");
            startupItem.Checked = StartWithWindows;
            profilesMenu.DropDownItems.Clear();
            foreach (var p in Config.Profiles)
            {
                string name = p.Name;
                profilesMenu.DropDownItems.Add(new ToolStripMenuItem(name, null, (s, e) => SelectProfile(name))
                {
                    Checked = name == Config.ActiveProfile
                });
            }
            tray.Text = "Free Crosshair - " + Truncate(Config.ActiveProfile, 40);
        }

        static string Truncate(string s, int n) { return s.Length <= n ? s : s.Substring(0, n); }
    }

    static class AppIcon
    {
        /// <summary>Tray icon drawn in code (green crosshair) so it's always crisp.</summary>
        public static Icon Create()
        {
            try
            {
                var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return new Icon(exeIcon, SystemInformation.SmallIconSize);
            }
            catch { }
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = new Pen(Color.Black, 6)) { g.DrawEllipse(p, 6, 6, 20, 20); }
                    using (var p = new Pen(Color.Lime, 3)) { g.DrawEllipse(p, 6, 6, 20, 20); }
                    g.FillRectangle(Brushes.Black, 14, 0, 4, 32);
                    g.FillRectangle(Brushes.Black, 0, 14, 32, 4);
                    g.FillRectangle(Brushes.Lime, 15, 1, 2, 30);
                    g.FillRectangle(Brushes.Lime, 1, 15, 30, 2);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    /// <summary>Non-interactive checks, run by the GitHub build on a real Windows machine.</summary>
    static class SelfTest
    {
        public static int RenderAll(string dir)
        {
            Directory.CreateDirectory(dir);
            var cfg = AppConfig.CreateDefault();
            var samples = cfg.Profiles.Select(p => p).ToList();
            foreach (CrosshairStyle st in Enum.GetValues(typeof(CrosshairStyle)))
                samples.Add(new Profile("style-" + st, new CrosshairSettings { Style = st, CenterDot = st != CrosshairStyle.Dot }));

            const int zoom = 8, cell = 64;
            using (var sheet = new Bitmap(cell * zoom, ((samples.Count + 1) / 2) * cell * zoom / 2, PixelFormat.Format32bppArgb))
            using (var sg = Graphics.FromImage(sheet))
            {
                sg.Clear(Color.FromArgb(70, 80, 90));
                sg.InterpolationMode = InterpolationMode.NearestNeighbor;
                sg.PixelOffsetMode = PixelOffsetMode.Half;
                int i = 0;
                foreach (var p in samples)
                {
                    using (var bmp = CrosshairRenderer.Render(p.Settings, out int half))
                    {
                        bmp.Save(Path.Combine(dir, Safe(p.Name) + ".png"), ImageFormat.Png);
                        int col = i % 2, row = i / 2;
                        var cellRect = new Rectangle(col * cell * zoom / 2, row * cell * zoom / 2, cell * zoom / 2, cell * zoom / 2);
                        int z = zoom / 2;
                        int w = Math.Min(bmp.Width, cell), h = Math.Min(bmp.Height, cell);
                        sg.DrawImage(bmp, new Rectangle(cellRect.X + (cellRect.Width - w * z) / 2, cellRect.Y + (cellRect.Height - h * z) / 2, w * z, h * z),
                            new Rectangle((bmp.Width - w) / 2, (bmp.Height - h) / 2, w, h), GraphicsUnit.Pixel);
                        using (var f = new Font("Segoe UI", 9))
                            sg.DrawString(p.Name, f, Brushes.White, cellRect.X + 4, cellRect.Y + 4);
                    }
                    i++;
                }
                sheet.Save(Path.Combine(dir, "_contact-sheet.png"), ImageFormat.Png);
            }

            // Round-trip checks for config + share code.
            var s = new CrosshairSettings { Style = CrosshairStyle.CircleCross, Color = Color.FromArgb(12, 34, 56), Gap = -2, Opacity = 55 };
            if (!CrosshairSettings.TryFromShareCode(s.ToShareCode(), out var back)) return Fail("share code did not parse");
            if (back.Style != s.Style || back.Color.ToArgb() != s.Color.ToArgb() || back.Gap != s.Gap || back.Opacity != s.Opacity)
                return Fail("share code round-trip mismatch");
            Console.WriteLine("render-test OK: " + samples.Count + " crosshairs");
            return 0;
        }

        public static int Screenshot(string file)
        {
            var cfg = AppConfig.CreateDefault();
            var s = cfg.Profiles[0].Settings;
            s.Length = 12; s.Thickness = 3; s.Gap = 4; s.CenterDot = true; // bigger so it's obvious in the capture
            var overlay = new OverlayForm();
            overlay.Apply(s, Screen.PrimaryScreen);
            overlay.Show();
            overlay.Redraw();
            int code = 1;
            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (o, e) =>
            {
                timer.Stop();
                try
                {
                    var b = Screen.PrimaryScreen.Bounds;
                    int cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2, r = 40;
                    using (var cap = new Bitmap(2 * r, 2 * r, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(cap))
                            g.CopyFromScreen(cx - r, cy - r, 0, 0, cap.Size, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
                        using (var big = new Bitmap(cap.Width * 6, cap.Height * 6))
                        {
                            using (var g = Graphics.FromImage(big))
                            {
                                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                                g.PixelOffsetMode = PixelOffsetMode.Half;
                                g.DrawImage(cap, 0, 0, big.Width, big.Height);
                            }
                            big.Save(file, ImageFormat.Png);
                        }
                        // The pixel right of center, past the gap, should be the crosshair green.
                        var px = cap.GetPixel(r + 1 + 4 + 3, r);
                        Console.WriteLine("screen " + b.Width + "x" + b.Height + ", sampled arm pixel = " + px);
                        code = (px.G > 200 && px.R < 60 && px.B < 60) ? 0 : 2;
                        Console.WriteLine(code == 0 ? "screenshot-test OK" : "screenshot-test: crosshair pixel not found");
                    }
                }
                catch (Exception ex) { Console.WriteLine("screenshot-test error: " + ex); code = 3; }
                overlay.Close();
                Application.ExitThread();
            };
            timer.Start();
            Application.Run();
            return code;
        }

        public static int SettingsWindow(string file)
        {
            var cfg = AppConfig.CreateDefault();
            cfg.FirstRun = false;
            var app = new CrosshairApp(cfg, new EventWaitHandle(false, EventResetMode.AutoReset), true);
            int code = 1;
            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (o, e) =>
            {
                timer.Stop();
                try
                {
                    var f = Application.OpenForms.OfType<SettingsForm>().First();
                    using (var bmp = new Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(file, ImageFormat.Png);
                    }
                    Console.WriteLine("settings-test OK");
                    code = 0;
                }
                catch (Exception ex) { Console.WriteLine("settings-test error: " + ex); code = 3; }
                app.Exit();
            };
            timer.Start();
            Application.Run(app);
            return code;
        }

        static int Fail(string msg) { Console.WriteLine("render-test FAILED: " + msg); return 1; }
        static string Safe(string n) { foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return n.Replace(' ', '-'); }
    }
}
