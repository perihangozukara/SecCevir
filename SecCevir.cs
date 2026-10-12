// Seç-Çevir: ekranda fareyle seçilen İngilizce metnin Türkçesini seçimin altında gösterir.
// Derleme: derle.bat
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SecCevir
{
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
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr hwnd);
    }

    class Popup : Form
    {
        readonly Label spk, en, tr;
        readonly float scale;
        Point anchor;

        public event EventHandler SpeakClicked;

        public Popup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(32, 33, 36);
            using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;

            spk = new Label();
            spk.Text = "🔊";
            spk.Font = new Font("Segoe UI Emoji", 12f);
            spk.ForeColor = Color.FromArgb(138, 180, 248);
            spk.Cursor = Cursors.Hand;
            spk.TextAlign = ContentAlignment.MiddleCenter;
            spk.Size = new Size(S(30), S(26));
            spk.Click += delegate { if (SpeakClicked != null) SpeakClicked(this, EventArgs.Empty); };

            en = new Label();
            en.Font = new Font("Segoe UI", 9.5f);
            en.ForeColor = Color.FromArgb(170, 173, 178);

            tr = new Label();
            tr.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            tr.ForeColor = Color.White;

            Controls.Add(spk);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(95, 99, 104)))
                e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        public void ShowAt(Point at, string enText, string trText)
        {
            anchor = at;
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
            Size sz = TextRenderer.MeasureText(l.Text, l.Font, new Size(maxWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return new Size(sz.Width + 8, sz.Height + 4);
        }

        void Relayout()
        {
            int p = S(10);
            en.UseMnemonic = false;
            tr.UseMnemonic = false;
            spk.Location = new Point(p - S(4), p - S(4));
            en.Size = Measure(en, S(430));
            en.Location = new Point(spk.Right + S(4), p);
            tr.Size = Measure(tr, S(470));
            tr.Location = new Point(p, Math.Max(spk.Bottom, en.Bottom) + S(4));
            ClientSize = new Size(Math.Max(en.Right, tr.Right) + p, tr.Bottom + p);

            Rectangle wa = Screen.FromPoint(anchor).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(anchor.X, wa.Right - Width));
            int y = anchor.Y;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            Location = new Point(x, y);
            Invalidate();
        }
    }

    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "SecCevir";
        const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        readonly NotifyIcon tray;
        readonly Popup popup;
        readonly Native.HookProc hookProc;
        readonly IntPtr hook;
        readonly string mp3Path = Path.Combine(Path.GetTempPath(), "seccevir_ses.mp3");

        bool enabled = true;
        bool busy;
        bool downInPopup;
        Point downPt;
        DateTime lastClickTime = DateTime.MinValue;
        Point lastClickPt;
        int requestId;
        string currentText = "";
        System.Speech.Synthesis.SpeechSynthesizer synth;

        public App()
        {
            popup = new Popup();
            IntPtr unused = popup.Handle;
            popup.SpeakClicked += delegate { Speak(currentText); };

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem onOff = new ToolStripMenuItem("Açık");
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
            tray.Text = "Seç-Çevir (İngilizce → Türkçe)";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.ShowBalloonTip(3000, "Seç-Çevir çalışıyor",
                "İngilizce bir metni fareyle seçin; Türkçesi altında görünür.", ToolTipIcon.Info);

            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
        }

        protected override void ExitThreadCore()
        {
            Native.UnhookWindowsHookEx(hook);
            Native.mciSendString("close seccevir", null, 0, IntPtr.Zero);
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (Font f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.Clear(Color.FromArgb(26, 115, 232));
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.DrawString("TR", f, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
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

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                if (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_LBUTTONUP ||
                    msg == Native.WM_RBUTTONDOWN || msg == Native.WM_MOUSEWHEEL)
                {
                    Native.MSLLHOOKSTRUCT s = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    Point pt = new Point(s.pt.x, s.pt.y);
                    // Kancadan hemen dön; işi mesaj kuyruğuna bırak ki fare takılmasın.
                    popup.BeginInvoke(new Action(delegate { OnMouse(msg, pt); }));
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
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
            if (!drag && !dbl) return;

            Point at = new Point(Math.Min(downPt.X, pt.X), Math.Max(downPt.Y, pt.Y) + 22);
            Capture(at);
        }

        // Terminallerde Ctrl+C çalışan komutu keser; oralarda hiç dokunma.
        static bool IsConsoleForeground()
        {
            StringBuilder sb = new StringBuilder(256);
            Native.GetClassName(Native.GetForegroundWindow(), sb, sb.Capacity);
            string cls = sb.ToString();
            return cls == "ConsoleWindowClass" || cls == "CASCADIA_HOSTING_WINDOW_CLASS"
                || cls == "mintty" || cls == "PuTTY";
        }

        async void Capture(Point at)
        {
            if (busy) { Log("atlandı: önceki seçim hâlâ kopyalanıyor"); return; }
            busy = true;
            string text;
            try
            {
                await Task.Delay(150);
                if (IsConsoleForeground()) { Log("atlandı: terminal penceresi"); return; }

                text = await CopySelection();
                if (text == null)
                {
                    // Bazı uygulamalar seçimi geç tamamlar; bir kez daha dene.
                    await Task.Delay(200);
                    text = await CopySelection();
                }
                if (text == null) { Log("atlandı: uygulama Ctrl+C'ye metin vermedi"); return; }
            }
            catch (Exception ex) { Log("hata (kopyalama): " + ex.Message); return; }
            finally { busy = false; }

            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length == 0 || text.Length > 500 || !Regex.IsMatch(text, "[A-Za-z]"))
            {
                Log("atlandı: metin boş, 500 karakterden uzun veya harf içermiyor (uzunluk " + text.Length + ")");
                return;
            }

            // Çeviri beklenirken yeni seçimler engellenmesin diye busy burada serbest.
            currentText = text;
            int id = ++requestId;
            popup.ShowAt(at, text, "…");

            string tr;
            try { tr = await Translate(text); }
            catch (Exception ex)
            {
                Log("hata (çeviri): " + ex.Message);
                tr = "Çeviri alınamadı (internet bağlantısını kontrol edin)";
            }
            if (id == requestId && popup.Visible) popup.SetTranslation(tr);
        }

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

        static DataObject BackupClipboard()
        {
            try
            {
                IDataObject old = Clipboard.GetDataObject();
                if (old == null) return null;
                DataObject copy = new DataObject();
                int n = 0;
                foreach (string f in old.GetFormats(false))
                {
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

            Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0x43, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0x43, 0, 2, UIntPtr.Zero);
            Native.keybd_event(0x11, 0, 2, UIntPtr.Zero);

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

        static async Task<string> Translate(string text)
        {
            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=tr&dt=t&q="
                         + Uri.EscapeDataString(text);
            string json;
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                json = await wc.DownloadStringTaskAsync(url);
            }
            object[] root = (object[])new JavaScriptSerializer().DeserializeObject(json);
            StringBuilder sb = new StringBuilder();
            foreach (object seg in (object[])root[0])
            {
                object[] parts = seg as object[];
                if (parts != null && parts.Length > 0 && parts[0] is string) sb.Append((string)parts[0]);
            }
            return sb.ToString();
        }

        async void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > 200) text = text.Substring(0, 200);
            try
            {
                Native.mciSendString("close seccevir", null, 0, IntPtr.Zero);
                string url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=en&q="
                             + Uri.EscapeDataString(text);
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                    await wc.DownloadFileTaskAsync(url, mp3Path);
                }
                if (Native.mciSendString("open \"" + mp3Path + "\" type mpegvideo alias seccevir", null, 0, IntPtr.Zero) != 0)
                    throw new IOException("mci");
                Native.mciSendString("play seccevir", null, 0, IntPtr.Zero);
            }
            catch
            {
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
