# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · [Español](README.es.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Polski](README.pl.md) · **Türkçe** · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

Albion Online sohbetini kendi dilinde oku, karşındakinin dilinde cevap ver.

Ücretsiz, hesap yok, API anahtarı yok, kurulum ayarı yok. **Açık kaynak**: tüm kod herkese açık, ne yaptığını herkes kontrol edebilir.

---

## Ne yapar

- **Okuduklarını çevirir.** Diğer oyuncuların mesajları (Say, Whisper, Party, Guild, Alliance ve istersen genel kanallar) geldiği anda senin dilinde görünür. Zaten senin dilinde olan mesajlara dokunulmaz.
- **Yazdıklarını çevirir.** Kısayola bas, kendi dilinde yaz, Enter'a bas; çeviri Albion'a yapıştırmaya hazır.
- **Sohbeti oyunun üstünde gösterir.** İsteğe bağlı, şeffaf bir katman; fare tıklamaları doğrudan oyuna geçer.

## Kurulum

1. [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases) sayfasını aç ve `AlbionChatTranslator-win-Setup.exe` dosyasını indir.
2. Çalıştır. Başka bir şey gerekmez (.NET dahil). Sadece senin Windows kullanıcın için kurulur.
3. Okuduğun dili seç.
4. **▶ Start**'a tıkla ve Windows penceresinde **Evet**'i seç ([neden?](#neden-yönetici-izni-istiyor)).
5. Albion'a gir. Sohbet sekmelerin uygulamada kendiliğinden görünür.

Güncellemeler kendiliğinden indirilir ve kurulur.

## Nasıl kullanılır

### Sohbeti okumak

Sadece oyna. Çevrilen mesajlar uygulamada (ve açıksa katmanda) görünür:

```
14:02 Ana: Lymhurst'te ucuz t6.1 çanta satan var mı? (Portuguese)
```

- Orijinal metni görmek için mesajın **üzerine gel**.
- O kişiye kendi dilinde cevap vermek için `(Portuguese)` etiketine **tıkla**.
- Senin dilindeki mesajlar, çok kısa mesajlar ("gg", "ok") ve eşya bağlantıları olduğu gibi gösterilir.

### Onların dilinde cevap vermek

1. Oyunda **Ctrl+Shift+R**'ye bas (ya da bir mesajın dil etiketine tıkla).
2. **Kendi** dilinde yaz ve **Enter**'a bas.
3. Çeviri artık panoda. Albion'da sohbeti aç, **Ctrl+V**'ye bas, sonra Enter.

Cevap kutusu, sana en son yazan kişinin diline zaten ayarlıdır; değiştirebilirsin.
Uygulama oyuna asla bir şey yazmaz veya göndermez. Yapıştıran ve gönderen her zaman **sensin**.

### Katman

**Ctrl+Shift+O** oyunun üstündeki şeffaf sohbeti gösterir veya gizler. Kilitliyken tıklamalar doğrudan Albion'a geçer. **Ctrl+Shift+L** taşımak ve boyutlandırmak için kilidi açar; kilitlemek için tekrar bas.

### Kısayollar

| Tuşlar | Ne yapar |
|---|---|
| **Ctrl+Shift+R** | Son yazan kişiye cevap ver |
| **Ctrl+Shift+O** | Katmanı göster / gizle |
| **Ctrl+Shift+L** | Taşımak için katmanın kilidini aç / kilitle |

Albion odaktayken çalışırlar. Başka bir program zaten kullanıyorsa uygulama söyler; uygulamadaki düğmeler aynı işi yapar.

### İşe yarar ayarlar

| Ayar | Ne için |
|---|---|
| **Sohbeti şu dile çevir** | Senin dilin. |
| **Ayrıca okuyabildiğim diller** | Anladığın diğer diller. Asla çevrilmez veya gönderilmez. |
| **Ne zaman çevrilsin** | *Otomatik* ya da *sadece [translate]'e tıkladığımda*. |
| **Bu kanalları asla gönderme** | Örneğin fısıltılar veya lonca, gizli kalsın istiyorsan. |
| **Bu kelimeleri asla çevirme** | Lonca adları, arkadaş adları, kendi argon. |
| **Çevirmenler** | Bu sırayla: **1.** kendi Microsoft Translator veya DeepL anahtarın (isteğe bağlı, en güvenilir), **2.** Google Çeviri (ayar gerekmez), **3.** çevrimdışı yedek. Her biri açılıp kapatılabilir. |
| **Çevirileri 7 gün hatırla** | Yeniden başlattıktan sonra daha az gönderim. Varsayılan olarak kapalı. |

## Nasıl çalışır

1. **Sohbeti okuma.** Albion'da eklenti sistemi yok; bu yüzden uygulama, bilinen topluluk araçları gibi oyunun ağ trafiğini okur. Küçük, gizli bir yardımcı bunu yapar ve uygulamaya sadece sohbet mesajlarını iletir. Sadece okur: hiçbir şeyi değiştirmez, göndermez veya enjekte etmez.
2. **Neyin çevrileceği.** Uygulama, senin bilgisayarında; zaten senin dillerinde olan, çok kısa olan, sadece eşya bağlantısı içeren veya daha önce çevrilmiş mesajları tanır. **Bunlar bilgisayarından asla çıkmaz.** Ayrıca Albion kelimelerini (şehirler, `t8.3` gibi tier'lar, eşya bağlantıları, "gank", "zerg"…) korur ve `lf healer pls` gibi argoyu önce düz İngilizceye çevirir; bu da çeviriyi çok iyileştirir.
3. **Çeviri.** Geriye kalan, yani sadece mesaj metni, Google Çeviri'ye (veya DeepL'e) gönderilir ve sonuç gösterilir.
4. **Cevap.** Yazdığın metin aynı şekilde çevrilir ve panoya konur.

### Neden yönetici izni istiyor?

Windows, ağ trafiğini okumaya sadece yönetici izniyle izin verir. Uygulama bunu en aza indirir:

- Kullandığın pencere **normal kullanıcı** olarak çalışır.
- İzni sadece küçücük, ayrı bir yardımcı alır. Tek yapabildiği Albion'un sohbet paketlerini okumak ve bunları sadece senin Windows kullanıcının açabildiği özel, tek yönlü bir bağlantıyla pencereye iletmektir.
- Yardımcının internet erişimi yoktur, dosya okumaz veya yazmaz ve komut kabul etmez.

## Gizlilik

| Google Çeviri'ye (veya DeepL'e) gönderilen | Asla gönderilmeyen |
|---|---|
| Senin dillerinde olmayan mesajların **metni** | Senin dillerindeki mesajlar, kısa mesajlar, tekrarlar |
| Cevap kutusuna yazdığın metin | Oyuncu adları, kanallar, loncalar, eşya bağlantıları |
| | Hesabın, karakterin veya herhangi bir oyun verisi |
| | Hariç tuttuğun kanallardan hiçbir şey |

- Her şey HTTPS ile istek gövdesinde gider (asla web adresinde değil); çerez yok, hesap yok, uygulama kimliği yok. Her web sitesinde olduğu gibi Google internet adresini (IP) görür.
- Durum çubuğu, bilgisayarından çıkan her mesajı sayar (*Sent for translation*).
- Telemetri, analiz, reklam veya sohbet kaydı yok. *Çevirileri hatırla* açılmadıkça diske hiçbir şey yazılmaz.
- Eklersen DeepL anahtarın Windows hesabınla şifrelenir.

Google Çeviri, Google'ın kendi web araçlarının kullandığı ücretsiz adres üzerinden kullanılır. Anahtar gerekmez ama resmi bir API değildir ve Google bağlantı başına sıkı şekilde sınırlar; hareketli bir sohbet sınıra dakikalar içinde ulaşabilir. Bu olduğunda uygulama kenara çekilir ve **çevrimdışı yedek** (bilgisayarında çalışan, hiçbir şey göndermeyen bir model; bir dil ilk kez gerektiğinde yaklaşık 110 MB indirir) ya da kendi anahtarın devralır, böylece çeviri durmaz. En güvenilir hizmet için ücretsiz bir Microsoft Translator veya DeepL anahtarı ekle.

## Açık kaynak: kendin kontrol et

Bu proje [GNU GPL v3](../../LICENSE) altında **özgür ve açık kaynaklıdır**. Yukarıdakilere körü körüne inanmak zorunda değilsin: bunları sağlayan kodu okuyabilirsin.

| İddia | Nerede kontrol edilir |
|---|---|
| Yardımcı sadece sohbeti okur, oyuna asla bir şey göndermez | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Sadece sohbet olayları işlenir | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| Yardımcı ve uygulama tek yönlü, sadece senin kullanıcın için konuşur | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| Göndermeden önce neler ayıklanır | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Google'a tam olarak ne ve nasıl gönderilir | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Albion kelimeleri ve eşya bağlantıları korunur | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| Bu davranışlar test edilmiştir | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**İndirilen dosyayı kontrol etme.** Kurulum dosyalarını, açık koddan [bu iş akışıyla](../../.github/workflows/release.yml) GitHub'ın kendisi derler ve derleme kaydını herkes açabilir. Her sürümde `SHA256SUMS.txt` bulunur: PowerShell'de `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` aynı değeri göstermelidir. Ya da kendin derle ([İngilizce geliştirici bölümüne](../../README.md#for-developers) bak).

Bir sorun mu buldun? [Bir issue aç](https://github.com/MrYellowHeart/AlbionChatTranslator/issues). Pull request'ler memnuniyetle karşılanır.

## Hesabım için güvenli mi?

Uygulama, diğer Albion topluluk araçları gibi ağ trafiğini sadece **okur**. Oyunu değiştirmez, belleğini okumaz, kod enjekte etmez, girişleri otomatikleştirmez ve oyun sunucularına hiçbir şey göndermez. Sandbox Interactive tarafından yapılmamış, onaylanmamış veya desteklenmemektedir; hiçbir üçüncü taraf araç, bir oyun şirketinin onu nasıl değerlendireceğini garanti edemez. **Kullanım riski sana aittir.**

## Sık karşılaşılan sorunlar

- **Hiçbir şey görünmüyor.** Durumun *Capturing* olduğundan emin ol ve Say'e bir şey yaz. VPN'ler ve ping düşürücüler (ExitLag vb.) oyun trafiğini gizleyebilir; onlarsız dene.
- **"Administrator permission was declined".** Start'a tekrar tıkla ve *Evet*'i seç. Windows hesabın yönetici değilse bir yöneticinin onaylaması gerekir.
- **Windows SmartScreen kurulum dosyası için uyarıyor.** Sürümler henüz imzalı değil. Dosya özetini kontrol et (yukarıda), *Daha fazla bilgi → Yine de çalıştır*'ı seç ya da kendin derle.
- **Bir kısayol çalışmıyor.** Başka bir program kullanıyor; uygulama hangisi olduğunu söyler. Düğmeleri kullan.
- **"Google is limiting requests; translating offline".** Google'ın ücretsiz adresinin sınırı düşüktür. Çevrimdışı yedek devralır; bir dil için ilk seferde paketini indirir, bu yüzden ilk birkaç satır çevrilmemiş görünebilir. Ücretsiz bir Microsoft veya DeepL anahtarı bunu önler.

## Lisans

**GNU GPL v3** ([`LICENSE`](../../LICENSE)) altında lisanslıdır: kullanabilir, inceleyebilir, paylaşabilir ve değiştirebilirsin; paylaştığın değiştirilmiş sürümler aynı lisansla açık kaynak kalmalıdır. Orijinal proje: **Tenith**.

Albion Online, Sandbox Interactive GmbH'nin ticari markasıdır. Bu projenin Sandbox Interactive ile bir bağı yoktur ve onun tarafından onaylanmamıştır.
