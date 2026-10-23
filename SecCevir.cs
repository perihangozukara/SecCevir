// Seç-Çevir: ekranda fareyle seçilen İngilizce metnin Türkçesini seçimin altında gösterir.
// Derleme: derle.bat
using System;
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
        readonly Label spk, en, tr;
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
            if (y + Height > wa.Bottom) y = Math.Max(wa.Top, aboveY - Height);
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
        readonly MouseHook mouseHook;
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
            Debug("bırakıldı: sürükleme=" + drag + " çift=" + dbl);
            if (!drag && !dbl) return;

            Point at = new Point(Math.Min(downPt.X, pt.X), Math.Max(downPt.Y, pt.Y) + 22);
            int topY = Math.Min(downPt.Y, pt.Y) - 22;
            Capture(at, topY);
        }

        // Terminallerde Ctrl+C çalışan komutu keser; Dosya Gezgini ve masaüstünde
        // dosyayı kopyalar. Bu pencerelerde hiç dokunma.
        static string ForegroundClass()
        {
            StringBuilder sb = new StringBuilder(256);
            Native.GetClassName(Native.GetForegroundWindow(), sb, sb.Capacity);
            return sb.ToString();
        }

        static bool IsSkippedForeground()
        {
            string cls = ForegroundClass();
            return cls == "ConsoleWindowClass" || cls == "CASCADIA_HOSTING_WINDOW_CLASS"
                || cls == "mintty" || cls == "PuTTY"
                || cls == "CabinetWClass" || cls == "ExploreWClass"
                || cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd";
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

        async void Capture(Point at, int topY)
        {
            if (busy) { Log("atlandı: önceki seçim hâlâ kopyalanıyor"); return; }
            busy = true;
            string text;
            try
            {
                await Task.Delay(150);
                if (IsSkippedForeground()) { Debug("atlandı: pencere " + ForegroundClass()); return; }
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
            catch (Exception ex) { Log("hata (kopyalama): " + ex.Message); return; }
            finally { busy = false; }

            text = Regex.Replace(text, @"\s+", " ").Trim();
            Debug("kopyalandı: " + text.Length + " karakter");
            if (text.Length == 0 || !Regex.IsMatch(text, "[A-Za-z]")) return;
            // ğ, ı, ş, İ yalnızca Türkçede geçer: metin zaten Türkçe, çevirmeye gerek yok.
            if (Regex.IsMatch(text, "[ğĞıİşŞ]")) { Debug("atlandı: Türkçe harf var"); return; }

            bool cut = text.Length > MaxChars;
            if (cut) text = text.Substring(0, MaxChars);
            string shown = text.Length > 300 ? text.Substring(0, 300) + "…" : text;

            // Çeviri beklenirken yeni seçimler engellenmesin diye busy burada serbest.
            currentText = text;
            int id = ++requestId;

            string[] hit;
            if (cache.TryGetValue(text, out hit))
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
                string[] res = await Translate(text);
                Debug("çevrildi: dil=" + res[1] + " görünür=" + popup.Visible + " güncel=" + (id == requestId));
                if (res[1] == "tr")
                {
                    // Metin zaten Türkçeymiş: popup'ı kapat.
                    if (id == requestId) popup.Hide();
                    return;
                }
                if (cache.Count > 300) cache.Clear();
                cache[text] = res;
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

        // Dönüş: [Türkçe çeviri, Google'ın tanıdığı kaynak dil (ör. "en")]
        // Ana adres "çok fazla istek" (429) ya da başka bir hata verirse ikinci adres denenir.
        static async Task<string[]> Translate(string text)
        {
            try { return await TranslateMain(text); }
            catch (TaskCanceledException) { throw; }
            catch (Exception ex) { Log("ana çeviri adresi: " + ex.Message + "; yedek adres deneniyor"); }
            return await TranslateBackup(text);
        }

        static async Task<string[]> TranslateBackup(string text)
        {
            string json = await Get("https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=tr&q="
                                    + Uri.EscapeDataString(text));
            // Biçim: [["çeviri","en"]] ya da ["çeviri"]
            object[] root = (object[])new JavaScriptSerializer().DeserializeObject(json);
            object[] first = root[0] as object[];
            if (first != null)
                return new string[] { (string)first[0], first.Length > 1 && first[1] is string ? (string)first[1] : "en" };
            return new string[] { (string)root[0], "en" };
        }

        static async Task<string[]> TranslateMain(string text)
        {
            string json = await Get("https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=tr&dt=t&q="
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

        async void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > 200) text = text.Substring(0, 200);
            try
            {
                Native.mciSendString("close seccevir", null, 0, IntPtr.Zero);
                string url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=" + Uri.EscapeDataString(currentLang) + "&q="
                             + Uri.EscapeDataString(text);
                using (HttpResponseMessage r = await http.GetAsync(url))
                {
                    if (!r.IsSuccessStatusCode) throw new HttpStatusException((int)r.StatusCode);
                    File.WriteAllBytes(mp3Path, await r.Content.ReadAsByteArrayAsync());
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
