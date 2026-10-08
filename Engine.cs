// 鼠标定时点击器 —— 计时与点击引擎
// 独立高优先级线程 + Stopwatch 计时；每次等待的最后 25 毫秒持续等待（忙等），不受系统计时器降频影响。
// 实际误差仍会受线程调度、系统负载等影响，日志里记录每次实测的延迟和间隔。
// 安全原则：光标没确认到位就不点；按下后一定尝试抬起；任何一步失败立即停止并如实报告。
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime;
using System.Threading;

namespace ClickTimer
{
    internal enum Phase { Idle, WaitFirst, WaitSecond, Done, Stopped, Failed }

    /// <summary>鼠标操作接口；正式运行用 WinMouse，测试时可换成模拟实现。</summary>
    internal interface IMouse
    {
        bool MoveTo(Point p);
        bool GetPosition(out Point p);
        bool Button(bool down);
    }

    internal sealed class WinMouse : IMouse
    {
        public bool MoveTo(Point p) { return Native.MoveCursor(p); }
        public bool GetPosition(out Point p) { return Native.GetCursor(out p); }
        public bool Button(bool down) { return Native.PrimaryButton(down); }
    }

    internal sealed class ClickJob
    {
        public Point First;
        public Point Second;
        public Point Away;
        public double DelaySeconds;
        public double IntervalSeconds;
    }

    /// <summary>一次运行的最终结果。</summary>
    internal sealed class RunResult
    {
        public Phase Phase = Phase.Done;   // Done / Stopped / Failed
        public int ClicksSent;             // 按下和抬起都已正常发出的点击次数
        public int UncertainClick;         // 效果不确定的那一次点击（0 表示没有）：可能变成拖拽，或左键可能仍按着
        public bool ButtonMayBeHeld;       // 抬起没能发出，左键可能仍处于按下状态
        public bool AwayAttempted;         // 是否尝试过把鼠标移开
        public bool MovedAway;             // 鼠标是否已确认移到"移开位置"
        public string Error;               // 出错原因（详细内容也写在日志里）

        /// <summary>点击情况摘要，例如"第 1 次已发出，第 2 次未执行"。</summary>
        public string ClickSummary()
        {
            if (UncertainClick == 1) return "第 1 次可能未生效，第 2 次未执行";
            if (UncertainClick == 2) return "第 1 次已发出，第 2 次可能未生效";
            if (ClicksSent == 0) return "没有发出点击";
            if (ClicksSent == 1) return "第 1 次已发出，第 2 次未执行";
            return "两次点击都已发出";
        }

        /// <summary>界面状态文字。level：0 正常完成，1 需要注意，2 停止或出错。</summary>
        public void Describe(out string title, out string detail, out int level)
        {
            string away = AwayAttempted && !MovedAway ? "；鼠标没能移开" : "";
            if (ButtonMayBeHeld)
            {
                title = "出错：" + ClickSummary();
                detail = "左键可能仍按着：请不要移动鼠标，按 F9 在原地松开";
                level = 2;
            }
            else if (Phase == Phase.Done && MovedAway)
            {
                title = "完成"; detail = "两次点击都已发出，鼠标已移开"; level = 0;
            }
            else if (Phase == Phase.Done)
            {
                title = "完成，但鼠标没能移开"; detail = "两次点击都已发出；鼠标可能还停在按钮上（见日志）"; level = 1;
            }
            else if (Phase == Phase.Stopped)
            {
                title = "已停止"; detail = ClickSummary() + away; level = 2;
            }
            else
            {
                title = "出错，已停止"; detail = ClickSummary() + away + "（原因见日志）"; level = 2;
            }
        }
    }

    internal sealed class ClickEngine
    {
        public const double MinIntervalSeconds = 0.1;   // 必须大于"提前移动 30 ms + 按住 40 ms"

        private const int PreMoveMs = 30;   // 提前 30 ms 把鼠标移到按钮上，让目标程序先识别到悬停
        private const int HoldMs = 40;      // 按下到抬起的时长
        private const int SettleMs = 60;    // 第二次点击抬起后，停一下再把鼠标移开
        private const int SpinMs = 25;      // 每次等待的最后 25 ms 忙等

        private readonly ClickJob job;
        private readonly IMouse mouse;
        private readonly ManualResetEvent cancel = new ManualResetEvent(false);
        private readonly Stopwatch clock = new Stopwatch();
        private readonly object gate = new object();
        private volatile bool stopRequested;
        private volatile bool buttonDown;   // 已发出按下、尚未确认抬起
        private Thread thread;

        private Phase phase = Phase.Idle;
        private long targetTicks;
        private int clicksDone;
        private bool cursorTouched;   // 是否已经把光标移到过点击位置

        /// <summary>日志（在后台线程触发）。</summary>
        public event Action<DateTime, string> Log;
        /// <summary>结束（在后台线程触发）。</summary>
        public event Action<RunResult> Finished;

        public ClickEngine(ClickJob job, IMouse mouse)
        {
            if (job.DelaySeconds < 0) throw new ArgumentOutOfRangeException("job", "延迟时间不能小于 0");
            if (job.IntervalSeconds < MinIntervalSeconds) throw new ArgumentOutOfRangeException("job", "间隔时间不能小于 0.1 秒");
            this.job = job;
            this.mouse = mouse;
        }

        public void Start()
        {
            clock.Start();   // 延迟从这一刻开始计算
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.Name = "ClickEngine";
            thread.Start();
        }

        /// <summary>停止尚未执行的点击。正在进行的一次点击（40 ms）会先完成，第二次点击后的移开也会照常执行。</summary>
        public void Stop()
        {
            stopRequested = true;
            cancel.Set();
        }

        public bool Join(int milliseconds) { return thread == null || thread.Join(milliseconds); }

        /// <summary>是否处在"已按下、还没确认抬起"的状态（退出程序时用来做最后的补救）。</summary>
        public bool ButtonIsDown { get { return buttonDown; } }

        /// <summary>供界面刷新：当前阶段、距离下一次点击的剩余秒数、已发出的点击次数。</summary>
        public void GetStatus(out Phase p, out double remainingSeconds, out int clicks)
        {
            lock (gate)
            {
                p = phase;
                clicks = clicksDone;
                if (p == Phase.WaitFirst || p == Phase.WaitSecond)
                    remainingSeconds = Math.Max(0.0, (targetTicks - clock.ElapsedTicks) / (double)Stopwatch.Frequency);
                else
                    remainingSeconds = 0.0;
            }
        }

        private void Run()
        {
            RunResult r = new RunResult();
            Native.BeginRunMode();
            // 运行期间尽量避免 .NET 垃圾回收造成的停顿（只是减少，不能完全消除）
            GCLatencyMode oldGc = GCSettings.LatencyMode;
            try { GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency; } catch (Exception) { }
            try
            {
                // ---- 第一次点击 ----
                long t1 = SecondsToTicks(job.DelaySeconds);
                SetPhase(Phase.WaitFirst, t1);
                if (!WaitUntil(t1 - MsToTicks(PreMoveMs))) { r.Phase = Phase.Stopped; return; }
                cursorTouched = true;
                mouse.MoveTo(job.First);   // 提前移过去；是否到位在按下前统一核对
                if (!WaitUntil(t1)) { r.Phase = Phase.Stopped; return; }
                long c1;
                if (!DoClick(job.First, 1, r, out c1)) return;
                Emit(string.Format("第 1 次点击已发出 ({0}, {1})，实际延迟 {2:0.000} 秒",
                    job.First.X, job.First.Y, TicksToSeconds(c1)));

                // ---- 第二次点击：间隔从第一次按下的实际时刻算起 ----
                long t2 = c1 + SecondsToTicks(job.IntervalSeconds);
                SetPhase(Phase.WaitSecond, t2);
                if (!WaitUntil(t2 - MsToTicks(PreMoveMs))) { r.Phase = Phase.Stopped; return; }
                mouse.MoveTo(job.Second);
                if (!WaitUntil(t2)) { r.Phase = Phase.Stopped; return; }
                long c2;
                if (!DoClick(job.Second, 2, r, out c2)) return;
                Emit(string.Format("第 2 次点击已发出 ({0}, {1})，实际间隔 {2:0.000} 秒",
                    job.Second.X, job.Second.Y, TicksToSeconds(c2 - c1)));

                // ---- 移开鼠标，防止误触 ----
                Thread.Sleep(SettleMs);
                r.AwayAttempted = true;
                r.MovedAway = MoveAway();
            }
            catch (Exception ex)
            {
                Fail(r, "程序出错：" + ex.Message);
            }
            finally
            {
                // 停止或出错时：如果光标已被移到按钮上、且左键确认已松开，就把它移开（只移动，不点击）
                if (r.Phase != Phase.Done && cursorTouched && !r.ButtonMayBeHeld)
                {
                    r.AwayAttempted = true;
                    r.MovedAway = MoveAway();
                }
                Native.EndRunMode();
                try { GCSettings.LatencyMode = oldGc; } catch (Exception) { }
                lock (gate) { phase = r.Phase; }
                Action<RunResult> h = Finished;
                if (h != null) h(r);
            }
        }

        /// <summary>一次完整点击。返回 false 表示失败（原因已写入结果和日志），调用方应立即停止。</summary>
        private bool DoClick(Point p, int index, RunResult r, out long downAt)
        {
            downAt = 0;
            string where;
            if (!PlaceCursor(p, out where))
            {
                return Fail(r, string.Format(
                    "鼠标没能移到第 {0} 次点击位置 ({1}, {2})，{3}。为避免点错，这次点击已取消（可能屏幕已锁定、屏幕布局变了，或有程序限制了鼠标活动范围）",
                    index, p.X, p.Y, where));
            }
            if (stopRequested) { r.Phase = Phase.Stopped; return false; }   // 核对光标期间按了停止，就不再按下

            // 默认按"不确定"处理：只有抬起前确认光标仍在原位、并且抬起成功，才算正常发出
            bool downSent = false;
            bool verifiedBeforeUp = false;
            try
            {
                downAt = clock.ElapsedTicks;
                downSent = mouse.Button(true);
                if (!downSent)
                {
                    return Fail(r, string.Format(
                        "第 {0} 次点击没有发出去：系统拒绝了这次输入（可能屏幕已锁定，或目标程序以管理员身份运行而本工具没有）", index));
                }
                buttonDown = true;
                Thread.Sleep(HoldMs);
                // 抬起前把光标拉回原位，避免按住期间被碰动变成拖拽；拉不回来（或核对出错）也要立即抬起
                string now;
                verifiedBeforeUp = PlaceCursor(p, out now);
            }
            finally
            {
                if (downSent)
                {
                    bool released = Release();
                    if (released) buttonDown = false;
                    else r.ButtonMayBeHeld = true;

                    if (released && verifiedBeforeUp)
                    {
                        r.ClicksSent = index;
                        lock (gate) { clicksDone = index; }
                    }
                    else
                    {
                        r.UncertainClick = index;
                    }
                }
            }

            if (r.ButtonMayBeHeld)
            {
                return Fail(r, string.Format(
                    "第 {0} 次点击的「抬起」重试 3 次仍没有发出去，鼠标左键可能仍处于按下状态。请不要移动鼠标，按 F9 在原地松开左键", index));
            }
            if (!verifiedBeforeUp)
            {
                return Fail(r, string.Format(
                    "第 {0} 次点击按住期间鼠标被移走，且拉不回原位，已在当前位置抬起。这次点击可能变成了拖拽、未必生效，后续操作已停止", index));
            }
            return true;
        }

        /// <summary>移到指定点并读回实际位置确认（允许 1 像素误差），最多尝试 3 次。失败时 where 说明实际情况。</summary>
        private bool PlaceCursor(Point p, out string where)
        {
            where = "读不到光标位置";
            for (int i = 0; i < 3; i++)
            {
                mouse.MoveTo(p);
                Point actual;
                if (mouse.GetPosition(out actual))
                {
                    if (Near(actual, p)) { where = ""; return true; }
                    where = string.Format("实际在 ({0}, {1})", actual.X, actual.Y);
                }
                else
                {
                    where = "读不到光标位置";
                }
                Thread.Sleep(5);
            }
            return false;
        }

        /// <summary>发出抬起，失败时重试，共 3 次。</summary>
        private bool Release()
        {
            for (int i = 0; i < 3; i++)
            {
                try { if (mouse.Button(false)) return true; }
                catch (Exception) { }
                Thread.Sleep(20);
            }
            return false;
        }

        /// <summary>把鼠标移到"移开位置"并确认到位；失败只记录，不抛异常。</summary>
        private bool MoveAway()
        {
            try
            {
                string where;
                if (PlaceCursor(job.Away, out where))
                {
                    Emit(string.Format("鼠标已移开到 ({0}, {1})", job.Away.X, job.Away.Y));
                    return true;
                }
                Emit(string.Format("⚠ 鼠标没能移开到 ({0}, {1})，{2}（可能屏幕已锁定，或有程序限制了鼠标活动范围）",
                    job.Away.X, job.Away.Y, where));
            }
            catch (Exception ex)
            {
                Emit("⚠ 移开鼠标时出错：" + ex.Message);
            }
            return false;
        }

        private bool Fail(RunResult r, string message)
        {
            r.Phase = Phase.Failed;
            r.Error = message;
            Emit("✖ " + message);
            return false;
        }

        /// <summary>等待到指定时刻；返回 false 表示被停止。</summary>
        private bool WaitUntil(long target)
        {
            while (true)
            {
                if (stopRequested) return false;
                long remain = target - clock.ElapsedTicks;
                if (remain <= 0) return true;
                double ms = remain * 1000.0 / Stopwatch.Frequency;
                if (ms > SpinMs)
                {
                    // 粗等待：即使系统计时精度降到 15.6 ms，醒来时也还剩约 9 ms 以上，交给下面的忙等
                    if (cancel.WaitOne((int)Math.Min(ms - SpinMs, 200))) return false;
                }
                else
                {
                    Thread.SpinWait(50);
                }
            }
        }

        private static bool Near(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
        }

        private void SetPhase(Phase p, long target)
        {
            lock (gate) { phase = p; targetTicks = target; }
        }

        private void Emit(string text)
        {
            Action<DateTime, string> h = Log;
            if (h != null) h(DateTime.Now, text);
        }

        private static long SecondsToTicks(double s) { return (long)Math.Round(s * Stopwatch.Frequency); }
        private static long MsToTicks(int ms) { return (long)ms * Stopwatch.Frequency / 1000; }
        private static double TicksToSeconds(long t) { return t / (double)Stopwatch.Frequency; }
    }
}
