# Seç-Çevir · Hata defteri

Geliştirirken çıkan hatalar, nedenleri ve sonrasında konan kurallar.

## Terminalde Ctrl+C çalışan komutu kesiyordu
- Neden: Seçili yazı Ctrl+C gönderilerek kopyalanıyordu; terminalde Ctrl+C komutu durdurur.
- Kural: Terminallere (Windows Terminal, konsol, Git Bash, PuTTY) tuş gönderilmez. Seçim panodan ya da UI Automation ile okunur.

## Ücretli API kullanılmaz
- "Doğal İngilizce" için ücretli API yerine bilgisayardaki Claude Code (`claude -p`, abonelik girişi) kullanılır; alt sürece API anahtarı geçirilmez.

## Sesli okuma 200. karakterde duruyordu
- Neden: Google'ın ses servisi tek istekte ~200 karakter alıyor.
- Kural: Metin cümle / virgül / kelime sınırından ≤190 karakterlik parçalara bölünür, sesler tek dosyada birleştirilir. Üst sınır 3000 karakter.

## Terminalde bazı seçimler çevrilmiyordu
- Neden: Pano kısa bekleniyordu; tek bir Türkçe harf metni "Türkçe" saydırıyordu; bazı atlama nedenleri log'a yazılmıyordu.
- Kural: Terminalde pano 1,5 sn beklenir, UI Automation aynı anda denenir. Her atlama nedeni log'a yazılır (seçilen metin asla yazılmaz).

## Açılışta eski sürüm başlıyordu
- Neden: "Windows ile başlat" o an çalışan exe'nin yolunu kaydeder; exe taşınınca kayıt eski yolu göstermeye devam etti.
- Kural: Exe taşınınca "Windows ile başlat" kapatılıp yeniden açılır.

## Ekran görüntüsü panoya gelmiyordu
- Neden: Ekran görüntüsü aracında alan sürüklemek "seçim" sayıldı; panonun geri yüklenmesi görüntüyü sildi.
- Kural: Pano yalnızca uygulamanın kendi Ctrl+C'si metin getirdiyse eski hâline döndürülür. Ekran görüntüsü araçlarında hiçbir şey yapılmaz.

## Tarayıcıda ve PDF okuyucularda çeviri çıkmıyordu
- Neden: Bazı okuyucular PDF sayfasını resim olarak çiziyor; "Türkçe mi" kontrolü harf sayıyordu; meşgulken gelen seçim atılıyordu.
- Kural: Türkçe kontrolü kelimeyle yapılır (%25). Google karışık metni Türkçe sayarsa kaynak dil İngilizce diye zorlanır. Ctrl+C metin vermezse UI Automation denenir. Meşgulken gelen seçim sıraya konur.

## Ekrandan oku (OCR) eski derleyiciyle derlenmedi
- Neden: .NET Framework 4 derleyicisinin hazır WinRT yardımcıları, Windows SDK'nın `Windows.winmd` dosyasını istiyor.
- Çözüm: WinRT işlemi `Completed` olayıyla beklenir, tampon `CryptographicBuffer.CreateFromByteArray` ile yapılır; `derle.bat` `System32\WinMetadata` dosyalarına başvurur.

## Ekrandan oku: "Object is currently in use elsewhere" ve 0x80004004
- Neden: Seçilen alan `Bitmap.Clone` ile kesilince büyük görüntüyle bellek paylaşıyordu; aynı OCR motoruna aynı anda iki okuma gidince biri iptal ediliyordu.
- Kural: Arka plana verilen resim bağımsız bir kopyadır. OCR okumaları sıraya konur; iptalde bir kez yeniden denenir.
- Not: İngilizce OCR parçası yoksa Türkçe okuyucu kullanılır ve küçük yazıda hata artar. Kurulum: `Add-WindowsCapability -Online -Name "Language.OCR~~~en-US~0.0.1.0"` (yönetici PowerShell).
