# Albion Chat Translator

**English** · [Português (BR)](docs/readme/README.pt-BR.md) · [Español](docs/readme/README.es.md) · [Русский](docs/readme/README.ru.md) · [Deutsch](docs/readme/README.de.md) · [Français](docs/readme/README.fr.md) · [Polski](docs/readme/README.pl.md) · [Türkçe](docs/readme/README.tr.md) · [简体中文](docs/readme/README.zh-CN.md) · [한국어](docs/readme/README.ko.md) · [日本語](docs/readme/README.ja.md)

Read Albion Online chat in your own language, and reply in theirs.

Free, no account, no API key, no setup. Keeps translating even when online services are busy. **Open source**: every line of code is public so anyone can check what it does.

---

## What it does

- **Translates what you read.** Messages from other players (Say, Whisper, Party, Guild, Alliance and, if you want, global channels) appear in your language as they arrive. Messages already in your language are left alone.
- **Translates what you say.** Press a hotkey, type in your own language, press Enter, and the translation is ready to paste into Albion.
- **Shows chat over the game.** An optional see-through overlay that your mouse clicks pass straight through.

You can read and write in about 26 languages, and incoming messages are translated from any language Google Translate knows.

## Install

1. Go to [**Releases**](../../releases) and download `AlbionChatTranslator-win-Setup.exe`.
2. Run it. Nothing else is needed (.NET is included). It installs for your Windows user only.
3. Choose the language you read and the server you play on, and tick the languages you see in chat. Their offline packs download in the background (on Americas, English, Spanish and Portuguese are pre-ticked; for Europe and Asia nothing is assumed, you choose).
4. Click **▶ Start** and choose **Yes** on the Windows prompt (see [why](#why-does-it-ask-for-administrator-rights)).
5. Log in to Albion. Your chat tabs appear in the app by themselves.

Updates download and install themselves.

## How to use it

### Reading chat

Just play. Translated messages appear in the app (and in the overlay, if it is on), like this:

```
14:02 Ana: Does anyone sell a cheap t6.1 bag in Lymhurst? (Portuguese)
```

- **Hover** a message to see the original text.
- **Click** the `(Portuguese)` tag to reply to that person in their language.
- Messages in your language, very short messages ("gg", "ok") and item links are shown as they are.
- Chat is sorted into the **same tabs as in the game**, including whispers, party, guild and alliance chat. Channels the game shows in none of your tabs are not shown here either.
- **Whispers you send** appear as `To <name>: …`. If you sent them in another language, you see what they mean in yours; replies made with the reply box show the exact words you typed.

### Replying in their language

1. Press **Ctrl+Shift+R** in game (or click the language tag after a message).
2. Type in **your** language and press **Enter**.
3. The translation is now on your clipboard. In Albion, open chat, press **Ctrl+V**, then Enter.

The reply box is already set to the language of the last person who wrote to you; you can change it.
The app never types into the game or sends anything there. **You** always paste and send.

### Overlay

Press **Ctrl+Shift+O** to show or hide a see-through chat feed on top of the game.
While it is locked, mouse clicks go straight through it to Albion. Press **Ctrl+Shift+L** to unlock it, drag it where you want, resize it from the corner, then press **Ctrl+Shift+L** again to lock it.

Whispers always stay visible in it, both sides, even with *show only translated messages* on. It has the **same tabs as your in-game chat**, in a small bar above it; click a tab to switch (this doesn't take focus from the game), and tabs with new messages show how many. The overlay hides while you're in another program and comes back when you return to Albion. Albion needs to run in windowed or borderless mode for overlays to show.

### Hotkeys

| Keys | What it does |
|---|---|
| **Ctrl+Shift+R** | Reply to whoever wrote last |
| **Ctrl+Shift+O** | Show / hide the overlay |
| **Ctrl+Shift+L** | Unlock / lock the overlay to move it |

They work while Albion has focus. If another program already uses one, the app tells you; the buttons in the app do the same thing.

### Settings worth knowing

| Setting | What it is for |
|---|---|
| **Translate chat into** | Your language. |
| **I can also read** | Other languages you understand. They are never translated or sent anywhere. |
| **When to translate** | *Automatically*, or *only when I click [translate]* on a message. |
| **Never send these channels** | For example whispers or guild chat, if you want them to stay private. |
| **Never translate these words** | Guild names, friends' names, your own slang. |
| **Server and chat languages** | Which offline packs to keep ready. Changing them downloads the new packs in the background. |
| **Translators** | Tried in order: **1.** your own Microsoft Translator or DeepL key (optional), **2.** offline models on your PC (nothing sent), **3.** Google Translate, only for languages without an offline model. Each can be turned on or off. |
| **Remember translations for 7 days** | Fewer messages sent after a restart. Off by default. |
| **Overlay** | Background opacity, and *show only translated messages*. |

## How it works

```
 Albion Online ──(network)──▶  1. Capture helper ──▶  2. App window ──▶  3. Translators
                               reads chat only        decides what       (offline first)
                               needs admin rights     needs translating
                                                             │
                                                             ▼
                                                      4. You read it here
```

1. **Reading chat.** Albion has no add-on system, so the app reads the game's network traffic, the same way established Albion community tools do. A small hidden helper does this and passes only chat messages to the app. It reads; it never changes, sends or injects anything.
2. **Deciding what to translate.** On your PC, the app recognises messages that are already in your languages, too short, only item links, or translated before. **Those never leave your PC.** It also protects Albion words (city names, tiers like `t8.3`, item links, "gank", "zerg"…) so they come back unchanged, and it turns gamer shorthand like `lf healer pls` into plain English first, which gives much better translations. Portuguese (*vc*, *blz*, *vlw*, *kkk*) and Spanish (*xq*, *ntp*, *porfa*, *jaja*) shorthand is spelled out the same way, and also helps recognise which of the two a short line is.
3. **Translating.** What is left, only the message text, goes through a chain of translators, each used only when the one before can't answer:
   1. **Your own key** (optional): Microsoft Translator (2,000,000 characters free) or DeepL (500,000/month free). Official services with published limits.
   2. **Offline models**: OPUS-MT running on your PC. Nothing is sent. Packs for the languages you ticked download once in the background (about 110 MB each, pinned and checksum-verified); a language you didn't tick downloads the first time it appears. They use about 200 MB more memory only while translating.
   3. **Google Translate**: only for languages without an offline model, or while a pack is still downloading. Lines that arrive together share one request; if Google says "slow down", the app steps aside for a while instead of retrying.

   Lines translated offline are marked `(Spanish · offline)` so you know.
4. **Replying.** Your typed text is translated the same way and put on your clipboard.

### Why does it ask for administrator rights?

Windows only lets programs read network traffic with administrator rights. The app keeps them to a minimum:

- The window you use runs as a **normal user**.
- Only a tiny separate helper gets admin rights. All it can do is read Albion's chat packets and pass them to the window through a private, one-way connection that only your Windows user can open.
- The helper has no internet access, reads and writes no files, and accepts no commands.

## Privacy

| Sent to an online translator (your key's service, or Google when there is no offline model) | Never sent anywhere |
|---|---|
| The **text** of messages that are not in your languages | Messages in your languages, short messages, repeats |
| The text you type into the reply box | Player names, channels, guild names, item links |
| | Your account, character, or any game data |
| | Anything from channels you excluded |
| | Anything translated offline |

- Everything goes over HTTPS in the request body (never in a web address), with no cookies, no account and no app identifier. Like any website, Google sees your internet (IP) address.
- The status bar counts every message that left your PC (*Sent for translation*).
- No telemetry, no analytics, no ads, no chat logs. Nothing is written to disk unless you turn on *Remember translations*.
- A DeepL key, if you add one, is encrypted with your Windows account.
- Other connections: github.com (update checks) and huggingface.co (offline packs: the languages you ticked, and any new language the first time it appears).

Google Translate is used through the free endpoint Google's own web widgets use. It needs no key, but it is not an official API and Google limits it sharply per internet connection; busy chat can hit that within minutes. When it does, the app steps aside and the offline backup (or your own key) takes over, so translation does not stop. For the most dependable service, add a free Microsoft Translator or DeepL key.

## Open source: review it yourself

This project is **free and open source** under the [GNU GPL v3](LICENSE). You don't have to take the privacy claims above on trust. You can read the code that makes them true:

| Claim | Where to check it |
|---|---|
| The helper only reads chat and never sends anything to the game | [`RawSocketCapture.cs`](src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Only chat events are parsed | [`ChatEventParser.cs`](src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| The admin helper and the app talk one way, current user only | [`CaptureClient.cs`](src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| What is filtered out before anything is sent | [`TranslationService.cs`](src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Exactly what is sent to Google, Microsoft or DeepL, and how; the order they are tried in | [`Providers.cs`](src/AlbionChatTranslator.Core/Translation/Providers.cs) (`ChainProvider`, `GoogleProvider`, `MicrosoftProvider`) |
| The offline backup downloads only pinned, checksum-verified files and sends nothing | [`OfflineProvider.cs`](src/AlbionChatTranslator.Core/Translation/OfflineProvider.cs), [`ModelStore.cs`](src/AlbionChatTranslator.Core/Translation/ModelStore.cs), [`model-catalog.json`](src/AlbionChatTranslator.Core/Translation/model-catalog.json) |
| Albion words and item links are masked | [`Glossary.cs`](src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| Settings and the encrypted DeepL key | [`AppSettings.cs`](src/AlbionChatTranslator.Core/Settings/AppSettings.cs) |
| These behaviours are tested | [`tests/`](tests/AlbionChatTranslator.Tests) |

**Checking a download.** Installers are built by GitHub itself from the public source, using [this workflow](.github/workflows/release.yml), and anyone can open the build log. Each release also includes `SHA256SUMS.txt`: in PowerShell, `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` must print the same value. Or build it yourself (below).

Found a problem or something that looks wrong? Please [open an issue](../../issues). Pull requests are welcome.

## Is it safe for my account?

The app only **reads** network traffic, like other Albion community tools. It does not modify the game, read its memory, inject code, automate input or send anything to the game servers. It is not made, endorsed or supported by Sandbox Interactive, and no third-party tool can promise how a game company will judge it. **Use it at your own risk.**

## Troubleshooting

- **Nothing shows up.** Make sure the status says *Capturing*, then say something in Say chat. VPNs and ping reducers (ExitLag and similar) can hide game traffic from Windows; try with them off.
- **"Administrator permission was declined."** Click Start again and choose *Yes*. If your Windows account is not an administrator, an administrator has to approve it.
- **Windows SmartScreen warns about the installer.** Releases are not code-signed yet. Check the file hash (see above), choose *More info → Run anyway*, or build it yourself.
- **A hotkey does nothing.** Another program uses it; the app says which. Use the buttons instead.
- **A line shows untranslated with "downloading offline pack".** That language's pack is still downloading (about 110 MB, once). Tick the languages you see in chat in Settings so they are ready before you play.
- **Chat is in the wrong tab, or a tab stays empty.** The game says which tab each chat room belongs to when you log in. If the app was started after that, change character once while it is capturing; it remembers from then on. Channels the game shows in none of your tabs are hidden in the app too.
- **After a big Albion update** chat may stop appearing until the app updates itself.

## For developers

```
src/AlbionChatTranslator.Core     capture, chat parsing, the helper link, translation, settings (no UI)
src/AlbionChatTranslator          WPF app and the --capture entry point
tests/AlbionChatTranslator.Tests  unit tests, plus an opt-in live test against Google
external/StatisticsAnalysis       packet decoding from AlbionOnline-StatisticsAnalysis (see VENDORED.md)
tools/                            refresh the packet code after an Albion patch
```

Build and test (Windows, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)):

```bash
dotnet test --project tests/AlbionChatTranslator.Tests
dotnet publish src/AlbionChatTranslator -c Release -o publish
```

- Live test against Google: set `ACT_LIVE_GOOGLE=1` before `dotnet test`.
- Real offline-model tests (download ~230 MB once): set `ACT_MODEL_DIR` to a folder to keep the models in.
- Offline packs are pinned in `model-catalog.json`; regenerate it with `tools/update_model_catalog.py`.
- Portable / test runs: set `ACT_DATA_DIR` to keep settings in that folder instead of your Windows profile.
- **After an Albion patch** that renumbers network events: run `tools/update_vendored.sh`, then the tests. The app looks events up by name, so refreshing the copied code is enough.
- **Releasing:** push a tag like `v2.0.0`; [`release.yml`](.github/workflows/release.yml) tests, builds and publishes the installer, update feed and checksums.

## Credits and licence

- Original Albion Chat Translator by **Tenith**.
- Network decoding: [AlbionOnline-StatisticsAnalysis](https://github.com/Triky313/AlbionOnline-StatisticsAnalysis) (GPL-3.0).
- Translation: Google Translate, [Microsoft Translator](https://learn.microsoft.com/azure/ai-services/translator/) or [DeepL](https://www.deepl.com) with your own key, and offline [OPUS-MT](https://github.com/Helsinki-NLP/Opus-MT) models (University of Helsinki, CC-BY 4.0; ONNX versions by [Xenova](https://huggingface.co/Xenova)).
- Local language recognition: [Lingua](https://github.com/searchpioneer/lingua-dotnet) (Apache-2.0).
- Offline runtime: [ONNX Runtime](https://onnxruntime.ai) (MIT).
- Installer and updates: [Velopack](https://velopack.io) (MIT).

Licensed under the **GNU GPL v3** ([`LICENSE`](LICENSE)): you may use, study, share and modify it; modified versions you share must stay open source under the same licence.

Albion Online is a trademark of Sandbox Interactive GmbH. This project is not affiliated with or endorsed by Sandbox Interactive.
