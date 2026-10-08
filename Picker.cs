// 鼠标定时点击器 —— 定格拾取坐标（带放大镜）与位置标记
//
// 拾取流程（保证任何时刻都没有"无保护空档"）：
//   1. 一点「拾取」就立刻在每块屏幕铺一层全屏遮罩。此时遮罩几乎全透明（肉眼看不见），但能接住所有鼠标点击，
//      这期间的点击一律吞掉，不会落到后面的真实程序上。
//   2. 主窗口再移到屏幕外，稍等后截图，把截图放进遮罩，遮罩变为不透明的"定格画面"，这时才接受选点。
//   3. 双击「拾取」的第二下（系统双击时间内、落在同一位置）会被忽略。
//   4. 任何方式结束（选中、右键、Esc、Alt+F4、窗口被关闭）都只回调一次，所有遮罩一起关闭。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ClickTimer
{
    internal sealed class PickSession
    {
        private readonly List<PickOverlay> overlays = new List<PickOverlay>();
        private readonly Action<Point?> done;
        private readonly int ignoreUntil;     // 双击过滤截止时刻（Environment.TickCount）
        private readonly Point ignorePoint;   // 触发拾取的那次点击位置
        private readonly Size ignoreHalf;
        private bool finished;
        private bool armed;

        /// <summary>开始拾取：立刻铺上接住点击的遮罩。done 在结束时恰好调用一次：选中返回坐标，取消返回 null。</summary>
        public static PickSession Begin(string what, Color accent, Point triggerPoint, Action<Point?> done)
        {
            return new PickSession(what, accent, triggerPoint, done);
        }

        private PickSession(string what, Color accent, Point triggerPoint, Action<Point?> done)
        {
            this.done = done;
            ignorePoint = triggerPoint;
            ignoreUntil = unchecked(Environment.TickCount + SystemInformation.DoubleClickTime);
            Size dbl = SystemInformation.DoubleClickSize;
            ignoreHalf = new Size(Math.Max(2, dbl.Width / 2), Math.Max(2, dbl.Height / 2));
            try
            {
                foreach (Screen s in Screen.AllScreens)
                    overlays.Add(new PickOverlay(this, s.Bounds, what, accent));
                foreach (PickOverlay o in overlays) o.Show();
                ActiveOverlay().Activate();   // 让遮罩接收键盘（Esc 随时可取消）
            }
            catch
            {
                finished = true;
                foreach (PickOverlay o in overlays)
                {
                    try { o.Close(); o.Dispose(); }
                    catch (Exception) { }
                }
                throw;
            }
        }

        public bool IsFinished { get { return finished; } }
        public bool IsArmed { get { return armed; } }

        /// <summary>截图并显示定格画面。应在主窗口移开之后调用；失败时抛出异常，由调用方取消。</summary>
        public void Arm()
        {
            if (finished || armed) return;
            Bitmap[] shots = new Bitmap[overlays.Count];
            try
            {
                for (int i = 0; i < overlays.Count; i++) shots[i] = Capture(overlays[i].Bounds);
            }
            catch
            {
                foreach (Bitmap b in shots) if (b != null) b.Dispose();
                throw;
            }
            for (int i = 0; i < overlays.Count; i++) overlays[i].SetShot(shots[i]);
            armed = true;
            PickOverlay active = ActiveOverlay();
            active.ShowLensAt(active.PointToClient(Cursor.Position));
        }

        private PickOverlay ActiveOverlay()
        {
            Point c = Cursor.Position;
            foreach (PickOverlay o in overlays) if (o.Bounds.Contains(c)) return o;
            return overlays[0];
        }

        private static Bitmap Capture(Rectangle r)
        {
            Bitmap bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        /// <summary>这次按下是否应当忽略：定格画面出现前一律忽略；双击「拾取」的第二下也忽略。</summary>
        internal bool ShouldIgnorePress(Point screenPoint)
        {
            if (!armed) return true;
            bool inTime = unchecked(Environment.TickCount - ignoreUntil) < 0;
            bool near = Math.Abs(screenPoint.X - ignorePoint.X) <= ignoreHalf.Width &&
                        Math.Abs(screenPoint.Y - ignorePoint.Y) <= ignoreHalf.Height;
            return inTime && near;
        }

        /// <summary>取消拾取（等同于按 Esc）。</summary>
        public void Cancel() { Finish(null); }

        internal void Finish(Point? result)
        {
            if (finished) return;   // 关闭遮罩时会再次进入这里，直接返回
            finished = true;
            try
            {
                foreach (PickOverlay o in overlays.ToArray())
                {
                    try { if (!o.IsClosing && !o.IsDisposed) o.Close(); }
                    catch (Exception) { }
                }
            }
            finally
            {
                done(result);   // 无论关闭遮罩是否顺利，都保证回调，让主窗口回来
            }
        }
    }

    internal sealed class PickOverlay : Form
    {
        private const int LensPixels = 21;   // 放大镜取样 21×21 像素（奇数，正中一个像素就是目标点）
        private const int Zoom = 8;
        private const double ShieldOpacity = 0.005;   // 约 1/255：肉眼看不见，但仍能接住鼠标

        private readonly PickSession session;
        private readonly string what;
        private readonly Color accent;
        private readonly Font font = new Font(Theme.FontName, 10F, FontStyle.Bold);
        private Bitmap shot;
        private Point cursor;
        private bool lensVisible;
        private Rectangle lensRect;
        private bool bannerAtBottom;
        private bool leftAccepted;   // 这次左键按下是在定格画面出现之后、且不是双击的第二下

        public PickOverlay(PickSession session, Rectangle bounds, string what, Color accent)
        {
            this.session = session;
            this.what = what;
            this.accent = accent;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;
            Opacity = ShieldOpacity;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        }

        internal bool IsClosing { get; private set; }

        /// <summary>放入截图：先在不可见状态下画好，再变为不透明，避免闪黑。</summary>
        internal void SetShot(Bitmap bmp)
        {
            shot = bmp;
            Invalidate();
            Update();
            Opacity = 0.999;   // 保持分层窗口，只改透明度，切换时不会重绘闪烁
        }

        public void ShowLensAt(Point clientPoint) { if (shot != null) MoveLens(clientPoint); }

        private string BannerText()
        {
            return "单击选择【" + what + "】    方向键可微调 · 右键或 Esc 取消";
        }

        private Rectangle BannerRect()
        {
            Size text = TextRenderer.MeasureText(BannerText(), font);
            int w = text.Width + 40, h = text.Height + 18;
            int y = bannerAtBottom ? ClientSize.Height - h - 28 : 28;
            return new Rectangle((ClientSize.Width - w) / 2, y, w, h);
        }

        private void MoveLens(Point p)
        {
            if (shot == null) return;
            Rectangle oldLens = lensRect;
            Rectangle oldBanner = BannerRect();
            cursor = p;
            lensVisible = true;

            int size = LensPixels * Zoom;
            int capH = font.Height + 12;
            int x = p.X + 24, y = p.Y + 24;
            if (x + size > ClientSize.Width) x = p.X - 24 - size;
            if (y + size + capH > ClientSize.Height) y = p.Y - 24 - size - capH;
            lensRect = new Rectangle(x, y, size, size + capH);

            // 鼠标靠近提示条时，把提示条挪到另一边，免得挡住要点的按钮
            bool wantBottom = p.Y < oldBanner.Height + 90;
            if (wantBottom != bannerAtBottom)
            {
                bannerAtBottom = wantBottom;
                Invalidate(Inflate(oldBanner));
                Invalidate(Inflate(BannerRect()));
            }
            Invalidate(Inflate(oldLens));
            Invalidate(Inflate(lensRect));
        }

        private static Rectangle Inflate(Rectangle r) { r.Inflate(4, 4); return r; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (shot == null) { g.Clear(Color.Black); return; }   // 准备阶段：几乎透明的"接点击层"

            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, new Rectangle(Point.Empty, shot.Size));
            if (clip.Width > 0 && clip.Height > 0)
                g.DrawImage(shot, clip, clip, GraphicsUnit.Pixel);

            Rectangle b = BannerRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.RoundRect(b, 10))
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(235, 17, 24, 39))) g.FillPath(bg, path);
                using (Pen pen = new Pen(accent, 2)) g.DrawPath(pen, path);
            }
            g.SmoothingMode = SmoothingMode.None;
            TextRenderer.DrawText(g, BannerText(), font, b, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (lensVisible) DrawLens(g);
        }

        private void DrawLens(Graphics g)
        {
            int size = LensPixels * Zoom, half = LensPixels / 2;
            Rectangle img = new Rectangle(lensRect.X, lensRect.Y, size, size);
            g.FillRectangle(Brushes.Black, img);

            Rectangle src = new Rectangle(cursor.X - half, cursor.Y - half, LensPixels, LensPixels);
            Rectangle part = Rectangle.Intersect(src, new Rectangle(Point.Empty, shot.Size));
            if (part.Width > 0 && part.Height > 0)
            {
                Rectangle dst = new Rectangle(img.X + (part.X - src.X) * Zoom, img.Y + (part.Y - src.Y) * Zoom,
                                              part.Width * Zoom, part.Height * Zoom);
                InterpolationMode im = g.InterpolationMode;
                PixelOffsetMode pm = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(shot, dst, part, GraphicsUnit.Pixel);
                g.InterpolationMode = im;
                g.PixelOffsetMode = pm;
            }

            Rectangle center = new Rectangle(img.X + half * Zoom, img.Y + half * Zoom, Zoom, Zoom);
            using (Pen thin = new Pen(Color.FromArgb(150, accent), 1))
            {
                g.DrawLine(thin, img.X, center.Y + Zoom / 2, center.X - 1, center.Y + Zoom / 2);
                g.DrawLine(thin, center.Right + 1, center.Y + Zoom / 2, img.Right, center.Y + Zoom / 2);
                g.DrawLine(thin, center.X + Zoom / 2, img.Y, center.X + Zoom / 2, center.Y - 1);
                g.DrawLine(thin, center.X + Zoom / 2, center.Bottom + 1, center.X + Zoom / 2, img.Bottom);
            }
            using (Pen bold = new Pen(accent, 2)) g.DrawRectangle(bold, center);

            Rectangle cap = new Rectangle(lensRect.X, img.Bottom, lensRect.Width, lensRect.Height - size);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(17, 24, 39))) g.FillRectangle(bg, cap);
            Point sp = PointToScreen(cursor);
            TextRenderer.DrawText(g, string.Format("X {0}   Y {1}", sp.X, sp.Y), font, cap, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Pen border = new Pen(accent, 2)) g.DrawRectangle(border, lensRect);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            MoveLens(e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!lensVisible) return;
            lensVisible = false;
            Invalidate(Inflate(lensRect));
        }

        // 按下时判断是否有效；在"抬起"时才确定。按下和抬起都由遮罩接收，不会漏一个抬起事件给下面的程序
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
                leftAccepted = !session.ShouldIgnorePress(PointToScreen(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                bool ok = leftAccepted;
                leftAccepted = false;
                if (ok) FinishLater(PointToScreen(e.Location));
            }
            else if (e.Button == MouseButtons.Right)
            {
                FinishLater(null);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape: FinishLater(null); return true;
                case Keys.Alt | Keys.F4: FinishLater(null); return true;
                case Keys.Left: Nudge(-1, 0); return true;
                case Keys.Right: Nudge(1, 0); return true;
                case Keys.Up: Nudge(0, -1); return true;
                case Keys.Down: Nudge(0, 1); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Nudge(int dx, int dy)
        {
            if (shot == null) return;
            Point p = Cursor.Position;
            Cursor.Position = new Point(p.X + dx, p.Y + dy);
            MoveLens(PointToClient(Cursor.Position));
        }

        private void FinishLater(Point? result)
        {
            BeginInvoke(new Action(delegate { session.Finish(result); }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            IsClosing = true;
            base.OnFormClosing(e);
        }

        // 遮罩被任何方式关掉（包括 Alt+F4、系统关闭窗口）都当作取消拾取，保证主窗口会回来、其他屏幕的遮罩也一起关掉
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            session.Finish(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (shot != null) shot.Dispose();
                font.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>在屏幕上闪烁一个十字圆圈，标出某个坐标（鼠标可穿透，不抢焦点）。</summary>
    internal sealed class MarkerForm : Form
    {
        private readonly Color color;
        private readonly Point center;
        private readonly int radius;
        private readonly Timer timer = new Timer();
        private int ticks;
        private bool on = true;

        public static void Flash(Point screenPoint, Color color, float scale)
        {
            new MarkerForm(screenPoint, color, scale).Show();
        }

        private MarkerForm(Point p, Color color, float scale)
        {
            this.color = color;
            center = p;
            radius = Math.Max(12, (int)(26 * scale));
            int size = radius * 2 + 9;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Bounds = new Rectangle(p.X - size / 2, p.Y - size / 2, size, size);
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            timer.Interval = 200;
            timer.Tick += delegate
            {
                ticks++;
                on = !on;
                Invalidate();
                if (ticks >= 9) { timer.Stop(); Close(); }
            };
            timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020 | 0x00000080 | 0x08000000; // TRANSPARENT | TOOLWINDOW | NOACTIVATE
                return cp;
            }
        }

        // 系统可能强制窗口最小宽度，显示后按实际大小重新居中，保证圆心就是目标点
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Location = new Point(center.X - ClientSize.Width / 2, center.Y - ClientSize.Height / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!on) return;
            Graphics g = e.Graphics;
            int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2, r = radius;
            int w = Math.Max(3, r / 8), gap = r / 3;
            using (Pen pen = new Pen(color, w))
            {
                g.DrawEllipse(pen, cx - r + w, cy - r + w, 2 * (r - w), 2 * (r - w));
                g.DrawLine(pen, cx, cy - r - 4, cx, cy - gap);
                g.DrawLine(pen, cx, cy + gap, cx, cy + r + 4);
                g.DrawLine(pen, cx - r - 4, cy, cx - gap, cy);
                g.DrawLine(pen, cx + gap, cy, cx + r + 4, cy);
            }
            using (SolidBrush b = new SolidBrush(color)) g.FillRectangle(b, cx - 1, cy - 1, 3, 3);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
