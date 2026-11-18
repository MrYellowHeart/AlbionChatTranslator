# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · [Español](README.es.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Polski](README.pl.md) · [Türkçe](README.tr.md) · **简体中文** · [한국어](README.ko.md) · [日本語](README.ja.md)

用你的语言阅读 Albion Online 的聊天，再用对方的语言回复。

免费、无需账号、无需 API 密钥、无需设置。**开源**：所有代码公开，任何人都可以检查它做了什么。

---

## 功能

- **翻译你看到的内容。** 其他玩家的消息（Say、Whisper、Party、Guild、Alliance，以及可选的全局频道）一到就显示为你的语言。已经是你语言的消息保持原样。
- **翻译你要说的话。** 按快捷键，用你的语言输入，按回车，译文即可粘贴到 Albion 中。
- **在游戏上方显示聊天。** 可选的半透明悬浮层，鼠标点击会直接穿透到游戏。

## 安装

1. 打开 [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases)，下载 `AlbionChatTranslator-win-Setup.exe`。
2. 运行它。不需要安装其他东西（已内置 .NET）。只为你当前的 Windows 用户安装。
3. 选择你阅读的语言和你所在的服务器，并勾选你在聊天中看到的语言。它们的离线包会在后台下载（Americas 服务器预先勾选英语、西班牙语和葡萄牙语；Europe 和 Asia 不做任何假设，由你选择）。
4. 点击 **▶ Start**，在 Windows 弹窗中选择 **是**（[为什么？](#为什么需要管理员权限)）。
5. 登录 Albion。你的聊天标签页会自动出现在程序中。

更新会自动下载并安装。

## 使用方法

### 阅读聊天

正常玩游戏即可。翻译后的消息会显示在程序中（如果开启了悬浮层，也会显示在悬浮层中）：

```
14:02 Ana: 有人在 Lymhurst 卖便宜的 t6.1 背包吗？ (Portuguese)
```

- 鼠标**悬停**在消息上可查看原文。
- **点击** `(Portuguese)` 标签，可以用对方的语言回复。
- 你的语言的消息、很短的消息（"gg"、"ok"）和物品链接会原样显示。

### 用对方的语言回复

1. 在游戏中按 **Ctrl+Shift+R**（或点击消息后的语言标签）。
2. 用**你自己的**语言输入，然后按 **回车**。
3. 译文已复制到剪贴板。在 Albion 中打开聊天，按 **Ctrl+V**，再按回车。

回复框已经设置为最后给你发消息的人的语言，你也可以更改。
本程序从不向游戏中输入或发送任何内容。粘贴和发送始终由**你自己**完成。

### 悬浮层

> **默认关闭，使用风险自负。** Albion Online 客服表示，第三方工具不得在游戏客户端上显示覆盖层。这个覆盖层是一个独立窗口，不会修改游戏，但客服没有说明它是否算在内。可在 **Settings → Overlay** 中开启。

**Ctrl+Shift+O** 显示或隐藏游戏上方的半透明聊天。锁定时，鼠标点击会直接穿透到 Albion。**Ctrl+Shift+L** 解锁后可拖动和调整大小，再按一次即可锁定。

### 快捷键

| 按键 | 作用 |
|---|---|
| **Ctrl+Shift+R** | 回复最后发消息的人 |
| **Ctrl+Shift+O** | 显示 / 隐藏悬浮层 |
| **Ctrl+Shift+L** | 解锁 / 锁定悬浮层以便移动 |

Albion 处于前台时也有效。如果某个快捷键已被其他程序占用，程序会提示你；程序里的按钮功能相同。

### 常用设置

| 设置 | 用途 |
|---|---|
| **把聊天翻译成** | 你的语言。 |
| **我也能读懂** | 你能看懂的其他语言。这些语言永远不会被翻译或发送。 |
| **何时翻译** | *自动*，或*仅在我点击 [translate] 时*。 |
| **永不发送这些频道** | 例如私聊或公会，如果你希望它们保持私密。 |
| **永不翻译这些词** | 公会名、好友名、你自己的俚语。 |
| **翻译器** | 按顺序使用：**1.** 你自己的 Microsoft Translator 或 DeepL 密钥（可选），**2.** 你电脑上的离线模型（不发送任何内容），**3.** Google 翻译，仅用于没有离线模型的语言。每一项都可以开关。 |
| **记住翻译 7 天** | 重启后发送更少。默认关闭。 |

## 工作原理

1. **读取聊天。** Albion 没有插件系统，所以本程序像社区里常用的工具一样读取游戏的网络流量。一个小型的隐藏助手负责这件事，只把聊天消息交给程序。它只读取：从不修改、发送或注入任何内容。
2. **决定翻译什么。** 程序在你的电脑上识别已经是你的语言、过短、只有物品链接或之前翻译过的消息。**这些消息永远不会离开你的电脑。** 它还会保护 Albion 词汇（城市、`t8.3` 这样的等级、物品链接、"gank"、"zerg"…），并先把 `lf healer pls` 这类游戏缩写转换成普通英语，大幅提升翻译质量。
3. **翻译。** 剩下的内容（只有消息文本）先由你电脑上的离线模型翻译（不发送任何内容）。只有没有离线模型的语言才会使用 Google 翻译。
4. **回复。** 你输入的文字以同样方式翻译并放入剪贴板。

### 为什么需要管理员权限？

Windows 只允许拥有管理员权限的程序读取网络流量。本程序把权限降到最低：

- 你使用的窗口以**普通用户**身份运行。
- 只有一个独立的极小助手获得该权限。它只能读取 Albion 的聊天数据包，并通过一个私有的、单向的、只有你的 Windows 用户能打开的连接传给窗口。
- 该助手没有网络访问权限，不读写任何文件，也不接受任何命令。

## 隐私

| 发送给 Google 翻译（或 DeepL） | 永不发送 |
|---|---|
| 不是你的语言的消息的**文本** | 你的语言的消息、短消息、重复消息 |
| 你在回复框中输入的文字 | 玩家名、频道、公会名、物品链接 |
| | 你的账号、角色或任何游戏数据 |
| | 你排除的频道中的任何内容 |

- 所有内容都通过 HTTPS 放在请求正文中发送（从不放在网址里），不使用 Cookie、不需要账号、没有程序标识。和访问任何网站一样，Google 能看到你的网络（IP）地址。
- 状态栏会统计每一条离开你电脑的消息（*Sent for translation*）。
- 没有遥测、没有统计分析、没有广告、没有聊天记录。除非你开启*记住翻译*，否则不会向磁盘写入任何内容。
- 如果你添加了 DeepL 密钥，它会用你的 Windows 账户加密保存。

Google 翻译通过 Google 自己的网页小工具所用的免费接口调用。不需要密钥，但它不是官方 API，Google 会按网络连接严格限制；聊天活跃时几分钟内就可能触发限制。此时程序会暂时避开，由**离线备用**（在你电脑上运行的模型，不发送任何内容；某种语言首次需要时下载约 110 MB）或你自己的密钥接替，翻译不会中断。想要最可靠的服务，请添加免费的 Microsoft Translator 或 DeepL 密钥。

## 开源：亲自检查

本项目基于 [GNU GPL v3](../../LICENSE) **自由开源**。你不必轻信上面的说法，可以直接阅读实现这些承诺的代码：

| 说法 | 在哪里检查 |
|---|---|
| 助手只读取聊天，从不向游戏发送任何内容 | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs)、[`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| 只解析聊天事件 | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| 助手与程序单向通信，且仅限当前用户 | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs)、[`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| 发送前过滤掉了什么 | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs)、[`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| 具体向 Google 发送了什么、如何发送 | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs)（`GoogleProvider`） |
| Albion 词汇和物品链接受到保护 | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| 这些行为都有测试 | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**检查下载文件。** 安装程序由 GitHub 使用[这个工作流](../../.github/workflows/release.yml)从公开源代码构建，任何人都可以查看构建日志。每个版本都附带 `SHA256SUMS.txt`：在 PowerShell 中，`Get-FileHash .\AlbionChatTranslator-win-Setup.exe` 应显示相同的值。也可以自己构建（见[英文开发者部分](../../README.md#for-developers)）。

发现问题？请[提交 issue](https://github.com/MrYellowHeart/AlbionChatTranslator/issues)。欢迎提交 pull request。

## 对我的账号安全吗？

本程序和其他 Albion 社区工具一样，只**读取**网络流量。它不修改游戏、不读取游戏内存、不注入代码、不自动操作，也不向游戏服务器发送任何内容。它不是由 Sandbox Interactive 制作、认可或支持的，任何第三方工具都无法保证游戏公司会如何看待它。**使用风险自负。**

Albion Online 客服告诉我们的内容（2026 年 11 月）：第三方工具不得修改游戏客户端、不得追踪你视野之外的玩家、不得在游戏客户端上显示覆盖层；所有第三方工具的使用风险自负，如果反作弊（BattlEye）检测到干扰，可能会将你踢出游戏。本应用只读取聊天内容，覆盖层默认关闭。

## 常见问题

- **什么都不显示。** 确认状态为 *Capturing*，然后在 Say 频道说句话。VPN 和加速器（ExitLag 等）可能会让 Windows 看不到游戏流量，请关闭后再试。
- **"Administrator permission was declined"。** 再次点击 Start 并选择*是*。如果你的 Windows 账户不是管理员，需要由管理员批准。
- **Windows SmartScreen 对安装程序发出警告。** 目前版本还没有代码签名。请核对文件哈希（见上文），选择*更多信息 → 仍要运行*，或自己构建。
- **某个快捷键没反应。** 它被其他程序占用了，程序会告诉你是哪个。请使用按钮。
- **"Google is limiting requests; translating offline"。** Google 免费接口的限制很低，离线备用会接替；某种语言第一次使用时需要下载语言包，所以最初几行可能未翻译。添加免费的 Microsoft 或 DeepL 密钥可以避免这种情况。

## 许可证

基于 **GNU GPL v3**（[`LICENSE`](../../LICENSE)）：你可以使用、研究、分享和修改；你分享的修改版本必须以相同许可证保持开源。原项目作者：**Tenith**。

Albion Online 是 Sandbox Interactive GmbH 的商标。本项目与 Sandbox Interactive 无关，也未获得其认可。
