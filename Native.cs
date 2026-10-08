// 鼠标定时点击器 —— Windows 原生调用
// 说明：本文件只使用 C# 5 语法，Windows 自带的 csc.exe 即可编译。
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ClickTimer
{
    internal static class Native
    {
        // ---------- 鼠标输入 ----------
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // INPUT 结构里联合体最大的成员就是 MOUSEINPUT，所以这样声明在 32/64 位下大小都正确
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int SM_SWAPBUTTON = 23;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public static bool MoveCursor(Point p)
        {
            return SetCursorPos(p.X, p.Y);
        }

        /// <summary>读取光标实际位置（用于确认确实移到了目标点）。</summary>
        public static bool GetCursor(out Point p)
        {
            POINT pt;
            bool ok = GetCursorPos(out pt);
            p = new Point(pt.X, pt.Y);
            return ok;
        }

        /// <summary>按下或抬起"主按钮"（一般是左键；如果系统设置了左右键互换，则自动换成物理右键）。</summary>
        public static bool PrimaryButton(bool down)
        {
            bool swapped = false;
            try { swapped = GetSystemMetrics(SM_SWAPBUTTON) != 0; }
            catch (Exception) { }

            uint flag;
            if (swapped) flag = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
            else flag = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;

            INPUT[] input = new INPUT[1];
            input[0].type = INPUT_MOUSE;
            input[0].mi.dwFlags = flag;
            return SendInput(1, input, Marshal.SizeOf(typeof(INPUT))) == 1;
        }

        // ---------- DPI：使用物理像素坐标，避免缩放 125%/150% 时点偏 ----------
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } // Per-Monitor V2（Win10 1703+）
            catch (Exception) { }
            try { if (SetProcessDpiAwareness(2) == 0) return; }                 // Per-Monitor（Win8.1+）
            catch (Exception) { }
            try { SetProcessDPIAware(); }                                        // System（Vista+）
            catch (Exception) { }
        }

        // ---------- 计时精度与防睡眠 ----------
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        /// <summary>运行期间：系统计时器精度设为 1 毫秒，并阻止电脑睡眠/关屏（关屏后可能锁屏，点击会失效）。</summary>
        public static void BeginRunMode()
        {
            try { timeBeginPeriod(1); } catch (Exception) { }
            try { SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED); } catch (Exception) { }
        }

        public static void EndRunMode()
        {
            try { timeEndPeriod(1); } catch (Exception) { }
            try { SetThreadExecutionState(ES_CONTINUOUS); } catch (Exception) { }
        }

        // ---------- 全局热键与窗口 ----------
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint VK_F9 = 0x78;
        public const int SW_SHOWNOACTIVATE = 4;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
