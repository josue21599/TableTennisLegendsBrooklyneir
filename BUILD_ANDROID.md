# Compilar e instalar en Android

Esta guía va desde cero hasta la APK instalada en tu móvil. **La compilación no se ha podido verificar** en el entorno donde se creó el proyecto (no tenía Unity), así que sigue los pasos en orden y, si algo falla, mira la sección 7.

## 1. Requisitos

| Componente | Versión |
|---|---|
| Unity Hub | Actual |
| Unity Editor | **6.3 LTS (6000.3.x)**, cualquier parche reciente |
| Módulo | **Android Build Support** con **OpenJDK** y **Android SDK & NDK Tools** (se instalan desde Unity Hub → Installs → ⚙ → Add modules) |
| Android Studio | Opcional (solo para trabajar con el proyecto Gradle exportado) |
| Móvil | Android 8.0 (API 26) o superior, 64 bits (ARM64) |

Unity usa su propio SDK, NDK y JDK instalados por el módulo. No hace falta instalarlos aparte ni configurar rutas a mano.

## 2. Abrir el proyecto

1. Unity Hub → **Projects → Add → Add project from disk** → selecciona la carpeta `TableTennisLegendsBrooklyneir` (la que contiene `Assets` y `Packages`).
2. Si Hub pregunta por la versión, elige tu **6000.3.x**.
3. La primera importación tarda unos minutos (compila shaders de URP).
4. Al terminar, `ProjectSetup` se ejecuta solo y escribe en la consola: *"[TTLB] Proyecto configurado"*. Puedes repetirlo cuando quieras con **Brooklyneir → Configurar proyecto**.
5. Si aparece el aviso de *Active Input Handling* y Unity pide reiniciar, acepta. Tras reiniciar, ejecuta otra vez **Brooklyneir → Configurar proyecto** para confirmar.
6. Pulsa **Play** para probar en el editor.

La configuración aplica: id `com.brooklyneir.tabletennislegends`, nombre *Table Tennis Legends Brooklyneir*, versión 0.1.0 (código 1), horizontal en ambos sentidos, IL2CPP + ARM64, API mínima 26, target automático (la más alta instalada), color lineal, sin permiso de Internet y el icono `Assets/Art/Icon/app_icon.png`.

## 3. APK de depuración (la forma más rápida)

**Brooklyneir → Compilar APK de depuración.**

- Cambia la plataforma a Android si hace falta (la primera vez tarda).
- Genera `Builds/Android/TableTennisLegendsBrooklyneir-debug.apk` y abre la carpeta.

Equivalente manual: *File → Build Profiles* (o *Build Settings*) → Android → **Switch Platform** → marca *Development Build* → **Build**.

Desde la línea de comandos (Windows, ajusta la ruta del editor):

```bat
"C:\Program Files\Unity\Hub\Editor\6000.3.XX\Editor\Unity.exe" -batchmode -quit -projectPath "C:\ruta\TableTennisLegendsBrooklyneir" -buildTarget Android -executeMethod Brooklyneir.EditorTools.BuildTools.CommandLineDebugApk -logFile build.log
```

## 4. Instalar la APK en el móvil

**Opción A, sin cables:** copia la APK al móvil (USB, Drive, correo…), ábrela desde el gestor de archivos y permite *Instalar apps desconocidas* para esa app cuando Android lo pida.

**Opción B, con ADB** (más rápida para probar varias veces):

1. En el móvil: *Ajustes → Información del teléfono →* toca 7 veces *Número de compilación* → vuelve a *Ajustes → Opciones de desarrollador →* activa **Depuración USB**.
2. Conecta el cable y acepta la huella del ordenador.
3. ADB viene con el módulo de Android de Unity: `…\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`.

```bat
adb devices
adb install -r Builds\Android\TableTennisLegendsBrooklyneir-debug.apk
```

También puedes usar **Build And Run** en Unity con el móvil conectado.

Para ver los registros del juego en el móvil: `adb logcat -s Unity` (los mensajes propios empiezan por `[TTLB]`).

## 5. Exportar a Android Studio

Unity **genera** un proyecto Gradle a partir del proyecto Unity. Android Studio no puede abrir directamente la carpeta del proyecto Unity, solo el proyecto exportado.

1. **Brooklyneir → Exportar proyecto para Android Studio** (o *Build Profiles → Android → Export Project*).
2. Se crea `Builds/AndroidStudioProject/`.
3. Android Studio → **Open** → selecciona esa carpeta (la que contiene `settings.gradle`).
4. Deja que sincronice Gradle. Si pide JDK, usa el de Unity (`…\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK`) o el que trae Android Studio.
5. APK de depuración: *Build → Build Bundle(s) / APK(s) → Build APK(s)*, o en terminal `gradlew assembleDebug`. La APK queda en `launcher/build/outputs/apk/debug/`.

Cada vez que cambies algo en Unity, vuelve a exportar: los cambios hechos a mano en el proyecto exportado se sobrescriben.

## 6. APK de lanzamiento firmada con tu clave

Las claves y contraseñas **nunca** se guardan en el proyecto.

1. *Edit → Project Settings → Player → Android → Publishing Settings → Keystore Manager*.
2. **Create New → Anywhere**, guarda el `.keystore` **fuera** de la carpeta del proyecto (y haz copia de seguridad: sin ella no podrás actualizar la app).
3. Crea un alias con contraseña. Marca **Custom Keystore** y selecciona tu keystore y alias.
4. Sube `Version` y `Bundle Version Code` en cada publicación.
5. **Brooklyneir → Compilar APK de lanzamiento (firmada)** → `Builds/Android/TableTennisLegendsBrooklyneir-release.apk`.

Unity pide las contraseñas en cada sesión del editor; no se escriben en disco con el proyecto. Para Google Play se usa un `.aab`: marca *Build App Bundle* en Build Profiles.

## 7. Errores habituales

| Síntoma | Solución |
|---|---|
| *Android Build Support no instalado* / no aparece Android | Unity Hub → Installs → ⚙ → Add modules → Android Build Support + OpenJDK + SDK & NDK Tools. Reinicia Unity. |
| *Unable to locate Android SDK / NDK / JDK* | Edit → Preferences → External Tools → marca las casillas *installed with Unity* de JDK, SDK, NDK y Gradle. Si estaban desmarcadas con rutas propias, vuelve a las de Unity. |
| *SDK Platform / Build-tools not found* o licencias | Deja *Target API Level* en *Automatic (highest installed)*. Si Unity ofrece actualizar el SDK, acepta y acepta las licencias. |
| Error de Gradle (*Gradle build failed*) | Mira la primera línea de error en la consola. Comprueba que Gradle sea el *installed with Unity*. Borra `Library/Bee` y `Temp` con Unity cerrado y vuelve a compilar. Evita rutas con espacios raros o tildes en la carpeta del proyecto. |
| *Unable to resolve package* / versión de paquete | Los paquetes del núcleo (URP, uGUI, Test Framework) se fijan solos a la versión del editor. Si `com.unity.inputsystem 1.17.0` no está en tu registro, ábrelo en *Window → Package Manager* y pulsa *Update* a la versión que ofrezca. Alternativa robusta: crea un proyecto nuevo con la plantilla **Universal 3D** en Hub y copia dentro la carpeta `Assets` de este proyecto. |
| Materiales rosas | Ejecuta **Brooklyneir → Configurar proyecto**. Comprueba en *Project Settings → Graphics* que *Default Render Pipeline* es `TTLB_URP`. |
| *You are trying to read Input using the UnityEngine.Input class…* | El proyecto quedó solo con el Input System sin el código preparado. Ejecuta *Configurar proyecto*, o pon *Project Settings → Player → Active Input Handling* en **Both** y reinicia. El juego usa el Input System cuando está activo. |
| El toque no mueve la raqueta | Toca dentro de tu mitad (J1 abajo, J2 arriba) y fuera de los botones. Prueba a subir la *Sensibilidad* en Ajustes. |
| IL2CPP tarda mucho o falla | La primera compilación IL2CPP es lenta (5-15 min). Si falla por memoria, cierra otros programas. Para iterar más rápido, cambia temporalmente *Scripting Backend* a Mono (solo depuración; las tiendas exigen ARM64 con IL2CPP). |
| *Requested API level below minimum* | La API mínima está en 26 (Android 8.0). Tu móvil debe tener Android 8 o superior. |
| La app se instala pero se cierra al abrir | `adb logcat -s Unity AndroidRuntime` y busca la primera excepción. Envía ese texto para diagnosticarlo. |
| Va a pocos FPS | Ajustes → Gráficos → **Bajo** (70 % de resolución, sin sombras, sin público). |

## 8. Lista de comprobación en el móvil

La lista completa de pruebas manuales está en [TESTING.md](TESTING.md#3-pruebas-manuales-en-el-móvil-pendientes). Prioriza: dos dedos a la vez moviendo ambas raquetas, que la pantalla no gire a vertical, pausa al recibir una llamada o salir de la app, y que el progreso se conserve al cerrar y volver a abrir.
