// 鼠标定时点击器 —— 主窗口布局（按 96 DPI 设计，系统缩放时自动放大；150% 缩放的 1080p 屏幕也放得下）
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ClickTimer
{
    /// <summary>一行坐标：色点、名称、X、Y、拾取、显示。</summary>
    internal sealed class PosRow
    {
        public string Title;
        public Color Accent;
        public Label Dot, NameLabel, XLabel, YLabel;
        public TextBox X, Y;
        public RoundButton Pick, Show;

        public Point? Value
        {
            get
            {
                int x, y;
                if (int.TryParse(X.Text.Trim(), out x) && int.TryParse(Y.Text.Trim(), out y)) return new Point(x, y);
                return null;
            }
            set
            {
                X.Text = value.HasValue ? value.Value.X.ToString() : "";
                Y.Text = value.HasValue ? value.Value.Y.ToString() : "";
            }
        }

        public bool Enabled
        {
            set
            {
                Dot.ForeColor = value ? Accent : Theme.Muted;
                NameLabel.ForeColor = value ? Theme.Text : Theme.Muted;
                XLabel.ForeColor = YLabel.ForeColor = value ? Theme.SubText : Theme.Muted;
                X.Enabled = Y.Enabled = Pick.Enabled = Show.Enabled = value;
            }
        }
    }

    internal sealed partial class MainForm
    {
        private static readonly Color Accent1 = Theme.Red;      // 点击位置
        private static readonly Color Accent2 = Theme.Orange;   // 第二次位置
        private static readonly Color Accent3 = Theme.Green;    // 移开位置

        private PosRow row1, row2, rowAway;
        private CheckBox chkSecond, chkTop;
        private NumericUpDown nudDelay, nudInterval;
        private Label lblDelayHint, lblIntervalHint;
        private RoundButton btnStart, btnStop;
        private StatusCard statusCard;
        private Label lblPhase, lblNext, lblCountdown;
        private TextBox txtLog;

        private void BuildUi()
        {
            SuspendLayout();
            Font = new Font(Theme.FontName, 9F);
            Text = BaseTitle;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(540, 616);
            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch (Exception) { }

            BuildHeader(appIcon);

            // ---- 位置 ----
            CardPanel posCard = AddCard(54, 166);
            AddCardTitle(posCard, "位置", "拾取后屏幕定格，单击目标即可");
            row1 = AddRow(posCard, 38, "点击位置", Accent1);
            chkSecond = new CheckBox();
            chkSecond.Text = "第二次点击用另一个位置（默认两次点同一处）";
            chkSecond.AutoSize = true;
            chkSecond.BackColor = Color.Transparent;
            chkSecond.ForeColor = Theme.SubText;
            chkSecond.Location = new Point(30, 72);
            posCard.Controls.Add(chkSecond);
            row2 = AddRow(posCard, 94, "第二次位置", Accent2);
            rowAway = AddRow(posCard, 126, "移开位置", Accent3);

            // ---- 时间 ----
            CardPanel timeCard = AddCard(230, 110);
            AddCardTitle(timeCard, "时间", "单位：秒 · 每次实测值见日志");
            nudDelay = AddTimeRow(timeCard, 38, "延迟时间", 0m, out lblDelayHint);
            nudInterval = AddTimeRow(timeCard, 70, "间隔时间", (decimal)ClickEngine.MinIntervalSeconds, out lblIntervalHint);

            // ---- 选项 ----
            chkTop = new CheckBox();
            chkTop.Text = "窗口置顶";
            chkTop.AutoSize = true;
            chkTop.ForeColor = Theme.SubText;
            chkTop.Location = new Point(20, 351);
            Controls.Add(chkTop);
            Label hotkeyHint = MakeLabel(this, "F9：停止尚未执行的点击", 0, 0, Theme.SubText);
            hotkeyHint.AutoSize = false;
            hotkeyHint.TextAlign = ContentAlignment.MiddleRight;
            hotkeyHint.Bounds = new Rectangle(240, 350, 284, 20);

            // ---- 按钮 ----
            btnStart = new RoundButton(RoundButton.Kind.Primary);
            btnStart.Text = "开始";
            btnStart.Font = new Font(Theme.FontName, 11F, FontStyle.Bold);
            btnStart.Bounds = new Rectangle(16, 378, 250, 42);
            Controls.Add(btnStart);
            btnStop = new RoundButton(RoundButton.Kind.Danger);
            btnStop.Text = "停止 (F9)";
            btnStop.Font = new Font(Theme.FontName, 11F, FontStyle.Bold);
            btnStop.Bounds = new Rectangle(274, 378, 250, 42);
            btnStop.Enabled = false;
            Controls.Add(btnStop);

            // ---- 状态 ----
            statusCard = new StatusCard();
            statusCard.Bounds = new Rectangle(16, 430, 508, 84);
            statusCard.AccentBar = Theme.Muted;
            Controls.Add(statusCard);
            lblPhase = MakeLabel(statusCard, "就绪", 20, 12, Theme.Text);
            lblPhase.Font = new Font(Theme.FontName, 11F, FontStyle.Bold);
            lblNext = MakeLabel(statusCard, "", 21, 44, Theme.SubText);
            lblCountdown = new Label();
            lblCountdown.AutoSize = false;
            lblCountdown.BackColor = Color.Transparent;
            lblCountdown.TextAlign = ContentAlignment.MiddleRight;
            lblCountdown.Font = new Font(Theme.FontName, 24F, FontStyle.Bold);
            lblCountdown.ForeColor = Theme.Text;
            lblCountdown.Bounds = new Rectangle(280, 4, 212, 60);
            statusCard.Controls.Add(lblCountdown);

            // ---- 日志 ----
            CardPanel logCard = AddCard(524, 80);
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.White;
            txtLog.ForeColor = Color.FromArgb(55, 65, 81);
            txtLog.Bounds = new Rectangle(12, 8, 486, 64);
            logCard.Controls.Add(txtLog);

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildHeader(Icon appIcon)
        {
            Panel header = new Panel();
            header.BackColor = Color.White;
            header.Bounds = new Rectangle(0, 0, 540, 44);
            Controls.Add(header);
            Label line = new Label();
            line.BackColor = Theme.Border;
            line.Bounds = new Rectangle(0, 43, 540, 1);
            header.Controls.Add(line);

            AppGlyph glyph = new AppGlyph();
            glyph.Bounds = new Rectangle(16, 10, 24, 24);
            header.Controls.Add(glyph);
            Label title = MakeLabel(header, BaseTitle, 46, 11, Theme.Text);
            title.Font = new Font(Theme.FontName, 11F, FontStyle.Bold);
            MakeLabel(header, "延迟后定点点击两次，再把鼠标移开", 168, 14, Theme.SubText);
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            Label ver = MakeLabel(header, string.Format("v{0}.{1}", v.Major, v.Minor), 0, 0, Theme.Muted);
            ver.AutoSize = false;
            ver.TextAlign = ContentAlignment.MiddleRight;
            ver.Bounds = new Rectangle(440, 12, 84, 20);
        }

        private CardPanel AddCard(int y, int height)
        {
            CardPanel c = new CardPanel();
            c.Bounds = new Rectangle(16, y, 508, height);
            Controls.Add(c);
            return c;
        }

        private void AddCardTitle(CardPanel card, string title, string hint)
        {
            Label t = MakeLabel(card, title, 14, 9, Theme.Text);
            t.Font = new Font(Theme.FontName, 10F, FontStyle.Bold);
            Label h = MakeLabel(card, hint, 0, 0, Theme.Muted);
            h.AutoSize = false;
            h.TextAlign = ContentAlignment.MiddleRight;
            h.Bounds = new Rectangle(150, 9, 344, 22);
        }

        private PosRow AddRow(Control parent, int y, string title, Color accent)
        {
            PosRow r = new PosRow();
            r.Title = title;
            r.Accent = accent;
            r.Dot = MakeLabel(parent, "●", 13, y + 5, accent);
            r.NameLabel = MakeLabel(parent, title, 30, y + 5, Theme.Text);
            r.NameLabel.Font = new Font(Theme.FontName, 9F, FontStyle.Bold);
            r.XLabel = MakeLabel(parent, "X", 108, y + 5, Theme.SubText);
            r.X = MakeCoordBox(parent, 124, y);
            r.YLabel = MakeLabel(parent, "Y", 200, y + 5, Theme.SubText);
            r.Y = MakeCoordBox(parent, 216, y);
            r.Pick = MakeButton(parent, "拾取", 298, y);
            r.Show = MakeButton(parent, "显示", 398, y);
            return r;
        }

        private NumericUpDown AddTimeRow(Control parent, int y, string title, decimal minimum, out Label hint)
        {
            MakeLabel(parent, title, 14, y + 5, Theme.Text).Font = new Font(Theme.FontName, 9F, FontStyle.Bold);
            NumericUpDown n = new NumericUpDown();
            n.BorderStyle = BorderStyle.None;
            n.DecimalPlaces = 3;
            n.Minimum = minimum;
            n.Maximum = 86400;
            n.Increment = 1;
            n.TextAlign = HorizontalAlignment.Right;
            InputFrame frame = new InputFrame(n);
            frame.Bounds = new Rectangle(90, y, 120, 28);
            parent.Controls.Add(frame);
            MakeLabel(parent, "秒", 218, y + 5, Theme.SubText);
            hint = MakeLabel(parent, "", 244, y + 5, Theme.Muted);
            return n;
        }

        private static Label MakeLabel(Control parent, string text, int x, int y, Color color)
        {
            Label l = new Label();
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            l.ForeColor = color;
            l.Text = text;
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
            return l;
        }

        private static TextBox MakeCoordBox(Control parent, int x, int y)
        {
            TextBox t = new TextBox();
            t.BorderStyle = BorderStyle.None;
            t.TextAlign = HorizontalAlignment.Center;
            t.MaxLength = 6;
            t.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '-') e.Handled = true;
            };
            InputFrame frame = new InputFrame(t);
            frame.Bounds = new Rectangle(x, y, 66, 28);
            parent.Controls.Add(frame);
            return t;
        }

        private static RoundButton MakeButton(Control parent, string text, int x, int y)
        {
            RoundButton b = new RoundButton(RoundButton.Kind.Secondary);
            b.Text = text;
            b.Bounds = new Rectangle(x, y, 92, 28);
            parent.Controls.Add(b);
            return b;
        }
    }
}
