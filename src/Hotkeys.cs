using System;
using System.Collections.Generic;
using System.Windows.Forms;
using static FreeCrosshair.NativeMethods;

namespace FreeCrosshair
{
    /// <summary>System-wide hotkeys (work while the game has focus) via RegisterHotKey on a hidden window.</summary>
    public class Hotkeys : NativeWindow, IDisposable
    {
        readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
        int nextId = 1;

        public Hotkeys() { CreateHandle(new CreateParams()); }

        /// <summary>Returns false if another program already owns that key.</summary>
        public bool Register(Keys key, Action action)
        {
            if (key == Keys.None) return true;
            int id = nextId++;
            if (!RegisterHotKey(Handle, id, MOD_NOREPEAT, (uint)key)) return false;
            actions[id] = action;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var id in actions.Keys) UnregisterHotKey(Handle, id);
            actions.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && actions.TryGetValue(m.WParam.ToInt32(), out var a)) { a(); return; }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }

        /// <summary>Keys offered in the settings window. All are rarely used by games.</summary>
        public static readonly Keys[] Choices =
        {
            Keys.None,
            Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12,
            Keys.Insert, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown, Keys.Pause, Keys.Scroll,
            Keys.NumPad0, Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4,
            Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9,
            Keys.Multiply, Keys.Add, Keys.Subtract, Keys.Divide, Keys.Decimal
        };
    }
}
