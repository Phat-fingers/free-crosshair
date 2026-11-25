using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace FreeCrosshair
{
    /// <summary>Draws a crosshair into a bitmap. Shared by the overlay, the preview and the self-test.</summary>
    public static class CrosshairRenderer
    {
        static readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static Image LoadImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (imageCache.TryGetValue(path, out var img)) return img;
            try
            {
                if (!File.Exists(path)) return null;
                // Load via a copy so the file isn't locked while the app runs.
                using (var fs = File.OpenRead(path))
                using (var tmp = Image.FromStream(fs))
                    img = new Bitmap(tmp);
            }
            catch { img = null; }
            imageCache[path] = img;
            return img;
        }

        public static void ForgetImage(string path)
        {
            if (path != null && imageCache.TryGetValue(path, out var img))
            {
                img?.Dispose();
                imageCache.Remove(path);
            }
        }

        /// <summary>Half-size in pixels of the area the crosshair needs.</summary>
        public static int Extent(CrosshairSettings s)
        {
            int o = s.Outline ? s.OutlineThickness : 0;
            int lines = s.Thickness + Math.Max(0, s.Gap) + s.Length;
            int circle = s.CircleRadius + s.Thickness;
            int dot = s.DotSize;
            int e;
            switch (s.Style)
            {
                case CrosshairStyle.Dot: e = dot; break;
                case CrosshairStyle.Circle: e = Math.Max(circle, dot); break;
                case CrosshairStyle.CircleCross: e = Math.Max(circle, lines); break;
                case CrosshairStyle.XShape: e = lines; break;
                case CrosshairStyle.Image:
                    var img = LoadImage(s.ImagePath);
                    e = img == null ? 16 : (int)Math.Ceiling(Math.Max(img.Width, img.Height) * s.ImageScale / 200.0);
                    e = Math.Max(e, s.CenterDot ? dot : 0);
                    break;
                default: e = lines; break;
            }
            return e + o + 4;
        }

        /// <summary>Renders the crosshair centered in a new square 32bpp ARGB bitmap of size 2*half (half = Extent).</summary>
        public static Bitmap Render(CrosshairSettings s, out int half)
        {
            half = Extent(s);
            int size = half * 2;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Draw(g, half, half, s);
            }
            return bmp;
        }

        /// <summary>
        /// Draws with (c, c) as the center pixel. Axis-aligned parts are drawn as whole-pixel rectangles so they stay crisp.
        /// </summary>
        public static void Draw(Graphics g, int cx, int cy, CrosshairSettings s)
        {
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingMode = CompositingMode.SourceOver;
            int o = s.Outline ? s.OutlineThickness : 0;

            switch (s.Style)
            {
                case CrosshairStyle.Cross:
                    DrawArms(g, cx, cy, s, o, top: true);
                    break;
                case CrosshairStyle.TShape:
                    DrawArms(g, cx, cy, s, o, top: false);
                    break;
                case CrosshairStyle.XShape:
                    DrawX(g, cx, cy, s, o);
                    break;
                case CrosshairStyle.Dot:
                    DrawDot(g, cx, cy, s.DotSize, s, o);
                    return; // the dot style *is* the center dot
                case CrosshairStyle.Circle:
                    DrawCircle(g, cx, cy, s, o);
                    break;
                case CrosshairStyle.CircleCross:
                    DrawCircle(g, cx, cy, s, o);
                    DrawArms(g, cx, cy, s, o, top: true);
                    break;
                case CrosshairStyle.Image:
                    DrawImage(g, cx, cy, s);
                    break;
            }

            if (s.CenterDot) DrawDot(g, cx, cy, s.DotSize, s, o);
        }

        static void FillWithOutline(Graphics g, Rectangle r, CrosshairSettings s, int o)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            g.SmoothingMode = SmoothingMode.None;
            if (o > 0)
            {
                var outer = r; outer.Inflate(o, o);
                using (var b = new SolidBrush(s.OutlineColor)) g.FillRectangle(b, outer);
            }
            using (var b = new SolidBrush(s.Color)) g.FillRectangle(b, r);
        }

        static void DrawArms(Graphics g, int cx, int cy, CrosshairSettings s, int o, bool top)
        {
            int t = s.Thickness;
            // The "center block" is the t x t square around the center pixel; the gap is measured from its edges.
            int c0x = cx - t / 2, c0y = cy - t / 2;
            int c1x = c0x + t, c1y = c0y + t; // exclusive
            int gap = s.Gap, len = s.Length;
            if (len <= 0) return;

            var arms = new List<Rectangle>
            {
                new Rectangle(c1x + gap, c0y, len, t),           // right
                new Rectangle(c0x - gap - len, c0y, len, t),     // left
                new Rectangle(c0x, c1y + gap, t, len),           // bottom
            };
            if (top) arms.Add(new Rectangle(c0x, c0y - gap - len, t, len)); // top

            // Outline everything first, then fill, so overlapping outlines never cover the colored part.
            g.SmoothingMode = SmoothingMode.None;
            if (o > 0)
                using (var b = new SolidBrush(s.OutlineColor))
                    foreach (var r in arms) { var x = r; x.Inflate(o, o); g.FillRectangle(b, x); }
            using (var b = new SolidBrush(s.Color))
                foreach (var r in arms) g.FillRectangle(b, r);
        }

        static void DrawX(Graphics g, int cx, int cy, CrosshairSettings s, int o)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float c = 0.5f; // pixel center
            float x0 = cx + c, y0 = cy + c;
            float d0 = (s.Gap + s.Thickness / 2f) * 0.7071f;
            float d1 = (s.Gap + s.Thickness / 2f + s.Length) * 0.7071f;
            var segs = new[]
            {
                new[] { new PointF(x0 + d0, y0 + d0), new PointF(x0 + d1, y0 + d1) },
                new[] { new PointF(x0 - d0, y0 + d0), new PointF(x0 - d1, y0 + d1) },
                new[] { new PointF(x0 + d0, y0 - d0), new PointF(x0 + d1, y0 - d1) },
                new[] { new PointF(x0 - d0, y0 - d0), new PointF(x0 - d1, y0 - d1) },
            };
            if (o > 0)
                using (var p = new Pen(s.OutlineColor, s.Thickness + 2 * o) { StartCap = LineCap.Square, EndCap = LineCap.Square })
                    foreach (var sg in segs) g.DrawLine(p, sg[0], sg[1]);
            using (var p = new Pen(s.Color, s.Thickness) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                foreach (var sg in segs) g.DrawLine(p, sg[0], sg[1]);
        }

        static void DrawCircle(Graphics g, int cx, int cy, CrosshairSettings s, int o)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x0 = cx + 0.5f, y0 = cy + 0.5f, r = s.CircleRadius;
            if (o > 0)
                using (var p = new Pen(s.OutlineColor, s.Thickness + 2 * o))
                    g.DrawEllipse(p, x0 - r, y0 - r, 2 * r, 2 * r);
            using (var p = new Pen(s.Color, s.Thickness))
                g.DrawEllipse(p, x0 - r, y0 - r, 2 * r, 2 * r);
        }

        static void DrawDot(Graphics g, int cx, int cy, int size, CrosshairSettings s, int o)
        {
            int x = cx - size / 2, y = cy - size / 2;
            if (!s.RoundDot || size < 3)
            {
                FillWithOutline(g, new Rectangle(x, y, size, size), s, o);
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (o > 0)
                using (var b = new SolidBrush(s.OutlineColor))
                    g.FillEllipse(b, x - o, y - o, size + 2 * o, size + 2 * o);
            using (var b = new SolidBrush(s.Color))
                g.FillEllipse(b, x, y, size, size);
        }

        static void DrawImage(Graphics g, int cx, int cy, CrosshairSettings s)
        {
            var img = LoadImage(s.ImagePath);
            if (img == null)
            {
                // No image picked yet: show a small placeholder cross so the user still sees where the center is.
                var fallback = s.Clone();
                fallback.Style = CrosshairStyle.Cross; fallback.Length = 5; fallback.Gap = 2; fallback.Thickness = 1;
                DrawArms(g, cx, cy, fallback, fallback.Outline ? 1 : 0, true);
                return;
            }
            float w = img.Width * s.ImageScale / 100f, h = img.Height * s.ImageScale / 100f;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = s.ImageScale % 100 == 0 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, (float)Math.Round(cx + 0.5f - w / 2f), (float)Math.Round(cy + 0.5f - h / 2f), w, h);
        }
    }
}
