using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FreeCrosshair
{
    public class SettingsForm : Form
    {
        readonly CrosshairApp app;
        AppConfig Cfg { get { return app.Config; } }
        CrosshairSettings S { get { return Cfg.Active.Settings; } }

        bool loading;
        bool toldAboutTray;

        // controls
        ComboBox profileBox, styleBox, monitorBox, toggleKeyBox, nextKeyBox, settingsKeyBox, zoomBox;
        Button colorBtn, outlineColorBtn, imageBtn;
        NumericUpDown lengthNum, thickNum, gapNum, dotSizeNum, radiusNum, outlineNum, imageScaleNum, opacityNum, offXNum, offYNum;
        CheckBox centerDotChk, roundDotChk, outlineChk, showChk, startupChk;
        Label imageLabel, hotkeyWarn;
        PreviewPanel preview;
        ToolTip tips = new ToolTip { AutoPopDelay = 15000 };

        public SettingsForm(CrosshairApp app)
        {
            this.app = app;
            Text = "Free Crosshair";
            Icon = AppIcon.Create();
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(760, 690);
            BackColor = Color.FromArgb(245, 246, 248);

            BuildUi();
            LoadFromConfig();

            app.Changed += OnAppChanged;
            FormClosed += (s, e) => app.Changed -= OnAppChanged;
        }

        void OnAppChanged()
        {
            if (!IsDisposed) LoadFromConfig();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing the window just hides it; the crosshair keeps running from the tray.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!toldAboutTray)
                {
                    toldAboutTray = true;
                    var ni = new NotifyIcon { Icon = Icon, Visible = true };
                    ni.ShowBalloonTip(3000, "Free Crosshair is still running",
                        "Find it in the system tray (bottom right). Press " + Cfg.SettingsKey + " to open settings.", ToolTipIcon.Info);
                    var t = new Timer { Interval = 4000 };
                    t.Tick += (s, a) => { t.Stop(); ni.Visible = false; ni.Dispose(); t.Dispose(); };
                    t.Start();
                }
            }
            base.OnFormClosing(e);
        }

        // ================= UI construction =================

        void BuildUi()
        {
            var left = new TableLayoutPanel
            {
                Location = new Point(12, 12), Size = new Size(390, 666), ColumnCount = 2, AutoScroll = true,
                Padding = new Padding(0), GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(left);

            // --- Profile row ---
            Header(left, "Profile");
            profileBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            profileBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading || profileBox.SelectedItem == null) return;
                app.SelectProfile(((Profile)profileBox.SelectedItem).Name);
            };
            Row(left, "Active profile", profileBox);

            var profBtns = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0), WrapContents = false };
            profBtns.Controls.Add(SmallButton("New", NewProfile, "Make a new profile from the current one"));
            profBtns.Controls.Add(SmallButton("Rename", RenameProfile, null));
            profBtns.Controls.Add(SmallButton("Delete", DeleteProfile, null));
            profBtns.Controls.Add(SmallButton("Reset", ResetProfile, "Set this profile back to the default green cross"));
            Row(left, "", profBtns);

            // --- Shape ---
            Header(left, "Shape");
            styleBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            styleBox.Items.AddRange(new object[] { "Cross  +", "T-shape  ⊥", "X-shape  ×", "Dot  •", "Circle  ○", "Circle + cross  ⊕", "Custom image (PNG)" });
            styleBox.SelectedIndexChanged += (s, e) => { if (!loading) { S.Style = (CrosshairStyle)styleBox.SelectedIndex; Changed(); } };
            Row(left, "Style", styleBox);

            colorBtn = ColorButton(() => S.Color, c => S.Color = c);
            Row(left, "Color", colorBtn);

            lengthNum = Num(0, 200, () => S.Length, v => S.Length = v);
            Row(left, "Line length", lengthNum);
            thickNum = Num(1, 50, () => S.Thickness, v => S.Thickness = v);
            Row(left, "Thickness", thickNum);
            gapNum = Num(-20, 200, () => S.Gap, v => S.Gap = v);
            Row(left, "Center gap", gapNum);
            radiusNum = Num(1, 300, () => S.CircleRadius, v => S.CircleRadius = v);
            Row(left, "Circle radius", radiusNum);

            centerDotChk = Chk("Show center dot", () => S.CenterDot, v => S.CenterDot = v);
            Row(left, "Center dot", centerDotChk);
            dotSizeNum = Num(1, 50, () => S.DotSize, v => S.DotSize = v);
            Row(left, "Dot size", dotSizeNum);
            roundDotChk = Chk("Round dot (size 3+)", () => S.RoundDot, v => S.RoundDot = v);
            Row(left, "", roundDotChk);

            // --- Outline ---
            Header(left, "Outline");
            outlineChk = Chk("Draw outline", () => S.Outline, v => S.Outline = v);
            Row(left, "Outline", outlineChk);
            outlineNum = Num(1, 10, () => S.OutlineThickness, v => S.OutlineThickness = v);
            Row(left, "Outline thickness", outlineNum);
            outlineColorBtn = ColorButton(() => S.OutlineColor, c => S.OutlineColor = c);
            Row(left, "Outline color", outlineColorBtn);

            // --- Image ---
            Header(left, "Custom image");
            var imgRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0), WrapContents = false };
            imageBtn = SmallButton("Choose PNG...", ChooseImage, "Any PNG with a transparent background works");
            imageLabel = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(6, 7, 0, 0), MaximumSize = new Size(140, 0) };
            imgRow.Controls.Add(imageBtn);
            imgRow.Controls.Add(imageLabel);
            Row(left, "Image file", imgRow);
            imageScaleNum = Num(5, 1000, () => S.ImageScale, v => S.ImageScale = v);
            Row(left, "Image size %", imageScaleNum);

            // --- Position ---
            Header(left, "Position && look");
            opacityNum = Num(5, 100, () => S.Opacity, v => S.Opacity = v);
            Row(left, "Opacity %", opacityNum);
            offXNum = Num(-2000, 2000, () => S.OffsetX, v => S.OffsetX = v);
            Row(left, "Move left/right", offXNum);
            offYNum = Num(-2000, 2000, () => S.OffsetY, v => S.OffsetY = v);
            Row(left, "Move up/down", offYNum);
            tips.SetToolTip(offXNum, "Pixels. Negative = left. Use this if a game's center is slightly off.");
            tips.SetToolTip(offYNum, "Pixels. Negative = up.");
            monitorBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            monitorBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                Cfg.MonitorIndex = monitorBox.SelectedIndex;
                Changed();
            };
            Row(left, "Monitor", monitorBox);

            // ================= right column =================
            int rx = 418, rw = 330;

            var previewLabel = new Label { Text = "Preview", Location = new Point(rx, 12), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            Controls.Add(previewLabel);
            zoomBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(rx + rw - 80, 8), Width = 80 };
            zoomBox.Items.AddRange(new object[] { "1x (real)", "2x", "4x", "8x" });
            zoomBox.SelectedIndex = 2;
            zoomBox.SelectedIndexChanged += (s, e) => { preview.Zoom = new[] { 1, 2, 4, 8 }[zoomBox.SelectedIndex]; preview.Invalidate(); };
            Controls.Add(zoomBox);

            preview = new PreviewPanel { Location = new Point(rx, 36), Size = new Size(rw, 280), Zoom = 4 };
            preview.GetSettings = () => S;
            Controls.Add(preview);

            showChk = new CheckBox { Text = "Show crosshair on screen", Location = new Point(rx, 326), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            showChk.CheckedChanged += (s, e) => { if (!loading) app.SetVisible(showChk.Checked); };
            Controls.Add(showChk);

            // Hotkeys
            var hk = new GroupBox { Text = "Hotkeys (work while gaming)", Location = new Point(rx, 356), Size = new Size(rw, 128) };
            Controls.Add(hk);
            toggleKeyBox = KeyBox(hk, "Show / hide", 24, k => Cfg.ToggleKey = k);
            nextKeyBox = KeyBox(hk, "Next profile", 54, k => Cfg.NextProfileKey = k);
            settingsKeyBox = KeyBox(hk, "Open settings", 84, k => Cfg.SettingsKey = k);
            hotkeyWarn = new Label { ForeColor = Color.Firebrick, Location = new Point(rx, 486), Size = new Size(rw, 30) };
            Controls.Add(hotkeyWarn);

            // Share
            var share = new GroupBox { Text = "Share with friends", Location = new Point(rx, 518), Size = new Size(rw, 60) };
            Controls.Add(share);
            var copyBtn = new Button { Text = "Copy crosshair code", Location = new Point(10, 22), Size = new Size(150, 28) };
            copyBtn.Click += (s, e) =>
            {
                try { Clipboard.SetText(S.ToShareCode()); MessageBox.Show(this, "Code copied. Paste it to a friend - they click \"Paste code\" in their app.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't copy: " + ex.Message); }
            };
            var pasteBtn = new Button { Text = "Paste code", Location = new Point(168, 22), Size = new Size(150, 28) };
            pasteBtn.Click += (s, e) => PasteCode();
            share.Controls.Add(copyBtn);
            share.Controls.Add(pasteBtn);

            startupChk = new CheckBox { Text = "Start with Windows (runs in the tray)", Location = new Point(rx, 590), AutoSize = true };
            startupChk.CheckedChanged += (s, e) => { if (!loading) app.StartWithWindows = startupChk.Checked; };
            Controls.Add(startupChk);

            var hideBtn = new Button { Text = "Hide window", Location = new Point(rx, 640), Size = new Size(160, 34) };
            hideBtn.Click += (s, e) => Close();
            tips.SetToolTip(hideBtn, "The crosshair keeps running. Open this window again from the tray icon or the hotkey.");
            var exitBtn = new Button { Text = "Quit app", Location = new Point(rx + rw - 160, 640), Size = new Size(160, 34) };
            exitBtn.Click += (s, e) => app.Exit();
            Controls.Add(hideBtn);
            Controls.Add(exitBtn);
        }

        // ---------- small helpers for building rows ----------

        void Header(TableLayoutPanel t, string text)
        {
            var l = new Label
            {
                Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(40, 90, 160),
                Margin = new Padding(0, t.Controls.Count == 0 ? 0 : 8, 0, 2)
            };
            t.Controls.Add(l);
            t.SetColumnSpan(l, 2);
        }

        void Row(TableLayoutPanel t, string label, Control c)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 5, 4, 0) });
            c.Margin = new Padding(0, 1, 0, 1);
            t.Controls.Add(c);
        }

        Button SmallButton(string text, Action onClick, string tip)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(10, 26), Height = 26, Margin = new Padding(0, 0, 3, 0), Padding = new Padding(2, 0, 2, 0) };
            if (text.Length <= 6) { b.AutoSize = false; b.Width = 58; }
            b.Click += (s, e) => onClick();
            if (tip != null) tips.SetToolTip(b, tip);
            return b;
        }

        NumericUpDown Num(int min, int max, Func<int> get, Action<int> set)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, Width = 80, Tag = get };
            n.ValueChanged += (s, e) => { if (!loading) { set((int)n.Value); Changed(); } };
            return n;
        }

        CheckBox Chk(string text, Func<bool> get, Action<bool> set)
        {
            var c = new CheckBox { Text = text, AutoSize = true, Tag = get };
            c.CheckedChanged += (s, e) => { if (!loading) { set(c.Checked); Changed(); } };
            return c;
        }

        Button ColorButton(Func<Color> get, Action<Color> set)
        {
            var b = new Button { Width = 110, Height = 26, FlatStyle = FlatStyle.Flat, Tag = get, TextAlign = ContentAlignment.MiddleCenter };
            b.Click += (s, e) =>
            {
                using (var d = new ColorDialog { Color = get(), FullOpen = true, AnyColor = true })
                {
                    if (d.ShowDialog(this) == DialogResult.OK) { set(d.Color); Changed(); }
                }
            };
            return b;
        }

        ComboBox KeyBox(Control parent, string label, int y, Action<Keys> set)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(10, y + 4), AutoSize = true });
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(130, y), Width = 120 };
            foreach (var k in Hotkeys.Choices) c.Items.Add(k == Keys.None ? "(none)" : k.ToString());
            c.SelectedIndexChanged += (s, e) =>
            {
                if (loading || c.SelectedIndex < 0) return;
                set(Hotkeys.Choices[c.SelectedIndex]);
                string err = app.RegisterHotkeys();
                hotkeyWarn.Text = err ?? "";
                app.SaveSoon();
            };
            parent.Controls.Add(c);
            return c;
        }

        // ================= data <-> UI =================

        void Changed()
        {
            S.Clamp();
            app.ApplyCurrent();
            RefreshEnabled();
            RefreshSwatches();
            preview.Invalidate();
        }

        void LoadFromConfig()
        {
            loading = true;
            try
            {
                profileBox.Items.Clear();
                foreach (var p in Cfg.Profiles) profileBox.Items.Add(p);
                profileBox.SelectedItem = Cfg.Active;

                styleBox.SelectedIndex = (int)S.Style;
                foreach (var n in new[] { lengthNum, thickNum, gapNum, dotSizeNum, radiusNum, outlineNum, imageScaleNum, opacityNum, offXNum, offYNum })
                    n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, ((Func<int>)n.Tag)()));
                foreach (var c in new[] { centerDotChk, roundDotChk, outlineChk })
                    c.Checked = ((Func<bool>)c.Tag)();

                monitorBox.Items.Clear();
                var screens = AppConfig.OrderedScreens();
                for (int i = 0; i < screens.Length; i++)
                {
                    var b = screens[i].Bounds;
                    monitorBox.Items.Add((i + 1) + ": " + b.Width + "x" + b.Height + (screens[i].Primary ? " (main)" : ""));
                }
                monitorBox.SelectedIndex = Cfg.MonitorIndex >= 0 && Cfg.MonitorIndex < screens.Length ? Cfg.MonitorIndex : 0;

                toggleKeyBox.SelectedIndex = Math.Max(0, Array.IndexOf(Hotkeys.Choices, Cfg.ToggleKey));
                nextKeyBox.SelectedIndex = Math.Max(0, Array.IndexOf(Hotkeys.Choices, Cfg.NextProfileKey));
                settingsKeyBox.SelectedIndex = Math.Max(0, Array.IndexOf(Hotkeys.Choices, Cfg.SettingsKey));

                showChk.Checked = Cfg.Visible;
                startupChk.Checked = app.StartWithWindows;
                imageLabel.Text = string.IsNullOrEmpty(S.ImagePath) ? "(none)" : Path.GetFileName(S.ImagePath);
                tips.SetToolTip(imageLabel, S.ImagePath);
            }
            finally { loading = false; }
            RefreshEnabled();
            RefreshSwatches();
            preview.Invalidate();
        }

        void RefreshSwatches()
        {
            foreach (var b in new[] { colorBtn, outlineColorBtn })
            {
                var c = ((Func<Color>)b.Tag)();
                b.BackColor = c;
                b.ForeColor = (c.R * 0.3 + c.G * 0.59 + c.B * 0.11) > 140 ? Color.Black : Color.White;
                b.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            }
        }

        void RefreshEnabled()
        {
            var st = S.Style;
            bool lines = st == CrosshairStyle.Cross || st == CrosshairStyle.TShape || st == CrosshairStyle.XShape || st == CrosshairStyle.CircleCross;
            bool circle = st == CrosshairStyle.Circle || st == CrosshairStyle.CircleCross;
            bool image = st == CrosshairStyle.Image;
            bool dotStyle = st == CrosshairStyle.Dot;

            lengthNum.Enabled = lines;
            gapNum.Enabled = lines;
            thickNum.Enabled = lines || circle;
            radiusNum.Enabled = circle;
            centerDotChk.Enabled = !dotStyle;
            dotSizeNum.Enabled = dotStyle || S.CenterDot;
            roundDotChk.Enabled = dotStyle || S.CenterDot;
            colorBtn.Enabled = !image || S.CenterDot;
            outlineNum.Enabled = outlineColorBtn.Enabled = S.Outline && !image;
            outlineChk.Enabled = !image || S.CenterDot;
            imageBtn.Enabled = imageScaleNum.Enabled = image;
        }

        // ================= profile actions =================

        string Ask(string prompt, string title, string initial)
        {
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, initial).Trim();
        }

        void NewProfile()
        {
            string name = Ask("Name for the new profile:", "New profile", Cfg.UniqueName(Cfg.Active.Name + " copy"));
            if (name.Length == 0) return;
            if (Cfg.HasProfile(name)) { MessageBox.Show(this, "A profile with that name already exists."); return; }
            Cfg.Profiles.Add(new Profile(name, S.Clone()));
            app.SelectProfile(name);
            LoadFromConfig();
        }

        void RenameProfile()
        {
            string name = Ask("New name:", "Rename profile", Cfg.Active.Name);
            if (name.Length == 0 || name == Cfg.Active.Name) return;
            if (Cfg.HasProfile(name)) { MessageBox.Show(this, "A profile with that name already exists."); return; }
            Cfg.Active.Name = name;
            Cfg.ActiveProfile = name;
            app.ApplyCurrent();
            LoadFromConfig();
        }

        void DeleteProfile()
        {
            if (Cfg.Profiles.Count <= 1) { MessageBox.Show(this, "You need at least one profile."); return; }
            if (MessageBox.Show(this, "Delete profile \"" + Cfg.Active.Name + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int i = Cfg.Profiles.IndexOf(Cfg.Active);
            Cfg.Profiles.RemoveAt(i);
            app.SelectProfile(Cfg.Profiles[Math.Max(0, i - 1)].Name);
            LoadFromConfig();
        }

        void ResetProfile()
        {
            if (MessageBox.Show(this, "Reset \"" + Cfg.Active.Name + "\" to the default green cross?", "Reset", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            Cfg.Active.Settings = new CrosshairSettings();
            app.ApplyCurrent();
            LoadFromConfig();
        }

        void ChooseImage()
        {
            using (var d = new OpenFileDialog { Filter = "Images (*.png;*.gif;*.bmp;*.jpg)|*.png;*.gif;*.bmp;*.jpg;*.jpeg|All files|*.*", Title = "Choose a crosshair image" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                // Copy into our own folder so the crosshair still works if the original file is moved or deleted.
                string dest = d.FileName;
                try
                {
                    string dir = Path.Combine(AppConfig.Folder, "images");
                    Directory.CreateDirectory(dir);
                    dest = Path.Combine(dir, Path.GetFileName(d.FileName));
                    if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(d.FileName), StringComparison.OrdinalIgnoreCase))
                    {
                        CrosshairRenderer.ForgetImage(dest);
                        File.Copy(d.FileName, dest, true);
                    }
                }
                catch { dest = d.FileName; }

                if (CrosshairRenderer.LoadImage(dest) == null)
                {
                    MessageBox.Show(this, "That file couldn't be opened as an image.");
                    return;
                }
                S.ImagePath = dest;
                S.Style = CrosshairStyle.Image;
                Changed();
                LoadFromConfig();
            }
        }

        void PasteCode()
        {
            string text = "";
            try { text = Clipboard.GetText(); } catch { }
            if (!CrosshairSettings.TryFromShareCode(text, out var s))
            {
                text = Ask("Paste a crosshair code (starts with FC1:)", "Paste code", "");
                if (!CrosshairSettings.TryFromShareCode(text, out s))
                {
                    if (text.Length > 0) MessageBox.Show(this, "That doesn't look like a valid crosshair code.");
                    return;
                }
            }
            string name = Ask("Save the pasted crosshair as a new profile named:", "Paste code", Cfg.UniqueName("Shared"));
            if (name.Length == 0) return;
            name = Cfg.UniqueName(name);
            Cfg.Profiles.Add(new Profile(name, s));
            app.SelectProfile(name);
            LoadFromConfig();
        }
    }

    /// <summary>Shows the crosshair over a fake game scene, zoomed so it's easy to tweak pixel by pixel.</summary>
    class PreviewPanel : Panel
    {
        public Func<CrosshairSettings> GetSettings;
        public int Zoom = 4;

        public PreviewPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = ClientRectangle;
            // Sky / ground background, so you can judge how visible the crosshair is on light and dark areas.
            using (var sky = new LinearGradientBrush(new Rectangle(0, 0, r.Width, r.Height / 2 + 1), Color.FromArgb(150, 190, 225), Color.FromArgb(215, 225, 230), 90f))
                g.FillRectangle(sky, 0, 0, r.Width, r.Height / 2);
            using (var ground = new LinearGradientBrush(new Rectangle(0, r.Height / 2, r.Width, r.Height / 2 + 1), Color.FromArgb(95, 85, 70), Color.FromArgb(45, 40, 35), 90f))
                g.FillRectangle(ground, 0, r.Height / 2, r.Width, r.Height - r.Height / 2);
            using (var wall = new SolidBrush(Color.FromArgb(60, 65, 75)))
                g.FillRectangle(wall, r.Width / 2 + 20 * Zoom / 4, r.Height / 4, r.Width, r.Height / 2);

            var s = GetSettings?.Invoke();
            if (s == null) return;
            using (var bmp = CrosshairRenderer.Render(s, out int half))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                int w = bmp.Width * Zoom, h = bmp.Height * Zoom;
                int cx = r.Width / 2 + s.OffsetX * Zoom, cy = r.Height / 2 + s.OffsetY * Zoom;
                var dest = new Rectangle(cx - half * Zoom, cy - half * Zoom, w, h);
                using (var ia = new ImageAttributes())
                {
                    var cm = new ColorMatrix { Matrix33 = s.Opacity / 100f };
                    ia.SetColorMatrix(cm);
                    g.DrawImage(bmp, dest, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
                }
            }
        }
    }
}
