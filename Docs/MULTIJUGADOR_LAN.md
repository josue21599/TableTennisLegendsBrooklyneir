# Multijugador LAN: diseño

Modo para dos móviles en la misma wifi, sin cuentas ni Internet. Aprobado el 3 de noviembre de 2026.

## Decisiones aprobadas

- **Solo LAN** (misma wifi o zona wifi compartida). Relay e Internet quedan fuera por ahora.
- **Modos:** solo partida rápida y personalizada.
- **Cámara del invitado** girada 180°: cada jugador ve su pala en su lado.
- **Desconexión sin reconexión en 30 s:** partida anulada, sin monedas ni estadísticas para nadie.
- **Abandono voluntario:** gana el que se queda, con sus recompensas normales.

## Autoridad

| Qué | Lo decide | El otro móvil |
|---|---|---|
| Golpes y saques de mi jugador | Mi móvil, al instante | Los recibe y los aplica |
| Botes, puntos, marcador, saque, tiempo | El anfitrión | Solo los muestra |
| Poderes (orbes, activación, duración) | El anfitrión (el invitado pide activar) | Solo los muestra |
| Aleatoriedad | El anfitrión | No usa números aleatorios de juego |

- Cada saque, golpe o evento que cambia la trayectoria (escudo, ralentización) viaja con el **estado completo de la pelota** (posición, velocidad, `SpinAccel`) y el tick: quien lo recibe sustituye su pelota, así que la deriva se corrige en cada golpe.
- El invitado no tiene marcador propio: su árbitro local solo responde "¿puedo golpear ya?".
- **Regla de discrepancia:** el anfitrión decide los puntos, pero un golpe válido gana a un punto aún no confirmado. Los puntos que ocurren en el campo del invitado quedan pendientes durante un margen (la mitad del tiempo de ida y vuelta + 60 ms, entre 40 y 200 ms); un `HitBegin` con tick anterior los anula. Bote en el borde o red dudosa: vale lo del anfitrión, y el golpe del invitado a esa bola se descarta.

## Tiempo y latencia

- En modo LAN la física avanza en **ticks fijos de 1/240 s** con `Substep()`: los dos móviles hacen los mismos subpasos para el mismo instante, vaya cada uno a 60 o 120 Hz. `Step(dt)` parte el tiempo según el fotograma y redondearía distinto. El modo sin red no cambia.
- **Reloj:** Ping/Pong tipo NTP cada 2 s; se usa la muestra de menor tiempo de ida y vuelta de las últimas 16.
- **Adelanto:** un golpe del tick `k` recibido en el tick `n` se aplica y se simulan `n − k` subpasos. El salto visual se suaviza en unos 100 ms (solo la vista; la física es exacta).
- **Golpe en dos mensajes:** `HitBegin` (contacto: el otro móvil sujeta la bola en la pala remota) y `HitLaunch` (al soltar tras la pausa de impacto, con el estado completo).
- **Pala remota:** se dibuja unos 100 ms en el pasado, interpolada (`RemotePaddleBuffer`). Nunca se comprueba para detectar golpes en este móvil.

## Librería

Unity Transport (`com.unity.transport` 2.x) directamente, con un canal fiable y ordenado y otro no fiable. Netcode for GameObjects no encaja: sus `NetworkObject` necesitan prefabs registrados (aquí todo se construye por código), y su sistema de ticks y su reparto de autoridad chocarían con la física propia. La búsqueda en la wifi usa un `UdpClient` de .NET, solo en esa pantalla.

## Mensajes

| Mensaje | Canal | Dirección | Contenido |
|---|---|---|---|
| `Hello` / `Welcome` / `Reject` | Fiable | I ↔ A | Versión del protocolo, versión de la app, hash de `balance.json`, nombre, raqueta |
| `MatchSetup` | Fiable | A → I | Opciones, mesa, raquetas, tick de inicio, código de sesión |
| `Ping` / `Pong` | No fiable | I ↔ A | Tiempos para sincronizar el reloj |
| `Paddle` | No fiable, con número de secuencia | Ambos | Tick, posición y objetivo de la pala (unas 30 veces por segundo) |
| `ServeLaunch`, `HitBegin`, `HitLaunch` | Fiable | Ambos | Tick, estado de la pelota, tipo de golpe, fuerza |
| `PowerRequest` / `PowerState` | Fiable | I → A / A → ambos | Activación / estado de los poderes (y pelota si cambia la trayectoria) |
| `ShieldSave` | Fiable | A → I | Estado de la pelota tras la salvada |
| `PointResult` | Fiable | A → I | Ganador, motivo, marcador completo, siguiente saque, punto de partido, fin |
| `OrbState` | Fiable | A → I | Orbe de poder: activo, posición y tipo (quién lo recoge llega en `PowerState`) |
| `Pause` / `ResumeAt` / `Bye` | Fiable | Ambos | Pausa con foto de la pelota, reanudación en un tick común, abandono |

## Conexión

- **Anuncio** cada segundo al puerto UDP 47777, enviado a `255.255.255.255` **y a la dirección de broadcast de la subred** (calculada con la IP y la máscara), porque algunos Android no envían bien al broadcast general. Juego en el puerto 47778.
- **Respaldo:** escribir la IP del anfitrión, que se muestra en su sala de espera. Si en 5 s no aparece nada, aviso de redes de invitados o aislamiento de dispositivos.
- **Permisos:** `INTERNET` (Android lo exige para cualquier socket), `ACCESS_NETWORK_STATE`, `ACCESS_WIFI_STATE` y `CHANGE_WIFI_MULTICAST_STATE`. El `MulticastLock` se pide solo mientras se busca partida. Se añaden con un script de editor al generar el proyecto Gradle. Hay que comprobar en Android 16 que no hace falta nada más.
- **README:** "No necesita Internet ni cuentas. El modo LAN usa tu wifi local; Android pide el permiso INTERNET para cualquier conexión de red, incluso dentro de casa, pero el juego no se conecta a ningún servidor externo".
- **Versión:** se comparan la versión del protocolo, la versión de la app y el hash de `balance.json`; si algo no coincide, se rechaza con un mensaje claro.

## Casos raros

| Caso | Qué pasa |
|---|---|
| Pausa | Cualquiera pausa: el anfitrión pausa los dos con la foto de la pelota. Cualquiera reanuda: cuenta atrás 3-2-1 y arranque en el mismo tick. |
| Segundo plano | Se envía `Pause` antes de suspender; al volver, se reanuda o se pasa a reconexión. |
| Desconexión | Si no llega nada en 1,5 s: pausa con "Esperando al rival…" y 30 s de margen. El punto en juego se anula. |
| Reconexión | El invitado vuelve con su código de sesión; el anfitrión reenvía el estado completo y se repite el saque. |
| Sin reconexión | Partida anulada, sin monedas para nadie. |
| Abandono (`Bye`) | Gana el que se queda. |

## Fases

### Fase 1: quién controla cada pala (hecha, pendiente de comprobar en Unity)

`PlayerControl` (`Local`, `Cpu`, `Remote`, `None`) y `ControlSetup`, que reproduce las reglas anteriores; controladores `TouchPaddleDriver`, `CpuPaddleDriver` (envuelve `AIPlayer` sin cambiarlo), `RemotePaddleDriver` y `NoPaddleDriver`; `RemotePaddleBuffer`. `MatchController` pregunta al controlador en vez de a `Human[]` y a la IA. La jugabilidad local no cambia.

### Fase 2: LAN con dos móviles (en curso)

Aprobada el 3 de noviembre de 2026. Móviles para la prueba final: **OPPO Reno11 F (Android 16)** y **OnePlus 12 (Android 15)**. Entrada: botón **"LAN"** en la fila inferior del menú, con *Crear partida* y *Unirse*. Nombre de cada jugador: el del perfil o, si no hay, "J1"/"J2".

| Paso | Contenido | Pruebas nuevas | Unity / fuera |
|---|---|---|---|
| 1 ✅ | `NetProtocol` (19 tipos de mensaje: en el paso 5 se añadieron `RallyVoid` y los indicadores de poderes consumidos en `HitLaunch`) y `NetConfig` + `net.json` | 6 | 137 / 133 |
| 2 ✅ | `ClockSync`: reloj compartido en ticks | 3 | 140 / 136 |
| 3 ✅ | `BallSync`: física en ticks fijos, aplicar estado con adelanto, ralentización por ticks | 4 | 144 / 140 |
| 4 ✅ | `HostArbiter` + `RallyReferee.CopyFrom` | 5 | 149 / 145 |
| 5 ✅ | `NetMatchSession` + `LoopbackLink`: dos móviles simulados con latencia y pérdidas, **con poderes** (ralentización y escudo incluidos; mismo marcador y mismo estado de poderes en los dos), corte corto, corte largo y **pausa con `ResumeAt`** (mismo tick y misma foto de la pelota). Aquí va la lógica de `PowerRequest`/`PowerState` y `OrbState` | 5 | 154 / 150 |
| 6 ✅ | `ViewMapping`, cámara del invitado girada 180°, pantalla completa para el jugador local, HUD de un marco | 3 | 157 / 153 |
| 7a ✅ | `SessionStateMachine` (estados de la conexión, apretón de manos, reconexión con código de sesión, plazo y anulación) y **`LanRewards`** (monedas y logros con el resultado del anfitrión; anulada no da nada; abandono = victoria del que se queda; el que sale no cobra el partido, pero sí los logros ya alcanzados). `lanWin`/`lanLoss` en `balance.json`, `lanMatches`/`lanWins` en las estadísticas. Compatibilidad confirmada: en Unity 6.6, Unity Transport y Multiplayer Play Mode son paquetes del núcleo con la versión fijada por el editor | 6 | 163 / 159 |
| 7b | Unity Transport (canal fiable y ordenado + canal no fiable; latencia y pérdida simuladas con `net.json`), `NetMatchBridge` en `MatchController`, poderes y orbes con el juego, segundo plano, resultados y `LanRewards` con el jugador local, pantalla mínima de IP manual | 0 | 163 / 159 |
| 8 | Búsqueda (broadcast general y de subred, IP y máscara con `NetworkInterface` de Java, también en la zona wifi compartida), pantallas LAN, permisos de Android, README | 2 | 165 / 161 |

**Decisiones tomadas en el paso 5 que el paso 7 debe respetar:**
- **`BasedOn` en cada estado de pelota:** un móvil ignora un estado basado en un lanzamiento anterior al suyo, para que un mensaje calculado antes de conocer un golpe no lo borre. La foto de la pausa es la excepción (decide el anfitrión).
- **`StartTick`:** nadie saca hasta que el reloj del invitado está sincronizado y se llega al tick de inicio, que viaja en `MatchSetup`. Sin esto, el primer saque del invitado llegaba con el reloj sin sincronizar.
- **Poderes antes que el punto:** el anfitrión envía `PowerState` antes de `PointResult`, porque el último punto cierra la sesión.
- **`RallyVoid`:** tras una reconexión, el anfitrión anula el punto en juego y los dos reanudan en el mismo tick con el mismo sacador.
- **`HitLaunch` lleva los poderes consumidos** (curva, súper remate, especial), para que el anfitrión los descuente cuando golpea el invitado.
- **Escudo:** lo decide el anfitrión con su pelota, igual que `MatchController` (segundo bote y muro detrás del jugador), y lo envía con `ShieldSave`.
- **`LoopbackLink`** vive en la carpeta de pruebas: no entra en el juego.
- Comprobación extra fuera de las pruebas: 60 partidos simulados (30 con poderes) con semillas distintas terminaron todos con el mismo marcador y 0 puntos leídos de forma distinta.

**Hecho en el paso 6 y pendiente para el paso 7:**
- La pantalla de resultados y `Rewards` dan por hecho que, contra la CPU, la persona es el J1. Con "Ver como invitado" (solo editor) el resultado sale como "¡GANA EL JUGADOR 1/2!" y cuenta como partida de dos jugadores. En el paso 7 los resultados y `LanRewards` usarán el jugador local (`MatchController.SingleLocalPlayer`).
- `MatchController.RivalName` queda listo para el nombre del rival en LAN.


Archivos nuevos: `Net/NetProtocol`, `Net/ClockSync`, `Net/BallSync`, `Net/HostArbiter`, `Net/NetConfig` + `net.json`, `Net/LanTransport`, `Net/LanDiscovery`, `Net/AndroidMulticastLock`, `Net/NetSession`, `Net/NetMatchBridge`, `UI/LanScreens`, `Editor/AndroidNetPermissions`. Cambios en `MatchController`, `MatchHud` (ping y "esperando al rival"), `CameraDirector`, menú, `Packages/manifest.json`, recompensas, README y TESTING.

**Puntos añadidos al aprobar la fase 1:**

1. **Entrada táctil:** en LAN cada móvil tiene un solo jugador local, que debe usar **toda la pantalla** en lugar de la mitad que asigna hoy `TouchZoneRouter`.
2. **Dirección del golpe en el invitado:** con la cámara girada 180°, la pelota debe ir hacia el lado al que el jugador mueve la pala **en su pantalla**. Una prueba para el anfitrión y otra para el invitado.
3. **Descubrimiento:** anuncio también a la broadcast de la subred (ver arriba). Prueba a mano: un móvil comparte su zona wifi y el otro se conecta a ella, sin router.

**Pruebas previstas:** `NetProtocolTests`, `ClockSyncTests`, `BallSyncTests` (dos simulaciones con una diferencia de 1e-4 m/s que divergen y quedan idénticas tras el siguiente `HitLaunch`; igual resultado a 60 y 120 Hz; el adelanto coincide con el otro móvil), `HostArbiterTests`, `LoopbackRallyTests` (enlace falso con 30 ms, variaciones y un 5 % de pérdida: mismo marcador; desconexión, pausa y anulación) y las dos pruebas de dirección del golpe.

**Pruebas a mano:**
- búsqueda en la lista, IP escrita a mano y red de invitados;
- zona wifi compartida desde un móvil, sin router;
- rechazo por versión;
- peloteos largos y ping estable;
- punto en el borde y golpe al límite con el mismo marcador en los dos móviles;
- los 7 poderes desde cada móvil;
- pausa y reanudación desde cada móvil;
- segundo plano;
- desconexión con reconexión y sin ella;
- abandono;
- `MulticastLock` suelto fuera de la búsqueda.
