// 鼠标定时点击器 —— 配色与自绘控件（卡片、圆角按钮、输入框外框、状态卡）
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickTimer
{
    internal static class Theme
    {
        public const string FontName = "Microsoft YaHei UI";

        public static readonly Color Back = Color.FromArgb(243, 244, 246);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(229, 231, 235);
        public static readonly Color InputBorder = Color.FromArgb(209, 213, 219);
        public static readonly Color Text = Color.FromArgb(17, 24, 39);
        public static readonly Color SubText = Color.FromArgb(107, 114, 128);
        public static readonly Color Muted = Color.FromArgb(156, 163, 175);
        public static readonly Color Blue = Color.FromArgb(37, 99, 235);
        public static readonly Color Red = Color.FromArgb(220, 38, 38);
        public static readonly Color Orange = Color.FromArgb(234, 88, 12);
        public static readonly Color Green = Color.FromArgb(22, 163, 74);
        public static readonly Color SoftFill = Color.FromArgb(243, 244, 246);
        public static readonly Color DisabledFill = Color.FromArgb(229, 231, 235);
        public static readonly Color DisabledText = Color.FromArgb(156, 163, 175);

        private static float scale = -1f;

        /// <summary>系统 DPI 缩放比例（100% = 1.0），用于自绘时放大圆角、线宽等。</summary>
        public static float Scale
        {
            get
            {
                if (scale < 0)
                {
                    try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f; }
                    catch (Exception) { scale = 1f; }
                }
                return scale;
            }
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Darker(Color c, float f)
        {
            return Color.FromArgb(c.A, (int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
        }

        public static Color ParentBack(Control c)
        {
            return c.Parent != null ? c.Parent.BackColor : Back;
        }
    }

    /// <summary>页头上的小图标（用代码画，不依赖从 exe 提取图标）：蓝底、时钟、鼠标箭头。</summary>
    internal sealed class AppGlyph : Control
    {
        public AppGlyph()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = Math.Min(Width, Height) / 24f;
            using (GraphicsPath bg = Theme.RoundRect(new RectangleF(0, 0, 24 * k - 0.5f, 24 * k - 0.5f), 5.5f * k))
            using (SolidBrush b = new SolidBrush(Theme.Blue))
                g.FillPath(b, bg);
            float cx = 10.5f * k, cy = 10.5f * k, r = 6.5f * k;
            g.FillEllipse(Brushes.White, cx - r, cy - r, 2 * r, 2 * r);
            using (Pen hand = new Pen(Color.FromArgb(30, 41, 59), 1.6f * k))
            {
                hand.StartCap = hand.EndCap = LineCap.Round;
                g.DrawLine(hand, cx, cy, cx, cy - 4.2f * k);
                g.DrawLine(hand, cx, cy, cx + 3f * k, cy + 1.4f * k);
            }
            using (SolidBrush red = new SolidBrush(Theme.Red)) g.FillEllipse(red, cx - 1.2f * k, cy - 1.2f * k, 2.4f * k, 2.4f * k);
            PointF[] arrow =
            {
                new PointF(13.5f * k, 12.5f * k), new PointF(13.5f * k, 21.5f * k), new PointF(15.8f * k, 19.4f * k),
                new PointF(17.5f * k, 22.6f * k), new PointF(19.3f * k, 21.8f * k), new PointF(17.6f * k, 18.6f * k),
                new PointF(20.6f * k, 18.6f * k)
            };
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(15, 23, 42))) g.FillPolygon(dark, arrow);
            using (Pen outline = new Pen(Color.White, 1.1f * k)) { outline.LineJoin = LineJoin.Round; g.DrawPolygon(outline, arrow); }
        }
    }

    /// <summary>白色圆角卡片，可选左侧色条。</summary>
    internal class CardPanel : Panel
    {
        private Color accentBar = Color.Empty;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
        }

        public Color AccentBar
        {
            get { return accentBar; }
            set { if (accentBar != value) { accentBar = value; Invalidate(); } }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Theme.Scale;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundRect(r, 8 * s))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, path);
                if (accentBar != Color.Empty)
                {
                    GraphicsState st = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    using (SolidBrush ab = new SolidBrush(accentBar)) g.FillRectangle(ab, 0, 0, 5 * s, Height);
                    g.Restore(st);
                }
                using (Pen pen = new Pen(Theme.Border, 1)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>状态卡：左侧色条 + 底部细进度条。</summary>
    internal sealed class StatusCard : CardPanel
    {
        private float progress = -1f;
        private Color progressColor = Theme.Blue;

        /// <summary>0~1 显示进度，小于 0 隐藏。</summary>
        public float Progress
        {
            get { return progress; }
            set
            {
                if (Math.Abs(value - progress) < 0.002f) return;
                progress = value;
                Invalidate(Rectangle.Inflate(BarRect(), 2, 2));
            }
        }

        public Color ProgressColor
        {
            get { return progressColor; }
            set { progressColor = value; Invalidate(Rectangle.Inflate(BarRect(), 2, 2)); }
        }

        private Rectangle BarRect()
        {
            float s = Theme.Scale;
            int x = (int)(20 * s);
            int h = Math.Max(3, (int)(4 * s));
            return new Rectangle(x, Height - (int)(14 * s), Width - 2 * x, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (progress < 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = BarRect();
            using (GraphicsPath track = Theme.RoundRect(r, r.Height / 2f))
            using (SolidBrush b = new SolidBrush(Theme.Border))
                g.FillPath(b, track);
            float w = r.Width * Math.Min(1f, progress);
            if (w >= 1)
            {
                using (GraphicsPath fill = Theme.RoundRect(new RectangleF(r.X, r.Y, Math.Max(w, r.Height), r.Height), r.Height / 2f))
                using (SolidBrush b = new SolidBrush(progressColor))
                    g.FillPath(b, fill);
            }
        }
    }

    /// <summary>圆角扁平按钮（保留 Button 的点击、键盘、可用状态等行为，只换外观）。</summary>
    internal sealed class RoundButton : Button
    {
        public enum Kind { Primary, Danger, Secondary }

        private Kind kind;
        private bool hover, down;

        public RoundButton(Kind kind)
        {
            this.kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        public Kind Style
        {
            get { return kind; }
            set { kind = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover = down = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill, border = Color.Empty, text;
            if (!Enabled)
            {
                fill = kind == Kind.Secondary ? Color.FromArgb(249, 250, 251) : Theme.DisabledFill;
                if (kind == Kind.Secondary) border = Theme.Border;
                text = Theme.DisabledText;
            }
            else if (kind == Kind.Secondary)
            {
                fill = down ? Color.FromArgb(209, 213, 219) : hover ? Theme.Border : Theme.SoftFill;
                border = Theme.InputBorder;
                text = Theme.Text;
            }
            else
            {
                Color baseColor = kind == Kind.Primary ? Theme.Blue : Theme.Red;
                fill = down ? Theme.Darker(baseColor, 0.78f) : hover ? Theme.Darker(baseColor, 0.88f) : baseColor;
                text = Color.White;
            }

            float s = Theme.Scale;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundRect(r, 6 * s))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border != Color.Empty) using (Pen p = new Pen(border, 1)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
            {
                Rectangle fr = Rectangle.Inflate(ClientRectangle, -(int)(4 * s), -(int)(4 * s));
                ControlPaint.DrawFocusRectangle(g, fr, text, fill);
            }
        }
    }

    /// <summary>输入框外框：给无边框的 TextBox / NumericUpDown 画圆角边框，获得焦点时变蓝。</summary>
    internal sealed class InputFrame : Panel
    {
        private readonly Control inner;
        private bool focused;

        public InputFrame(Control inner)
        {
            this.inner = inner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            inner.BackColor = Color.White;
            Controls.Add(inner);
            inner.Enter += delegate { focused = true; Invalidate(); };
            inner.Leave += delegate { focused = false; Invalidate(); };
            inner.EnabledChanged += delegate
            {
                Color c = inner.Enabled ? Color.White : Color.FromArgb(249, 250, 251);
                BackColor = c;
                inner.BackColor = c;
                Invalidate();
            };
            Click += delegate { inner.Focus(); };
        }

        public Control Inner { get { return inner; } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = (int)(7 * Theme.Scale);
            inner.Left = pad;
            inner.Width = Math.Max(10, Width - 2 * pad + (inner is NumericUpDown ? pad - 2 : 0));
            inner.Top = Math.Max(1, (Height - inner.Height) / 2);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundRect(r, 5 * Theme.Scale))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, path);
                Color bc = !inner.Enabled ? Theme.Border : focused ? Theme.Blue : Theme.InputBorder;
                using (Pen p = new Pen(bc, focused ? 1.5f : 1f)) g.DrawPath(p, path);
            }
        }
    }
}
