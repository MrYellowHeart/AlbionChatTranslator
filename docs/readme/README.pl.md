# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · [Español](README.es.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · **Polski** · [Türkçe](README.tr.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

Czytaj czat Albion Online w swoim języku i odpowiadaj w języku rozmówcy.

Za darmo, bez konta, bez klucza API, bez konfiguracji. **Otwarte źródło**: cały kod jest publiczny, każdy może sprawdzić, co robi.

---

## Co robi

- **Tłumaczy to, co czytasz.** Wiadomości innych graczy (Say, Whisper, Party, Guild, Alliance i, jeśli chcesz, kanały globalne) pojawiają się w Twoim języku od razu. Wiadomości już w Twoim języku zostają bez zmian.
- **Tłumaczy to, co piszesz.** Skrót klawiszowy, piszesz po swojemu, Enter, i tłumaczenie jest gotowe do wklejenia w Albion.
- **Pokazuje czat nad grą.** Opcjonalna przezroczysta nakładka, przez którą kliknięcia myszy trafiają prosto do gry.

## Instalacja

1. Otwórz [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases) i pobierz `AlbionChatTranslator-win-Setup.exe`.
2. Uruchom. Nic więcej nie trzeba (.NET jest w zestawie). Instaluje się tylko dla Twojego użytkownika Windows.
3. Wybierz język, w którym czytasz, i serwer, na którym grasz, oraz zaznacz języki, które widzisz na czacie. Ich pakiety offline pobiorą się w tle (na Americas angielski, hiszpański i portugalski są zaznaczone z góry; dla Europe i Asia nic nie jest zakładane, wybierasz ty).
4. Kliknij **▶ Start** i wybierz **Tak** w oknie Windows ([dlaczego?](#dlaczego-potrzebne-są-uprawnienia-administratora)).
5. Zaloguj się do Albion. Twoje karty czatu pojawią się w aplikacji same.

Aktualizacje pobierają się i instalują automatycznie.

## Jak używać

### Czytanie czatu

Po prostu graj. Przetłumaczone wiadomości pojawiają się w aplikacji (i w nakładce, jeśli jest włączona):

```
14:02 Ana: Ktoś sprzedaje tanią torbę t6.1 w Lymhurst? (Portuguese)
```

- **Najedź kursorem** na wiadomość, by zobaczyć oryginał.
- **Kliknij** etykietę `(Portuguese)`, aby odpowiedzieć tej osobie w jej języku.
- Wiadomości w Twoim języku, bardzo krótkie ("gg", "ok") i linki do przedmiotów są pokazywane bez zmian.

### Odpowiadanie w ich języku

1. Naciśnij **Ctrl+Shift+R** w grze (albo kliknij etykietę języka przy wiadomości).
2. Napisz w **swoim** języku i naciśnij **Enter**.
3. Tłumaczenie jest w schowku. W Albion otwórz czat, naciśnij **Ctrl+V**, potem Enter.

Okno odpowiedzi jest już ustawione na język osoby, która ostatnio do Ciebie pisała; możesz go zmienić.
Aplikacja nigdy nic nie wpisuje ani nie wysyła do gry. Wklejasz i wysyłasz zawsze **Ty**.

### Nakładka

**Ctrl+Shift+O** pokazuje lub ukrywa przezroczysty czat nad grą. Gdy jest zablokowana, kliknięcia trafiają prosto do Albion. **Ctrl+Shift+L** odblokowuje ją, by ją przesunąć i zmienić rozmiar; naciśnij ponownie, by zablokować.

### Skróty

| Klawisze | Działanie |
|---|---|
| **Ctrl+Shift+R** | Odpowiedz osobie, która pisała ostatnia |
| **Ctrl+Shift+O** | Pokaż / ukryj nakładkę |
| **Ctrl+Shift+L** | Odblokuj / zablokuj nakładkę, by ją przesunąć |

Działają, gdy Albion jest aktywny. Jeśli inny program już używa skrótu, aplikacja to zgłosi; przyciski w aplikacji robią to samo.

### Przydatne ustawienia

| Ustawienie | Do czego służy |
|---|---|
| **Tłumacz czat na** | Twój język. |
| **Czytam też** | Inne języki, które rozumiesz. Nigdy nie są tłumaczone ani wysyłane. |
| **Kiedy tłumaczyć** | *Automatycznie* albo *tylko gdy kliknę [translate]*. |
| **Nigdy nie wysyłaj tych kanałów** | Np. szepty lub gildię, jeśli mają zostać prywatne. |
| **Nigdy nie tłumacz tych słów** | Nazwy gildii, imiona znajomych, własny slang. |
| **Tłumacze** | Używane w tej kolejności: **1.** twój własny klucz Microsoft Translator lub DeepL (opcjonalnie), **2.** modele offline na twoim PC (nic nie jest wysyłane), **3.** Tłumacz Google, tylko dla języków bez modelu offline. Każdy można włączyć lub wyłączyć. |
| **Zapamiętuj tłumaczenia przez 7 dni** | Mniej wysyłek po ponownym uruchomieniu. Domyślnie wyłączone. |

## Jak to działa

1. **Czytanie czatu.** Albion nie ma systemu dodatków, więc aplikacja czyta ruch sieciowy gry, tak jak znane narzędzia społeczności. Robi to mały ukryty pomocnik, który przekazuje aplikacji wyłącznie wiadomości czatu. Tylko czyta: niczego nie zmienia, nie wysyła ani nie wstrzykuje.
2. **Co tłumaczyć.** Na Twoim komputerze aplikacja rozpoznaje wiadomości już w Twoich językach, zbyt krótkie, same linki do przedmiotów lub wcześniej przetłumaczone. **Te nigdy nie opuszczają Twojego komputera.** Chroni też słowa z Albion (miasta, tiery jak `t8.3`, linki do przedmiotów, "gank", "zerg"…) i najpierw zamienia slang typu `lf healer pls` na zwykły angielski, co bardzo poprawia tłumaczenie.
3. **Tłumaczenie.** To, co zostaje, czyli sam tekst wiadomości, najpierw tłumaczą modele offline na twoim PC (nic nie jest wysyłane). Tłumacz Google jest używany tylko dla języków bez modelu offline.
4. **Odpowiedź.** Twój tekst jest tłumaczony tak samo i trafia do schowka.

### Dlaczego potrzebne są uprawnienia administratora?

Windows pozwala czytać ruch sieciowy tylko z uprawnieniami administratora. Aplikacja ogranicza je do minimum:

- Okno, którego używasz, działa jako **zwykły użytkownik**.
- Uprawnienia dostaje tylko malutki, osobny pomocnik. Potrafi jedynie czytać pakiety czatu Albion i przekazywać je do okna prywatnym, jednokierunkowym połączeniem, które może otworzyć tylko Twój użytkownik Windows.
- Pomocnik nie ma dostępu do internetu, nie czyta ani nie zapisuje plików i nie przyjmuje poleceń.

## Prywatność

| Wysyłane do Google Tłumacza (lub DeepL) | Nigdy nie wysyłane |
|---|---|
| **Tekst** wiadomości, które nie są w Twoich językach | Wiadomości w Twoich językach, krótkie, powtórzenia |
| Tekst, który wpisujesz w oknie odpowiedzi | Nicki graczy, kanały, gildie, linki do przedmiotów |
| | Twoje konto, postać ani żadne dane z gry |
| | Nic z kanałów, które wykluczyłeś |

- Wszystko idzie przez HTTPS w treści zapytania (nigdy w adresie), bez ciasteczek, bez konta i bez identyfikatora aplikacji. Jak każda strona, Google widzi Twój adres internetowy (IP).
- Pasek stanu liczy każdą wiadomość, która opuściła komputer (*Sent for translation*).
- Bez telemetrii, analityki, reklam i logów czatu. Nic nie jest zapisywane na dysku, chyba że włączysz *Zapamiętuj tłumaczenia*.
- Klucz DeepL, jeśli go dodasz, jest szyfrowany Twoim kontem Windows.

Google Tłumacz działa przez darmowy adres, którego używają widżety samego Google. Nie wymaga klucza, ale nie jest oficjalnym API i Google mocno go ogranicza dla każdego połączenia; ruchliwy czat może wyczerpać limit w kilka minut. Wtedy aplikacja ustępuje i zastępuje go **zapas offline** (model działający na Twoim komputerze, nic nie wysyła; przy pierwszym użyciu języka pobiera ok. 110 MB) albo Twój własny klucz, więc tłumaczenie się nie zatrzymuje. Najbardziej niezawodny jest darmowy klucz Microsoft Translator lub DeepL.

## Otwarte źródło: sprawdź sam

Projekt jest **wolny i otwartoźródłowy** na licencji [GNU GPL v3](../../LICENSE). Nie musisz wierzyć na słowo: możesz przeczytać kod, który za tym stoi.

| Twierdzenie | Gdzie sprawdzić |
|---|---|
| Pomocnik tylko czyta czat i nic nie wysyła do gry | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Analizowane są tylko zdarzenia czatu | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| Pomocnik i aplikacja rozmawiają jednokierunkowo, tylko dla Twojego użytkownika | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| Co jest odfiltrowywane przed wysłaniem | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Co dokładnie i jak trafia do Google | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Słowa z Albion i linki do przedmiotów są chronione | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| To zachowanie jest przetestowane | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**Sprawdzanie pobranego pliku.** Instalatory buduje sam GitHub z publicznego kodu przy użyciu [tego workflow](../../.github/workflows/release.yml), a każdy może otworzyć log budowania. Każde wydanie zawiera `SHA256SUMS.txt`: w PowerShell `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` musi pokazać tę samą wartość. Albo zbuduj sam (zobacz [sekcję dla programistów po angielsku](../../README.md#for-developers)).

Znalazłeś problem? [Zgłoś issue](https://github.com/MrYellowHeart/AlbionChatTranslator/issues). Pull requesty są mile widziane.

## Czy to bezpieczne dla konta?

Aplikacja tylko **czyta** ruch sieciowy, jak inne narzędzia społeczności Albion. Nie modyfikuje gry, nie czyta jej pamięci, nie wstrzykuje kodu, nie automatyzuje akcji i nic nie wysyła na serwery gry. Nie jest stworzona, zatwierdzona ani wspierana przez Sandbox Interactive, a żadne zewnętrzne narzędzie nie może obiecać, jak oceni je firma od gry. **Używasz na własne ryzyko.**

## Częste problemy

- **Nic się nie pojawia.** Sprawdź, czy status to *Capturing*, i napisz coś na Say. VPN-y i reduktory pingu (ExitLag i podobne) mogą ukrywać ruch gry; spróbuj bez nich.
- **"Administrator permission was declined".** Kliknij Start jeszcze raz i wybierz *Tak*. Jeśli Twoje konto Windows nie jest administratorem, musi to zatwierdzić administrator.
- **Windows SmartScreen ostrzega przed instalatorem.** Wydania nie są jeszcze podpisane. Sprawdź skrót pliku (wyżej), wybierz *Więcej informacji → Uruchom mimo to* albo zbuduj sam.
- **Skrót nic nie robi.** Używa go inny program; aplikacja powie który. Użyj przycisków.
- **"Google is limiting requests; translating offline".** Darmowy adres Google ma niski limit. Zastępuje go zapas offline; przy pierwszym użyciu języka pobiera paczkę, więc pierwsze linijki mogą pozostać nieprzetłumaczone. Darmowy klucz Microsoft lub DeepL temu zapobiega.

## Licencja

Na licencji **GNU GPL v3** ([`LICENSE`](../../LICENSE)): możesz używać, badać, udostępniać i modyfikować; zmodyfikowane wersje, które udostępniasz, muszą pozostać otwarte na tej samej licencji. Oryginalny projekt: **Tenith**.

Albion Online jest znakiem towarowym Sandbox Interactive GmbH. Ten projekt nie jest powiązany z Sandbox Interactive ani przez nią zatwierdzony.
