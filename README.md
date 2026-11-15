# Albion Chat Translator

Live translation of Albion Online chat, **on your own PC**. Free, no account, no API key, and your chat never leaves your computer.

![Albion Chat Translator](screenshot.png)

## Install

1. Download **`AlbionChatTranslator-win-Setup.exe`** from [Releases](../../releases) and run it. Nothing else to install: .NET is included.
2. Pick the language you read.
3. Click **▶ Start** and approve the Windows prompt.
4. Play. Your in-game chat tabs appear by themselves after you log in.

The first time a new language shows up in chat, its language pack (about 110 MB) downloads in the background. After that, translation works offline. Updates install themselves.

## Features

- **Offline translation** of Say, Whisper, Party, Guild, Alliance and (optionally) global channels, using open [OPUS-MT](https://github.com/Helsinki-NLP/Opus-MT) models running locally.
- **Reply in their language**: click the `(Spanish)` tag after a message, type in your language, copy the translation into Albion.
- **Albion-aware**: city names and abbreviations (BW, FS, Thet, Brec…), tiers (`t8.3`, `@2`), item links and gamer words (gank, zerg, dps…) are never translated.
- **Smart about chat**: English chat shorthand (`lf healer`, `wts`, `gg wp`) and your own language are recognised and left alone; short lines like "ok" are skipped; repeated spam is translated once and cached.
- **Light**: about 170 MB of RAM idle, about 300 ms per translated line on a normal CPU.
- Optional **DeepL** mode with your own key, if you want the best slang handling and don't mind sending chat to DeepL.

### Languages

| Chat in these languages is translated | You can read/reply in these languages |
|---|---|
| Arabic, Chinese, Czech, Danish, Dutch, English, Estonian, Finnish, French, German, Hindi, Hungarian, Indonesian, Italian, Japanese, Korean, Polish, Portuguese, Romanian, Russian, Spanish, Swedish, Thai, Turkish, Ukrainian, Vietnamese | Arabic, Chinese, Czech, Danish, Dutch, English, Finnish, French, German, Hindi, Hungarian, Indonesian, Italian, Portuguese, Romanian, Russian, Spanish, Swedish, Ukrainian, Vietnamese |

Japanese, Korean, Polish, Thai and Turkish can be *read*, but there is no offline model yet for *writing* in them (use DeepL mode for that).

## Privacy and security

- **Your chat stays on your PC** with the default Offline translator. The app only connects to `huggingface.co` (to download language packs) and `github.com` (to check for updates).
- **Downloads are verified.** Every language-pack file is pinned to an exact commit and checked against a SHA-256 built into the app; anything that doesn't match is deleted. Updates are verified by Velopack the same way.
- **Admin rights are kept to a tiny helper.** The window you see runs as a normal user. Reading game traffic needs administrator rights, so a separate hidden helper (the same exe with `--capture`) gets them, and all it does is read Albion's chat packets and pass them to the window over a private, one-way, current-user-only pipe. It has no network access, no files, no settings and accepts no commands.
- **Read-only.** It never modifies, sends or injects game traffic, and never touches game files.
- **Keys are encrypted.** If you use DeepL, the key is stored with Windows DPAPI (only your Windows account on this PC can read it). Older versions stored it in plain text; it is converted automatically.
- **No telemetry, no analytics, no chat logs on disk.**

## Troubleshooting

- **Nothing shows up**: make sure the status says *Capturing*, then send a message in Say chat. VPNs and ping reducers (ExitLag etc.) can hide game traffic from Windows; try with them off.
- **"Administrator permission was declined"**: click Start again and choose *Yes*. If your Windows account isn't an administrator, an admin account has to approve it; signing in as a *different* user for the prompt is not supported.
- **Windows SmartScreen warning**: release builds are not code-signed yet. Check the file against `SHA256SUMS.txt` in the release, or build it yourself (below).
- **After a big Albion patch** chat may stop appearing until the app updates. Updates arrive automatically.

## How it works

```
Albion  ──UDP──▶  capture helper (admin)  ──pipe──▶  app (normal user)  ──▶  your screen
                  raw socket, Photon 18      one-way      language detection (Lingua)
                  parser, chat events only   JSON lines   OPUS-MT via ONNX Runtime, cache
```

| Folder | What's inside |
|---|---|
| `src/AlbionChatTranslator.Core` | Capture, chat parsing, the pipe link, translation, settings. No UI. |
| `src/AlbionChatTranslator` | WPF app and the `--capture` entry point. |
| `tests/AlbionChatTranslator.Tests` | Unit tests, plus opt-in tests against real models. |
| `external/StatisticsAnalysis` | Packet decoding copied from AlbionOnline-StatisticsAnalysis ([details](external/StatisticsAnalysis/VENDORED.md)). |
| `tools/` | Scripts to refresh the copied packet code and the pinned model list. |

## Building from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.

```bash
dotnet test --project tests/AlbionChatTranslator.Tests
dotnet publish src/AlbionChatTranslator -c Release -o publish
```

To also run the end-to-end tests against real models (downloads about 230 MB once):

```bash
ACT_MODEL_DIR=C:/temp/act-models dotnet test --project tests/AlbionChatTranslator.Tests
```

Releases are built by [`.github/workflows/release.yml`](.github/workflows/release.yml) when a `v*` tag is pushed.

### After an Albion patch

SBI sometimes renumbers network events. AlbionOnline-StatisticsAnalysis tracks those changes, so:

```bash
tools/update_vendored.sh
dotnet test --project tests/AlbionChatTranslator.Tests
```

The translator looks events up by name, so refreshing the copied code is all that's needed.

### Adding or updating language packs

Edit the language lists in `tools/update_model_catalog.py`, run it to re-pin every pack (commit + SHA-256), and add the language to `Translation/Languages.cs` and the Lingua list in `AlbionChatTranslator.Core.csproj`.

## Credits and licences

- Original Albion Chat Translator by **Tenith**.
- Packet capture and decoding: [AlbionOnline-StatisticsAnalysis](https://github.com/Triky313/AlbionOnline-StatisticsAnalysis) (GPL-3.0).
- Translation models: [OPUS-MT](https://github.com/Helsinki-NLP/Opus-MT) by the University of Helsinki (CC-BY 4.0), ONNX versions by [Xenova](https://huggingface.co/Xenova).
- Language detection: [Lingua](https://github.com/searchpioneer/lingua-dotnet) (Apache-2.0).
- Runtime: [ONNX Runtime](https://onnxruntime.ai) (MIT), [Velopack](https://velopack.io) (MIT).

This project is licensed under the **GNU GPL v3** (see `LICENSE`), as required by the StatisticsAnalysis code it includes.

Albion Online is a trademark of Sandbox Interactive GmbH. This project is not affiliated with or endorsed by Sandbox Interactive.
