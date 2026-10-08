// 鼠标定时点击器 —— 主窗口逻辑
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClickTimer
{
    internal sealed partial class MainForm : Form
    {
        private const string BaseTitle = "鼠标定时点击器";
        private const int HotkeyId = 0x4C31;

        private readonly AppSettings settings;
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private ClickEngine engine;
        private ClickJob currentJob;
        private bool hotkeyOn;
        private bool autoMinimized;
        private Phase shownPhase = Phase.Idle;
        private string lastTitle = "";
        private bool picking;
        private PickSession currentPick;
        private bool heldPending;   // 左键可能仍按着，等待用户按 F9 在原地松开

        public MainForm()
        {
            BuildUi();
            settings = AppSettings.Load();
            row1.Value = settings.First;
            row2.Value = settings.Second;
            rowAway.Value = settings.Away;
            chkSecond.Checked = settings.UseSecond;
            chkTop.Checked = settings.TopMost;
            TopMost = settings.TopMost;
            nudDelay.Value = Clamp(settings.Delay, nudDelay);
            nudInterval.Value = Clamp(settings.Interval, nudInterval);
            row2.Enabled = chkSecond.Checked;
            UpdateTimeHints();

            foreach (PosRow r in new PosRow[] { row1, row2, rowAway })
            {
                PosRow row = r;
                row.Pick.Click += delegate { BeginPick(row); };
                row.Show.Click += delegate { ShowMarker(row); };
            }
            chkSecond.CheckedChanged += delegate { row2.Enabled = chkSecond.Checked && engine == null && !heldPending; };
            chkTop.CheckedChanged += delegate { TopMost = chkTop.Checked; };
            nudDelay.ValueChanged += delegate { UpdateTimeHints(); };
            nudInterval.ValueChanged += delegate { UpdateTimeHints(); };
            btnStart.Click += delegate { StartRun(); };
            btnStop.Click += delegate { StopRun(); };

            uiTimer.Interval = 50;
            uiTimer.Tick += delegate { RefreshStatus(); };
            SetStatus(Theme.Muted, "就绪", Theme.Text, "设置好位置和时间后点「开始」", "", Theme.Text, -1f);
            ActiveControl = txtLog;   // 初始焦点放在只读日志上，避免误按键改坐标或误触发「开始」
        }

        private static decimal Clamp(decimal v, NumericUpDown n)
        {
            return Math.Min(n.Maximum, Math.Max(n.Minimum, v));
        }

        // ================= 拾取 / 显示 =================
        private void BeginPick(PosRow row)
        {
            if (engine != null || picking || heldPending) return;
            picking = true;
            Point home = Location;
            Enabled = false;
            PickSession session;
            try
            {
                // 第 1 步：立刻铺上全屏遮罩（看不见，但接住所有点击）。无论之后怎么结束，回调都只走一次，主窗口一定会回来
                session = PickSession.Begin(row.Title, row.Accent, Cursor.Position, delegate(Point? p)
                {
                    EndPick(home);
                    if (!p.HasValue) return;
                    row.Value = p;
                    SaveSettings();
                    Log(DateTime.Now, string.Format("已设置{0}：({1}, {2})", row.Title, p.Value.X, p.Value.Y));
                    MarkerForm.Flash(p.Value, row.Accent, DpiScale());
                });
            }
            catch (Exception ex)
            {
                EndPick(home);
                Warn("拾取失败：" + ex.Message);
                return;
            }
            currentPick = session;

            // 第 2 步：遮罩已接住点击，再把主窗口移出屏幕（不隐藏，本程序保持在前台），稍等屏幕刷新后截图
            Rectangle vs = SystemInformation.VirtualScreen;
            Location = new Point(vs.Left - Width - 200, vs.Top - Height - 200);
            After(150, delegate
            {
                if (session.IsFinished) return;   // 准备期间已被取消
                try { session.Arm(); }
                catch (Exception ex)
                {
                    session.Cancel();
                    Warn("拾取失败：" + ex.Message);
                }
            });
        }

        private void EndPick(Point home)
        {
            picking = false;
            currentPick = null;
            Location = home;
            Enabled = true;
            Activate();
        }

        /// <summary>在界面线程上延迟执行一次。</summary>
        private static void After(int milliseconds, Action action)
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = milliseconds;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                action();
            };
            t.Start();
        }

        private void ShowMarker(PosRow row)
        {
            Point? p = row.Value;
            if (!p.HasValue) { Warn("「" + row.Title + "」还没有设置"); return; }
            MarkerForm.Flash(p.Value, row.Accent, DpiScale());
        }

        private float DpiScale()
        {
            using (Graphics g = CreateGraphics()) return g.DpiX / 96f;
        }

        // ================= 开始 / 停止 =================
        private void StartRun()
        {
            if (engine != null || picking || heldPending) return;
            Point? p1 = row1.Value;
            Point? p2 = chkSecond.Checked ? row2.Value : p1;
            Point? pa = rowAway.Value;
            if (!Check(p1, "点击位置") || !Check(p2, "第二次位置") || !Check(pa, "移开位置")) return;

            SaveSettings();
            ClickJob job = new ClickJob();
            job.First = p1.Value;
            job.Second = p2.Value;
            job.Away = pa.Value;
            job.DelaySeconds = (double)nudDelay.Value;
            job.IntervalSeconds = (double)nudInterval.Value;
            currentJob = job;

            engine = new ClickEngine(job, new WinMouse());
            engine.Log += delegate(DateTime t, string s) { PostToUi(delegate { Log(t, s); }); };
            engine.Finished += delegate(RunResult result) { PostToUi(delegate { OnFinished(result); }); };

            SetRunningUi(true);
            Log(DateTime.Now, string.Format("开始：{0:0.000} 秒后点击 ({1}, {2})，再隔 {3:0.000} 秒点击 ({4}, {5})，然后移到 ({6}, {7})",
                job.DelaySeconds, job.First.X, job.First.Y, job.IntervalSeconds, job.Second.X, job.Second.Y, job.Away.X, job.Away.Y));

            hotkeyOn = Native.RegisterHotKey(Handle, HotkeyId, Native.MOD_NOREPEAT, Native.VK_F9);
            if (!hotkeyOn) Log(DateTime.Now, "提示：F9 已被其他程序占用，请用「停止」按钮");

            // 本窗口挡住了点击位置时，先最小化，结束后再还原
            if (WindowState == FormWindowState.Normal && (Bounds.Contains(job.First) || Bounds.Contains(job.Second)))
            {
                autoMinimized = true;
                WindowState = FormWindowState.Minimized;
                Log(DateTime.Now, "本窗口挡住了点击位置，已自动最小化");
            }

            shownPhase = Phase.Idle;
            engine.Start();
            uiTimer.Start();
            RefreshStatus();
        }

        private bool Check(Point? p, string name)
        {
            if (!p.HasValue) { Warn("请先设置「" + name + "」（点「拾取」）"); return false; }
            foreach (Screen s in Screen.AllScreens) if (s.Bounds.Contains(p.Value)) return true;
            Warn("「" + name + "」不在任何屏幕上，请重新拾取");
            return false;
        }

        private void StopRun()
        {
            if (engine != null) engine.Stop();
            else if (heldPending) TryRelease();
        }

        private void OnFinished(RunResult result)
        {
            uiTimer.Stop();
            engine = null;

            string title, detail;
            int level;
            result.Describe(out title, out detail, out level);
            Log(DateTime.Now, "结果：" + (result.ButtonMayBeHeld ? "出错，" + result.ClickSummary() : title + "。" + detail));

            if (result.ButtonMayBeHeld)
            {
                EnterHeldMode();   // 保留 F9 热键，锁住「开始」「拾取」，直到确认左键已松开
            }
            else
            {
                if (hotkeyOn) { Native.UnregisterHotKey(Handle, HotkeyId); hotkeyOn = false; }
                SetRunningUi(false);
                Color c = level == 0 ? Theme.Green : level == 1 ? Theme.Orange : Theme.Red;
                SetStatus(c, title, c,
                    level == 0 ? string.Format("{0:HH:mm:ss} {1}", DateTime.Now, detail) : detail,
                    level == 0 ? "完成" : "", Theme.Green, level == 0 ? 1f : -1f);
            }
            SetTitle(BaseTitle);

            if (autoMinimized)
            {
                autoMinimized = false;
                if (WindowState == FormWindowState.Minimized) Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            }
        }

        // ================= 左键可能仍按着：恢复流程 =================
        private void EnterHeldMode()
        {
            heldPending = true;
            SetRunningUi(false);
            btnStop.Text = hotkeyOn ? "原地松开左键 (F9)" : "原地松开左键";
            SetStatus(Theme.Red, "左键可能仍按着 · 请不要移动鼠标", Theme.Red,
                hotkeyOn ? "按 F9 在原地松开左键（会完成这次点击）" : "F9 不可用：请在原地单击一次左键（会完成这次点击）",
                hotkeyOn ? "F9" : "", Theme.Red, -1f);
            Log(DateTime.Now, "左键可能仍处于按下状态。移动鼠标可能造成拖拽，请按 F9 在原地松开（在原地松开会完成这次点击）");
        }

        private void TryRelease()
        {
            if (ReleaseButton())
            {
                heldPending = false;
                if (hotkeyOn) { Native.UnregisterHotKey(Handle, HotkeyId); hotkeyOn = false; }
                btnStop.Text = "停止 (F9)";
                SetRunningUi(false);
                SetStatus(Theme.Muted, "左键已松开", Theme.Text, "可以重新开始", "", Theme.Text, -1f);
                Log(DateTime.Now, "已在原地松开左键");
            }
            else
            {
                lblNext.Text = "松开失败：请在原地单击一次左键（会完成这次点击）";
                Log(DateTime.Now, "⚠ 松开左键失败（系统拒绝了输入）。可在原地单击一次左键；如已单击过，可关闭本程序重新打开");
            }
        }

        private static bool ReleaseButton()
        {
            for (int i = 0; i < 3; i++)
            {
                try { if (Native.PrimaryButton(false)) return true; }
                catch (Exception) { }
                Thread.Sleep(20);
            }
            return false;
        }

        private void SetRunningUi(bool running)
        {
            bool idle = !running && !heldPending;
            btnStart.Enabled = idle;
            btnStop.Enabled = running || heldPending;
            row1.Enabled = idle;
            rowAway.Enabled = idle;
            row2.Enabled = idle && chkSecond.Checked;
            chkSecond.Enabled = idle;
            nudDelay.Enabled = nudInterval.Enabled = idle;
        }

        // ================= 状态显示 =================
        private void SetStatus(Color bar, string phase, Color phaseColor, string next, string countdown, Color countdownColor, float progress)
        {
            statusCard.AccentBar = bar;
            statusCard.ProgressColor = bar;
            statusCard.Progress = progress;
            lblPhase.Text = phase;
            lblPhase.ForeColor = phaseColor;
            lblNext.Text = next;
            lblCountdown.Text = countdown;
            lblCountdown.ForeColor = countdownColor;
        }

        private void RefreshStatus()
        {
            if (engine == null || currentJob == null) return;
            Phase ph;
            double remain;
            int clicks;
            engine.GetStatus(out ph, out remain, out clicks);
            if (ph != Phase.WaitFirst && ph != Phase.WaitSecond) return;

            bool first = ph == Phase.WaitFirst;
            if (ph != shownPhase)
            {
                shownPhase = ph;
                Color c = first ? Theme.Blue : Theme.Orange;
                Point target = first ? currentJob.First : currentJob.Second;
                SetStatus(c, first ? "等待第 1 次点击" : "已点击 1 次，等待第 2 次", Theme.Text,
                    string.Format("预计 {0:HH:mm:ss} 点击 ({1}, {2})", DateTime.Now.AddSeconds(remain), target.X, target.Y),
                    lblCountdown.Text, Theme.Text, 0f);
            }
            double total = first ? currentJob.DelaySeconds : currentJob.IntervalSeconds;
            statusCard.Progress = total > 0 ? (float)Math.Max(0, Math.Min(1, 1 - remain / total)) : 1f;
            string cd = FormatCountdown(remain);
            if (lblCountdown.Text != cd) lblCountdown.Text = cd;
            SetTitle(cd + "  " + (first ? "等待第1次" : "等待第2次") + " - " + BaseTitle);
        }

        private void SetTitle(string t)
        {
            if (t != lastTitle) { Text = t; lastTitle = t; }
        }

        private static string FormatCountdown(double sec)
        {
            TimeSpan t = TimeSpan.FromSeconds(Math.Ceiling(sec * 10) / 10);   // 显示 0.1 秒，向上取整，到 0 才点击
            if (t.TotalHours >= 1) return string.Format("{0}:{1:00}:{2:00}.{3}", (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds / 100);
            return string.Format("{0:00}:{1:00}.{2}", t.Minutes, t.Seconds, t.Milliseconds / 100);
        }

        private void UpdateTimeHints()
        {
            lblDelayHint.Text = nudDelay.Value >= 60 ? "= " + Friendly(nudDelay.Value) : "开始 → 第 1 次点击";
            lblIntervalHint.Text = nudInterval.Value >= 60 ? "= " + Friendly(nudInterval.Value) : "第 1 次 → 第 2 次，最短 0.1 秒";
        }

        private static string Friendly(decimal sec)
        {
            TimeSpan t = TimeSpan.FromSeconds((double)sec);
            string s = t.TotalHours >= 1 ? string.Format("{0} 时 {1} 分", (int)t.TotalHours, t.Minutes) : string.Format("{0} 分", t.Minutes);
            double rest = t.Seconds + t.Milliseconds / 1000.0;
            if (rest > 0) s += string.Format(" {0:0.###} 秒", rest);
            return s;
        }

        // ================= 杂项 =================
        private void Warn(string text)
        {
            SetStatus(Theme.Red, text, Theme.Red, "", "", Theme.Text, -1f);
            System.Media.SystemSounds.Beep.Play();
        }

        private void Log(DateTime t, string text)
        {
            if (txtLog.TextLength > 60000) txtLog.Clear();
            txtLog.AppendText(string.Format("{0:HH:mm:ss.fff}  {1}\r\n", t, text));
        }

        private void PostToUi(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); }
            catch (Exception) { }
        }

        private void SaveSettings()
        {
            settings.First = row1.Value;
            settings.Second = row2.Value;
            settings.Away = rowAway.Value;
            settings.UseSecond = chkSecond.Checked;
            settings.TopMost = chkTop.Checked;
            settings.Delay = nudDelay.Value;
            settings.Interval = nudInterval.Value;
            settings.Save();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                if (engine != null) StopRun();
                else if (heldPending) TryRelease();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 拾取期间主窗口在屏幕外；此时收到的关闭请求（如 Alt+F4 落到主窗口）只当作"取消拾取"
            if (picking && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (currentPick != null) currentPick.Cancel();
                return;
            }
            if (engine != null)
            {
                DialogResult r = MessageBox.Show(this, "正在计时中，确定停止并退出吗？", BaseTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
                // 确认框开着时任务可能已经结束（engine 已被置空），所以重新取一次
                ClickEngine running = engine;
                if (running != null)
                {
                    running.Stop();
                    running.Join(3000);
                    if (running.ButtonIsDown) ReleaseButton();   // 后台线程没确认抬起（卡住或抬起失败），退出前补发
                }
            }
            if (heldPending) ReleaseButton();   // 左键可能仍按着：退出前在原地补发一次抬起
            if (hotkeyOn) { Native.UnregisterHotKey(Handle, HotkeyId); hotkeyOn = false; }
            SaveSettings();
            base.OnFormClosing(e);
        }
    }
}
