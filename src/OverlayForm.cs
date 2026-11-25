using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using static FreeCrosshair.NativeMethods;

namespace FreeCrosshair
{
    /// <summary>
    /// The crosshair itself: a tiny borderless, always-on-top, click-through window in the middle of the screen.
    /// It never touches the game process (no injection, no memory reading), so it's invisible to anti-cheat.
    /// </summary>
    public class OverlayForm : Form
    {
        readonly Timer topmostTimer;
        CrosshairSettings settings;
        Screen screen;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "Free Crosshair Overlay";

            // Games sometimes push themselves above other topmost windows; re-assert our place regularly.
            topmostTimer = new Timer { Interval = 750 };
            topmostTimer.Tick += (s, e) =>
            {
                if (Visible && IsHandleCreated)
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            };
            topmostTimer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        // Never take focus, even if clicked somehow.
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021, MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        public void Apply(CrosshairSettings s, Screen target)
        {
            settings = s;
            screen = target;
            if (IsHandleCreated) Redraw();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (settings != null) Redraw();
        }

        public void Redraw()
        {
            if (settings == null) return;
            var sc = screen ?? Screen.PrimaryScreen;
            using (var bmp = CrosshairRenderer.Render(settings, out int half))
            {
                // Center pixel of the screen; for even widths (1920) that's pixel 960, the one just right/below the middle,
                // which matches where most games draw their own crosshair. Offsets let the user nudge it.
                int cx = sc.Bounds.X + sc.Bounds.Width / 2 + settings.OffsetX;
                int cy = sc.Bounds.Y + sc.Bounds.Height / 2 + settings.OffsetY;
                var pos = new Point(cx - half, cy - half);
                SetLayeredBitmap(bmp, pos, (byte)Math.Round(255 * settings.Opacity / 100.0));
            }
        }

        void SetLayeredBitmap(Bitmap bmp, Point pos, byte opacity)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0)); // premultiplied 32bpp DIB with alpha
                old = SelectObject(memDc, hBitmap);
                var size = new SIZE(bmp.Width, bmp.Height);
                var src = new POINT(0, 0);
                var dst = new POINT(pos.X, pos.Y);
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = opacity, AlphaFormat = AC_SRC_ALPHA
                };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero) { SelectObject(memDc, old); DeleteObject(hBitmap); }
                DeleteDC(memDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) topmostTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
