// WinSwitcher - a tiny always-on-top one-click window switcher for Windows.
// Compiles with the in-box .NET Framework C# compiler (C# 5 syntax on purpose).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinSwitcher
{
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int DWMWA_CLOAKED = 14;

        public const int SW_SHOW = 5;

        // SetWindowPos: climb back to the top of the topmost band.
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        // WinEvent: a different top-level window became foreground.
        public const uint EVENT_SYSTEM_FOREGROUND = 3;
        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const uint WINEVENT_SKIPOWNPROCESS = 2;

        public const uint GW_HWNDNEXT = 2;
        public const uint GW_OWNER = 4;

        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0;
        public const byte AC_SRC_ALPHA = 1;

        // Win32 popup menu.
        public const uint MF_STRING = 0x00000000;
        public const uint MF_SEPARATOR = 0x00000800;
        public const uint MF_CHECKED = 0x00000008;
        public const uint TPM_RETURNCMD = 0x00000100;
        public const uint TPM_NONOTIFY = 0x00000080;
        public const uint TPM_RIGHTBUTTON = 0x00000002;

        public const int IDM_STARTUP = 1;
        public const int IDM_EXIT = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumThreadProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        /// <summary>Push our window back to the top of the topmost band without
        /// moving, resizing, or activating it (never steals focus).</summary>
        public static void ReassertTopmost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        [DllImport("user32.dll")]
        private static extern bool SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateEllipticRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y,
            IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadProc lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const uint WM_KEYDOWN = 0x0100;
        private const int VK_ESCAPE = 0x1B;

        // Must stay alive (not GC'd) for as long as the hook is installed.
        private static LowLevelMouseProc menuHookProc;
        private static IntPtr menuHook;

        private static readonly uint CurrentPid = (uint)Process.GetCurrentProcess().Id;

        // Our own most-recently-used window order. Windows reorder themselves to the
        // top of the system z-order every time they are activated, so walking the live
        // z-order on every click can only ever toggle between the two topmost windows.
        // This list is moved-to-front on real foreground changes but left untouched for
        // activations we trigger ourselves, letting clicks advance through every window.
        private static readonly object orderLock = new object();
        private static readonly List<IntPtr> windowOrder = new List<IntPtr>();
        private static readonly HashSet<IntPtr> skipWindows = new HashSet<IntPtr>();
        private static IntPtr selfActivation;
        private static int selfActivationTick;

        private static WinEventProc foregroundProc;
        private static IntPtr foregroundHook;
        private static IntPtr ownerHwnd;

        public static void InstallForegroundHook(IntPtr owner)
        {
            ownerHwnd = owner;
            if (foregroundHook != IntPtr.Zero) return;
            foregroundProc = ForegroundChanged;
            // Out-of-context global hook: events are posted to this (UI) thread's queue.
            foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, foregroundProc, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }

        private static void ForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0) return; // window-level events only
            try
            {
                // A foreground change reshuffles z-order (and the new window may itself be
                // topmost): climb straight back to the top of the topmost band.
                ReassertTopmost(ownerHwnd);

                if (!IsSwitchable(hwnd)) return;
                lock (orderLock)
                {
                    skipWindows.Remove(hwnd);

                    // The activation we caused ourselves must not reorder the rotation;
                    // consume the one-shot marker.
                    if (hwnd == selfActivation &&
                        unchecked(Environment.TickCount - selfActivationTick) < 2000)
                    {
                        selfActivation = IntPtr.Zero;
                        return;
                    }

                    windowOrder.Remove(hwnd);
                    windowOrder.Insert(0, hwnd);
                }
            }
            catch { }
        }

        /// <summary>
        /// Drop dead/minimized entries and pick up windows that appeared since last time.
        /// Called under orderLock.
        /// </summary>
        private static void RefreshOrderLocked()
        {
            for (int i = windowOrder.Count - 1; i >= 0; i--)
            {
                IntPtr h = windowOrder[i];
                if (!IsWindow(h) || !IsSwitchable(h))
                {
                    windowOrder.RemoveAt(i);
                    skipWindows.Remove(h);
                }
            }

            IntPtr w = GetTopWindow(IntPtr.Zero);
            while (w != IntPtr.Zero)
            {
                if (IsSwitchable(w) && !windowOrder.Contains(w))
                    windowOrder.Add(w); // appended in z-order: most recently used first
                w = GetWindow(w, GW_HWNDNEXT);
            }
        }

        /// <summary>Next entry after the current foreground in our own rotation order.</summary>
        private static IntPtr PickNextLocked(IntPtr foreground)
        {
            if (windowOrder.Count == 0) return IntPtr.Zero;

            int idx = windowOrder.IndexOf(foreground);
            int start = idx < 0 ? -1 : idx;
            for (int step = 1; step <= windowOrder.Count; step++)
            {
                IntPtr candidate = windowOrder[(start + step) % windowOrder.Count];
                if (candidate != foreground && !skipWindows.Contains(candidate))
                    return candidate;
            }

            // Everything was marked unraisable: give it a fresh try from the top.
            skipWindows.Clear();
            return windowOrder[0] != foreground ? windowOrder[0] :
                   (windowOrder.Count > 1 ? windowOrder[1] : IntPtr.Zero);
        }

        /// <summary>
        /// Alt-Tab rule: visible, restored (non-minimized), titled, non-tool-window,
        /// non-cloaked top-level window.
        /// </summary>
        private static bool IsSwitchable(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd)) return false;
            if (IsIconic(hWnd)) return false; // minimized windows stay out of the rotation
            if (GetWindowTextLength(hWnd) == 0) return false;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

            bool hasOwner = GetWindow(hWnd, GW_OWNER) != IntPtr.Zero;
            if (hasOwner && (exStyle & WS_EX_APPWINDOW) == 0) return false;

            int cloaked = 0;
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0)
                return false;

            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == CurrentPid) return false;

            StringBuilder sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.Length > 0;
        }

        /// <summary>Pick and activate the next window in the kept rotation on a fresh worker.</summary>
        public static void RequestSwitch(IntPtr owner)
        {
            Thread t = new Thread(delegate()
            {
                IntPtr target;
                IntPtr fore = GetForegroundWindow();
                lock (orderLock)
                {
                    RefreshOrderLocked();
                    target = PickNextLocked(fore);
                    selfActivation = target;
                    selfActivationTick = Environment.TickCount;
                }

                if (target != IntPtr.Zero)
                {
                    ActivateFromWorker(target);
                    if (GetForegroundWindow() != target)
                    {
                        // Unraisable (e.g. elevated window while we run unelevated):
                        // don't let it block the rest of the rotation.
                        lock (orderLock) skipWindows.Add(target);
                    }
                }

                Thread.Sleep(250);
                // The activated window may itself be topmost and have climbed above us.
                ReassertTopmost(owner);
                lock (orderLock)
                {
                    // Drop a stale marker if no foreground event matched the activation.
                    if (selfActivation == target &&
                        unchecked(Environment.TickCount - selfActivationTick) > 1500)
                        selfActivation = IntPtr.Zero;
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private static void ActivateFromWorker(IntPtr hWnd)
        {
            // Retry briefly: the very first handover after process launch can be swallowed
            // by the foreground-lock grant initialization while later ones always succeed.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                IntPtr fore = GetForegroundWindow();
                uint forePid;
                uint foreThread = GetWindowThreadProcessId(fore, out forePid);
                uint currentThread = GetCurrentThreadId();

                bool attached = fore != IntPtr.Zero && currentThread != foreThread &&
                                AttachThreadInput(currentThread, foreThread, true);
                bool ok = false;
                try
                {
                    BringWindowToTop(hWnd);
                    ok = SetForegroundWindow(hWnd);
                    SetActiveWindow(hWnd);
                    ShowWindow(hWnd, SW_SHOW);
                }
                finally
                {
                    if (attached) AttachThreadInput(currentThread, foreThread, false);
                }

                Thread.Sleep(60);
                if (ok && GetForegroundWindow() == hWnd) break;
            }
        }

        /// <summary>
        /// Show the native context menu at screen point (x,y). Returns the chosen command id,
        /// or 0 if dismissed. A classic popup menu closes itself on outside clicks even when
        /// the owner window carries WS_EX_NOACTIVATE (a WinForms ContextMenuStrip does not).
        /// </summary>
        public static int ShowContextMenu(IntPtr owner, int x, int y, bool startupEnabled)
        {
            IntPtr hMenu = CreatePopupMenu();
            int cmd;
            try
            {
                AppendMenu(hMenu, MF_STRING | (startupEnabled ? MF_CHECKED : 0),
                    (UIntPtr)IDM_STARTUP, "开机自动启动");
                AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, null);
                AppendMenu(hMenu, MF_STRING, (UIntPtr)IDM_EXIT, "退出 WinSwitcher");

                // A popup owned by a WS_EX_NOACTIVATE window won't dismiss itself on outside
                // clicks (the usual foreground-change signal never fires). Cancel it ourselves
                // via a thread-local low-level mouse hook.
                menuHookProc = MenuMouseHook;
                using (Process cur = Process.GetCurrentProcess())
                using (ProcessModule mod = cur.MainModule)
                    menuHook = SetWindowsHookEx(WH_MOUSE_LL, menuHookProc, mod.BaseAddress, 0);

                cmd = TrackPopupMenuEx(
                    hMenu,
                    TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON,
                    x, y, owner, IntPtr.Zero);
            }
            finally
            {
                if (menuHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(menuHook);
                    menuHook = IntPtr.Zero;
                }
                menuHookProc = null;
                DestroyMenu(hMenu);
            }
            return cmd;
        }

        private static IntPtr MenuMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                ((int)wParam == WM_LBUTTONDOWN || (int)wParam == WM_RBUTTONDOWN))
            {
                MSLLHOOKSTRUCT data = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                IntPtr popup = FindPopupMenuWindow();
                if (popup != IntPtr.Zero)
                {
                    RECT rc;
                    GetWindowRect(popup, out rc);
                    bool inside = data.pt.X >= rc.Left && data.pt.X <= rc.Right &&
                                  data.pt.Y >= rc.Top && data.pt.Y <= rc.Bottom;
                    // WM_CANCELMODE is ignored by a TrackPopupMenuEx popup; a targeted ESC
                    // keypress (never leaves our process) closes it instead.
                    if (!inside)
                        PostMessage(popup, WM_KEYDOWN, (IntPtr)VK_ESCAPE, (IntPtr)0x00010001);
                }
            }
            return CallNextHookEx(menuHook, nCode, wParam, lParam);
        }

        private static IntPtr FindPopupMenuWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr hWnd, IntPtr lParam)
            {
                StringBuilder cn = new StringBuilder(32);
                GetClassName(hWnd, cn, cn.Capacity);
                if (cn.ToString() == "#32768" && IsWindowVisible(hWnd))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>
        /// Push a per-pixel-alpha bitmap onto a WS_EX_LAYERED window.
        /// </summary>
        public static void PaintLayeredWindow(IntPtr hWnd, Bitmap bitmap, int x, int y)
        {
            SIZE size = new SIZE { cx = bitmap.Width, cy = bitmap.Height };
            POINT dst = new POINT { X = x, Y = y };
            POINT src = new POINT { X = 0, Y = 0 };
            BLENDFUNCTION blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA
            };

            IntPtr hdcScreen = GetDC(IntPtr.Zero);
            IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
            IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = SelectObject(hdcMem, hBitmap);

            UpdateLayeredWindow(hWnd, hdcScreen, ref dst, ref size, hdcMem, ref src, 0, ref blend, ULW_ALPHA);

            SelectObject(hdcMem, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }

        public static void ApplyEllipticRegion(IntPtr hWnd, int diameter)
        {
            IntPtr rgn = CreateEllipticRgn(0, 0, diameter + 1, diameter + 1);
            SetWindowRgn(hWnd, rgn, true);
        }
    }

    internal static class Startup
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "WinSwitcher";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                if (key == null) return false;
                string stored = key.GetValue(ValueName) as string;
                if (string.IsNullOrEmpty(stored)) return false;
                stored = stored.Trim('"');
                return string.Equals(stored, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null) return;
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else
                {
                    try { key.DeleteValue(ValueName, false); }
                    catch (ArgumentException) { }
                }
            }
        }
    }

    /// <summary>
    /// A small semi-transparent circular always-on-top button at the right screen edge.
    /// Left-click cycles to the next recently used window; the button can be dragged.
    /// Right-click opens a native popup menu (startup / exit).
    /// </summary>
    internal sealed class SwitchButtonForm : Form
    {
        private const int Diameter = 38;
        private const int DragThreshold = 5;

        private bool mouseInside;
        private bool mouseDown;
        private bool potentialDrag;
        private bool dragging;
        private Point dragOffset;

        private readonly ToolTip toolTip;
        private System.Windows.Forms.Timer topmostTimer;
        private bool menuOpen;

        public SwitchButtonForm()
        {
            Text = "WinSwitcher";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Magenta; // never seen: the whole surface is painted via UpdateLayeredWindow
            SetBounds(0, 0, Diameter, Diameter, BoundsSpecified.Size);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Diameter - 10, wa.Top + (wa.Height - Diameter) / 2);

            toolTip = new ToolTip();
            toolTip.SetToolTip(this, "单击切换到下一个窗口（可拖动）");
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Layered for per-pixel alpha; tool window hides us from taskbar / Alt+Tab;
                // noactivate means clicks never steal foreground from the app being switched,
                // and launch can't steal foreground either.
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyEllipticRegion(Handle, Diameter);
            Native.InstallForegroundHook(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // First layered-window paint must happen after the native window is positioned & shown.
            Render();

            // Safety net for topmost windows that appear without a foreground change
            // (or a borderless fullscreen app). Foreground events re-assert immediately;
            // this is the periodic backstop.
            topmostTimer = new System.Windows.Forms.Timer();
            topmostTimer.Interval = 1000;
            topmostTimer.Tick += delegate
            {
                if (!menuOpen && IsHandleCreated) Native.ReassertTopmost(Handle);
            };
            topmostTimer.Start();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            mouseInside = true;
            Cursor = Cursors.Hand;
            Render();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mouseInside = false;
            Cursor = Cursors.Default;
            Render();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                mouseDown = true;
                potentialDrag = true;
                dragging = false;
                dragOffset = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!mouseDown) return;

            if (!dragging && potentialDrag &&
                (Math.Abs(e.X - dragOffset.X) > DragThreshold || Math.Abs(e.Y - dragOffset.Y) > DragThreshold))
            {
                dragging = true;
                Cursor = Cursors.SizeAll;
            }

            if (dragging)
                Location = new Point(Left + e.X - dragOffset.X, Top + e.Y - dragOffset.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            bool wasClick = !dragging;
            mouseDown = false;
            potentialDrag = false;
            dragging = false;
            Cursor = mouseInside ? Cursors.Hand : Cursors.Default;

            if (wasClick) SwitchToNextWindow();
            else Native.ReassertTopmost(Handle);
        }

        private void ShowMenu()
        {
            bool startupEnabled = Startup.IsEnabled();
            Point p = Cursor.Position;
            int cmd;
            menuOpen = true;
            try
            {
                cmd = Native.ShowContextMenu(Handle, p.X, p.Y, startupEnabled);
            }
            finally
            {
                menuOpen = false;
            }

            if (cmd == Native.IDM_STARTUP)
            {
                Startup.SetEnabled(!startupEnabled);
            }
            else if (cmd == Native.IDM_EXIT)
            {
                Application.Exit();
            }
        }

        private void SwitchToNextWindow()
        {
            Native.RequestSwitch(Handle);
        }

        private void Render()
        {
            if (!IsHandleCreated) return;

            // Idle: medium slate so it stays visible on both dark windows and light wallpaper.
            // Hover or mouse held: solid blue.
            bool hot = mouseInside || mouseDown;
            int fillAlpha = hot ? 255 : 215;
            Color fillColor = hot ? Color.FromArgb(64, 110, 190) : Color.FromArgb(72, 82, 98);
            int ringAlpha = hot ? 220 : 120;

            using (Bitmap bmp = new Bitmap(Diameter, Diameter, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Rectangle circle = new Rectangle(1, 1, Diameter - 2, Diameter - 2);

                using (SolidBrush fill = new SolidBrush(Color.FromArgb(fillAlpha, fillColor)))
                    g.FillEllipse(fill, circle);
                using (Pen ring = new Pen(Color.FromArgb(ringAlpha, 245, 245, 250), 1.2f))
                    g.DrawEllipse(ring, circle);

                // Two horizontal arrows (⇄) meaning "switch": round-capped shafts plus
                // filled triangle heads, matching the embedded app.ico glyph.
                Color glyphColor = Color.FromArgb(240, 245, 250);
                using (Pen glyphPen = new Pen(glyphColor, 2.1f))
                using (SolidBrush glyphBrush = new SolidBrush(glyphColor))
                {
                    glyphPen.StartCap = LineCap.Round;
                    glyphPen.EndCap = LineCap.Round;

                    // Top arrow, pointing right.
                    g.DrawLine(glyphPen, 10, 14, 22.5f, 14);
                    g.FillPolygon(glyphBrush, new[]
                    {
                        new PointF(27, 14),
                        new PointF(22.5f, 10.8f),
                        new PointF(22.5f, 17.2f)
                    });

                    // Bottom arrow, pointing left.
                    g.DrawLine(glyphPen, 28, 24, 15.5f, 24);
                    g.FillPolygon(glyphBrush, new[]
                    {
                        new PointF(11, 24),
                        new PointF(15.5f, 20.8f),
                        new PointF(15.5f, 27.2f)
                    });
                }

                Native.PaintLayeredWindow(Handle, bmp, Left, Top);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (toolTip != null) toolTip.Dispose();
                if (topmostTimer != null) topmostTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\WinSwitcher_SingleInstance_v2", out createdNew))
            {
                if (!createdNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { ShowError(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    ShowError(e.ExceptionObject as Exception);
                };

                Application.Run(new SwitchButtonForm());
            }
        }

        private static void ShowError(Exception ex)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinSwitcher-error.log");
                System.IO.File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            MessageBox.Show(ex == null ? "未知错误" : ex.ToString(), "WinSwitcher 发生错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
