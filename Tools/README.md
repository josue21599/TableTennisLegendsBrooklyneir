# Herramientas (no forman parte del juego)

Unity solo compila lo que está dentro de `Assets/`, así que esta carpeta no afecta al proyecto.

- `assets/gen_audio.py` — genera los 28 efectos y las 2 músicas (`python3 gen_audio.py ../../Assets/Resources/Audio`). Requiere numpy.
- `assets/gen_icon.py` — genera el icono (`python3 gen_icon.py ../../Assets/Art/Icon/app_icon.png`). Requiere Pillow.
- `validation/` — comprobaciones hechas sin Unity:
  - `stubs/` declaraciones de la API de Unity usadas para comprobar tipos con Mono (`mcs`), y una implementación funcional mínima de `Vector3`/`Mathf` + sustituto de NUnit para ejecutar las pruebas. `TransportStubs.cs` declara la parte de Unity Transport que usa `Net/LanTransport.cs`; para comprobarla, compila con `-define:BROOKLYNEIR_TRANSPORT`.
  - `TestRunner.cs` ejecuta las pruebas de `Assets/Scripts/Tests/EditMode`.
  - `Tune.cs` simula cientos de peloteos CPU contra CPU para equilibrar la física.
  - `SmashSweep.cs` mide la altura de las devoluciones de la CPU y el efecto de `smashMinHeight` (remates, golpes por punto y puntos que gana la CPU) contra un sustituto del jugador.
  - `GenBalance.cs` regenera `balance.json` a partir de los valores por defecto.
  - `meshcheck/` verifica la orientación de los triángulos de todas las mallas procedurales.
  - `render/` ejecuta los constructores de modelos con un motor mínimo y renderiza vistas previas.

Ejemplo (Linux con Mono, desde `Assets/Scripts`):

```bash
mcs -out:/tmp/tests.exe ../../Tools/validation/stubs/UnityMath.cs ../../Tools/validation/stubs/NUnitShim.cs \
    ../../Tools/validation/TestRunner.cs $(cat ../../Tools/validation/pure_files.txt) Tests/EditMode/*.cs
mono /tmp/tests.exe
```
