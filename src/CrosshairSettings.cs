using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace FreeCrosshair
{
    public enum CrosshairStyle
    {
        Cross,
        TShape,
        XShape,
        Dot,
        Circle,
        CircleCross,
        Image
    }

    /// <summary>One crosshair profile. Every public field is saved to the config file automatically.</summary>
    public class CrosshairSettings
    {
        public CrosshairStyle Style = CrosshairStyle.Cross;
        public Color Color = Color.FromArgb(0, 255, 0);

        // Lines
        public int Length = 6;
        public int Thickness = 2;
        public int Gap = 3;

        // Center dot
        public bool CenterDot = false;
        public int DotSize = 2;
        public bool RoundDot = false;

        // Circle
        public int CircleRadius = 10;

        // Outline
        public bool Outline = true;
        public int OutlineThickness = 1;
        public Color OutlineColor = Color.Black;

        // Image
        public string ImagePath = "";
        public int ImageScale = 100; // percent

        // General
        public int Opacity = 100; // percent
        public int OffsetX = 0;
        public int OffsetY = 0;

        public CrosshairSettings Clone()
        {
            return (CrosshairSettings)MemberwiseClone();
        }

        // ---------- simple key=value serialization ----------

        static IEnumerable<FieldInfo> Fields()
        {
            return typeof(CrosshairSettings).GetFields(BindingFlags.Public | BindingFlags.Instance);
        }

        public static string ValueToString(object v)
        {
            if (v is Color c) return "#" + (c.A == 255 ? "" : c.A.ToString("X2")) + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            if (v is bool b) return b ? "true" : "false";
            if (v is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return v?.ToString() ?? "";
        }

        public static object ParseValue(Type t, string s)
        {
            s = s.Trim();
            if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
            if (t == typeof(string)) return s;
            if (t == typeof(Color))
            {
                string h = s.TrimStart('#');
                if (h.Length == 6) h = "FF" + h;
                int argb = int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return Color.FromArgb(argb);
            }
            if (t.IsEnum) return Enum.Parse(t, s, true);
            throw new NotSupportedException(t.Name);
        }

        public IEnumerable<KeyValuePair<string, string>> ToPairs()
        {
            foreach (var f in Fields())
                yield return new KeyValuePair<string, string>(f.Name, ValueToString(f.GetValue(this)));
        }

        /// <summary>Sets one field from text. Unknown keys or bad values are ignored so old/new configs never crash the app.</summary>
        public void Set(string key, string value)
        {
            var f = typeof(CrosshairSettings).GetField(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f == null) return;
            try { f.SetValue(this, ParseValue(f.FieldType, value)); } catch { }
        }

        public void Clamp()
        {
            Length = Math.Max(0, Math.Min(200, Length));
            Thickness = Math.Max(1, Math.Min(50, Thickness));
            Gap = Math.Max(-20, Math.Min(200, Gap));
            DotSize = Math.Max(1, Math.Min(50, DotSize));
            CircleRadius = Math.Max(1, Math.Min(300, CircleRadius));
            OutlineThickness = Math.Max(1, Math.Min(10, OutlineThickness));
            ImageScale = Math.Max(5, Math.Min(1000, ImageScale));
            Opacity = Math.Max(5, Math.Min(100, Opacity));
            OffsetX = Math.Max(-2000, Math.Min(2000, OffsetX));
            OffsetY = Math.Max(-2000, Math.Min(2000, OffsetY));
            if (ImagePath == null) ImagePath = "";
        }

        // ---------- share codes ----------
        // A share code is the profile's settings packed into one line of text that can be pasted to a friend.

        const string SharePrefix = "FC1:";

        public string ToShareCode()
        {
            var sb = new StringBuilder();
            foreach (var p in ToPairs())
            {
                if (p.Key == nameof(ImagePath)) continue; // a local file path is useless on another PC
                sb.Append(p.Key).Append('=').Append(p.Value).Append(';');
            }
            return SharePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public static bool TryFromShareCode(string code, out CrosshairSettings result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();
            if (!code.StartsWith(SharePrefix, StringComparison.Ordinal)) return false;
            try
            {
                string text = Encoding.UTF8.GetString(Convert.FromBase64String(code.Substring(SharePrefix.Length)));
                var s = new CrosshairSettings();
                foreach (var part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0) s.Set(part.Substring(0, eq), part.Substring(eq + 1));
                }
                s.Clamp();
                result = s;
                return true;
            }
            catch { return false; }
        }
    }
}
