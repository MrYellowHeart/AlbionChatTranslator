# Albion Chat Translator

[English](../../README.md) · [Português (BR)](README.pt-BR.md) · **Español** · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Polski](README.pl.md) · [Türkçe](README.tr.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

Lee el chat de Albion Online en tu idioma y responde en el suyo.

Gratis, sin cuenta, sin clave de API, sin configurar nada. **Código abierto**: todo el código es público, así que cualquiera puede comprobar lo que hace.

---

## Qué hace

- **Traduce lo que lees.** Los mensajes de otros jugadores (Say, Whisper, Party, Guild, Alliance y, si quieres, canales globales) aparecen en tu idioma al llegar. Los que ya están en tu idioma se quedan igual.
- **Traduce lo que escribes.** Pulsa un atajo, escribe en tu idioma, pulsa Enter y la traducción queda lista para pegar en Albion.
- **Muestra el chat sobre el juego.** Una capa transparente opcional; los clics del ratón pasan directamente al juego.

## Instalación

1. Abre [**Releases**](https://github.com/MrYellowHeart/AlbionChatTranslator/releases) y descarga `AlbionChatTranslator-win-Setup.exe`.
2. Ejecútalo. No hace falta nada más (.NET viene incluido). Se instala solo para tu usuario de Windows.
3. Elige el idioma en que lees y el servidor en el que juegas, y marca los idiomas que ves en el chat. Sus paquetes sin conexión se descargan en segundo plano (en América, inglés, español y portugués vienen marcados; para Europa y Asia no se presupone nada, eliges tú).
4. Haz clic en **▶ Start** y elige **Sí** en el aviso de Windows ([¿por qué?](#por-qué-pide-permisos-de-administrador)).
5. Entra en Albion. Tus pestañas de chat aparecen solas en la app.

Las actualizaciones se descargan e instalan solas.

## Cómo usarlo

### Leer el chat

Solo juega. Los mensajes traducidos aparecen en la app (y en la capa, si está activada):

```
14:02 Ana: ¿Alguien vende una bolsa t6.1 barata en Lymhurst? (Portuguese)
```

- **Pasa el ratón** por encima de un mensaje para ver el original.
- **Haz clic** en la etiqueta `(Portuguese)` para responder a esa persona en su idioma.
- Los mensajes en tu idioma, los muy cortos ("gg", "ok") y los enlaces de objetos se muestran tal cual.

### Responder en su idioma

1. Pulsa **Ctrl+Shift+R** en el juego (o haz clic en la etiqueta de idioma de un mensaje).
2. Escribe en **tu** idioma y pulsa **Enter**.
3. La traducción queda en el portapapeles. En Albion, abre el chat, pulsa **Ctrl+V** y luego Enter.

La ventana de respuesta ya viene en el idioma de la última persona que te escribió; puedes cambiarlo.
La app nunca escribe ni envía nada en el juego. **Tú** siempre pegas y envías.

### Capa sobre el juego

**Ctrl+Shift+O** muestra u oculta un chat transparente encima del juego. Mientras está bloqueada, los clics pasan directamente a Albion. **Ctrl+Shift+L** la desbloquea para moverla y cambiar su tamaño; vuelve a pulsarlo para bloquearla.

### Atajos

| Teclas | Qué hace |
|---|---|
| **Ctrl+Shift+R** | Responder a quien escribió el último |
| **Ctrl+Shift+O** | Mostrar / ocultar la capa |
| **Ctrl+Shift+L** | Desbloquear / bloquear la capa para moverla |

Funcionan con Albion en primer plano. Si otro programa ya usa alguno, la app te avisa; los botones de la app hacen lo mismo.

### Ajustes útiles

| Ajuste | Para qué sirve |
|---|---|
| **Traducir el chat a** | Tu idioma. |
| **También leo** | Otros idiomas que entiendes. Nunca se traducen ni se envían. |
| **Cuándo traducir** | *Automáticamente* o *solo cuando haga clic en [translate]*. |
| **No enviar nunca estos canales** | Por ejemplo susurros o gremio, si quieres que sean privados. |
| **No traducir nunca estas palabras** | Nombres de gremio, de amigos, tu propia jerga. |
| **Traductores** | Se usan en este orden: **1.** tu propia clave de Microsoft Translator o DeepL (opcional), **2.** modelos sin conexión en tu PC (no se envía nada), **3.** Google Translate, solo para idiomas sin modelo sin conexión. Cada uno se puede activar o desactivar. Abreviaturas como *xq*, *ntp*, *porfa*, *jaja*, *vc*, *blz* se expanden antes de traducir. |
| **Recordar traducciones 7 días** | Menos mensajes enviados tras reiniciar. Desactivado por defecto. |

## Cómo funciona

1. **Lectura del chat.** Albion no tiene sistema de addons, así que la app lee el tráfico de red del juego, como las herramientas conocidas de la comunidad. Un pequeño ayudante oculto lo hace y pasa solo los mensajes de chat. Solo lee: nunca cambia, envía ni inyecta nada.
2. **Qué traducir.** En tu PC, la app reconoce los mensajes que ya están en tus idiomas, los demasiado cortos, los que solo son enlaces de objetos o los ya traducidos. **Esos nunca salen de tu PC.** También protege palabras de Albion (ciudades, tiers como `t8.3`, enlaces de objetos, "gank", "zerg"…) y convierte jerga como `lf healer pls` en inglés normal antes, lo que mejora mucho la traducción.
3. **Traducción.** Lo que queda, solo el texto del mensaje, lo traducen primero los modelos sin conexión de tu PC (no se envía nada). Google Translate solo se usa para idiomas sin modelo sin conexión.
4. **Respuesta.** Lo que escribes se traduce igual y va al portapapeles.

### ¿Por qué pide permisos de administrador?

Windows solo deja leer el tráfico de red con permisos de administrador. La app los reduce al mínimo:

- La ventana que usas funciona como **usuario normal**.
- Solo un ayudante aparte y diminuto recibe esos permisos. Lo único que puede hacer es leer los paquetes de chat de Albion y pasarlos a la ventana por una conexión privada, de un solo sentido, que solo tu usuario de Windows puede abrir.
- El ayudante no tiene acceso a internet, no lee ni escribe archivos y no acepta órdenes.

## Privacidad

| Se envía a Google Translate (o DeepL) | Nunca se envía |
|---|---|
| El **texto** de los mensajes que no están en tus idiomas | Mensajes en tus idiomas, mensajes cortos, repetidos |
| El texto que escribes en la ventana de respuesta | Nombres de jugadores, canales, gremios, enlaces de objetos |
| | Tu cuenta, tu personaje o cualquier dato del juego |
| | Nada de los canales que excluiste |

- Todo va por HTTPS en el cuerpo de la petición (nunca en la dirección web), sin cookies, sin cuenta y sin identificador de la app. Como cualquier web, Google ve tu dirección de internet (IP).
- La barra de estado cuenta cada mensaje que salió de tu PC (*Sent for translation*).
- Sin telemetría, sin analíticas, sin anuncios, sin registros del chat. No se guarda nada en disco salvo que actives *Recordar traducciones*.
- Una clave de DeepL, si la añades, se cifra con tu cuenta de Windows.

Google Translate se usa mediante la dirección gratuita que usan los propios widgets de Google. No necesita clave, pero no es una API oficial y Google la limita mucho por conexión; un chat movido puede alcanzar el límite en minutos. Cuando pasa, la app se aparta un rato y toma el relevo la **copia sin conexión** (un modelo que funciona en tu PC y no envía nada; descarga unos 110 MB por idioma la primera vez) o tu propia clave, así que la traducción no se detiene. Para el servicio más fiable, añade una clave gratuita de Microsoft Translator o DeepL.

## Código abierto: compruébalo tú mismo

Este proyecto es **libre y de código abierto** bajo la [GNU GPL v3](../../LICENSE). No tienes que creer lo que dice arriba: puedes leer el código que lo hace realidad.

| Afirmación | Dónde comprobarlo |
|---|---|
| El ayudante solo lee el chat y nunca envía nada al juego | [`RawSocketCapture.cs`](../../src/AlbionChatTranslator.Core/Capture/RawSocketCapture.cs), [`CaptureHost.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureHost.cs) |
| Solo se leen eventos de chat | [`ChatEventParser.cs`](../../src/AlbionChatTranslator.Core/Chat/ChatEventParser.cs) |
| Ayudante y app hablan en un solo sentido, solo para tu usuario | [`CaptureClient.cs`](../../src/AlbionChatTranslator.Core/Ipc/CaptureClient.cs), [`PipeProtocol.cs`](../../src/AlbionChatTranslator.Core/Ipc/PipeProtocol.cs) |
| Qué se filtra antes de enviar nada | [`TranslationService.cs`](../../src/AlbionChatTranslator.Core/Translation/TranslationService.cs), [`LanguageDetector.cs`](../../src/AlbionChatTranslator.Core/Translation/LanguageDetector.cs) |
| Qué se envía exactamente a Google, y cómo | [`Providers.cs`](../../src/AlbionChatTranslator.Core/Translation/Providers.cs) (`GoogleProvider`) |
| Las palabras de Albion y los enlaces se protegen | [`Glossary.cs`](../../src/AlbionChatTranslator.Core/Translation/Glossary.cs) |
| Estos comportamientos están probados | [`tests/`](../../tests/AlbionChatTranslator.Tests) |

**Comprobar una descarga.** Los instaladores los compila GitHub a partir del código público con [este workflow](../../.github/workflows/release.yml), y cualquiera puede abrir el registro de compilación. Cada versión incluye `SHA256SUMS.txt`: en PowerShell, `Get-FileHash .\AlbionChatTranslator-win-Setup.exe` debe mostrar el mismo valor. O compílalo tú mismo (ver la [sección para desarrolladores en inglés](../../README.md#for-developers)).

¿Encontraste un problema? [Abre un issue](https://github.com/MrYellowHeart/AlbionChatTranslator/issues). Los pull requests son bienvenidos.

## ¿Es seguro para mi cuenta?

La app solo **lee** el tráfico de red, como otras herramientas de la comunidad de Albion. No modifica el juego, no lee su memoria, no inyecta código, no automatiza acciones y no envía nada a los servidores del juego. No está hecha, aprobada ni soportada por Sandbox Interactive, y ninguna herramienta de terceros puede garantizar cómo la valorará una empresa de juegos. **Úsala bajo tu propia responsabilidad.**

## Problemas frecuentes

- **No aparece nada.** Comprueba que el estado dice *Capturing* y escribe algo en Say. Las VPN y reductores de ping (ExitLag y similares) pueden ocultar el tráfico del juego; prueba sin ellos.
- **"Administrator permission was declined".** Vuelve a pulsar Start y elige *Sí*. Si tu cuenta de Windows no es administradora, un administrador tiene que aprobarlo.
- **Windows SmartScreen avisa sobre el instalador.** Las versiones aún no están firmadas. Comprueba el hash (arriba), elige *Más información → Ejecutar de todas formas*, o compílalo tú.
- **Un atajo no hace nada.** Otro programa ya lo usa; la app dice cuál. Usa los botones.
- **"Google is limiting requests; translating offline".** La dirección gratuita de Google tiene un límite bajo. La copia sin conexión toma el relevo; la primera vez para un idioma descarga su paquete, así que las primeras líneas pueden verse sin traducir. Una clave gratuita de Microsoft o DeepL lo evita.

## Licencia

Bajo la **GNU GPL v3** ([`LICENSE`](../../LICENSE)): puedes usarlo, estudiarlo, compartirlo y modificarlo; las versiones modificadas que compartas deben seguir siendo abiertas con la misma licencia. Proyecto original de **Tenith**.

Albion Online es una marca de Sandbox Interactive GmbH. Este proyecto no está afiliado ni respaldado por Sandbox Interactive.
