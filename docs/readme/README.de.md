# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · [Español](README.es.md) · [Русский](README.ru.md) · **Deutsch** · [Français](README.fr.md) · [Polski](README.pl.md) · [Türkçe](README.tr.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

Lies den Chat von Albion Online in deiner Sprache und antworte in der Sprache der anderen.

Kostenlos, ohne Konto, ohne API-Schlüssel, ohne Einrichtung. **Open Source**: Der gesamte Code ist öffentlich, jeder kann prüfen, was er tut.

---

## Was es macht

- **Übersetzt, was du liest.** Nachrichten anderer Spieler (Say, Whisper, Party, Guild, Alliance und auf Wunsch globale Kanäle) erscheinen sofort in deiner Sprache. Nachrichten, die schon in deiner Sprache sind, bleiben unverändert.
- **Übersetzt, was du schreibst.** Tastenkürzel drücken, in deiner Sprache schreiben, Enter, und die Übersetzung ist bereit zum Einfügen in Albion.
- **Zeigt den Chat über dem Spiel.** Ein optionales durchsichtiges Overlay, durch das Mausklicks direkt ins Spiel gehen.

## Installation

1. Öffne [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases) und lade `AlbionChatTranslator-win-Setup.exe` herunter.
2. Starte die Datei. Mehr braucht es nicht (.NET ist enthalten). Installiert wird nur für deinen Windows-Benutzer.
3. Wähle die Sprache, die du liest, und den Server, auf dem du spielst, und hake die Sprachen an, die du im Chat siehst. Ihre Offline-Pakete laden im Hintergrund herunter (auf Americas sind Englisch, Spanisch und Portugiesisch vorausgewählt; für Europe und Asia wird nichts angenommen, du wählst).
4. Klicke auf **▶ Start** und wähle **Ja** im Windows-Dialog ([warum?](#warum-braucht-es-administratorrechte)).
5. Logge dich in Albion ein. Deine Chat-Tabs erscheinen automatisch in der App.

Updates werden automatisch heruntergeladen und installiert.

## Benutzung

### Chat lesen

Einfach spielen. Übersetzte Nachrichten erscheinen in der App (und im Overlay, falls aktiv):

```
14:02 Ana: Verkauft jemand eine günstige t6.1-Tasche in Lymhurst? (Portuguese)
```

- **Maus darüber halten** zeigt den Originaltext.
- **Klick** auf `(Portuguese)` öffnet eine Antwort in der Sprache dieser Person.
- Nachrichten in deiner Sprache, sehr kurze ("gg", "ok") und Item-Links werden unverändert angezeigt.

### In ihrer Sprache antworten

1. Drücke im Spiel **Strg+Umschalt+R** (oder klicke auf die Sprachmarke einer Nachricht).
2. Schreibe in **deiner** Sprache und drücke **Enter**.
3. Die Übersetzung liegt jetzt in der Zwischenablage. In Albion den Chat öffnen, **Strg+V**, dann Enter.

Das Antwortfenster ist schon auf die Sprache der letzten Person eingestellt, die dir geschrieben hat; du kannst sie ändern.
Die App tippt oder sendet nie etwas ins Spiel. **Du** fügst immer selbst ein und sendest.

### Overlay

**Strg+Umschalt+O** zeigt oder versteckt einen durchsichtigen Chat über dem Spiel. Solange es gesperrt ist, gehen Klicks direkt an Albion. **Strg+Umschalt+L** entsperrt es zum Verschieben und Vergrößern; nochmal drücken zum Sperren.

### Tastenkürzel

| Tasten | Funktion |
|---|---|
| **Strg+Umschalt+R** | Der Person antworten, die zuletzt geschrieben hat |
| **Strg+Umschalt+O** | Overlay zeigen / verstecken |
| **Strg+Umschalt+L** | Overlay zum Verschieben entsperren / sperren |

Sie funktionieren, während Albion im Vordergrund ist. Ist eine Kombination schon von einem anderen Programm belegt, sagt die App Bescheid; die Schaltflächen in der App tun dasselbe.

### Nützliche Einstellungen

| Einstellung | Wofür |
|---|---|
| **Chat übersetzen in** | Deine Sprache. |
| **Ich lese auch** | Weitere Sprachen, die du verstehst. Sie werden nie übersetzt oder verschickt. |
| **Wann übersetzen** | *Automatisch* oder *nur wenn ich auf [translate] klicke*. |
| **Diese Kanäle nie senden** | Zum Beispiel Flüstern oder Gilde, wenn sie privat bleiben sollen. |
| **Diese Wörter nie übersetzen** | Gildennamen, Namen von Freunden, eigener Slang. |
| **Übersetzer** | Der Reihe nach: **1.** dein eigener Microsoft-Translator- oder DeepL-Schlüssel (optional), **2.** Offline-Modelle auf deinem PC (nichts wird gesendet), **3.** Google Übersetzer, nur für Sprachen ohne Offline-Modell. Jeder lässt sich ein- und ausschalten. |
| **Übersetzungen 7 Tage merken** | Weniger Anfragen nach einem Neustart. Standardmäßig aus. |

## So funktioniert es

1. **Chat lesen.** Albion hat kein Add-on-System, deshalb liest die App den Netzwerkverkehr des Spiels, wie bekannte Community-Tools. Ein kleiner versteckter Helfer erledigt das und gibt nur Chatnachrichten an die App weiter. Er liest nur: Er ändert, sendet oder injiziert nichts.
2. **Was übersetzt wird.** Auf deinem PC erkennt die App Nachrichten, die schon in deinen Sprachen sind, zu kurz sind, nur aus Item-Links bestehen oder schon übersetzt wurden. **Diese verlassen deinen PC nie.** Außerdem schützt sie Albion-Begriffe (Städte, Tiers wie `t8.3`, Item-Links, "gank", "zerg"…) und macht aus Slang wie `lf healer pls` zuerst normales Englisch, was die Übersetzung deutlich verbessert.
3. **Übersetzen.** Was übrig bleibt, nur der Nachrichtentext, wird zuerst von den Offline-Modellen auf deinem PC übersetzt (nichts wird gesendet). Google Übersetzer wird nur für Sprachen ohne Offline-Modell genutzt.
4. **Antworten.** Dein Text wird genauso übersetzt und in die Zwischenablage gelegt.

### Warum braucht es Administratorrechte?

Windows erlaubt das Mitlesen von Netzwerkverkehr nur mit Administratorrechten. Die App hält sie so klein wie möglich:

- Das Fenster, das du benutzt, läuft als **normaler Benutzer**.
- Nur ein winziger, getrennter Helfer bekommt die Rechte. Er kann ausschließlich Albions Chat-Pakete lesen und über eine private Einbahn-Verbindung, die nur dein Windows-Benutzer öffnen kann, an das Fenster weitergeben.
- Der Helfer hat keinen Internetzugang, liest und schreibt keine Dateien und nimmt keine Befehle an.

## Datenschutz

| Wird an Google Übersetzer (oder DeepL) gesendet | Wird nie gesendet |
|---|---|
| Der **Text** von Nachrichten, die nicht in deinen Sprachen sind | Nachrichten in deinen Sprachen, kurze Nachrichten, Wiederholungen |
| Der Text, den du im Antwortfenster schreibst | Spielernamen, Kanäle, Gilden, Item-Links |
| | Dein Konto, dein Charakter oder sonstige Spieldaten |
| | Nichts aus Kanälen, die du ausgeschlossen hast |

- Alles geht per HTTPS im Anfrage-Inhalt (nie in der Webadresse), ohne Cookies, ohne Konto und ohne App-Kennung. Wie jede Website sieht Google deine Internetadresse (IP).
- Die Statusleiste zählt jede Nachricht, die deinen PC verlassen hat (*Sent for translation*).
- Keine Telemetrie, keine Analyse, keine Werbung, keine Chatprotokolle. Nichts wird gespeichert, außer du aktivierst *Übersetzungen merken*.
- Ein DeepL-Schlüssel wird, falls du einen einträgst, mit deinem Windows-Konto verschlüsselt.

Der Google Übersetzer wird über die kostenlose Adresse genutzt, die Googles eigene Widgets verwenden. Sie braucht keinen Schlüssel, ist aber keine offizielle API, und Google begrenzt sie pro Verbindung stark; ein belebter Chat kann das Limit in Minuten erreichen. Dann tritt die App zur Seite und die **Offline-Reserve** (ein Modell auf deinem PC, das nichts verschickt; lädt beim ersten Mal etwa 110 MB pro Sprache) oder dein eigener Schlüssel übernimmt, damit die Übersetzung nicht stoppt. Am zuverlässigsten ist ein kostenloser Schlüssel für Microsoft Translator oder DeepL.

## Open Source: Prüf es selbst

Dieses Projekt ist **frei und quelloffen** unter der [GNU GPL v3](../../LICENSE). Du musst den Aussagen oben nicht glauben, sondern kannst den Code lesen, der sie umsetzt.

| Aussage | Wo du es prüfst |
|---|---|
| Der Helfer liest nur den Chat und sendet nie etwas ans Spiel | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Nur Chat-Ereignisse werden ausgewertet | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| Helfer und App sprechen nur in eine Richtung, nur für deinen Benutzer | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| Was vor dem Senden herausgefiltert wird | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Was genau an Google geht und wie | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Albion-Begriffe und Item-Links werden geschützt | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| Dieses Verhalten ist getestet | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**Download prüfen.** Die Installer baut GitHub selbst aus dem öffentlichen Code mit [diesem Workflow](../../.github/workflows/release.yml), und jeder kann das Build-Protokoll öffnen. Jedes Release enthält `SHA256SUMS.txt`: In PowerShell muss `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` denselben Wert ausgeben. Oder bau es selbst (siehe den [englischen Abschnitt für Entwickler](../../README.md#for-developers)).

Ein Problem gefunden? [Erstelle ein Issue](https://github.com/MrYellowHeart/AlbionChatTranslator/issues). Pull Requests sind willkommen.

## Ist das sicher für mein Konto?

Die App **liest** nur Netzwerkverkehr, wie andere Albion-Community-Tools. Sie verändert das Spiel nicht, liest nicht seinen Speicher, injiziert keinen Code, automatisiert keine Eingaben und sendet nichts an die Spielserver. Sie wurde nicht von Sandbox Interactive erstellt, genehmigt oder unterstützt, und kein Drittanbieter-Tool kann versprechen, wie ein Spielehersteller es beurteilt. **Nutzung auf eigenes Risiko.**

## Häufige Probleme

- **Es erscheint nichts.** Prüfe, ob der Status *Capturing* zeigt, und schreib etwas in Say. VPNs und Ping-Reducer (ExitLag und ähnliche) können den Spielverkehr verbergen; probier es ohne.
- **"Administrator permission was declined".** Klicke erneut auf Start und wähle *Ja*. Ist dein Windows-Konto kein Administrator, muss ein Administrator zustimmen.
- **Windows SmartScreen warnt vor dem Installer.** Releases sind noch nicht signiert. Prüfe den Hash (oben), wähle *Weitere Informationen → Trotzdem ausführen* oder bau es selbst.
- **Ein Tastenkürzel tut nichts.** Ein anderes Programm nutzt es; die App sagt welches. Nutze die Schaltflächen.
- **"Google is limiting requests; translating offline".** Googles kostenlose Adresse hat ein niedriges Limit. Die Offline-Reserve übernimmt; beim ersten Mal für eine Sprache lädt sie ihr Paket, daher bleiben die ersten Zeilen eventuell unübersetzt. Ein kostenloser Microsoft- oder DeepL-Schlüssel verhindert das.

## Lizenz

Lizenziert unter der **GNU GPL v3** ([`LICENSE`](../../LICENSE)): Du darfst es nutzen, untersuchen, weitergeben und verändern; veränderte Versionen, die du weitergibst, müssen unter derselben Lizenz quelloffen bleiben. Ursprüngliches Projekt von **Tenith**.

Albion Online ist eine Marke der Sandbox Interactive GmbH. Dieses Projekt steht in keiner Verbindung zu Sandbox Interactive und wird nicht von ihr unterstützt.
