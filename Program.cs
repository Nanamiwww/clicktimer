// 鼠标定时点击器 —— 程序入口与设置保存
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("鼠标定时点击器")]
[assembly: AssemblyProduct("鼠标定时点击器")]
[assembly: AssemblyDescription("延迟后定点点击，间隔后再次点击，然后移开鼠标")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace ClickTimer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Native.EnableDpiAwareness();   // 必须在创建任何窗口之前
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    /// <summary>设置保存在 %APPDATA%\ClickTimer\settings.ini，下次打开自动恢复。</summary>
    internal sealed class AppSettings
    {
        public Point? First;
        public Point? Second;
        public Point? Away;
        public bool UseSecond;
        public bool TopMost;
        public decimal Delay = 5m;
        public decimal Interval = 10m;

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickTimer");
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                Dictionary<string, string> kv = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) kv[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                s.First = ReadPoint(kv, "First");
                s.Second = ReadPoint(kv, "Second");
                s.Away = ReadPoint(kv, "Away");
                s.UseSecond = ReadBool(kv, "UseSecond");
                s.TopMost = ReadBool(kv, "TopMost");
                s.Delay = ReadDecimal(kv, "Delay", s.Delay);
                s.Interval = ReadDecimal(kv, "Interval", s.Interval);
            }
            catch (Exception) { }
            return s;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                WritePoint(sb, "First", First);
                WritePoint(sb, "Second", Second);
                WritePoint(sb, "Away", Away);
                sb.AppendLine("UseSecond=" + (UseSecond ? "1" : "0"));
                sb.AppendLine("TopMost=" + (TopMost ? "1" : "0"));
                sb.AppendLine("Delay=" + Delay.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("Interval=" + Interval.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        private static Point? ReadPoint(Dictionary<string, string> kv, string key)
        {
            string v;
            if (!kv.TryGetValue(key, out v)) return null;
            string[] parts = v.Split(',');
            int x, y;
            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                return new Point(x, y);
            return null;
        }

        private static void WritePoint(StringBuilder sb, string key, Point? p)
        {
            if (p.HasValue) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}={1},{2}", key, p.Value.X, p.Value.Y));
        }

        private static bool ReadBool(Dictionary<string, string> kv, string key)
        {
            string v;
            return kv.TryGetValue(key, out v) && v == "1";
        }

        private static decimal ReadDecimal(Dictionary<string, string> kv, string key, decimal fallback)
        {
            string v;
            decimal d;
            if (kv.TryGetValue(key, out v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }
    }
}
