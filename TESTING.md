# Pruebas

Este documento separa lo que **se ha ejecutado de verdad** de lo que **queda pendiente** porque necesita Unity o un móvil.

## 1. Pruebas ejecutadas (fuera de Unity)

El proyecto se creó en un entorno Linux sin Unity. Para no entregar código sin comprobar, se hizo lo siguiente con el compilador de C# de Mono 6.8. Las herramientas están en `Tools/validation` (no forman parte del juego y Unity no las compila).

### 1.1 Pruebas automáticas: 73 de 73 superadas

Las pruebas están en `Assets/Scripts/Tests/EditMode` (NUnit, ensamblado `Brooklyneir.Tests.EditMode`). Se ejecutaron con Mono sobre el código real del juego, usando una implementación funcional de `Vector3`/`Mathf` y un sustituto mínimo de NUnit con los mismos nombres:

| Grupo | Pruebas | Qué garantizan |
|---|---|---|
| Física de la pelota (7) | bote con el coeficiente de restitución, una bola a 25 m/s no atraviesa la mesa, red que frena una bola baja, bola alta que pasa, toque en la cinta que sigue, caída al suelo hasta pararse, curva por efecto lateral | Rebotes, red y ausencia de atravesamientos |
| Resolvedor de golpes (2) | 60 golpes aleatorios caen a menos de 2 cm del objetivo y salvan la red; el aplanado reduce la altura sobre la red | La pelota va donde el modelo decide |
| Modelo de golpes (7) | clasificación por gesto (normal, rápido, remate, defensivo), el J2 golpea hacia −z, >90 % de golpes normales centrados entran, más potencia = vuelo más corto, efecto lateral, súper remate limitado y devolvible, la curva dobla hacia el objetivo | Controles coherentes y justos |
| Árbitro del punto (7) | bote único obligatorio, doble bote, fallo tras bote, bola fuera, red + bote propio, red que entra, eventos tras el final ignorados | Reglas del punto |
| Marcador (7) | victoria a 11 por 2, empate final, sin "ganar por 2", rotación de saque cada 2 y cada punto en el empate, sets y primer saque alterno, límite de tiempo y punto de oro, nada tras el final | Puntuación consistente |
| Configuración (2) | corrige opciones contradictorias, sets necesarios | Partida personalizada |
| Poderes (11) | un poder guardado, el aleatorio da poderes reales, los temporizadores terminan, enfriamiento, congelación limitada, el escudo bloquea la congelación, el fuego se apaga al devolverlo, golpes armados que se consumen, no acumulables, todo se limpia al terminar el punto y al reiniciar, golpe especial | Los 7 poderes y sus límites |
| Economía (5), Guardado (3), Torneo (5), Logros (1), Recompensas (5), Raquetas (2) | sin saldo negativo, compras válidas, mesa de torneo no vendible, saneado de guardados corruptos o antiguos, cuadro reproducible con semilla, campeón y eliminación, logros pagados una vez, estrellas de entrenamiento pagadas una vez, estadísticas que cambian valores reales | Progresión |
| Entrada e IA (8) | dos dedos simultáneos a dos jugadores, un segundo dedo en la misma zona se ignora, toques sobre botones no mueven raquetas, contra la CPU toda la pantalla es del J1, el dedo conserva dueño al cruzar el centro; la IA espera su tiempo de reacción, falla menos en difícil y golpea hacia el lado correcto | Multitáctil local e IA |
| Integración (1) | 60 peloteos CPU contra CPU con física, golpes, árbitro e IA reales: todos terminan y hay intercambios | Las piezas encajan |

Para ejecutarlas en Unity: *Window → General → Test Runner → EditMode → Run All*.

### 1.2 Simulación de equilibrado (400 peloteos por dificultad, CPU contra CPU)

| Dificultad | Golpes por peloteo | Altura media del vuelo sobre la mesa | Margen medio sobre la red |
|---|---|---|---|
| Fácil | 3,2 | 0,53 m | 0,27 m |
| Normal | 7,4 | 0,48 m | 0,25 m |
| Difícil | 15,3 | 0,44 m | 0,22 m |

Con estos datos se ajustaron gravedad, velocidades, efecto y márgenes (`balance.json`). Contra una persona los peloteos serán más cortos: la IA no tiene ayudas.

### 1.3 Comprobación de tipos del proyecto completo

Todo el código (juego, interfaz, editor) se compiló sin errores contra declaraciones de la API de Unity 6 escritas a mano (`Tools/validation/stubs`), en cuatro variantes: Input System / Input Manager clásico × editor / Android. Esto detectó y corrigió errores reales (por ejemplo, argumentos de tipo incorrecto en el generador de escenarios).

**Límite:** esas declaraciones las escribí yo a partir de la documentación de Unity. Si alguna firma no coincide exactamente con Unity 6.3, el error solo aparecerá al abrir el proyecto en el editor.

### 1.4 Mallas y modelos

- Las 17 mallas procedurales (esfera, caja biselada, cilindros, cápsulas, toro, discos, anillos, extrusiones de pala, torneados, cúpula del cielo…) se generaron y se comprobó que **todos los triángulos tienen la orientación correcta** y que la caja biselada queda sobre su superficie (error < 0,0001 m). Una orientación invertida haría invisibles las caras en el móvil.
- Los constructores reales de mesa, raquetas, manos y los 6 escenarios se ejecutaron con un motor mínimo funcional y se renderizaron con un renderizador propio (`Docs/previews`). Así se detectaron y corrigieron tres problemas: las palas se veían de canto desde la cámara cenital (ahora se muestran con la goma hacia arriba y un 30 % más grandes), unos arcos de neón y focos tapaban la mesa, y la mesa de la Arena de Leyendas se confundía con el suelo.
- Esas imágenes son aproximadas (sin texturas detalladas, sombras ni orden de profundidad real); en Unity se verán con la iluminación URP.

### 1.5 Recursos

- 28 efectos y 2 músicas WAV generados, comprobados (duración, picos) y con los mismos nombres que espera `AudioManager`; si faltara alguno, se sintetiza uno de respaldo.
- Icono de 1024 px revisado visualmente.
- `balance.json` generado desde los valores por defecto del código y validado como JSON.

## 2. Pruebas que no se han podido hacer aquí

- Abrir el proyecto en Unity 6.3, resolver paquetes y compilar con el compilador de Unity.
- Ejecutar la configuración automática (URP, materiales, escena, ajustes de Android).
- Ver el juego en movimiento, la interfaz, los efectos de partículas y la cámara.
- Compilar la APK con IL2CPP e instalarla.
- Medir FPS en un móvil real.

## 3. Pruebas manuales en el móvil (pendientes)

Marca cada punto al probarlo. Si algo falla, `adb logcat -s Unity` muestra la causa.

| # | Prueba | Cómo | Resultado |
|---|---|---|---|
| 1 | Arranca correctamente | Abrir la app: título animado sobre la arena | ☐ |
| 2 | El menú permite navegar | Entrar y salir de cada pantalla; botón Atrás vuelve | ☐ |
| 3 | Siempre en horizontal | Girar el móvil en todas las direcciones | ☐ |
| 4 | Dos dedos a la vez | Partida rápida: los dos jugadores mueven y golpean a la vez | ☐ |
| 5 | La raqueta no sale de su zona | Arrastrar al máximo en todas las direcciones | ☐ |
| 6 | Botes correctos | Observar botes en la mesa y fuera | ☐ |
| 7 | Red consistente | Golpes bajos y rápidos: la red frena; la cinta deja pasar a veces | ☐ |
| 8 | Puntos bien contados | Comparar el marcador con lo que ocurre | ☐ |
| 9 | El partido termina | Llegar a los puntos configurados; pantalla de resultados | ☐ |
| 10 | Cada poder funciona | Recoger y usar los 7 (partida personalizada, frecuencia alta) | ☐ |
| 11 | Los efectos terminan | Ver que cada indicador se apaga | ☐ |
| 12 | Las raquetas cambian el juego | Iron Wall frente a Rooftop Viper: control frente a potencia | ☐ |
| 13 | Mesas desbloqueadas seleccionables | Comprar Azotea Nocturna y jugar en ella | ☐ |
| 14 | Las monedas se guardan | Ganar monedas, cerrar la app del todo, abrir | ☐ |
| 15 | El progreso se conserva | Desbloqueos, logros, estrellas y torneo tras reiniciar | ☐ |
| 16 | Contra la máquina | Fácil, normal y difícil: diferencia notable | ☐ |
| 17 | El torneo progresa | Ganar cuartos y ver el cuadro; perder y ver el campeón | ☐ |
| 18 | Partida personalizada | 3 puntos, 3 sets, 2 minutos, sin poderes: se respeta | ☐ |
| 19 | Pausa y reanudación | Botón de pausa y botón Atrás | ☐ |
| 20 | Cambio de aplicación | Ir a inicio o recibir una llamada a mitad de punto: queda en pausa | ☐ |
| 21 | Distintos tamaños de pantalla | Móvil con notch y tableta: botones visibles y nada cortado | ☐ |
| 22 | Compilación Android | APK de depuración generada e instalada | ☐ |
| 23 | Rendimiento | Calidad Media a ~60 FPS en un móvil medio; si no, probar Baja | ☐ |
| 24 | Vibración | Remates con la vibración activada y sin ella | ☐ |
