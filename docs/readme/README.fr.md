# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · [Español](README.es.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · **Français** · [Polski](README.pl.md) · [Türkçe](README.tr.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

Lisez le chat d'Albion Online dans votre langue et répondez dans la leur.

Gratuit, sans compte, sans clé d'API, sans configuration. **Open source** : tout le code est public, chacun peut vérifier ce qu'il fait.

---

## Ce qu'il fait

- **Traduit ce que vous lisez.** Les messages des autres joueurs (Say, Whisper, Party, Guild, Alliance et, si vous le voulez, les canaux globaux) s'affichent dans votre langue dès leur arrivée. Ceux déjà dans votre langue restent tels quels.
- **Traduit ce que vous écrivez.** Un raccourci, vous écrivez dans votre langue, Entrée, et la traduction est prête à coller dans Albion.
- **Affiche le chat par-dessus le jeu.** Une surcouche transparente optionnelle ; les clics de souris passent directement au jeu.

## Installation

1. Ouvrez [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases) et téléchargez `AlbionChatTranslator-win-Setup.exe`.
2. Lancez-le. Rien d'autre à installer (.NET est inclus). Il s'installe uniquement pour votre utilisateur Windows.
3. Choisissez la langue dans laquelle vous lisez.
4. Cliquez sur **▶ Start** puis **Oui** dans la fenêtre de Windows ([pourquoi ?](#pourquoi-faut-il-les-droits-administrateur-)).
5. Connectez-vous à Albion. Vos onglets de chat apparaissent tout seuls dans l'application.

Les mises à jour se téléchargent et s'installent automatiquement.

## Utilisation

### Lire le chat

Jouez, tout simplement. Les messages traduits apparaissent dans l'application (et dans la surcouche si elle est active) :

```
14:02 Ana: Quelqu'un vend un sac t6.1 pas cher à Lymhurst ? (Portuguese)
```

- **Survolez** un message pour voir le texte original.
- **Cliquez** sur l'étiquette `(Portuguese)` pour répondre à cette personne dans sa langue.
- Les messages dans votre langue, les très courts ("gg", "ok") et les liens d'objets s'affichent tels quels.

### Répondre dans leur langue

1. Appuyez sur **Ctrl+Maj+R** en jeu (ou cliquez sur l'étiquette de langue d'un message).
2. Écrivez dans **votre** langue et appuyez sur **Entrée**.
3. La traduction est dans le presse-papiers. Dans Albion, ouvrez le chat, **Ctrl+V**, puis Entrée.

La fenêtre de réponse est déjà réglée sur la langue de la dernière personne qui vous a écrit ; vous pouvez la changer.
L'application ne tape et n'envoie jamais rien dans le jeu. C'est toujours **vous** qui collez et envoyez.

### Surcouche

**Ctrl+Maj+O** affiche ou masque un chat transparent par-dessus le jeu. Tant qu'elle est verrouillée, les clics passent directement à Albion. **Ctrl+Maj+L** la déverrouille pour la déplacer et la redimensionner ; appuyez à nouveau pour la verrouiller.

### Raccourcis

| Touches | Action |
|---|---|
| **Ctrl+Maj+R** | Répondre à la dernière personne qui a écrit |
| **Ctrl+Maj+O** | Afficher / masquer la surcouche |
| **Ctrl+Maj+L** | Déverrouiller / verrouiller la surcouche pour la déplacer |

Ils fonctionnent quand Albion est au premier plan. Si un autre programme utilise déjà l'un d'eux, l'application vous le dit ; ses boutons font la même chose.

### Réglages utiles

| Réglage | À quoi il sert |
|---|---|
| **Traduire le chat en** | Votre langue. |
| **Je lis aussi** | D'autres langues que vous comprenez. Jamais traduites ni envoyées. |
| **Quand traduire** | *Automatiquement* ou *seulement quand je clique sur [translate]*. |
| **Ne jamais envoyer ces canaux** | Par exemple les chuchotements ou la guilde, pour qu'ils restent privés. |
| **Ne jamais traduire ces mots** | Noms de guilde, d'amis, votre propre argot. |
| **Traducteur** | Google Traduction (par défaut, sans configuration) ou DeepL avec votre clé gratuite, et *utiliser DeepL quand Google est occupé*. |
| **Mémoriser les traductions 7 jours** | Moins d'envois après un redémarrage. Désactivé par défaut. |

## Comment ça marche

1. **Lecture du chat.** Albion n'a pas de système d'add-ons ; l'application lit donc le trafic réseau du jeu, comme les outils connus de la communauté. Un petit assistant caché s'en charge et ne transmet que les messages de chat. Il lit seulement : il ne modifie, n'envoie et n'injecte rien.
2. **Ce qui est traduit.** Sur votre PC, l'application reconnaît les messages déjà dans vos langues, trop courts, composés uniquement de liens d'objets ou déjà traduits. **Ceux-là ne quittent jamais votre PC.** Elle protège aussi les mots d'Albion (villes, tiers comme `t8.3`, liens d'objets, "gank", "zerg"…) et transforme d'abord l'argot comme `lf healer pls` en anglais normal, ce qui améliore beaucoup la traduction.
3. **Traduction.** Ce qui reste, uniquement le texte du message, est envoyé à Google Traduction (ou DeepL), puis le résultat s'affiche.
4. **Réponse.** Votre texte est traduit de la même façon et mis dans le presse-papiers.

### Pourquoi faut-il les droits administrateur ?

Windows n'autorise la lecture du trafic réseau qu'avec les droits administrateur. L'application les réduit au minimum :

- La fenêtre que vous utilisez tourne en **utilisateur normal**.
- Seul un minuscule assistant séparé reçoit ces droits. Il peut uniquement lire les paquets de chat d'Albion et les transmettre à la fenêtre par une connexion privée, à sens unique, que seul votre utilisateur Windows peut ouvrir.
- L'assistant n'a pas accès à internet, ne lit ni n'écrit aucun fichier et n'accepte aucune commande.

## Confidentialité

| Envoyé à Google Traduction (ou DeepL) | Jamais envoyé |
|---|---|
| Le **texte** des messages qui ne sont pas dans vos langues | Messages dans vos langues, messages courts, répétitions |
| Le texte que vous tapez dans la fenêtre de réponse | Noms de joueurs, canaux, guildes, liens d'objets |
| | Votre compte, votre personnage ou toute donnée de jeu |
| | Rien des canaux que vous avez exclus |

- Tout passe en HTTPS dans le corps de la requête (jamais dans l'adresse web), sans cookies, sans compte et sans identifiant d'application. Comme tout site web, Google voit votre adresse internet (IP).
- La barre d'état compte chaque message sorti de votre PC (*Sent for translation*).
- Pas de télémétrie, pas de statistiques, pas de publicité, pas de journal du chat. Rien n'est écrit sur le disque sauf si vous activez *Mémoriser les traductions*.
- Une clé DeepL, si vous en ajoutez une, est chiffrée avec votre compte Windows.

Google Traduction est utilisé via l'adresse gratuite qu'utilisent les propres widgets de Google. Pas de clé nécessaire, mais ce n'est pas une API officielle : Google pourrait la limiter. L'application patiente toute seule, et DeepL peut prendre le relais.

## Open source : vérifiez par vous-même

Ce projet est **libre et open source** sous [GNU GPL v3](../../LICENSE). Pas besoin de nous croire sur parole : vous pouvez lire le code qui tient ces promesses.

| Affirmation | Où la vérifier |
|---|---|
| L'assistant lit seulement le chat et n'envoie rien au jeu | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Seuls les événements de chat sont analysés | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| L'assistant et l'application communiquent à sens unique, pour votre utilisateur seulement | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| Ce qui est filtré avant tout envoi | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Ce qui est envoyé à Google, exactement, et comment | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Les mots d'Albion et les liens d'objets sont protégés | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| Ces comportements sont testés | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**Vérifier un téléchargement.** Les installateurs sont construits par GitHub lui-même à partir du code public, avec [ce workflow](../../.github/workflows/release.yml), et tout le monde peut consulter le journal de compilation. Chaque version contient `SHA256SUMS.txt` : dans PowerShell, `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` doit afficher la même valeur. Ou compilez-le vous-même (voir la [section développeurs en anglais](../../README.md#for-developers)).

Un problème ? [Ouvrez une issue](https://github.com/MrYellowHeart/AlbionChatTranslator/issues). Les pull requests sont les bienvenues.

## Est-ce sans risque pour mon compte ?

L'application **lit** seulement le trafic réseau, comme d'autres outils de la communauté Albion. Elle ne modifie pas le jeu, ne lit pas sa mémoire, n'injecte pas de code, n'automatise aucune action et n'envoie rien aux serveurs du jeu. Elle n'est ni créée, ni approuvée, ni soutenue par Sandbox Interactive, et aucun outil tiers ne peut garantir comment un éditeur de jeu le jugera. **Utilisation à vos risques.**

## Problèmes courants

- **Rien ne s'affiche.** Vérifiez que l'état indique *Capturing*, puis écrivez dans Say. Les VPN et réducteurs de ping (ExitLag et similaires) peuvent masquer le trafic du jeu ; essayez sans.
- **"Administrator permission was declined".** Cliquez à nouveau sur Start et choisissez *Oui*. Si votre compte Windows n'est pas administrateur, un administrateur doit valider.
- **Windows SmartScreen avertit à propos de l'installateur.** Les versions ne sont pas encore signées. Vérifiez l'empreinte (ci-dessus), choisissez *Informations complémentaires → Exécuter quand même*, ou compilez-le vous-même.
- **Un raccourci ne fait rien.** Un autre programme l'utilise ; l'application indique lequel. Utilisez les boutons.
- **"Translation paused".** Google limite les requêtes un instant ; l'application réessaie seule. Une clé DeepL évite cela.

## Licence

Sous **GNU GPL v3** ([`LICENSE`](../../LICENSE)) : vous pouvez l'utiliser, l'étudier, le partager et le modifier ; les versions modifiées que vous partagez doivent rester open source sous la même licence. Projet d'origine de **Tenith**.

Albion Online est une marque de Sandbox Interactive GmbH. Ce projet n'est ni affilié ni approuvé par Sandbox Interactive.
