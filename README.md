# Seç-Çevir

🇹🇷 Türkçe · [English below](#english)

İngilizce bilmeyenler için küçük bir Windows aracı. Bilgisayarda hangi programda olursan ol, İngilizce bir yazıyı fareyle seç; Türkçesi hemen altında görünür. Türkçe yazılara dokunmaz.

<p>
  <img src="docs/kapak.png" alt="Seç-Çevir: İngilizce yazıyı seç, Türkçesini gör" width="49%">
  <img src="docs/yaz-cevir.png" alt="Yaz-Çevir penceresi" width="49%">
</p>

## İndir

**[⬇ SecCevir.exe dosyasını indir](https://github.com/perihangozukara/SecCevir/releases/latest/download/SecCevir.exe)**

İndirdiğin dosyaya çift tıkla, çalışır. Kurulum yoktur. Windows 10 ya da 11 gerekir.

### İlk açılışta Windows uyarı verirse

Program küçük ve yeni olduğu için Windows ilk açılışta mavi bir uyarı penceresi gösterebilir. Bu, programın zararlı olduğu anlamına gelmez; yalnızca imzalı olmadığını söyler.

1. **Ek bilgi** yazısına tıkla.
2. Altta çıkan **Yine de çalıştır** düğmesine bas.

(Windows'un İngilizceyse: **More info → Run anyway**.)

## Nasıl kullanılır

1. Program açılınca ekranın sağ altında, saatin yanındaki küçük simgelerin arasına yerleşir.
2. Herhangi bir programda İngilizce bir yazıyı fareyle seç.
3. Türkçesi seçtiğin yerin hemen altında çıkar.

## Neler yapar

- **Her yerde çalışır.** Yalnızca internet tarayıcısında değil; programlarda, pencerelerde ve terminalde de çalışır. Terminalde çalışan komutu kesmez.
- **Yaz-Çevir.** Saatin yanındaki simgeye tıkla ya da `Ctrl+Alt+T` tuşlarına bas. Türkçe bir cümle yaz, İngilizcesi çıkar. İngilizce yazarsan Türkçesi çıkar. `Ctrl+Enter` kopyalar ve pencereyi kapatır, `Esc` kapatır.
- **✨ Doğal İngilizce.** Yaz-Çevir penceresinde, yazdığın cümleyi Claude'a daha doğal bir İngilizceye çevirtir ve neyi değiştirdiğini kısa bir Türkçe notla söyler. Bunun için bilgisayarında [Claude Code](https://claude.com/claude-code) kurulu ve giriş yapılmış olmalıdır. Diğer her şey onsuz çalışır.
- **Kopyala.** Çeviri kutusundaki 📋 düğmesi Türkçe çeviriyi kopyalar.
- **Sesli okur.** 🔊 düğmesi yazıyı sesli okur.
- **Aç / kapat.** Simgeye sağ tıkla: "Seçince çevir" ile açıp kapatabilir, "Windows ile başlat" ile bilgisayar açılınca kendiliğinden başlamasını sağlayabilirsin.

## Gizlilik

Seçtiğin yazı, çevrilmek üzere Google Çeviri'ye gönderilir. Gizli yazılarda kullanma.

## Sorun olursa

Program, bir yazıyı neden çevirmediğini exe dosyasının yanındaki `SecCevir.log` dosyasına yazar. Seçtiğin yazının kendisini asla yazmaz.

---

<a name="english"></a>

# Seç-Çevir (Select & Translate) · English

A small Windows tool for people reading English: select English (or other foreign-language) text with the mouse in any app, and its Turkish translation appears right under the selection. Turkish text is ignored.

<p>
  <img src="docs/kapak.png" alt="Seç-Çevir: select English text, see the Turkish translation" width="49%">
  <img src="docs/yaz-cevir.png" alt="Type & translate window" width="49%">
</p>

## Download
Get `SecCevir.exe` from the [latest release](https://github.com/perihangozukara/SecCevir/releases/latest) and run it. No installer; it lives in the system tray.

- Needs Windows 10 or 11 (.NET Framework 4, already part of Windows).
- The exe is not code-signed, so Windows SmartScreen may warn on first run: click **More info → Run anyway**. You can also build it yourself from the source (see Build).
- ✨ Make it natural needs [Claude Code](https://claude.com/claude-code) installed and logged in; everything else works without it.

## Features

- Works in terminals too (Windows Terminal, console, Git Bash, PuTTY) without sending Ctrl+C, so running commands are never interrupted. The selection is read through UI Automation, or from the clipboard when the terminal copies on select.
- **Type & translate:** click the tray icon (or press Ctrl+Alt+T anywhere), type a Turkish sentence and get its English translation (English input is translated to Turkish). Copy button, Ctrl+Enter copies and closes, Esc closes.
- **✨ Make it natural:** in the type & translate window, asks Claude (Claude Haiku 5.5) for natural English, with a short Turkish note on what changed. If you type English, it corrects your English. It runs through the locally installed Claude Code (`claude -p`) with your existing Claude subscription login; no API key is used or passed on, so it never bills the paid API.
- The selection popup has a copy button for the Turkish translation
- Works from the system tray; can be switched on/off and set to start with Windows
- Can read the text aloud
- Single C# file, no installer

## Build
Run `derle.bat` (uses the C# compiler that ships with Windows / .NET Framework 4). It creates `SecCevir.exe`.

## Troubleshooting
The app writes skip reasons to `SecCevir.log` next to the exe (never the selected text). Create an empty `SecCevir.debug` file there for step-by-step logging.

## Privacy
The selected text is sent to Google Translate to get the translation. Do not use it on confidential text.

Built with the help of an AI coding assistant as part of my learning journey.
