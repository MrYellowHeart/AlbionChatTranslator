# Albion Chat Translator

Live translation of Albion Online chat. Free, no account, no API key, no setup, with privacy built in.

![Albion Chat Translator](screenshot.png)

## Install

1. Download **`AlbionChatTranslator-win-Setup.exe`** from [Releases](../../releases) and run it. Nothing else to install: .NET is included.
2. Pick the language you read.
3. Click **▶ Start** and approve the Windows prompt.
4. Play. Your in-game chat tabs appear by themselves after you log in.

Updates install themselves.

## Features

- **Google Translate quality** for Say, Whisper, Party, Guild, Alliance and (optionally) global channels, in every language Google supports.
- **Reply in their language**: click the `(Spanish)` tag after a message, type in your language, copy the translation into Albion.
- **Albion-aware**: city names and abbreviations (BW, FS, Thet, Brec…), tiers (`t8.3`, `@2`), item links and gamer words (gank, zerg, dps…) are never translated.
- **Smart about chat**: English chat shorthand (`lf healer`, `wts`, `gg wp`) and your own language are recognised and left alone; short lines like "ok" are skipped; repeated spam is translated once and cached.
- **Light**: about 150 MB of RAM, nothing to download after install, translations in about 0.3–1 s.
- Optional **DeepL** mode with your own free key.

### Languages

You can read chat in, and reply into, Arabic, Chinese, Czech, Danish, Dutch, English, Estonian, Finnish, French, German, Hindi, Hungarian, Indonesian, Italian, Japanese, Korean, Polish, Portuguese, Romanian, Russian, Spanish, Swedish, Thai, Turkish, Ukrainian and Vietnamese. Incoming messages in any language Google recognises are translated.

## Privacy and security

Translation is done by Google Translate (or DeepL, if you choose it). The app is built to send as little as possible:

- **Only the message text** of lines that are *not* in your language is sent. Never player names, channels, guilds, item links, your account, or anything else about you.
- **Most chat never leaves your PC.** Lines in your own language are recognised locally and not sent; neither are short lines ("gg", "ok"), item links, numbers or repeats (answered from a cache). The status bar shows exactly how many lines were sent.
- **Albion terms are masked.** City names, abbreviations, tiers and item links are replaced with placeholders before sending and put back afterwards.
- **No tracking.** HTTPS POST (text never appears in a URL), no cookies, no account, a generic browser user agent, no telemetry or analytics, no chat logs on disk.
- **If Google rate-limits**, the app backs off on its own (30 s up to 10 min) instead of retrying in a loop.
- **Admin rights are kept to a tiny helper.** The window runs as a normal user. Reading game traffic needs administrator rights, so a separate hidden helper (the same exe with `--capture`) gets them, and all it does is read Albion's chat packets and pass them to the window over a private, one-way, current-user-only pipe. It has no network access, no files, no settings and accepts no commands.
- **Read-only.** It never modifies, sends or injects game traffic, and never touches game files.
- **Keys are encrypted.** A DeepL key is stored with Windows DPAPI (only your Windows account on this PC can read it).

Google's endpoint is the free one Google's own web widgets use. It needs no key, but it isn't an official API, so Google could limit or change it; DeepL with your own key is the fallback.

## Troubleshooting

- **Nothing shows up**: make sure the status says *Capturing*, then send a message in Say chat. VPNs and ping reducers (ExitLag etc.) can hide game traffic from Windows; try with them off.
- **"Administrator permission was declined"**: click Start again and choose *Yes*. If your Windows account isn't an administrator, an admin account has to approve it; signing in as a *different* user for the prompt is not supported.
- **Windows SmartScreen warning**: release builds are not code-signed yet. Check the file against `SHA256SUMS.txt` in the release, or build it yourself (below).
- **After a big Albion patch** chat may stop appearing until the app updates. Updates arrive automatically.

## How it works

```
Albion  ──UDP──▶  capture helper (admin)  ──pipe──▶  app (normal user)  ──HTTPS──▶  Google Translate
                  raw socket, Photon 18      one-way      local language check,         (foreign-language
                  parser, chat events only   JSON lines   term masking, cache            text only)
```

| Folder | What's inside |
|---|---|
| `src/AlbionChatTranslator.Core` | Capture, chat parsing, the pipe link, translation, settings. No UI. |
| `src/AlbionChatTranslator` | WPF app and the `--capture` entry point. |
| `tests/AlbionChatTranslator.Tests` | Unit tests, plus an opt-in live test against Google. |
| `external/StatisticsAnalysis` | Packet decoding copied from AlbionOnline-StatisticsAnalysis ([details](external/StatisticsAnalysis/VENDORED.md)). |
| `tools/` | Script to refresh the copied packet code after an Albion patch. |

## Building from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.

```bash
dotnet test --project tests/AlbionChatTranslator.Tests
dotnet publish src/AlbionChatTranslator -c Release -o publish
```

To also run the live test, which sends a few sample game lines to Google:

```bash
ACT_LIVE_GOOGLE=1 dotnet test --project tests/AlbionChatTranslator.Tests
```

Releases are built by [`.github/workflows/release.yml`](.github/workflows/release.yml) when a `v*` tag is pushed.

### After an Albion patch

SBI sometimes renumbers network events. AlbionOnline-StatisticsAnalysis tracks those changes, so:

```bash
tools/update_vendored.sh
dotnet test --project tests/AlbionChatTranslator.Tests
```

The translator looks events up by name, so refreshing the copied code is all that's needed.

## Credits and licences

- Original Albion Chat Translator by **Tenith**.
- Packet capture and decoding: [AlbionOnline-StatisticsAnalysis](https://github.com/Triky313/AlbionOnline-StatisticsAnalysis) (GPL-3.0).
- Translation: Google Translate, or [DeepL](https://www.deepl.com) with your own key.
- Local language check: [Lingua](https://github.com/searchpioneer/lingua-dotnet) (Apache-2.0).
- Installer and updates: [Velopack](https://velopack.io) (MIT).

This project is licensed under the **GNU GPL v3** (see `LICENSE`), as required by the StatisticsAnalysis code it includes.

Albion Online is a trademark of Sandbox Interactive GmbH. This project is not affiliated with or endorsed by Sandbox Interactive.
