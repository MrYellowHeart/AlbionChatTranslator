# Albion Chat Translator

[English](../../README.md) · **Português (BR)** · [Español](README.es.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Polski](README.pl.md) · [Türkçe](README.tr.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

Leia o chat do Albion Online no seu idioma e responda no idioma dos outros.

Grátis, sem conta, sem chave de API, sem configuração. **Código aberto**: todo o código é público, então qualquer pessoa pode conferir o que ele faz.

---

## O que faz

- **Traduz o que você lê.** Mensagens de outros jogadores (Say, Whisper, Party, Guild, Alliance e, se quiser, canais globais) aparecem no seu idioma assim que chegam. Mensagens que já estão no seu idioma ficam como estão.
- **Traduz o que você escreve.** Aperte um atalho, escreva no seu idioma, aperte Enter, e a tradução fica pronta para colar no Albion.
- **Mostra o chat por cima do jogo.** Uma sobreposição transparente opcional; os cliques do mouse passam direto para o jogo.

## Instalação

1. Abra [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases) e baixe `AlbionChatTranslator-win-Setup.exe`.
2. Execute. Não precisa de mais nada (o .NET já vem incluído). Instala só para o seu usuário do Windows.
3. Escolha o idioma em que você lê.
4. Clique em **▶ Start** e escolha **Sim** no aviso do Windows ([por quê?](#por-que-pede-permissão-de-administrador)).
5. Entre no Albion. Suas abas de chat aparecem no app sozinhas.

As atualizações são baixadas e instaladas automaticamente.

## Como usar

### Ler o chat

É só jogar. As mensagens traduzidas aparecem no app (e na sobreposição, se estiver ligada):

```
14:02 John: Alguém quer fazer dungeon hoje à noite? (English)
```

- **Passe o mouse** sobre uma mensagem para ver o texto original.
- **Clique** na etiqueta `(English)` para responder à pessoa no idioma dela.
- Mensagens no seu idioma, mensagens muito curtas ("gg", "ok") e links de itens aparecem como estão.

### Responder no idioma deles

1. Aperte **Ctrl+Shift+R** no jogo (ou clique na etiqueta de idioma de uma mensagem).
2. Escreva no **seu** idioma e aperte **Enter**.
3. A tradução vai para a área de transferência. No Albion, abra o chat, aperte **Ctrl+V** e depois Enter.

A caixa de resposta já vem no idioma da última pessoa que escreveu para você; dá para trocar.
O app nunca digita nem envia nada no jogo. **Você** sempre cola e envia.

### Sobreposição

**Ctrl+Shift+O** mostra ou esconde um chat transparente por cima do jogo. Enquanto está travada, os cliques passam direto para o Albion. **Ctrl+Shift+L** destrava para arrastar e redimensionar; aperte de novo para travar.

### Atalhos

| Teclas | Função |
|---|---|
| **Ctrl+Shift+R** | Responder a quem escreveu por último |
| **Ctrl+Shift+O** | Mostrar / esconder a sobreposição |
| **Ctrl+Shift+L** | Destravar / travar a sobreposição para movê-la |

Funcionam com o Albion em foco. Se outro programa já usa algum, o app avisa; os botões do app fazem o mesmo.

### Configurações úteis

| Configuração | Para que serve |
|---|---|
| **Traduzir o chat para** | O seu idioma. |
| **Também leio** | Outros idiomas que você entende. Nunca são traduzidos nem enviados. |
| **Quando traduzir** | *Automaticamente* ou *só quando eu clicar em [translate]*. |
| **Nunca enviar estes canais** | Por exemplo sussurros ou guilda, se quiser mantê-los privados. |
| **Nunca traduzir estas palavras** | Nomes de guilda, de amigos, suas gírias. |
| **Tradutor** | Google Tradutor (padrão, sem configuração) ou DeepL com sua chave gratuita, e *usar o DeepL quando o Google estiver ocupado*. |
| **Lembrar traduções por 7 dias** | Menos mensagens enviadas após reiniciar. Desligado por padrão. |

## Como funciona

1. **Leitura do chat.** O Albion não tem sistema de addons, então o app lê o tráfego de rede do jogo, como as ferramentas conhecidas da comunidade. Um pequeno ajudante oculto faz isso e repassa só as mensagens de chat. Ele só lê: nunca altera, envia ou injeta nada.
2. **O que traduzir.** No seu PC, o app reconhece mensagens que já estão nos seus idiomas, curtas demais, só links de itens ou já traduzidas antes. **Essas nunca saem do seu PC.** Ele também protege palavras do Albion (cidades, tiers como `t8.3`, links de itens, "gank", "zerg"…) e transforma gírias como `lf healer pls` em inglês normal antes, o que melhora muito a tradução.
3. **Tradução.** O que sobra, só o texto da mensagem, vai para o Google Tradutor (ou DeepL) e o resultado aparece.
4. **Resposta.** O que você digita é traduzido do mesmo jeito e vai para a área de transferência.

### Por que pede permissão de administrador?

O Windows só deixa programas lerem o tráfego de rede com permissão de administrador. O app usa o mínimo possível:

- A janela que você usa roda como **usuário normal**.
- Só um ajudante separado e minúsculo recebe essa permissão. Ele só consegue ler os pacotes de chat do Albion e repassá-los à janela por uma conexão privada, de mão única, que só o seu usuário do Windows pode abrir.
- O ajudante não acessa a internet, não lê nem grava arquivos e não aceita comandos.

## Privacidade

| Enviado ao Google Tradutor (ou DeepL) | Nunca enviado |
|---|---|
| O **texto** das mensagens que não estão nos seus idiomas | Mensagens nos seus idiomas, mensagens curtas, repetidas |
| O texto que você digita na caixa de resposta | Nomes de jogadores, canais, guildas, links de itens |
| | Sua conta, personagem ou qualquer dado do jogo |
| | Nada dos canais que você excluiu |

- Tudo vai por HTTPS no corpo da requisição (nunca no endereço web), sem cookies, sem conta e sem identificador do app. Como qualquer site, o Google vê seu endereço de internet (IP).
- A barra de status conta cada mensagem que saiu do seu PC (*Sent for translation*).
- Sem telemetria, sem análises, sem anúncios, sem registro do chat. Nada é gravado em disco, a menos que você ative *Lembrar traduções*.
- Uma chave do DeepL, se você adicionar, fica criptografada com a sua conta do Windows.

O Google Tradutor é usado pelo endereço gratuito que os próprios widgets do Google usam. Não precisa de chave, mas não é uma API oficial: o Google pode limitá-lo. O app espera sozinho, e o DeepL pode assumir.

## Código aberto: confira você mesmo

Este projeto é **livre e de código aberto** sob a [GNU GPL v3](../../LICENSE). Você não precisa confiar no que está escrito acima: pode ler o código que faz isso acontecer.

| Afirmação | Onde conferir |
|---|---|
| O ajudante só lê o chat e nunca envia nada ao jogo | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Só eventos de chat são lidos | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| Ajudante e app conversam em mão única, só para o seu usuário | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| O que é filtrado antes de qualquer envio | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Exatamente o que é enviado ao Google, e como | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Palavras do Albion e links de itens são protegidos | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| Esses comportamentos são testados | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**Conferindo um download.** Os instaladores são gerados pelo próprio GitHub a partir do código público, com [este workflow](../../.github/workflows/release.yml), e qualquer pessoa pode abrir o log da compilação. Cada versão traz `SHA256SUMS.txt`: no PowerShell, `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` deve mostrar o mesmo valor. Ou compile você mesmo (veja a [seção para desenvolvedores em inglês](../../README.md#for-developers)).

Achou um problema? [Abra uma issue](https://github.com/MrYellowHeart/AlbionChatTranslator/issues). Pull requests são bem-vindos.

## É seguro para a minha conta?

O app só **lê** o tráfego de rede, como outras ferramentas da comunidade do Albion. Não altera o jogo, não lê a memória dele, não injeta código, não automatiza comandos e não envia nada aos servidores do jogo. Não é feito, aprovado nem suportado pela Sandbox Interactive, e nenhuma ferramenta de terceiros pode garantir como uma empresa de jogos vai avaliá-la. **Use por sua conta e risco.**

## Problemas comuns

- **Nada aparece.** Confira se o status diz *Capturing* e escreva algo no Say. VPNs e redutores de ping (ExitLag e similares) podem esconder o tráfego do jogo; tente sem eles.
- **"Administrator permission was declined".** Clique em Start de novo e escolha *Sim*. Se sua conta do Windows não for administradora, um administrador precisa aprovar.
- **O Windows SmartScreen avisa sobre o instalador.** As versões ainda não são assinadas digitalmente. Confira o hash (acima), escolha *Mais informações → Executar assim mesmo*, ou compile você mesmo.
- **Um atalho não funciona.** Outro programa já usa; o app avisa qual. Use os botões.
- **"Translation paused".** O Google está limitando pedidos por um momento; o app tenta de novo sozinho. Uma chave do DeepL evita isso.

## Licença

Licenciado sob a **GNU GPL v3** ([`LICENSE`](../../LICENSE)): você pode usar, estudar, compartilhar e modificar; versões modificadas que você compartilhar devem continuar abertas sob a mesma licença. Projeto original de **Tenith**.

Albion Online é marca registrada da Sandbox Interactive GmbH. Este projeto não é afiliado nem aprovado pela Sandbox Interactive.
