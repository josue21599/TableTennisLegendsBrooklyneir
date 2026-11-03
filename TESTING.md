# Pruebas

Este documento recoge las pruebas automáticas, las comprobaciones hechas fuera de Unity y las pruebas manuales en un móvil real.

**Resumen:** 157 pruebas automáticas en el Test Runner de Unity y las 24 pruebas manuales superadas en un **OPPO Reno11 F con Android 16**.

## 1. Pruebas ejecutadas (fuera de Unity)

El proyecto se creó en un entorno Linux sin Unity. Para no entregar código sin comprobar, se hizo lo siguiente con el compilador de C# de Mono 6.8. Las herramientas están en `Tools/validation` (no forman parte del juego y Unity no las compila).

### 1.1 Pruebas automáticas: 157

Las pruebas están en `Assets/Scripts/Tests/EditMode` (NUnit, ensamblado `Brooklyneir.Tests.EditMode`). En el Test Runner de Unity son **157**. Fuera de Unity, con Mono y un sustituto mínimo de NUnit con los mismos nombres, se ejecutan **153**: las 4 de `UiFontTests` necesitan cargar la fuente real y solo funcionan dentro de Unity.

| Archivo | Pruebas | Qué cubren |
|---|---|---|
| `PhysicsTests` | 26 | Física de la pelota (botes, red, cinta, sin atravesar la mesa a 25 m/s, efecto lateral), resolvedor de golpes y modelo de golpes: tipo según la dirección del gesto, desvío lateral acotado y proporcional, profundidad, determinismo, súper remate devolvible y curva |
| `RulesTests` | 16 | Árbitro del punto (bote, doble bote, fuera, red, cinta), marcador (11 por 2, empates, rotación de saque, sets, tiempo y punto de oro) y configuración de partida |
| `PowerUpTests` | 11 | Los 7 poderes: duraciones, enfriamientos, interacciones y limpieza al final del punto |
| `ProgressionTests` | 16 | Economía, guardado corrupto o antiguo, torneo reproducible con semilla, logros y catálogo de raquetas |
| `RewardsTests` | 5 | Recompensas de partido y estrellas de entrenamiento pagadas una sola vez |
| `InputAndAITests` | 9 | Dos dedos simultáneos, prioridad de la interfaz, tiempos de reacción de la IA e integración de 60 peloteos CPU contra CPU |
| `PracticeWallTests` | 18 | Pared de práctica: rebote dirigido, sin atravesar la pared a 10–40 m/s, reglas de pérdida, racha, sesión sin fin y determinismo |
| `UiConfigTests` | 4 | Tamaños de texto fijos, configuración inválida reparada y logotipo apilado con las métricas de la fuente |
| `UiFontTests` | 4 | Solo en Unity: fuente única, respaldo si falta y todos los caracteres del juego (tildes, ñ, ¡ ¿) en la fuente |
| `AnimationTests` | 10 | Golpes de la mano según su tipo, reposo, celebración antes del siguiente saque, miradas, reacciones y ola del público |
| `ControlSetupTests` | 3 | Quién controla cada pala: los mismos controles que antes en todos los modos, demo y entrenamiento, y jugador en red solo en partida rápida y personalizada |
| `RemotePaddleBufferTests` | 4 | Pala del rival en red: interpolación, paquetes repetidos o desordenados descartados, extrapolación limitada y salto limpio tras un hueco |
| `ShotDiagnosticsTests` | 3 | Texto de depuración del golpe: dice qué condición faltó para remate, efecto o rápido, con las mismas condiciones que la clasificación real |
| `MousePointerTests` | 2 | Ratón del editor: un clic rápido dentro de un fotograma también se suelta, y el ratón nunca se queda enganchado a un jugador |
| `NetProtocolTests` | 6 | Multijugador LAN: los 19 mensajes van y vuelven exactos (bit a bit), paquetes cortados, con bytes de más o basura rechazados sin excepciones, versión de protocolo distinta, textos recortados sin romper letras, un millón de paquetes sin recolección de basura, y `net.json` validado |
| `ClockSyncTests` | 3 | Reloj compartido LAN: desfase exacto con retardo simétrico; con retardos irregulares y picos de wifi (200 partidas simuladas) el error queda por debajo de la mitad de la mejor ida y vuelta; si el desfase cambia a mitad de partida, converge sin que el tick retroceda nunca |
| `BallSyncTests` | 4 | Pelota en LAN: dos simulaciones con una diferencia de 1e-4 m/s divergen durante el vuelo y quedan idénticas bit a bit tras el siguiente golpe; el mismo estado da la misma pelota a 60, 90 y 120 Hz; el adelanto deja la pelota donde la tiene el otro móvil, con sus botes, y el dibujo se suaviza; la ralentización por ticks coincide en los dos móviles |
| `HostArbiterTests` | 5 | Árbitro del anfitrión en LAN: un golpe del invitado dentro del margen anula el punto pendiente; uno tardío no; un golpe posterior al segundo bote (o anterior al primero) se rechaza; la copia del árbitro lo restaura todo y los puntos en el campo del anfitrión son inmediatos; en 50 partidos el marcador del invitado, a través del protocolo real, es siempre el del anfitrión |
| `NetSessionTests` | 5 | Dos móviles simulados (anfitrión a 60 fps, invitado a 90 fps, relojes distintos) conectados por un enlace falso con 30 ms de latencia, ±15 ms de variación y un 5 % de pérdida, usando la misma `NetMatchSession` que el juego: un partido completo termina con el mismo marcador y sin ningún punto leído distinto; con poderes (4 partidos, con ralentización y salvadas de escudo) terminan además con el mismo estado de poderes; un corte de 2 s pausa a los dos, anula el punto y la partida sigue; un corte más largo que el plazo de reconexión anula la partida en los dos; tras una pausa, `ResumeAt` reanuda a los dos en el mismo tick y con la misma pelota bit a bit |
| `ViewTests` | 3 | Vista del invitado LAN: el único jugador local usa toda la pantalla (anfitrión e invitado) y la cámara del invitado gira 180°; con el modelo de golpe real, la pelota va hacia el lado de la pantalla al que se mueve la pala, para el anfitrión y para el invitado con la cámara girada; `ViewMapping` coincide con las cuentas de la cámara |

Para ejecutarlas en Unity: *Window → General → Test Runner → EditMode → Run All*.

### 1.2 Simulación de equilibrado (400 peloteos por dificultad, CPU contra CPU)

Valores actuales, con `smashMinHeight` = 0,32 m:

| Dificultad | Golpes por peloteo | Altura media del vuelo sobre la mesa | Margen medio sobre la red |
|---|---|---|---|
| Fácil | 3,83 | 0,55 m | 0,29 m |
| Normal | 8,49 | 0,51 m | 0,26 m |
| Difícil | 16,49 | 0,46 m | 0,23 m |

Con estos datos se ajustaron gravedad, velocidades, efecto y márgenes (`balance.json`). Contra una persona los peloteos serán más cortos: la IA no tiene ayudas.

### 1.2.1 Altura mínima del remate (`Tools/validation/SmashSweep.cs`)

El remate exige que la bola llegue a una altura mínima sobre la mesa (`smashMinHeight`). Con 0,38 m, contra la CPU casi nunca se podía rematar. `SmashSweep` enfrenta a un sustituto del jugador (IA de nivel normal con el ruido de golpe humano) con la CPU de cada dificultad, con las mismas reglas de remate para los dos, en 2000 puntos por casilla (margen de unos ±2 puntos).

Altura de las devoluciones de la CPU al llegar a la pala del jugador: mediana 0,36 / 0,30 / 0,26 m y percentil 75 de 0,39 / 0,37 / 0,28 m (fácil / normal / difícil).

| `smashMinHeight` | Bolas rematables (F/N/D) | Remates CPU por punto | Remates jugador por punto | Golpes por punto | Puntos que gana la CPU |
|---|---|---|---|---|---|
| 0,38 (anterior) | 28 / 17 / 7 % | 0,02 / 0,10 / 0,22 | 0,07 / 0,10 / 0,14 | 6,4 / 9,5 / 13,2 | 32,7 / 47,3 / 61,6 % |
| **0,32 (actual)** | 75 / 45 / 9 % | 0,09 / 0,28 / 0,60 | 0,34 / 0,34 / 0,16 | 6,4 / 9,6 / 12,8 | 33,5 / 45,5 / 60,0 % |

Criterio aplicado: bajar la altura siempre que el porcentaje de puntos que gana la CPU no cambie más de 2 puntos en ninguna dificultad (cambios: +0,8 / −1,8 / −1,6). Los remates del jugador se multiplican por unos 3–5 en fácil y normal, y el equilibrio se mantiene.

### 1.3 Comprobación de tipos del proyecto completo

Todo el código (juego, interfaz, editor) se compiló sin errores contra declaraciones de la API de Unity 6 escritas a mano (`Tools/validation/stubs`), en cuatro variantes: Input System / Input Manager clásico × editor / Android. Esto detectó y corrigió errores reales (por ejemplo, argumentos de tipo incorrecto en el generador de escenarios).

**Límite:** esas declaraciones las escribí yo a partir de la documentación de Unity. El proyecto ya se ha abierto y compilado en Unity 6.6, así que esta comprobación queda como red de seguridad para el código nuevo.

### 1.4 Mallas y modelos

- Las 17 mallas procedurales (esfera, caja biselada, cilindros, cápsulas, toro, discos, anillos, extrusiones de pala, torneados, cúpula del cielo…) se generaron y se comprobó que **todos los triángulos tienen la orientación correcta** y que la caja biselada queda sobre su superficie (error < 0,0001 m). Una orientación invertida haría invisibles las caras en el móvil.
- Los constructores reales de mesa, raquetas, manos y los 6 escenarios se ejecutaron con un motor mínimo funcional y se renderizaron con un renderizador propio (`Docs/previews`). Así se detectaron y corrigieron tres problemas: las palas se veían de canto desde la cámara cenital (ahora se muestran con la goma hacia arriba y un 30 % más grandes), unos arcos de neón y focos tapaban la mesa, y la mesa de la Arena de Leyendas se confundía con el suelo.
- Esas imágenes son aproximadas (sin texturas detalladas, sombras ni orden de profundidad real); en Unity se verán con la iluminación URP.

### 1.5 Recursos

- 28 efectos y 2 músicas WAV generados, comprobados (duración, picos) y con los mismos nombres que espera `AudioManager`; si faltara alguno, se sintetiza uno de respaldo.
- Icono de 1024 px revisado visualmente.
- `balance.json` generado desde los valores por defecto del código y validado como JSON.

## 2. Pruebas en Unity y en el móvil

Lo que no se podía comprobar fuera de Unity ya está hecho: el proyecto se abre y compila en **Unity 6.6**, la configuración automática funciona, el juego se ha visto en movimiento (interfaz, efectos y cámara), la APK se compila con IL2CPP y se ha probado en un móvil real.

## 3. Pruebas manuales en el móvil: 24 de 24 superadas

**Dispositivo:** OPPO Reno11 F · Android 16 · pantalla con agujero para la cámara.

Para repetirlas en otro móvil: si algo falla, `adb logcat -s Unity` muestra la causa.

| # | Prueba | Cómo | Resultado |
|---|---|---|---|
| 1 | Arranca correctamente | Abrir la app: título animado sobre la arena | ✅ |
| 2 | El menú permite navegar | Entrar y salir de cada pantalla; botón Atrás vuelve | ✅ |
| 3 | Siempre en horizontal | Girar el móvil en todas las direcciones | ✅ |
| 4 | Dos dedos a la vez | Partida rápida: los dos jugadores mueven y golpean a la vez | ✅ |
| 5 | La raqueta no sale de su zona | Arrastrar al máximo en todas las direcciones | ✅ |
| 6 | Botes correctos | Observar botes en la mesa y fuera | ✅ |
| 7 | Red consistente | Golpes bajos y rápidos: la red frena; la cinta deja pasar a veces | ✅ |
| 8 | Puntos bien contados | Comparar el marcador con lo que ocurre | ✅ |
| 9 | El partido termina | Llegar a los puntos configurados; pantalla de resultados | ✅ |
| 10 | Cada poder funciona | Recoger y usar los 7 (partida personalizada, frecuencia alta) | ✅ |
| 11 | Los efectos terminan | Ver que cada indicador se apaga | ✅ |
| 12 | Las raquetas cambian el juego | Iron Wall frente a Rooftop Viper: control frente a potencia | ✅ |
| 13 | Mesas desbloqueadas seleccionables | Comprar Azotea Nocturna y jugar en ella | ✅ |
| 14 | Las monedas se guardan | Ganar monedas, cerrar la app del todo, abrir | ✅ |
| 15 | El progreso se conserva | Desbloqueos, logros, estrellas y torneo tras reiniciar | ✅ |
| 16 | Contra la máquina | Fácil, normal y difícil: diferencia notable | ✅ |
| 17 | El torneo progresa | Ganar cuartos y ver el cuadro; perder y ver el campeón | ✅ |
| 18 | Partida personalizada | 3 puntos, 3 sets, 2 minutos, sin poderes: se respeta | ✅ |
| 19 | Pausa y reanudación | Botón de pausa y botón Atrás | ✅ |
| 20 | Cambio de aplicación | Ir a inicio o recibir una llamada a mitad de punto: queda en pausa | ✅ |
| 21 | Distintos tamaños de pantalla | Móvil con notch y tableta: botones visibles y nada cortado | ✅ |
| 22 | Compilación Android | APK de depuración generada e instalada | ✅ |
| 23 | Rendimiento | Calidad Media a ~60 FPS en un móvil medio; si no, probar Baja | ✅ |
| 24 | Vibración | Remates con la vibración activada y sin ella | ✅ |
