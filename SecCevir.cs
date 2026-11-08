// Seç-Çevir: ekranda fareyle seçilen İngilizce metnin Türkçesini seçimin altında gösterir.
// Terminallerde tuş göndermeden çalışır. Simgeye tıklayınca (ya da Ctrl+Alt+T) açılan
// Yaz-Çevir penceresinde Türkçe yazılan cümlenin İngilizcesi çıkar ve kopyalanır.
// Derleme: derle.bat
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SecCevir
{
    // Görünüm: lacivert + altın tema, logo ve Windows 11 pencere süsleri.
    static class Theme
    {
        public static readonly Color Navy = Color.FromArgb(11, 26, 51);      // #0B1A33
        public static readonly Color Navy2 = Color.FromArgb(22, 50, 92);     // #16325C
        public static readonly Color Window = Color.FromArgb(15, 27, 46);    // #0F1B2E
        public static readonly Color Box = Color.FromArgb(22, 37, 61);       // #16253D
        public static readonly Color BoxRead = Color.FromArgb(19, 33, 56);   // #132138
        public static readonly Color Hover = Color.FromArgb(30, 51, 84);     // #1E3354
        public static readonly Color Gold = Color.FromArgb(201, 164, 92);    // #C9A45C
        public static readonly Color GoldLight = Color.FromArgb(232, 207, 149); // #E8CF95
        public static readonly Color GoldDim = Color.FromArgb(120, 100, 62);
        public static readonly Color Muted = Color.FromArgb(159, 176, 200);  // #9FB0C8
        public static readonly Color Faint = Color.FromArgb(111, 129, 156);  // #6F819C

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // Kaydırma çubuklarını koyu yapar (Windows 10/11).
        public static void DarkScroll(IntPtr h) { try { SetWindowTheme(h, "DarkMode_Explorer", null); } catch { } }

        static int Ref(Color c) { return c.R | (c.G << 8) | (c.B << 16); }
        static bool Set(IntPtr h, int attr, int v) { try { return DwmSetWindowAttribute(h, attr, ref v, 4) == 0; } catch { return false; } }

        // Yuvarlak köşe + altın kenar (Windows 11). Olmazsa false döner; çağıran kenarı kendi çizer.
        public static bool RoundWithBorder(IntPtr h)
        {
            bool ok = Set(h, 33, 2);           // DWMWA_WINDOW_CORNER_PREFERENCE = yuvarlak
            ok &= Set(h, 34, Ref(Gold));       // DWMWA_BORDER_COLOR
            return ok;
        }

        // Başlık çubuğu: koyu mod, lacivert zemin, altın yazı.
        public static void TitleBar(IntPtr h)
        {
            Set(h, 20, 1);                     // DWMWA_USE_IMMERSIVE_DARK_MODE
            Set(h, 35, Ref(Window));           // DWMWA_CAPTION_COLOR
            Set(h, 36, Ref(GoldLight));        // DWMWA_TEXT_COLOR
            Set(h, 34, Ref(GoldDim));          // DWMWA_BORDER_COLOR
        }

        public static System.Drawing.Drawing2D.GraphicsPath Rounded(float x, float y, float w, float h, float r)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Logo: lacivert yuvarlak kare, ince altın çerçeve, altın tırnaklı "Ç".
        public static Icon MakeIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (StringFormat sf = new StringFormat())
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.Clear(Color.Transparent);
                    float s = size;
                    using (var bg = Rounded(0, 0, s - 1, s - 1, s * 0.22f))
                    using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(0, 0), new PointF(s, s), Navy, Navy2))
                        g.FillPath(br, bg);
                    float m = s * 0.07f;
                    using (var fr = Rounded(m, m, s - 1 - 2 * m, s - 1 - 2 * m, s * 0.16f))
                    using (Pen pen = new Pen(Gold, Math.Max(1f, s / 26f)))
                        g.DrawPath(pen, fr);
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    using (Font f = new Font("Georgia", s * 0.6f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var tb = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(0, 0), new PointF(0, s), GoldLight, Gold))
                        g.DrawString("Ç", f, tb, new RectangleF(0, -s * 0.04f, s, s), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_RBUTTONDOWN = 0x204, WM_MOUSEWHEEL = 0x20A;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint code, uint mapType);

        static INPUT Key(ushort vk, bool up)
        {
            INPUT i = new INPUT();
            i.type = 1; // INPUT_KEYBOARD
            i.u.ki.wVk = vk;
            i.u.ki.wScan = (ushort)MapVirtualKey(vk, 0);
            i.u.ki.dwFlags = up ? 2u : 0u; // KEYEVENTF_KEYUP
            return i;
        }

        // Ctrl+C'nin dört tuş olayı tek seferde gönderilir; araya kullanıcının ya da başka
        // bir programın tuşu girip Ctrl'siz bir "c" yazılmasın.
        public static bool SendCtrlC()
        {
            INPUT[] seq = { Key(0x11, false), Key(0x43, false), Key(0x43, true), Key(0x11, true) };
            return SendInput((uint)seq.Length, seq, Marshal.SizeOf(typeof(INPUT))) == seq.Length;
        }

        // Win+H: Windows'un sesle yazma (voice typing) panelini açar. Söylenen yazı
        // o an odaktaki kutuya yazılır. Win tuşu "genişletilmiş tuş" olarak gönderilir.
        public static bool SendWinH()
        {
            INPUT winDown = Key(0x5B, false), winUp = Key(0x5B, true);
            winDown.u.ki.dwFlags |= 1u; // KEYEVENTF_EXTENDEDKEY
            winUp.u.ki.dwFlags |= 1u;
            INPUT[] seq = { winDown, Key(0x48, false), Key(0x48, true), winUp };
            return SendInput((uint)seq.Length, seq, Marshal.SizeOf(typeof(INPUT))) == seq.Length;
        }
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr hwnd);

        public const uint WM_QUIT = 0x12, WM_TIMER = 0x113;

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam, lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll")]
        public static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern UIntPtr SetTimer(IntPtr hWnd, UIntPtr id, uint ms, IntPtr proc);
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public const int WM_HOTKEY = 0x312;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
    }

    // Her yerden çalışan kısayol tuşu (Ctrl+Alt+T). Başka bir program aynı tuşu
    // almışsa kurulamaz; o zaman yalnızca simgeye tıklamak çalışır.
    class HotkeyWindow : NativeWindow
    {
        readonly Action onPress;
        public readonly bool Ok;

        public HotkeyWindow(Action onPress)
        {
            this.onPress = onPress;
            CreateHandle(new CreateParams());
            Ok = Native.RegisterHotKey(Handle, 1, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.T);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) { try { onPress(); } catch { } }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Ok) Native.UnregisterHotKey(Handle, 1);
            DestroyHandle();
        }
    }

    // Terminalde seçili yazıyı UI Automation ile okur (ekran okuyucuların kullandığı yol).
    // Hiç tuş göndermez, panoya dokunmaz; bu yüzden çalışan komut kesilmez.
    static class UiaReader
    {
        public static string ReadSelection(Point pt, int maxChars)
        {
            string s = null;
            try
            {
                s = FromElement(System.Windows.Automation.AutomationElement.FromPoint(
                    new System.Windows.Point(pt.X, pt.Y)), maxChars);
            }
            catch { }
            if (string.IsNullOrEmpty(s))
            {
                try { s = FromElement(System.Windows.Automation.AutomationElement.FocusedElement, maxChars); }
                catch { }
            }
            return s;
        }

        // Fare altındaki öğe metin alanının bir parçası olabilir; birkaç üst öğeye bak.
        static string FromElement(System.Windows.Automation.AutomationElement el, int maxChars)
        {
            for (int depth = 0; el != null && depth < 5; depth++)
            {
                object p;
                if (el.TryGetCurrentPattern(System.Windows.Automation.TextPattern.Pattern, out p))
                {
                    System.Windows.Automation.Text.TextPatternRange[] ranges =
                        ((System.Windows.Automation.TextPattern)p).GetSelection();
                    StringBuilder sb = new StringBuilder();
                    if (ranges != null)
                        foreach (System.Windows.Automation.Text.TextPatternRange r in ranges)
                        {
                            sb.Append(r.GetText(maxChars)).Append(' ');
                            if (sb.Length > maxChars) break;
                        }
                    string s = sb.ToString().Trim();
                    if (s.Length > 0) return s;
                }
                el = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(el);
            }
            return null;
        }
    }

    // Fare kancası kendi iş parçacığında (thread) çalışır. Kanca arayüzün iş parçacığında
    // olursa, arayüz bir an takıldığında (pano, ağ, ses) Windows kancayı "yanıt vermiyor"
    // diye sessizce kaldırır ve program seçimleri hiç duymaz olur. Ayrıca her dakika kanca
    // yeniden kurulur; uyku/uyanma gibi durumlarda kopsa bile kendiliğinden düzelir.
    class MouseHook
    {
        readonly Action<int, Point> onMouse;
        readonly Native.HookProc proc;
        IntPtr hook;
        uint threadId;

        public MouseHook(Action<int, Point> onMouse)
        {
            this.onMouse = onMouse;
            proc = Callback;
            System.Threading.Thread t = new System.Threading.Thread(Run);
            t.IsBackground = true;
            t.Name = "SecCevir fare kancası";
            t.Start();
        }

        void Install()
        {
            IntPtr fresh = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, proc, Native.GetModuleHandle(null), 0);
            if (fresh == IntPtr.Zero) return;
            IntPtr old = hook;
            hook = fresh;
            if (old != IntPtr.Zero) Native.UnhookWindowsHookEx(old);
        }

        void Run()
        {
            threadId = Native.GetCurrentThreadId();
            Install();
            Native.SetTimer(IntPtr.Zero, UIntPtr.Zero, 60000, IntPtr.Zero);
            Native.MSG msg;
            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == Native.WM_TIMER) Install();
            }
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
        }

        public void Stop()
        {
            if (threadId != 0) Native.PostThreadMessage(threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                if (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_LBUTTONUP ||
                    msg == Native.WM_RBUTTONDOWN || msg == Native.WM_MOUSEWHEEL)
                {
                    Native.MSLLHOOKSTRUCT s = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    try { onMouse(msg, new Point(s.pt.x, s.pt.y)); } catch { }
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }
    }

    // Google'ın verdiği HTTP hata kodunu (ör. 429) taşır.
    class HttpStatusException : Exception
    {
        public readonly int Code;
        public HttpStatusException(int code) : base("HTTP " + code) { Code = code; }
    }

    class Popup : Form
    {
        readonly Label head, spk, cpy, en, tr;
        bool dwmBorder;
        readonly Timer cpyReset = new Timer();
        readonly float scale;
        Point anchor;
        int aboveY;

        public event EventHandler SpeakClicked;

        public Popup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Window;
            using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;

            head = new Label();
            head.Text = "SEÇ-ÇEVİR";
            head.AutoSize = true;
            head.Font = new Font("Segoe UI Semibold", 7.5f);
            head.ForeColor = Theme.Gold;

            spk = new Label();
            spk.Text = "🔊";
            spk.Font = new Font("Segoe UI Emoji", 11f);
            spk.ForeColor = Theme.Gold;
            spk.Cursor = Cursors.Hand;
            spk.TextAlign = ContentAlignment.MiddleCenter;
            spk.Size = new Size(S(30), S(26));
            spk.Click += delegate { if (SpeakClicked != null) SpeakClicked(this, EventArgs.Empty); };

            // Türkçe çeviriyi panoya kopyalar; kısa süre ✓ gösterir.
            cpy = new Label();
            cpy.Text = "📋";
            cpy.Font = new Font("Segoe UI Emoji", 10.5f);
            cpy.ForeColor = Theme.Gold;
            cpy.Cursor = Cursors.Hand;
            cpy.TextAlign = ContentAlignment.MiddleCenter;
            cpy.Size = new Size(S(30), S(26));
            cpy.Click += delegate
            {
                if (tr.Text.Length == 0 || tr.Text == "…") return;
                if (App.SetClipboardText(tr.Text)) { cpy.Text = "✓"; cpyReset.Start(); }
            };
            cpyReset.Interval = 1200;
            cpyReset.Tick += delegate { cpyReset.Stop(); cpy.Text = "📋"; };

            en = new Label();
            en.Font = new Font("Segoe UI", 9.5f);
            en.ForeColor = Theme.Muted;

            tr = new Label();
            tr.Font = new Font("Segoe UI Semibold", 12.5f);
            tr.ForeColor = Color.White;

            Controls.Add(head);
            Controls.Add(spk);
            Controls.Add(cpy);
            Controls.Add(en);
            Controls.Add(tr);
        }

        int S(int v) { return (int)Math.Round(v * scale); }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            dwmBorder = Theme.RoundWithBorder(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Başlığın solundaki altın çizgi.
            using (SolidBrush b = new SolidBrush(Theme.Gold))
                e.Graphics.FillRectangle(b, S(12), head.Top + S(2), S(3), head.Height - S(4));
            if (!dwmBorder)
                using (Pen p = new Pen(Theme.Gold))
                    e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // at: seçimin altı; topY: seçimin üstü (altta yer yoksa popup oraya çıkar).
        public void ShowAt(Point at, int topY, string enText, string trText)
        {
            anchor = at;
            aboveY = topY;
            en.Text = enText;
            tr.Text = trText;
            Relayout();
            if (!Visible) Show();
        }

        public void SetTranslation(string trText)
        {
            tr.Text = trText;
            Relayout();
        }

        static Size Measure(Label l, int maxWidth)
        {
            // Label'ın kendi ölçüsünü kullan; ayrı ölçüm son kelimeyi görünmeyen satıra itebiliyordu.
            l.AutoSize = false;
            Size sz = l.GetPreferredSize(new Size(maxWidth, 0));
            return new Size(Math.Min(sz.Width, maxWidth) + 4, sz.Height + 2);
        }

        void Relayout()
        {
            // Üst satır: altın çizgi + SEÇ-ÇEVİR, sağda 🔊 📋. Altında İngilizce (soluk), en altta Türkçe.
            int p = S(12);
            en.UseMnemonic = false;
            tr.UseMnemonic = false;
            head.Location = new Point(p + S(8), p);
            en.Size = Measure(en, S(440));
            tr.Size = Measure(tr, S(450));
            int contentW = Math.Max(Math.Max(en.Width, tr.Width), head.Width + S(8) + 2 * S(30) + S(20));
            en.Location = new Point(p - S(3), head.Bottom + S(6));
            tr.Location = new Point(p - S(3), en.Bottom + S(2));
            int right = p - S(3) + contentW;
            cpy.Location = new Point(right - cpy.Width + S(6), p - S(7));
            spk.Location = new Point(cpy.Left - spk.Width, cpy.Top);
            ClientSize = new Size(right + p, tr.Bottom + p);

            Rectangle wa = Screen.FromPoint(anchor).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(anchor.X, wa.Right - Width));
            int y = anchor.Y;
            if (y + Height > wa.Bottom) y = Math.Max(wa.Top, aboveY - Height);
            Location = new Point(x, y);
            Invalidate();
        }
    }

    // Yaz-Çevir: Türkçe yazılan metnin İngilizcesini çıkarır (İngilizce yazılırsa Türkçesini).
    // Yazmayı bırakınca kendiliğinden çevirir. Ctrl+Enter: kopyala ve kapat. Esc: kapat.
    class WriteForm : Form
    {
        readonly TextBox input, output;
        readonly Label status;
        readonly Button copyBtn;
        readonly Timer debounce = new Timer();
        readonly Timer copyReset = new Timer();
        readonly Func<string, Task<string[]>> translate; // dönüş: [çeviri, yön yazısı, hedef dil]
        readonly Func<string, string, Task<string[]>> naturalize; // (yazılan, Google çevirisi) → [doğal İngilizce, Türkçe not]
        readonly Action<string, string> speak;
        readonly Func<string> speechState;
        readonly Action pauseSpeech, resumeSpeech;
        readonly Timer speechPoll = new Timer();
        readonly Label note;
        readonly Button natBtn;
        readonly float scale;
        int req;
        string doneFor = "";
        string outLang = "en";

        public WriteForm(Func<string, Task<string[]>> translate, Func<string, string, Task<string[]>> naturalize,
                         Action<string, string> speak, Func<string> speechState, Action pauseSpeech, Action resumeSpeech)
        {
            this.translate = translate;
            this.naturalize = naturalize;
            this.speak = speak;
            this.speechState = speechState;
            this.pauseSpeech = pauseSpeech;
            this.resumeSpeech = resumeSpeech;
            Text = "Yaz-Çevir";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Theme.Window;
            Icon = Theme.MakeIcon(32);
            using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;

            int p = S(12), w = S(440);
            Label lin = MakeLabel("Türkçe yazın:", p, p);
            input = MakeBox(p, lin.Bottom + S(2), w, S(96), false);
            status = MakeLabel("", p, input.Bottom + S(8));
            status.AutoSize = false;
            status.Size = new Size(w - S(122), S(20));
            natBtn = MakeButton("✨ Doğal yap", p + w - S(116), input.Bottom + S(4), S(116));
            natBtn.Height = S(28);
            natBtn.BackColor = Theme.Gold;
            natBtn.ForeColor = Theme.Navy;
            natBtn.FlatAppearance.BorderColor = Theme.Gold;
            natBtn.FlatAppearance.MouseOverBackColor = Theme.GoldLight;
            natBtn.FlatAppearance.MouseDownBackColor = Theme.GoldLight;
            output = MakeBox(p, natBtn.Bottom + S(4), w, S(96), true);
            // Claude'un kısa Türkçe açıklaması (neyi neden değiştirdi).
            note = MakeLabel("", p, output.Bottom + S(4));
            note.AutoSize = false;
            note.Size = new Size(w, S(44));
            note.Font = new Font("Segoe UI", 9f);
            note.ForeColor = Theme.GoldLight;

            Button mic = MakeButton("Konuş", p, note.Bottom + S(6), S(100));
            copyBtn = MakeButton("Kopyala", mic.Right + S(8), mic.Top, S(116));
            Button spk = MakeButton("Dinle", copyBtn.Right + S(8), copyBtn.Top, S(92));
            Button clr = MakeButton("Temizle", spk.Right + S(8), copyBtn.Top, S(92));
            Label hint = MakeLabel("Konuş ya da Win+H: sesle yaz · Ctrl+Enter: kopyala ve kapat · Esc: kapat", p, copyBtn.Bottom + S(8));
            hint.ForeColor = Theme.Faint;
            hint.Font = new Font("Segoe UI", 8.5f);
            ClientSize = new Size(w + 2 * p, hint.Bottom + p);

            // Yazı kutusuna odaklan, sonra Windows'un sesle yazma panelini aç. Söylenen
            // yazı kutuya düştükçe çeviri kendiliğinden yapılır.
            mic.Click += delegate
            {
                Activate();
                input.Focus();
                input.SelectionStart = input.TextLength;
                BeginInvoke(new Action(delegate
                {
                    if (!Native.SendWinH()) status.Text = "Sesle yazma açılamadı; Win+H tuşlarına kendiniz basın.";
                    else status.Text = "Konuşun… (Windows sesle yazma; bitince panelden durdurun)";
                }));
            };
            natBtn.Click += async delegate { await DoNaturalize(); };
            copyBtn.Click += delegate { CopyOutput(); };
            // Tek düğme: çalarken Duraklat, duraklatılmışken Devam, yoksa baştan Dinle.
            spk.Click += delegate
            {
                string st = speechState();
                if (st == "playing") pauseSpeech();
                else if (st == "paused") resumeSpeech();
                else if (st == "stopped" && output.Text.Length > 0) speak(output.Text, outLang);
                UpdateSpeakButton(spk);
            };
            speechPoll.Interval = 250;
            speechPoll.Tick += delegate { if (Visible) UpdateSpeakButton(spk); };
            speechPoll.Start();
            clr.Click += delegate { input.Clear(); output.Clear(); status.Text = ""; doneFor = ""; input.Focus(); };

            debounce.Interval = 600;
            debounce.Tick += async delegate { debounce.Stop(); await DoTranslate(); };
            input.TextChanged += delegate { debounce.Stop(); debounce.Start(); };
            copyReset.Interval = 1500;
            copyReset.Tick += delegate { copyReset.Stop(); copyBtn.Text = "Kopyala"; };

            input.KeyDown += async delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A) { input.SelectAll(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await CopyAndClose(); }
            };
            output.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A) { output.SelectAll(); e.SuppressKeyPress = true; }
            };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Hide(); }
            };
        }

        int S(int v) { return (int)Math.Round(v * scale); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.TitleBar(Handle);
        }

        Label MakeLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", 9.5f);
            l.ForeColor = Theme.Muted;
            l.Location = new Point(x, y);
            Controls.Add(l);
            return l;
        }

        TextBox MakeBox(int x, int y, int w, int h, bool readOnly)
        {
            TextBox t = new TextBox();
            t.Multiline = true;
            t.AcceptsReturn = true;
            t.ScrollBars = ScrollBars.None; // tekerlekle kayar; beyaz çubuk görünmesin
            t.ReadOnly = readOnly;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = new Font(readOnly ? "Segoe UI Semibold" : "Segoe UI", 11.5f);
            t.BackColor = readOnly ? Theme.BoxRead : Theme.Box;
            t.ForeColor = Color.White;
            t.HandleCreated += delegate { Theme.DarkScroll(t.Handle); };
            t.Location = new Point(x, y);
            t.Size = new Size(w, h);
            Controls.Add(t);
            return t;
        }

        Button MakeButton(string text, int x, int y, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.GoldDim;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.FlatAppearance.MouseDownBackColor = Theme.Navy2;
            b.BackColor = Theme.Box;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.5f);
            b.Location = new Point(x, y);
            b.Size = new Size(w, S(32));
            b.Cursor = Cursors.Hand;
            Controls.Add(b);
            return b;
        }

        async Task DoTranslate()
        {
            string text = input.Text.Trim();
            if (text.Length == 0) { output.Clear(); status.Text = ""; doneFor = ""; return; }
            if (text == doneFor) return;
            int id = ++req;
            status.Text = "Çevriliyor…";
            try
            {
                string[] r = await translate(text);
                if (id != req) return;
                output.Text = r[0];
                status.Text = r[1];
                outLang = r[2];
                note.Text = "";
                doneFor = text;
            }
            catch (Exception ex)
            {
                if (id == req) status.Text = ex.Message;
            }
        }

        void UpdateSpeakButton(Button b)
        {
            string st = speechState();
            string t = st == "playing" ? "Duraklat" : st == "paused" ? "Devam" : st == "loading" ? "Ses…" : "Dinle";
            if (b.Text != t) b.Text = t;
        }

        // ✨ Doğal yap: Claude Code'a (abonelik) gönderir; ek ücret yok.
        async Task DoNaturalize()
        {
            string text = input.Text.Trim();
            if (text.Length == 0) return;
            debounce.Stop();
            if (text != doneFor) await DoTranslate();
            string literal = outLang == "en" ? output.Text : "";
            int id = ++req;
            natBtn.Enabled = false;
            status.Text = "✨ Claude düzenliyor…";
            try
            {
                string[] r = await naturalize(text, literal);
                if (id != req) return;
                output.Text = r[0];
                note.Text = r[1];
                outLang = "en";
                doneFor = text;
                status.Text = "✨ Doğal İngilizce (Claude Haiku)";
            }
            catch (Exception ex)
            {
                if (id == req) status.Text = ex.Message;
            }
            finally { natBtn.Enabled = true; }
        }

        bool CopyOutput()
        {
            if (output.Text.Length == 0) return false;
            if (!App.SetClipboardText(output.Text)) { status.Text = "Panoya yazılamadı; tekrar deneyin."; return false; }
            copyBtn.Text = "Kopyalandı ✓";
            copyReset.Stop();
            copyReset.Start();
            return true;
        }

        async Task CopyAndClose()
        {
            debounce.Stop();
            if (input.Text.Trim() != doneFor) await DoTranslate();
            if (input.Text.Trim() == doneFor && CopyOutput()) Hide();
        }

        // Ekranın sağ altında, saatin üstünde açılır.
        public void ShowNear()
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - S(12), wa.Bottom - Height - S(12));
            Show();
            Activate();
            input.Focus();
            input.SelectAll();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }
    }

    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "SecCevir";
        const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        readonly NotifyIcon tray;
        readonly Popup popup;
        readonly MouseHook mouseHook;
        readonly WriteForm writer;
        readonly HotkeyWindow hotkey;
        uint seqAtDown;
        static readonly HttpClient http = MakeHttp();
        readonly string mp3Path = Path.Combine(Path.GetTempPath(), "seccevir_ses.mp3");

        bool enabled = true;
        bool busy;
        bool downInPopup;
        Point downPt;
        DateTime lastClickTime = DateTime.MinValue;
        Point lastClickPt;
        int requestId;
        string currentText = "";
        string currentLang = "en";
        // Aynı metni tekrar tekrar Google'a sormamak için son çeviriler burada tutulur.
        readonly System.Collections.Generic.Dictionary<string, string[]> cache =
            new System.Collections.Generic.Dictionary<string, string[]>();
        // Google "çok fazla istek" (429) derse bu zamana kadar yeni istek gönderilmez.
        DateTime pauseUntil = DateTime.MinValue;
        const int MaxChars = 1000;
        System.Speech.Synthesis.SpeechSynthesizer synth;

        public App()
        {
            popup = new Popup();
            IntPtr unused = popup.Handle;
            popup.SpeakClicked += delegate { Speak(currentText, currentLang); };

            writer = new WriteForm(TranslateForWriter, Naturalize, Speak, SpeechState, PauseSpeech, ResumeSpeech);
            hotkey = new HotkeyWindow(delegate { writer.ShowNear(); });
            if (!hotkey.Ok) Log("Ctrl+Alt+T kısayolu kurulamadı (başka bir program kullanıyor olabilir)");

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem write = new ToolStripMenuItem(hotkey.Ok ? "Yaz-Çevir  (Ctrl+Alt+T)" : "Yaz-Çevir");
            write.Font = new Font(write.Font, FontStyle.Bold);
            write.Click += delegate { writer.ShowNear(); };
            menu.Items.Add(write);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem onOff = new ToolStripMenuItem("Seçince çevir");
            onOff.Checked = true;
            onOff.Click += delegate
            {
                enabled = !enabled;
                onOff.Checked = enabled;
                if (!enabled) popup.Hide();
            };
            ToolStripMenuItem autoStart = new ToolStripMenuItem("Windows ile başlat");
            autoStart.Checked = IsAutoStart();
            autoStart.Click += delegate
            {
                SetAutoStart(!autoStart.Checked);
                autoStart.Checked = IsAutoStart();
            };
            ToolStripMenuItem exit = new ToolStripMenuItem("Çıkış");
            exit.Click += delegate { ExitThread(); };
            menu.Items.Add(onOff);
            menu.Items.Add(autoStart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "Seç-Çevir · tıklayın: Yaz-Çevir";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) writer.ShowNear();
            };
            tray.Visible = true;
            tray.ShowBalloonTip(3000, "Seç-Çevir çalışıyor",
                "İngilizce metni seçin: Türkçesi çıkar. Simgeye tıklayın: Türkçe yazın, İngilizcesini kopyalayın.",
                ToolTipIcon.Info);

            // Kancadan hemen dön; işi arayüzün mesaj kuyruğuna bırak ki fare takılmasın.
            mouseHook = new MouseHook(delegate(int msg, Point pt)
            {
                popup.BeginInvoke(new Action(delegate { OnMouse(msg, pt); }));
            });
        }

        static HttpClient MakeHttp()
        {
            HttpClient c = new HttpClient();
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            return c;
        }

        protected override void ExitThreadCore()
        {
            mouseHook.Stop();
            hotkey.Dispose();
            Native.mciSendString("close seccevir", null, 0, IntPtr.Zero);
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }

        static Icon MakeIcon()
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            return Theme.MakeIcon(size);
        }

        static bool IsAutoStart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(AppName) != null;
        }

        static void SetAutoStart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(AppName, false);
            }
        }

        void OnMouse(int msg, Point pt)
        {
            bool inPopup = popup.Visible && popup.Bounds.Contains(pt);

            if (msg == Native.WM_RBUTTONDOWN || msg == Native.WM_MOUSEWHEEL)
            {
                if (!inPopup) popup.Hide();
                return;
            }

            if (msg == Native.WM_LBUTTONDOWN)
            {
                downInPopup = inPopup;
                if (!inPopup)
                {
                    popup.Hide();
                    downPt = pt;
                    // "Seçince kopyalayan" terminaller (Git Bash, PuTTY) panoyu bırakınca
                    // değiştirir; değişip değişmediğini anlamak için şimdiki sırayı sakla.
                    seqAtDown = Native.GetClipboardSequenceNumber();
                }
                return;
            }

            // WM_LBUTTONUP
            if (downInPopup || !enabled) return;

            bool drag = Math.Abs(pt.X - downPt.X) > 6 || Math.Abs(pt.Y - downPt.Y) > 6;
            bool dbl = false;
            if (!drag)
            {
                DateTime now = DateTime.UtcNow;
                dbl = (now - lastClickTime).TotalMilliseconds <= Native.GetDoubleClickTime()
                      && Math.Abs(pt.X - lastClickPt.X) <= 4 && Math.Abs(pt.Y - lastClickPt.Y) <= 4;
                lastClickTime = now;
                lastClickPt = pt;
            }
            Debug("bırakıldı: sürükleme=" + drag + " çift=" + dbl);
            if (!drag && !dbl) return;

            Point at = new Point(Math.Min(downPt.X, pt.X), Math.Max(downPt.Y, pt.Y) + 22);
            int topY = Math.Min(downPt.Y, pt.Y) - 22;
            Capture(at, topY, pt);
        }

        static string ForegroundClass()
        {
            StringBuilder sb = new StringBuilder(256);
            Native.GetClassName(Native.GetForegroundWindow(), sb, sb.Capacity);
            return sb.ToString();
        }

        // Terminallerde Ctrl+C çalışan komutu keser. Bunlara tuş gönderilmez;
        // seçim UI Automation ya da "seçince kopyala" panosuyla okunur.
        static bool IsTerminal(string cls)
        {
            return cls == "ConsoleWindowClass" || cls == "CASCADIA_HOSTING_WINDOW_CLASS"
                || cls == "mintty" || cls == "PuTTY" || cls == "VirtualConsoleClass";
        }

        // Dosya Gezgini ve masaüstünde Ctrl+C dosyayı kopyalar: hiç dokunma.
        static bool IsSkipped(string cls)
        {
            return cls == "CabinetWClass" || cls == "ExploreWClass"
                || cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd";
        }

        // Yaz-Çevir penceresinde yapılan seçimler çevrilmez.
        static bool IsOwnForeground()
        {
            uint pid;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out pid);
            return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        public static bool SetClipboardText(string text)
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetText(text); return true; }
                catch { System.Threading.Thread.Sleep(40); }
            }
            return false;
        }

        // Terminaldeki seçimi okur. İki yol aynı anda denenir:
        // - UI Automation: Windows Terminal'in kendi seçimi (hemen başlar).
        // - Pano: terminal ya da içinde çalışan program (ör. Claude Code) seçimi kendisi
        //   kopyalayabilir; bu bazen bir saniyeye kadar gecikir, o yüzden 1,5 sn beklenir.
        // Pano eski haline döndürülmez, çünkü kopyalamayı terminal kendisi yaptı.
        async Task<string> ReadTerminalSelection(Point pt)
        {
            Task<string> uia = Task.Run(delegate { return UiaReader.ReadSelection(pt, MaxChars * 2); });
            bool uiaChecked = false;
            DateTime until = DateTime.UtcNow.AddMilliseconds(1500);
            while (true)
            {
                if (Native.GetClipboardSequenceNumber() != seqAtDown)
                {
                    string c = await ReadClipboardText();
                    if (!string.IsNullOrEmpty(c)) { Debug("terminal: panodan"); return c; }
                }
                if (!uiaChecked && uia.IsCompleted)
                {
                    uiaChecked = true;
                    string s = uia.Status == TaskStatus.RanToCompletion ? uia.Result : null;
                    if (!string.IsNullOrEmpty(s)) { Debug("terminal: UI Automation"); return s; }
                }
                if (DateTime.UtcNow >= until) break;
                await Task.Delay(40);
            }
            Log("terminal: pano değişmedi, UI Automation " + (uia.IsCompleted ? "boş döndü" : "yanıt vermedi"));
            return null;
        }

        static async Task<string> ReadClipboardText()
        {
            for (int j = 0; j < 6; j++)
            {
                try { if (Clipboard.ContainsText()) return Clipboard.GetText(); }
                catch { }
                await Task.Delay(40);
            }
            return null;
        }

        // Metindeki Türkçeye özgü harflerin oranı. İngilizce bir metinde tek bir Türkçe
        // ad (ör. bir yer ya da dosya adı) geçmesi metni Türkçe yapmaz.
        static bool LooksTurkish(string text)
        {
            int letters = 0, tr = 0;
            foreach (char c in text)
            {
                if (!char.IsLetter(c)) continue;
                letters++;
                if ("çÇğĞıİöÖşŞüÜ".IndexOf(c) >= 0) tr++;
            }
            return letters > 0 && tr * 100 >= letters * 2; // en az %2
        }

        static bool KeyDown(int vk) { return (Native.GetAsyncKeyState(vk) & 0x8000) != 0; }

        // Shift, Ctrl, Alt ya da Windows tuşu basılıyken Ctrl+C göndermek başka bir
        // kısayola dönüşür (ör. Chrome'da Ctrl+Shift+C geliştirici panelini açar).
        // Tuşlar bırakılana kadar en fazla 1,5 saniye bekle.
        static async Task<bool> WaitModifiersReleased()
        {
            for (int i = 0; i < 30; i++)
            {
                if (!KeyDown(0x10) && !KeyDown(0x11) && !KeyDown(0x12) && !KeyDown(0x5B) && !KeyDown(0x5C))
                    return true;
                await Task.Delay(50);
            }
            return false;
        }

        async void Capture(Point at, int topY, Point upPt)
        {
            if (busy) { Log("atlandı: önceki seçim hâlâ kopyalanıyor"); return; }
            busy = true;
            string text;
            try
            {
                await Task.Delay(150);
                string cls = ForegroundClass();
                if (IsSkipped(cls)) { Debug("atlandı: pencere " + cls); return; }
                if (IsOwnForeground()) return;
                if (IsTerminal(cls))
                {
                    text = await ReadTerminalSelection(upPt);
                    if (string.IsNullOrEmpty(text)) { Log("atlandı: terminalde seçim okunamadı (" + cls + ")"); return; }
                    Log("terminal: seçim okundu (" + text.Length + " karakter)");
                }
                else
                {
                    if (!await WaitModifiersReleased()) { Log("atlandı: Shift/Ctrl/Alt tuşu bırakılmadı"); return; }

                    text = await CopySelection();
                    if (text == null)
                    {
                        // Bazı uygulamalar seçimi geç tamamlar; bir kez daha dene.
                        await Task.Delay(200);
                        text = await CopySelection();
                    }
                    if (text == null) { Log("atlandı: uygulama Ctrl+C'ye metin vermedi (" + ForegroundClass() + ")"); return; }
                }
            }
            catch (Exception ex) { Log("hata (kopyalama): " + ex.Message); return; }
            finally { busy = false; }

            text = Regex.Replace(text, @"\s+", " ").Trim();
            Debug("kopyalandı: " + text.Length + " karakter");
            if (text.Length == 0 || !Regex.IsMatch(text, "[A-Za-z]")) { Log("atlandı: seçimde harf yok"); return; }
            // Türkçe harfler çoksa metin zaten Türkçedir, çevirmeye gerek yok.
            if (LooksTurkish(text)) { Log("atlandı: metin Türkçe görünüyor (" + text.Length + " karakter)"); return; }

            bool cut = text.Length > MaxChars;
            if (cut) text = text.Substring(0, MaxChars);
            string shown = text.Length > 300 ? text.Substring(0, 300) + "…" : text;

            // Çeviri beklenirken yeni seçimler engellenmesin diye busy burada serbest.
            currentText = text;
            int id = ++requestId;

            string[] hit;
            if (cache.TryGetValue("tr|" + text, out hit))
            {
                currentLang = hit[1];
                popup.ShowAt(at, topY, shown, hit[0]);
                return;
            }

            if (DateTime.UtcNow < pauseUntil)
            {
                int sec = (int)Math.Ceiling((pauseUntil - DateTime.UtcNow).TotalSeconds);
                popup.ShowAt(at, topY, shown, "Google kısa bir mola istedi; " + sec + " saniye sonra tekrar deneyin.");
                return;
            }

            popup.ShowAt(at, topY, shown, "…");

            string tr;
            try
            {
                string[] res = await Translate(text, "tr");
                Debug("çevrildi: dil=" + res[1] + " görünür=" + popup.Visible + " güncel=" + (id == requestId));
                if (res[1] == "tr")
                {
                    // Metin zaten Türkçeymiş: popup'ı kapat.
                    Log("atlandı: Google metni Türkçe saydı");
                    if (id == requestId) popup.Hide();
                    return;
                }
                if (cache.Count > 300) cache.Clear();
                cache["tr|" + text] = res;
                if (id == requestId) currentLang = res[1];
                tr = cut ? res[0] + " …(ilk " + MaxChars + " karakter çevrildi)" : res[0];
            }
            catch (Exception ex)
            {
                Log("hata (çeviri): " + ex.Message);
                HttpStatusException he = ex as HttpStatusException;
                if (he != null && he.Code == 429)
                {
                    pauseUntil = DateTime.UtcNow.AddSeconds(60);
                    tr = "Google kısa bir mola istedi; 1 dakika sonra tekrar deneyin.";
                }
                else if (ex is TaskCanceledException)
                    tr = "Çeviri gecikti (internet yavaş olabilir); tekrar seçip deneyin.";
                else
                    tr = "Çeviri alınamadı (internet bağlantısını kontrol edin)";
            }
            if (id == requestId && popup.Visible) popup.SetTranslation(tr);
        }

        // Ayrıntılı kayıt yalnızca exe'nin yanında "SecCevir.debug" dosyası varsa yazılır.
        static readonly bool debug = File.Exists(Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath), "SecCevir.debug"));

        static void Debug(string msg) { if (debug) Log("[ayrıntı] " + msg); }

        // Yalnızca neden atlandığını yazar; seçilen metnin kendisi kaydedilmez.
        static void Log(string msg)
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "SecCevir.log");
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 200000) fi.Delete();
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        // Yalnızca yaygın biçimler yedeklenir. Hepsini okumak, panodaki büyük ya da
        // "istenince üretilen" verilerde (Office, görsel programları) saniyelerce takılıyordu.
        static readonly string[] BackupFormats =
        {
            DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html,
            DataFormats.CommaSeparatedValue, DataFormats.FileDrop, DataFormats.Bitmap
        };

        static DataObject BackupClipboard()
        {
            try
            {
                IDataObject old = Clipboard.GetDataObject();
                if (old == null) return null;
                DataObject copy = new DataObject();
                int n = 0;
                foreach (string f in BackupFormats)
                {
                    if (!old.GetDataPresent(f, false)) continue;
                    try
                    {
                        object v = old.GetData(f, false);
                        if (v != null) { copy.SetData(f, false, v); n++; }
                    }
                    catch { }
                }
                return n > 0 ? copy : null;
            }
            catch { return null; }
        }

        // Seçili metni Ctrl+C ile alır, sonra panoyu eski haline getirir.
        static async Task<string> CopySelection()
        {
            DataObject backup = BackupClipboard();
            uint seq = Native.GetClipboardSequenceNumber();

            if (!Native.SendCtrlC())
            {
                // Yönetici olarak çalışan pencerelere Windows tuş göndermeye izin vermez.
                Log("atlandı: Ctrl+C gönderilemedi (pencere yönetici olarak çalışıyor olabilir)");
                return null;
            }

            bool changed = false;
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(20);
                if (Native.GetClipboardSequenceNumber() != seq) { changed = true; break; }
            }
            if (!changed) return null;

            // Pano o an başka bir programca kilitli olabilir; birkaç kez dene.
            string result = null;
            for (int i = 0; i < 6 && result == null; i++)
            {
                await Task.Delay(40);
                try { if (Clipboard.ContainsText()) result = Clipboard.GetText(); } catch { }
            }
            try
            {
                if (backup != null) Clipboard.SetDataObject(backup, true);
                else Clipboard.Clear();
            }
            catch { }
            return result;
        }

        static async Task<string> Get(string url)
        {
            using (HttpResponseMessage r = await http.GetAsync(url))
            {
                if (!r.IsSuccessStatusCode) throw new HttpStatusException((int)r.StatusCode);
                byte[] b = await r.Content.ReadAsByteArrayAsync();
                return Encoding.UTF8.GetString(b);
            }
        }

        // Yaz-Çevir penceresi için: Türkçe yazıldıysa İngilizceye, İngilizce yazıldıysa
        // Türkçeye çevirir. Dönüş: [çeviri, yön yazısı, hedef dil]. Hata mesajı kullanıcıya gösterilir.
        async Task<string[]> TranslateForWriter(string text)
        {
            if (text.Length > MaxChars * 4) throw new Exception("Metin çok uzun; en fazla " + (MaxChars * 4) + " karakter.");
            if (DateTime.UtcNow < pauseUntil)
            {
                int sec = (int)Math.Ceiling((pauseUntil - DateTime.UtcNow).TotalSeconds);
                throw new Exception("Google kısa bir mola istedi; " + sec + " saniye sonra tekrar deneyin.");
            }
            try
            {
                string[] r = await CachedTranslate(text, "en");
                if (r[1] != "en") return new string[] { r[0], "Türkçe → İngilizce", "en" };
                string[] back = await CachedTranslate(text, "tr");
                return new string[] { back[0], "İngilizce → Türkçe", "tr" };
            }
            catch (Exception ex)
            {
                Log("hata (Yaz-Çevir): " + ex.Message);
                HttpStatusException he = ex as HttpStatusException;
                if (he != null && he.Code == 429)
                {
                    pauseUntil = DateTime.UtcNow.AddSeconds(60);
                    throw new Exception("Google kısa bir mola istedi; 1 dakika sonra tekrar deneyin.");
                }
                if (ex is TaskCanceledException) throw new Exception("Çeviri gecikti (internet yavaş olabilir); tekrar deneyin.");
                throw new Exception("Çeviri alınamadı (internet bağlantısını kontrol edin).");
            }
        }

        // ---- Claude ile doğal İngilizce (Claude Code aboneliği üzerinden, ek ücret yok) ----
        // Bilgisayardaki Claude Code (claude.exe) "-p" ile çağrılır; istek kullanıcının abonelik
        // girişiyle gider. API anahtarı alt sürece hiç geçirilmez: anahtar sonradan eklense bile
        // ücretli API'ye düşülmez.
        const string NaturalPrompt =
            "You help a Turkish speaker write natural, everyday English. " +
            "The user message is text they wrote, in Turkish or in their own English; a machine translation may be attached, often too literal. " +
            "If the text is Turkish: write how a native English speaker would naturally say it, keeping the meaning and tone. " +
            "If the text is English: correct it and make it natural, changing as little as needed. " +
            "Keep the exact meaning: do not add, drop or soften ideas, and keep every action as described " +
            "(if they press a button, they press it; they do not say it). " +
            "Avoid redundant words (for example, 'dictating' already means by voice). " +
            "When the text is speech-to-text, read past dictation slips and keep what they clearly meant. " +
            "Reply with ONLY a JSON object, no code fences: " +
            "{\"natural\": \"<final English text>\", \"note\": \"<one or two short sentences in simple Turkish: the most important change and why; if nothing needed changing, say so>\"}";

        static string FindClaude()
        {
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".local\bin\claude.exe");
            if (File.Exists(p)) return p;
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    string c = Path.Combine(dir.Trim(), "claude.exe");
                    if (File.Exists(c)) return c;
                }
                catch { }
            }
            return null;
        }

        // Claude Code'un çalıştığı boş klasör (proje kurallarını yüklemesin) ve sistem metni dosyası.
        static string PrepareWorkDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "seccevir-claude");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "sistem.txt"), NaturalPrompt, new UTF8Encoding(false));
            return dir;
        }

        async Task<string[]> Naturalize(string text, string literal)
        {
            if (text.Length > 2000) throw new Exception("Doğal yap için metin çok uzun (en fazla 2000 karakter).");
            string exe = FindClaude();
            if (exe == null) throw new Exception("Claude Code bulunamadı (claude.exe).");

            string user = "Text the user wrote:\n" + text;
            if (literal.Length > 0 && literal != text) user += "\n\nMachine translation (may be too literal):\n" + literal;

            string[] res = await Task.Run(delegate { return RunClaude(exe, user); });
            return res;
        }

        static string[] RunClaude(string exe, string user)
        {
            string dir = PrepareWorkDir();
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe,
                "-p --model haiku --tools \"\" --strict-mcp-config --no-session-persistence --output-format json " +
                "--system-prompt-file \"" + Path.Combine(dir, "sistem.txt") + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = dir;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            // Ücretli API'ye asla düşmesin: anahtarlar alt sürece verilmez, abonelik girişi kullanılır.
            psi.EnvironmentVariables.Remove("ANTHROPIC_API_KEY");
            psi.EnvironmentVariables.Remove("ANTHROPIC_AUTH_TOKEN");

            string stdout;
            int exit;
            using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi))
            {
                byte[] input = new UTF8Encoding(false).GetBytes(user);
                proc.StandardInput.BaseStream.Write(input, 0, input.Length);
                proc.StandardInput.Close();
                Task<string> outTask = proc.StandardOutput.ReadToEndAsync();
                Task<string> errTask = proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit(90000))
                {
                    try { proc.Kill(); } catch { }
                    throw new Exception("Claude gecikti; tekrar deneyin.");
                }
                stdout = outTask.Result;
                exit = proc.ExitCode;
            }

            JavaScriptSerializer js = new JavaScriptSerializer();
            Dictionary<string, object> root = null;
            try { root = js.DeserializeObject(stdout.Trim()) as Dictionary<string, object>; } catch { }
            if (root == null)
            {
                Log("Claude Code: çıktı okunamadı (çıkış kodu " + exit + ")");
                throw new Exception("Claude Code yanıt vermedi. Bir terminalde \"claude\" yazıp giriş yapılı olduğunu kontrol edin.");
            }
            object v;
            string result = root.TryGetValue("result", out v) ? (v as string ?? "") : "";
            bool isError = root.TryGetValue("is_error", out v) && v is bool && (bool)v;
            if (isError || exit != 0)
            {
                Log("Claude Code hata (çıkış kodu " + exit + ")");
                string m = result.Length > 160 ? result.Substring(0, 160) + "…" : result;
                throw new Exception("Claude Code: " + (m.Length > 0 ? m : "istek tamamlanamadı (abonelik limiti dolmuş olabilir)."));
            }

            // Model bazen JSON'u ``` içine alır: ilk { ile son } arasını al.
            int a = result.IndexOf('{'), b = result.LastIndexOf('}');
            if (a >= 0 && b > a)
            {
                try
                {
                    Dictionary<string, object> o = js.DeserializeObject(result.Substring(a, b - a + 1)) as Dictionary<string, object>;
                    if (o != null && o.ContainsKey("natural"))
                        return new string[] { (o["natural"] as string) ?? "", o.ContainsKey("note") ? (o["note"] as string) ?? "" : "" };
                }
                catch { }
            }
            Log("Claude Code: JSON okunamadı");
            throw new Exception("Claude'un yanıtı okunamadı; tekrar deneyin.");
        }

        async Task<string[]> CachedTranslate(string text, string tl)
        {
            string[] hit;
            if (cache.TryGetValue(tl + "|" + text, out hit)) return hit;
            string[] res = await Translate(text, tl);
            if (cache.Count > 300) cache.Clear();
            cache[tl + "|" + text] = res;
            return res;
        }

        // Dönüş: [çeviri, Google'ın tanıdığı kaynak dil (ör. "en")]. tl: hedef dil ("tr" ya da "en").
        // Ana adres "çok fazla istek" (429) ya da başka bir hata verirse ikinci adres denenir.
        static async Task<string[]> Translate(string text, string tl)
        {
            try { return await TranslateMain(text, tl); }
            catch (TaskCanceledException) { throw; }
            catch (Exception ex) { Log("ana çeviri adresi: " + ex.Message + "; yedek adres deneniyor"); }
            return await TranslateBackup(text, tl);
        }

        static async Task<string[]> TranslateBackup(string text, string tl)
        {
            string json = await Get("https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=" + tl + "&q="
                                    + Uri.EscapeDataString(text));
            // Biçim: [["çeviri","en"]] ya da ["çeviri"]
            object[] root = (object[])new JavaScriptSerializer().DeserializeObject(json);
            object[] first = root[0] as object[];
            if (first != null)
                return new string[] { (string)first[0], first.Length > 1 && first[1] is string ? (string)first[1] : "en" };
            return new string[] { (string)root[0], "en" };
        }

        static async Task<string[]> TranslateMain(string text, string tl)
        {
            string json = await Get("https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=" + tl + "&dt=t&q="
                                    + Uri.EscapeDataString(text));
            object[] root = (object[])new JavaScriptSerializer().DeserializeObject(json);
            StringBuilder sb = new StringBuilder();
            foreach (object seg in (object[])root[0])
            {
                object[] parts = seg as object[];
                if (parts != null && parts.Length > 0 && parts[0] is string) sb.Append((string)parts[0]);
            }
            string lang = root.Length > 2 && root[2] is string ? (string)root[2] : "en";
            return new string[] { sb.ToString(), lang };
        }

        // ---- Ses: dinle, duraklat, devam et ----
        // Ses ya Google'dan gelen mp3 (MCI ile çalınır) ya da Windows'un kendi sesidir (synth).
        bool speechLoading;
        bool usingSynth;

        // "loading", "playing", "paused" ya da "stopped".
        public string SpeechState()
        {
            if (speechLoading) return "loading";
            if (usingSynth)
            {
                if (synth == null) return "stopped";
                if (synth.State == System.Speech.Synthesis.SynthesizerState.Speaking) return "playing";
                if (synth.State == System.Speech.Synthesis.SynthesizerState.Paused) return "paused";
                return "stopped";
            }
            StringBuilder sb = new StringBuilder(32);
            if (Native.mciSendString("status seccevir mode", sb, sb.Capacity, IntPtr.Zero) != 0) return "stopped";
            string m = sb.ToString();
            return m == "playing" || m == "paused" ? m : "stopped";
        }

        public void PauseSpeech()
        {
            if (usingSynth) { if (synth != null) try { synth.Pause(); } catch { } }
            else Native.mciSendString("pause seccevir", null, 0, IntPtr.Zero);
        }

        public void ResumeSpeech()
        {
            if (usingSynth) { if (synth != null) try { synth.Resume(); } catch { } }
            else Native.mciSendString("resume seccevir", null, 0, IntPtr.Zero);
        }

        void StopSpeech()
        {
            Native.mciSendString("close seccevir", null, 0, IntPtr.Zero);
            if (synth != null)
            {
                try
                {
                    if (synth.State == System.Speech.Synthesis.SynthesizerState.Paused) synth.Resume();
                    synth.SpeakAsyncCancelAll();
                }
                catch { }
            }
        }

        // Google'ın ses servisi tek istekte en fazla ~200 karakter alır. Uzun metin cümle,
        // virgül ya da kelime sınırından parçalanır; parçaların sesi tek mp3'te birleştirilir.
        const int TtsChunk = 190, TtsMaxChars = 3000;

        static List<string> SplitForTts(string text)
        {
            List<string> parts = new List<string>();
            string rest = Regex.Replace(text, @"\s+", " ").Trim();
            while (rest.Length > TtsChunk)
            {
                int cut = -1;
                foreach (char c in new[] { '.', '!', '?', ';', ':', ',' })
                {
                    int i = rest.LastIndexOf(c, TtsChunk - 1);
                    if (i > TtsChunk / 3 && i > cut) { cut = i; break; }
                }
                if (cut < 0) cut = rest.LastIndexOf(' ', TtsChunk - 1);
                if (cut <= 0) cut = TtsChunk - 1;
                parts.Add(rest.Substring(0, cut + 1).Trim());
                rest = rest.Substring(cut + 1).Trim();
            }
            if (rest.Length > 0) parts.Add(rest);
            return parts;
        }

        int speakId;

        async void Speak(string text, string lang)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > TtsMaxChars) text = text.Substring(0, TtsMaxChars);
            int id = ++speakId;
            StopSpeech();
            usingSynth = false;
            speechLoading = true;
            try
            {
                using (MemoryStream all = new MemoryStream())
                {
                    foreach (string part in SplitForTts(text))
                    {
                        string url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=" + Uri.EscapeDataString(lang)
                                     + "&total=1&idx=0&textlen=" + part.Length + "&q=" + Uri.EscapeDataString(part);
                        using (HttpResponseMessage r = await http.GetAsync(url))
                        {
                            if (!r.IsSuccessStatusCode) throw new HttpStatusException((int)r.StatusCode);
                            byte[] b = await r.Content.ReadAsByteArrayAsync();
                            all.Write(b, 0, b.Length);
                        }
                        if (id != speakId) return; // bu arada yeni bir Dinle'ye basıldı
                    }
                    File.WriteAllBytes(mp3Path, all.ToArray());
                }
                if (Native.mciSendString("open \"" + mp3Path + "\" type mpegvideo alias seccevir", null, 0, IntPtr.Zero) != 0)
                    throw new IOException("mci");
                Native.mciSendString("play seccevir", null, 0, IntPtr.Zero);
                speechLoading = false;
            }
            catch
            {
                if (id != speakId) return;
                speechLoading = false;
                usingSynth = true;
                // İnternetten ses alınamazsa Windows'un kendi İngilizce sesine düş.
                try
                {
                    if (synth == null)
                    {
                        synth = new System.Speech.Synthesis.SpeechSynthesizer();
                        try
                        {
                            synth.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.NotSet,
                                System.Speech.Synthesis.VoiceAge.NotSet, 0, new CultureInfo("en-US"));
                        }
                        catch { }
                    }
                    synth.SpeakAsyncCancelAll();
                    synth.SpeakAsync(text);
                }
                catch { }
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            System.Threading.Mutex single = new System.Threading.Mutex(true, "SecCevir_tek_kopya", out created);
            if (!created) return;

            Native.SetProcessDPIAware();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.Run(new App());
            GC.KeepAlive(single);
        }
    }
}
