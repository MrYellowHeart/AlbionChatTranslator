# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · [Español](README.es.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Polski](README.pl.md) · [Türkçe](README.tr.md) · [简体中文](README.zh-CN.md) · **한국어** · [日本語](README.ja.md)

Albion Online 채팅을 내 언어로 읽고, 상대방의 언어로 답장하세요.

무료, 계정 불필요, API 키 불필요, 설정 불필요. **오픈 소스**: 모든 코드가 공개되어 있어 누구나 무엇을 하는지 확인할 수 있습니다.

---

## 기능

- **읽는 내용을 번역합니다.** 다른 플레이어의 메시지(Say, Whisper, Party, Guild, Alliance, 원하면 전체 채널)가 도착하는 즉시 내 언어로 표시됩니다. 이미 내 언어인 메시지는 그대로 둡니다.
- **쓰는 내용을 번역합니다.** 단축키를 누르고, 내 언어로 입력하고, Enter를 누르면 Albion에 붙여넣을 번역이 준비됩니다.
- **게임 위에 채팅을 표시합니다.** 선택 사항인 반투명 오버레이로, 마우스 클릭은 그대로 게임에 전달됩니다.

## 설치

1. [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases)에서 `AlbionChatTranslator-win-Setup.exe`를 내려받습니다.
2. 실행합니다. 다른 것은 필요 없습니다(.NET 포함). 현재 Windows 사용자에게만 설치됩니다.
3. 읽는 언어와 플레이하는 서버를 고르고, 채팅에서 보이는 언어를 체크하세요. 해당 오프라인 팩이 백그라운드에서 다운로드됩니다 (Americas 서버는 영어, 스페인어, 포르투갈어가 미리 체크되어 있고, Europe과 Asia는 아무것도 가정하지 않으니 직접 고르세요).
4. **▶ Start**를 누르고 Windows 창에서 **예**를 선택합니다([이유](#왜-관리자-권한이-필요한가요)).
5. Albion에 로그인하면 채팅 탭이 앱에 자동으로 나타납니다.

업데이트는 자동으로 내려받아 설치됩니다.

## 사용 방법

### 채팅 읽기

그냥 플레이하면 됩니다. 번역된 메시지가 앱(오버레이를 켰다면 오버레이에도)에 표시됩니다:

```
14:02 Ana: Lymhurst에서 싼 t6.1 가방 파는 사람 있나요? (Portuguese)
```

- 메시지 위에 **마우스를 올리면** 원문이 보입니다.
- `(Portuguese)` 태그를 **클릭**하면 그 사람의 언어로 답장할 수 있습니다.
- 내 언어로 된 메시지, 아주 짧은 메시지("gg", "ok"), 아이템 링크는 그대로 표시됩니다.

### 상대방 언어로 답장하기

1. 게임 중에 **Ctrl+Shift+R**을 누릅니다(또는 메시지 뒤의 언어 태그를 클릭).
2. **내** 언어로 입력하고 **Enter**를 누릅니다.
3. 번역이 클립보드에 복사됩니다. Albion에서 채팅을 열고 **Ctrl+V**, 그리고 Enter.

답장 창은 마지막으로 메시지를 보낸 사람의 언어로 이미 맞춰져 있으며 바꿀 수 있습니다.
앱은 게임에 아무것도 입력하거나 보내지 않습니다. 붙여넣고 보내는 것은 항상 **나**입니다.

### 오버레이

**Ctrl+Shift+O**로 게임 위의 반투명 채팅을 표시하거나 숨깁니다. 잠겨 있는 동안 클릭은 그대로 Albion에 전달됩니다. **Ctrl+Shift+L**로 잠금을 풀면 끌어서 옮기고 크기를 바꿀 수 있으며, 다시 누르면 잠깁니다.

### 단축키

| 키 | 동작 |
|---|---|
| **Ctrl+Shift+R** | 마지막으로 메시지를 보낸 사람에게 답장 |
| **Ctrl+Shift+O** | 오버레이 표시 / 숨기기 |
| **Ctrl+Shift+L** | 오버레이 이동을 위한 잠금 해제 / 잠금 |

Albion이 활성 창일 때도 작동합니다. 다른 프로그램이 이미 사용 중이면 앱이 알려 주며, 앱의 버튼으로도 같은 일을 할 수 있습니다.

### 알아두면 좋은 설정

| 설정 | 용도 |
|---|---|
| **채팅 번역 언어** | 내 언어. |
| **나도 읽을 수 있는 언어** | 이해할 수 있는 다른 언어. 번역되거나 전송되지 않습니다. |
| **번역 시점** | *자동* 또는 *[translate]를 클릭할 때만*. |
| **이 채널은 절대 전송하지 않기** | 귓속말이나 길드처럼 비공개로 두고 싶은 채널. |
| **이 단어는 번역하지 않기** | 길드 이름, 친구 이름, 나만의 은어. |
| **번역기** | 이 순서로 사용: **1.** 본인의 Microsoft Translator 또는 DeepL 키 (선택), **2.** PC의 오프라인 모델 (아무것도 전송하지 않음), **3.** Google 번역, 오프라인 모델이 없는 언어에만. 각각 켜고 끌 수 있습니다. |
| **번역을 7일간 기억** | 재시작 후 전송이 줄어듭니다. 기본값은 꺼짐. |

## 작동 방식

1. **채팅 읽기.** Albion에는 애드온 시스템이 없어서, 앱은 잘 알려진 커뮤니티 도구들처럼 게임의 네트워크 트래픽을 읽습니다. 작은 숨겨진 도우미가 이 일을 하고 채팅 메시지만 앱에 전달합니다. 읽기만 하며, 아무것도 바꾸거나 보내거나 주입하지 않습니다.
2. **무엇을 번역할지.** 앱은 내 PC에서 이미 내 언어인 메시지, 너무 짧은 메시지, 아이템 링크뿐인 메시지, 이전에 번역한 메시지를 알아봅니다. **이런 메시지는 절대 PC 밖으로 나가지 않습니다.** 또한 Albion 용어(도시, `t8.3` 같은 티어, 아이템 링크, "gank", "zerg"…)를 보호하고, `lf healer pls` 같은 게임 줄임말을 먼저 일반 영어로 바꿔 번역 품질을 크게 높입니다.
3. **번역.** 남은 것, 즉 메시지 텍스트만 먼저 PC의 오프라인 모델이 번역합니다 (아무것도 전송하지 않음). Google 번역은 오프라인 모델이 없는 언어에만 사용됩니다.
4. **답장.** 입력한 텍스트도 같은 방식으로 번역되어 클립보드에 복사됩니다.

### 왜 관리자 권한이 필요한가요?

Windows는 관리자 권한이 있어야 네트워크 트래픽을 읽을 수 있게 합니다. 앱은 이를 최소한으로 사용합니다:

- 사용하는 창은 **일반 사용자** 권한으로 실행됩니다.
- 아주 작은 별도의 도우미만 권한을 받습니다. 이 도우미는 Albion의 채팅 패킷을 읽어 내 Windows 사용자만 열 수 있는 비공개 단방향 연결로 창에 전달하는 일만 할 수 있습니다.
- 도우미는 인터넷에 접속하지 않고, 파일을 읽거나 쓰지 않으며, 명령을 받지 않습니다.

## 개인정보

| Google 번역(또는 DeepL)으로 전송 | 절대 전송하지 않음 |
|---|---|
| 내 언어가 아닌 메시지의 **텍스트** | 내 언어의 메시지, 짧은 메시지, 반복 메시지 |
| 답장 창에 입력한 텍스트 | 플레이어 이름, 채널, 길드 이름, 아이템 링크 |
| | 내 계정, 캐릭터, 그 밖의 게임 데이터 |
| | 제외한 채널의 모든 내용 |

- 모든 내용은 HTTPS로 요청 본문에 담겨 전송되며(웹 주소에는 절대 포함되지 않음), 쿠키·계정·앱 식별자를 쓰지 않습니다. 다른 웹사이트와 마찬가지로 Google은 인터넷(IP) 주소를 볼 수 있습니다.
- 상태 표시줄에서 PC 밖으로 나간 메시지 수를 확인할 수 있습니다(*Sent for translation*).
- 원격 측정, 분석, 광고, 채팅 기록이 없습니다. *번역 기억*을 켜지 않는 한 디스크에 아무것도 저장하지 않습니다.
- DeepL 키를 추가하면 Windows 계정으로 암호화되어 저장됩니다.

Google 번역은 Google 자체 웹 위젯이 사용하는 무료 주소로 사용합니다. 키가 필요 없지만 공식 API가 아니며, Google이 연결마다 엄격하게 제한합니다. 채팅이 활발하면 몇 분 만에 제한에 걸릴 수 있습니다. 그러면 앱이 잠시 물러나고 **오프라인 예비**(내 PC에서 실행되어 아무것도 보내지 않는 모델, 언어별로 처음 필요할 때 약 110MB 다운로드) 또는 내 키가 이어받아 번역이 멈추지 않습니다. 가장 안정적으로 쓰려면 무료 Microsoft Translator나 DeepL 키를 추가하세요.

## 오픈 소스: 직접 확인하세요

이 프로젝트는 [GNU GPL v3](../../LICENSE)에 따른 **자유 오픈 소스**입니다. 위 내용을 그대로 믿을 필요 없이, 이를 구현하는 코드를 직접 읽어볼 수 있습니다.

| 주장 | 확인할 곳 |
|---|---|
| 도우미는 채팅을 읽기만 하고 게임에 아무것도 보내지 않음 | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| 채팅 이벤트만 해석함 | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| 도우미와 앱은 현재 사용자 전용으로 단방향 통신함 | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| 전송 전에 무엇을 걸러내는지 | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Google에 정확히 무엇을 어떻게 보내는지 | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Albion 용어와 아이템 링크를 보호함 | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| 이 동작들은 테스트됨 | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**다운로드 확인하기.** 설치 파일은 공개 소스로부터 [이 워크플로](../../.github/workflows/release.yml)를 통해 GitHub가 직접 빌드하며, 누구나 빌드 로그를 열어볼 수 있습니다. 각 릴리스에는 `SHA256SUMS.txt`가 포함됩니다. PowerShell에서 `Get-FileHash .\AlbionChatTranslator-win-Setup.exe`가 같은 값을 출력해야 합니다. 직접 빌드할 수도 있습니다([영어 개발자 섹션](../../README.md#for-developers) 참고).

문제를 발견했나요? [이슈를 등록](https://github.com/MrYellowHeart/AlbionChatTranslator/issues)해 주세요. 풀 리퀘스트도 환영합니다.

## 내 계정에 안전한가요?

앱은 다른 Albion 커뮤니티 도구들처럼 네트워크 트래픽을 **읽기만** 합니다. 게임을 수정하지 않고, 메모리를 읽지 않고, 코드를 주입하지 않고, 입력을 자동화하지 않으며, 게임 서버에 아무것도 보내지 않습니다. Sandbox Interactive가 만들거나 승인하거나 지원하는 도구가 아니며, 어떤 서드파티 도구도 게임사가 이를 어떻게 판단할지 보장할 수 없습니다. **사용에 따른 책임은 본인에게 있습니다.**

## 자주 있는 문제

- **아무것도 표시되지 않음.** 상태가 *Capturing*인지 확인하고 Say 채널에 아무 말이나 입력해 보세요. VPN이나 핑 감소 프로그램(ExitLag 등)이 게임 트래픽을 가릴 수 있으니 끄고 시도해 보세요.
- **"Administrator permission was declined".** Start를 다시 누르고 *예*를 선택하세요. Windows 계정이 관리자가 아니라면 관리자가 승인해야 합니다.
- **Windows SmartScreen이 설치 파일을 경고함.** 아직 코드 서명이 되어 있지 않습니다. 파일 해시를 확인하고(위 참고) *추가 정보 → 실행*을 선택하거나 직접 빌드하세요.
- **단축키가 작동하지 않음.** 다른 프로그램이 사용 중이며 앱이 어떤 것인지 알려 줍니다. 버튼을 사용하세요.
- **"Google is limiting requests; translating offline".** Google 무료 주소는 제한이 낮습니다. 오프라인 예비가 이어받으며, 언어별로 처음에는 언어 팩을 내려받기 때문에 처음 몇 줄은 번역되지 않을 수 있습니다. 무료 Microsoft 또는 DeepL 키를 추가하면 피할 수 있습니다.

## 라이선스

**GNU GPL v3**([`LICENSE`](../../LICENSE)) 라이선스: 사용, 연구, 공유, 수정할 수 있으며, 공유하는 수정본은 같은 라이선스로 오픈 소스를 유지해야 합니다. 원작자: **Tenith**.

Albion Online은 Sandbox Interactive GmbH의 상표입니다. 이 프로젝트는 Sandbox Interactive와 관련이 없으며 승인을 받지 않았습니다.
