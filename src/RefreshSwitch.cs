using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace RefreshSwitch
{
    // ------------------------------------------------------------ Win32
    public static class Native
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const uint DM_BITSPERPEL = 0x00040000;
        public const uint DM_PELSWIDTH = 0x00080000;
        public const uint DM_PELSHEIGHT = 0x00100000;
        public const uint DM_DISPLAYFREQUENCY = 0x00400000;
        public const uint CDS_UPDATEREGISTRY = 0x00000001;
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public POINTL dmPosition;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    // ------------------------------------------------------------ 显示模式
    public static class Display
    {
        public static System.Collections.Generic.List<string> GetDevices()
        {
            var list = new System.Collections.Generic.List<string>();
            int size = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
            for (uint i = 0; i < 16; i++)
            {
                var dd = new Native.DISPLAY_DEVICE();
                dd.cb = size;
                if (!Native.EnumDisplayDevices(null, i, ref dd, 0)) break;
                if ((dd.StateFlags & Native.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;
                list.Add(dd.DeviceName);
            }
            return list;
        }

        public static Native.DEVMODE Current(string device)
        {
            var dm = new Native.DEVMODE();
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(Native.DEVMODE));
            Native.EnumDisplaySettings(device, Native.ENUM_CURRENT_SETTINGS, ref dm);
            return dm;
        }

        public static System.Collections.Generic.List<Native.DEVMODE> AllModes(string device)
        {
            var list = new System.Collections.Generic.List<Native.DEVMODE>();
            for (int i = 0; i < 400; i++)
            {
                var dm = new Native.DEVMODE();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(Native.DEVMODE));
                if (!Native.EnumDisplaySettings(device, i, ref dm)) break;
                if ((dm.dmDisplayFlags & 0x2) != 0) continue;
                bool dup = false;
                foreach (var m in list)
                    if (m.dmPelsWidth == dm.dmPelsWidth && m.dmPelsHeight == dm.dmPelsHeight
                        && m.dmBitsPerPel == dm.dmBitsPerPel && m.dmDisplayFrequency == dm.dmDisplayFrequency) { dup = true; break; }
                if (!dup) list.Add(dm);
            }
            return list;
        }

        public static bool TrySet(int targetHz, out int newHz, out string info, out string error, out bool changed)
        {
            newHz = 0; info = ""; error = ""; changed = false;
            var devices = GetDevices();
            if (devices.Count == 0) { error = "没有检测到已连接的显示器"; return false; }

            string device = devices[0];
            var cur = Current(device);
            if (Math.Abs((int)cur.dmDisplayFrequency - targetHz) <= 2)
            {
                newHz = (int)cur.dmDisplayFrequency;
                info = cur.dmPelsWidth + " x " + cur.dmPelsHeight + " · " + cur.dmBitsPerPel + " 位色";
                changed = false;
                return true;
            }

            var dm = Current(device);
            dm.dmDisplayFrequency = (uint)targetHz;
            dm.dmFields = Native.DM_PELSWIDTH | Native.DM_PELSHEIGHT | Native.DM_BITSPERPEL | Native.DM_DISPLAYFREQUENCY;

            int rc = Native.ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, Native.CDS_UPDATEREGISTRY, IntPtr.Zero);
            if (rc != 0)
                rc = Native.ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, 0, IntPtr.Zero);

            if (rc != 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var m in AllModes(device))
                {
                    if (m.dmPelsWidth != cur.dmPelsWidth || m.dmPelsHeight != cur.dmPelsHeight) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(m.dmDisplayFrequency);
                }
                error = (sb.Length == 0)
                    ? ("切换失败，返回码 " + rc)
                    : ("当前分辨率下没有 " + targetHz + " Hz，可选：" + sb);
                return false;
            }

            Thread.Sleep(80);
            var after = Current(device);
            newHz = (int)after.dmDisplayFrequency;
            info = after.dmPelsWidth + " x " + after.dmPelsHeight + " · " + after.dmBitsPerPel + " 位色";
            if (Math.Abs(newHz - targetHz) > 2)
            {
                error = "驱动没有真正切换到 " + targetHz + " Hz（当前 " + newHz + " Hz）";
                return false;
            }
            changed = true;
            return true;
        }
    }

    // ------------------------------------------------------------ 屏幕提示
    public static class Hud
    {
        public static Window Build(string title, string sub, Color accent, int ms)
        {
            var win = new Window();
            win.Title = "RefreshSwitchHud";
            win.WindowStyle = WindowStyle.None;
            win.AllowsTransparency = true;
            win.Background = Brushes.Transparent;
            win.ShowInTaskbar = false;
            win.Topmost = true;
            win.ShowActivated = false;
            win.SizeToContent = SizeToContent.WidthAndHeight;
            win.ResizeMode = ResizeMode.NoResize;
            win.FontFamily = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");
            TextOptions.SetTextFormattingMode(win, TextFormattingMode.Ideal);

            var border = new Border();
            border.CornerRadius = new CornerRadius(14);
            border.Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x14, 0x17, 0x1D));
            border.Padding = new Thickness(36, 21, 36, 21);
            border.Margin = new Thickness(26);
            border.Effect = new DropShadowEffect { BlurRadius = 34, ShadowDepth = 0, Opacity = 0.55, Color = Colors.Black };

            var stack = new StackPanel();
            stack.HorizontalAlignment = HorizontalAlignment.Center;

            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Center;

            var dot = new Border();
            dot.Width = 9; dot.Height = 9;
            dot.CornerRadius = new CornerRadius(4.5);
            dot.Background = new SolidColorBrush(accent);
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(0, 2, 12, 0);
            dot.Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Opacity = 0.9, Color = accent };

            var t1 = new TextBlock();
            t1.Text = title;
            t1.FontSize = 33;
            t1.FontWeight = FontWeights.SemiBold;
            t1.Foreground = Brushes.White;
            t1.VerticalAlignment = VerticalAlignment.Center;

            row.Children.Add(dot);
            row.Children.Add(t1);
            stack.Children.Add(row);

            if (!string.IsNullOrEmpty(sub))
            {
                var t2 = new TextBlock();
                t2.Text = sub;
                t2.FontSize = 14.5;
                t2.Foreground = new SolidColorBrush(Color.FromArgb(0xB3, 255, 255, 255));
                t2.HorizontalAlignment = HorizontalAlignment.Center;
                t2.Margin = new Thickness(0, 10, 0, 0);
                t2.TextWrapping = TextWrapping.Wrap;
                t2.MaxWidth = 480;
                t2.LineHeight = 20;
                stack.Children.Add(t2);
            }

            border.Child = stack;
            win.Content = border;

            win.SourceInitialized += delegate
            {
                IntPtr h = new WindowInteropHelper(win).Handle;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
            };

            win.Loaded += delegate
            {
                Rect wa = SystemParameters.WorkArea;
                win.Left = wa.Left + (wa.Width - win.ActualWidth) / 2;
                win.Top = wa.Top + (wa.Height - win.ActualHeight) * 0.62;
            };

            win.MouseDown += delegate { try { win.Close(); } catch { } };

            if (ms > 400)
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320));
                fade.BeginTime = TimeSpan.FromMilliseconds(ms - 320);
                win.BeginAnimation(Window.OpacityProperty, fade);
            }

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(ms);
            timer.Tick += delegate
            {
                timer.Stop();
                try { win.Close(); } catch { }
            };
            timer.Start();

            return win;
        }
    }

    // ------------------------------------------------------------ 入口
    public static class Program
    {
        // 想改循环的刷新率，就改这两行
        const int HzA = 120;
        const int HzB = 60;

        [STAThread]
        public static void Main(string[] args)
        {
            Native.SetProcessDPIAware();

            // 连续双击两次时不至于连切两次
            var guard = new Mutex(false, "Local\\RefreshSwitch.Switching");
            bool owned = false;
            try { owned = guard.WaitOne(0); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned) return;

            int forced = 0;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], "--rate", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    int.TryParse(args[i + 1], out forced);

            int target = 0;
            string info = "";
            int newHz = 0;
            bool changed = false;
            string err = null;

            var app = new Application();

            try
            {
                var devs = Display.GetDevices();
                if (devs.Count == 0) { err = "没有检测到已连接的显示器"; }
                else
                {
                    var cur = Display.Current(devs[0]);
                    int now = (int)cur.dmDisplayFrequency;
                    target = (forced > 0) ? forced : (now == HzA ? HzB : HzA);

                    if (!Display.TrySet(target, out newHz, out info, out err, out changed))
                    {
                        if (string.IsNullOrEmpty(err)) err = "切换失败";
                    }
                }
            }
            catch (Exception ex) { err = ex.Message; }
            finally
            {
                if (owned) { try { guard.ReleaseMutex(); } catch { } }
            }

            Window win;
            if (!string.IsNullOrEmpty(err))
                win = Hud.Build("切换失败", err, Color.FromRgb(0xF8, 0x71, 0x71), 5000);
            else if (changed)
                win = Hud.Build("已切换到 " + newHz + " Hz", info, Color.FromRgb(0x4A, 0xDE, 0x80), 3000);
            else
                win = Hud.Build("已经是 " + newHz + " Hz", info, Color.FromRgb(0xFB, 0xBF, 0x24), 3000);

            app.Run(win);
        }
    }
}
