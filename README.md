# UTEC MetaQuestVR — Estación de Bomberos VR

Proyecto universitario de realidad virtual (UTEC) desarrollado en **Unity 2022.3** con el **Meta XR SDK**.
La escena principal es una estación de bomberos interactiva con:

- **Puerta de entrada automática por proximidad**: se abre al acercarse y se cierra al alejarse.
- **Portón elevadizo (basculante)** con brazos hidráulicos, que se abre/cierra con un **botón de presión** (poke con la mano o la punta del controlador).
- **Locomoción VR**: caminar con joystick + teletransporte (Interaction SDK / Building Blocks).
- Interacciones con manos y controles (poke, ray, grab) sobre el rig de interacción moderno.
- **Juego "Emergencia en la estación"**: misión con tareas, HUD, sonidos, vibración, puntaje y **subida al camión de bomberos** (ver abajo).

## Cómo probar

**Requisitos**: Unity 2022.3.x con el Meta XR SDK (se instala solo al abrir el proyecto vía UPM).
Escena: `Assets/Scenes/SampleScene.unity`.

**Opción A — Con visor (Quest Link):**
1. Conecta el Meta Quest por Quest Link.
2. Dale Play en Unity. Te moverás con los joysticks y con tracking real.

**Opción B — Sin visor (Meta XR Simulator):**
1. Abre la aplicación **Meta XR Simulator** (debe estar instalada y corriendo).
2. En Unity: menú **`Meta > Meta XR Simulator > Activate`** (la activación es por sesión de Unity: se pierde al cerrar Unity, basta volver a activarla).
3. Dale **Play**. Los controles se usan **en la ventana del simulador** (no en la pestaña Game):
   - `WASD` para moverte, botón derecho del mouse para mirar.
   - `Inputs → Global Input Settings` para elegir manos o controles simulados.

## Juego interactivo: "Emergencia en la estación"

### Modo directo: manejar el camión (activo por defecto)

Al darle Play vas directo al camión, sin tareas previas. Sigue la flecha amarilla hasta el **círculo junto a la puerta del conductor**, quédate ahí un momento (o presiona **A**) y subes a la cabina. El portón se abre solo.

| Acción | Control | Teclado (editor) |
|---|---|---|
| Avanzar / retroceder | Stick izquierdo arriba / abajo | `W`/`S` o flechas |
| Girar | Stick izquierdo o derecho a los lados | `A`/`D` o flechas |
| Sirena on/off | **A** | `H` |
| Bajar del camión | **B** (o mantener el pellizco 2 s) | `Q` |

Al soltar el stick, el camión frena solo. Choca con las paredes y con el portón cerrado (se detiene, suena y vibra). El HUD muestra la velocidad en km/h y el estado de la sirena y del portón. Para manejar se necesitan los controles: con manos solo se puede subir y bajar.

Para volver a la **misión completa** (portón, chaleco, extintor, puntaje), desmarca `Modo Directo` en el objeto `JuegoBomberos` de la escena.

### Misión completa (modo directo desactivado)

Al darle Play aparece un panel de bienvenida con los controles. Presiona **A** (o pellizca) para empezar —o espera 15 s y empieza solo—. Suena la alarma y hay que completar las tareas **lo más rápido posible**:

1. **Abrir el portón de la cochera**: toca el botón rojo con el dedo o con la punta del control.
2. **Ponerte el chaleco**: acerca la mano, agárralo con **GRIP/gatillo** (o pellizcando) y llévalo a tu pecho. Al soltarlo ahí, queda puesto.
3. **Cargar el extintor al camión**: agárralo, llévalo hasta el camión y suéltalo junto a él.
4. **Subir al camión de bomberos**: párate en el **círculo amarillo** junto a la puerta del conductor. El círculo se llena y subes a la cabina (o presiona **A** para subir al instante). Se encienden las balizas, la sirena y el motor.
   - **Bajar**: **B/Y** o mantener el pellizco 2 s.
   - Si intentas subir sin haber terminado tu equipo, el camión te avisa qué falta.

Al subir se muestra el **tiempo, el puntaje** (100 por tarea + bono por rapidez) y la **calificación** (EXCELENTE / MUY BIEN / COMPLETADO). Después de bajar, **A** reinicia el juego.

**Feedback al jugador**
- **Visual**: un panel que sigue tu mirada sin pegarse a ella (objetivo actual, lista de tareas, tiempo y puntaje), avisos grandes en verde, amarillo o rojo, una flecha flotante con la distancia sobre el objetivo, una pista de dirección ("detrás de ti, a 12 m"), objetos que brillan al acercar la mano y se ponen verdes cuando están en su destino, una luz roja de alarma intermitente, las balizas del camión y un círculo de abordaje que se va llenando.
- **Sonoro** (generado por código, sin archivos de audio): alarma, campana periódica, clic al tocar, sonido al agarrar, arpegio de éxito, zumbido de error, motor del portón, puerta neumática, sirena, motor del camión y fanfarria final.
- **Háptico**: vibración de los controles al pasar la mano por un objeto, al agarrarlo, al llegar a su destino, al completar tareas, al presionar el botón, durante el abordaje y en los errores.
- **Recordatorios**: si pasan 35 s sin avanzar, se vuelve a mostrar la pista de la tarea actual.

**Teclado (editor/simulador)**: `Espacio`/`E`/`Enter` = A, `Q`/`Backspace` = B, `R` = reiniciar al final.

**Implementación** (`Assets/Scripts/Juego/`): basta con el objeto `JuegoBomberos` de la escena. Al iniciar busca solo el rig, el portón, la puerta, el chaleco, el extintor y el camión (por tipo o por nombre) y arma el resto en tiempo de ejecución.
- `JuegoBomberos.cs`: flujo, tareas, puntaje y feedback.
- `CamionBomberos.cs`: abordaje, asiento, balizas, sirena y motor. Si el modelo de pago del camión no está en el proyecto, genera un **camión simple de reemplazo**. En el Inspector (en Play) puedes ajustar `ajusteAsiento`, `ajusteZonaSubida`, `invertirFrente` y `conductorIzquierda` si la cabina no cae justo en el modelo.
- `ObjetoAgarrable.cs`: agarre por cercanía con controles o manos.
- `HudBomberos.cs`, `MarcadorObjetivo.cs`, `SonidosVR.cs`, `EntradaVR.cs` y `VisualesVR.cs`: HUD, flecha guía, sonidos, entrada y háptica, y utilidades visuales.

## Créditos y licencias

### Modelo 3D: "Fire Department Building"
- **Autor**: jimbogies (https://sketchfab.com/jimbogies)
- **Fuente**: https://sketchfab.com/3d-models/fire-department-building-6f95f738a4ed4ad1853b4185629b403b
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones realizadas**: reescalado de las unidades originales (pulgadas) a metros reales, separación/duplicación y animación de las puertas (portón basculante y puerta automática por proximidad), colliders añadidos a las piezas y desactivación de piezas originales reemplazadas.

> La licencia CC BY 4.0 requiere acreditar al autor, enlazar la licencia e indicar los cambios realizados (cumplido en esta sección). El uso comercial está permitido.

### Modelo 3D: "Office Chair"
- **Autor**: Red Fox / nokillnando (https://sketchfab.com/nokillnando)
- **Fuente**: https://sketchfab.com/3d-models/office-chair-b228a29fa84544c2be501c295653ffe7
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: ninguna (se usa tal cual).

### Modelo 3D: "Office Desk 140x60"
- **Autor**: AleixoAlonso (https://sketchfab.com/AleixoAlonso)
- **Fuente**: https://sketchfab.com/3d-models/office-desk-140x60-9262f311271c4c4390341e526d3fe103
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: ninguna (se usa tal cual).

### Modelo 3D: "Fire extinguisher low-poly"
- **Autor**: fumestic (https://sketchfab.com/fumestic)
- **Fuente**: https://sketchfab.com/3d-models/fire-extinguisher-low-poly-91457bbb11e5431e8985076ad5b13563
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: ninguna (se usa tal cual).

### Modelo 3D: "Life Vest / Life Jacket (low poly)"
- **Autor**: Akimkhan (https://sketchfab.com/akimkhan) — versión low-poly del modelo original de @DPMA
- **Fuente**: https://sketchfab.com/3d-models/life-vest-life-jacket-low-poly-f1e8b0bb85dd4abe866e2c8de7a91e95
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: ninguna (se usa tal cual).

### Modelo 3D: "Fire Hose Cabinet"
- **Autor**: RED2000 (https://sketchfab.com/RED2000)
- **Fuente**: https://sketchfab.com/3d-models/fire-hose-cabinet-befe18817b094900ad2290654f655cb8
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: ninguna (se usa tal cual).

### Modelo 3D: "Wall-mounted television on a bracket (Hotel and Resort Operations)"
- **Contribuidor**: 3D Assets (@3dassets) (https://3dassets.dev/u/3dassets)
- **Fuente**: https://3dassets.dev/assets/hotel-and-resort-operations-tv-wall-bracket-085491b3
- **Licencia**: CC0 1.0 Universal (dominio público) — https://creativecommons.org/publicdomain/zero/1.0/ — **no exige atribución** (se acredita igualmente por trazabilidad).
- **Modificaciones**: ninguna (se usa tal cual; colocada como pantalla colgada en alto frente al escritorio del rincón de oficina).

### Modelos de terceros NO incluidos en este repositorio

- **"Cartoon Stylized American Fire Truck"** — autor: ZippyLee (CGTrader)
  - Fuente: https://www.cgtrader.com/3d-models/vehicle/truck/cartoon-stylized-american-fire-truck
  - Licencia: **Royalty Free de CGTrader** — permite el uso del modelo dentro del proyecto (juego/simulación/VR), renders y capturas, pero **prohíbe la redistribución de los archivos**.
  - Por esa razón, el modelo **no se incluye** en este repositorio. Para usarlo, adquiérelo en el enlace (carpeta local esperada: `Assets/Models/CamionBomberos/`).

- **"Lockers"** (pack de casilleros) — autor: TridentGame (itch.io)
  - Fuente: https://tridentgame.itch.io/lockers
  - Licencia: **no declarada** por el autor (asset gratuito en itch.io) — sin permiso explícito de redistribución.
  - Por esa razón, el asset **no se incluye** en este repositorio (uso local, carpeta `Assets/Models/Casilleros/`). Si el autor otorga permiso, puede incluirse con atribución.

#### Cómo agregar los assets locales (para que la escena se vea completa)

1. Consigue cada asset desde su enlace (el camión se compra; los casilleros son gratuitos en itch.io).
2. Extrae su contenido en la carpeta indicada — `Assets/Models/CamionBomberos/` o `Assets/Models/Casilleros/` — **respetando los nombres de archivo originales**.
3. Abre Unity: los objetos se **re-vinculan automáticamente** en la escena (los archivos `.meta` versionados en este repositorio conservan los mismos identificadores/GUIDs).

> Si los assets no están presentes, el proyecto **compila y ejecuta igual**; solo se muestran como referencias vacías ("Missing") en la escena.

### Tecnologías y SDKs
- **Meta XR SDK / Oculus Integration** — © Meta Platforms, bajo su licencia de SDK (Meta Platform Technologies SDK License).
- **TextMesh Pro** — © Unity Technologies, bajo la Unity Companion License.
- **Scripts, mesa y taburete**: contenido propio del proyecto (trabajo académico).
