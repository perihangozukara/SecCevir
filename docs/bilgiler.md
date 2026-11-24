# Seç-Çevir · Bilgiler

## Ne işe yarar?
- İngilizce bir yazıyı fareyle seçersin. Türkçesi hemen altında çıkar.
- Her programda çalışır: tarayıcı, PDF, Word, terminal.
- Türkçe yazılara dokunmaz.
- Ücretsizdir. Kurulum yoktur.

## Nasıl indirilir?
- GitHub sayfasındaki "SecCevir.exe dosyasını indir" bağlantısına tıkla.
- Bağlantı her zaman son sürümü verir.
- Windows 10 ya da 11 gerekir.
- İlk açılışta mavi bir uyarı çıkabilir. "Ek bilgi" → "Yine de çalıştır" de.
- Uyarı programın zararlı olduğunu söylemez. Yalnızca imzalı olmadığını söyler.

## Kendiliğinden güncellenir mi?
- Hayır. Seç-Çevir kendini güncellemez.
- Yeni sürüm için aynı bağlantıdan tekrar indir.
- Yeni exe'yi eskisinin üstüne koy. Böylece "Windows ile başlat" çalışmaya devam eder.
- Başka bir klasöre koyarsan: simgeye sağ tıkla, "Windows ile başlat"ı bir kez kapatıp aç.

## Kısayollar
- Ctrl+Alt+T: Yaz-Çevir. Türkçe yaz, İngilizcesi çıksın.
- Ctrl+Alt+O: Ekrandan oku. Seçilemeyen yazıyı okur.
- Esc: Pencereyi kapatır ya da vazgeçer.
- Ctrl+Enter: Yaz-Çevir'de çeviriyi kopyalar ve kapatır.

## Ekrandan oku (Ctrl+Alt+O)
- Taranmış PDF'de yazı seçilemez. Çünkü sayfa aslında bir resimdir.
- Ctrl+Alt+O'ya bas. Ekran kararır.
- Okunacak yerin çevresine fareyle bir dikdörtgen çiz.
- Windows'un kendi yazı tanıma özelliği (OCR) yazıyı okur.
- Kutuda hem okunan İngilizce hem Türkçesi görünür. Okuma hatası varsa fark edersin.
- İnternet gerekmez, okuma bilgisayarda yapılır. Yalnızca çeviri için internet gerekir.

## Ekrandan oku iyi okumuyorsa
- Windows'ta İngilizce OCR parçası kurulu olmalı.
- İngilizce dil yüklü olsa bile bu parça ayrıca inmemiş olabilir.
- Kurmak için PowerShell'i yönetici olarak aç ve şunu yaz:
  `Add-WindowsCapability -Online -Name "Language.OCR~~~en-US~0.0.1.0"`
- Sonra Seç-Çevir'i kapatıp aç.
- Çok küçük yazıda daha büyük bir alan çiz ya da sayfayı yakınlaştır.

## Uzun yazı seçersen
- Program bir seferde en fazla 1000 karakter çevirir.
- Daha uzun seçersen ilk 1000 karakteri çevirir.
- Kutunun üstünde altın renkli bir uyarı çıkar.
- Yazı cümle sonunda kesilir, kelime yarım kalmaz.

## Gizlilik
- Seçtiğin yazı çeviri için Google Çeviri'ye gider. Gizli yazılarda kullanma.
- Ekrandan okumada ekran görüntüsü diske kaydedilmez.
- Program seçtiğin yazıyı hiçbir dosyaya yazmaz.

## Sorun olursa
- Exe'nin yanında SecCevir.log dosyası var.
- Program bir yazıyı neden çevirmediğini oraya yazar.
- Yazının kendisini asla yazmaz.

## Diğer özellikler
- 🔊 düğmesi yazıyı sesli okur.
- 📋 düğmesi Türkçe çeviriyi kopyalar.
- ✨ Doğal İngilizce: yazdığın cümleyi daha doğal yapar. Bunun için bilgisayarda Claude Code kurulu olmalı.
- Terminalde çalışan komutu kesmez.
