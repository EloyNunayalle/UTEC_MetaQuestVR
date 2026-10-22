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
Escena principal: `Assets/Scenes/Ciudad_Tile_00_Estacion.unity` (tile de la ciudad que contiene la estación). Las demás partes de la ciudad se llamarán `Ciudad_Tile_01_...`, `Ciudad_Tile_02_...`, etc.

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

Al darle Play aparece un panel de bienvenida con los controles. Presiona **A** (o pellizca) para empezar —o espera 15 s y empieza solo—. Suena la alarma y hay que completar las tareas **lo más rápido posible**:

1. **Abrir el portón de la cochera**: toca el botón rojo con el dedo o con la punta del control.
2. **Ponerte el chaleco**: acerca la mano, agárralo con **GRIP/gatillo** (o pellizcando) y llévalo a tu pecho. Al soltarlo ahí, queda puesto.
3. **Cargar el extintor al camión**: agárralo, llévalo hasta el camión y suéltalo junto a él.
4. **Subir al camión de bomberos**: párate en el **círculo amarillo** junto a la puerta del conductor. El círculo se llena y subes a la cabina (o presiona **A** para subir al instante). Se encienden las balizas, la sirena y el motor.
   - **Bajar**: **B/Y** o mantener el pellizco 2 s.
   - Si intentas subir sin haber terminado tu equipo, el camión te avisa qué falta.

**Al final, manejas el camión**: al subir termina la misión y puedes conducir. El portón se abre si estaba cerrado.

| Acción | Control | Teclado (editor) |
|---|---|---|
| Avanzar / retroceder | Stick izquierdo arriba / abajo | `W`/`S` o flechas |
| Girar | Stick a los lados | `A`/`D` o flechas |
| Sirena on/off | **A** | `H` |
| Bajar | **B** | `Q` |

El camión frena solo al soltar el stick y se detiene contra paredes u objetos (con sonido, vibración y aviso).

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
- `InteriorCamion.cs`: interior del camión. Con el panel importado activo (`usarPanelMercedes`) usa el **panel Mercedes-Benz** real (tablero/agujas/volante del modelo) y solo construye por código la **luz de cabina** y — si no está la palanca importada — la **palanca procedural**; sin el panel, genera el interior completo por código (volante, tablero, cluster, pantalla GPS). Se crea solo al dar Play (y preview en el editor); ajustes finos en el Inspector.
- `VolanteInteractivo.cs`: volante agarrable (GRIP/pellizco cerca del aro) con tope ±120° y joystick de respaldo; gira sobre el centro visual (estable, sin parpadeos) y las piezas que van con el volante giran con él; entrega el eje de giro al camión (`ejeGiroManual`).
- `PalancaCambios.cs`: palanca de cambios agarrable (modelo importado o procedural) que se asienta en la ranura más cercana; soporta ranuras extra (p. ej. L → D), **ilumina la letra activa** y publica la marcha al camión (`marchaManual`).

## Portón, manguera y agua

### Portón de la estación (`PortonElevadizo.cs`)
Abre y cierra con una interpolación suave (*smoothstep*) y brazos hidráulicos animados. Formas de activarlo:
- **Botón rojo** junto al portón (poke con el dedo o la punta del control).
- **Tecla `P`** (editor). Se cambia en `Tecla Alternar`.
- **Proximidad**: se abre solo cuando te acercas **manejando el camión** (`Abrir Al Acercarse Camion`, `Distancia Camion`).
- **Seguridad**: no se cierra si el jugador o el camión están bajo su recorrido. Mientras está cerrado, el camión choca con él y no puede salir.

### Manguera física (`MangueraBomberos.cs`)
Va conectada al costado derecho trasero del camión y se tiende en el piso a lo largo del camión.
- **Física**: cadena de `Rigidbody` unidos con `ConfigurableJoint`. La distancia entre eslabones está bloqueada (no se estira), el doblez está limitado a ±35° por unión y un resorte de flexión le da rigidez. Con agua, la manguera se pone más dura.
- **Visual**: tubo suave (curva Catmull-Rom) generado cada frame.
- **Boquilla**: acércale la mano y presiona **GRIP**. Sigue a tu mano con un pequeño retraso (seguimiento por velocidad), así se sienten su **peso e inercia**. Si la sueltas en movimiento, sale despedida. Si te alejas más de lo que da la manguera, se te suelta con un aviso.
- Al subir al camión la manguera se guarda, y al bajar se vuelve a desplegar.
- Está en la capa *Ignore Raycast*, así no bloquea el teletransporte ni al jugador.

### Agua (`LanzaAgua.cs`)
| Acción | Control (el que sostiene la boquilla) | Teclado |
|---|---|---|
| Abrir el agua (caudal analógico) | **Gatillo** | `F` |
| Presión baja / media / alta | **A / X** | `T` |
| Chorro directo / niebla (abanico) | **B / Y** | `N` |

- `ParticleSystem` en espacio de mundo, con gravedad ajustable y **World Collision**. Al chocar genera salpicaduras.
- La presión cambia la velocidad del chorro y su alcance. El gatillo abre la válvula con una pequeña inercia.
- **Retroceso**: el chorro empuja la boquilla hacia atrás y la hace temblar levemente, con vibración del control y sonido de agua proporcionales a la fuerza.
- El agua solo sale mientras sostienes la boquilla.
- Evento `AlMojar(objeto, punto)` para conectar después, por ejemplo, un fuego que se apague.

Todo se crea solo al darle Play (`JuegoBomberos` → `Usar Manguera`). Para ajustar valores antes de Play, agrega el componente `MangueraBomberos` al objeto `Camion_Bomberos`: tus valores se respetan.

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

### Modelo 3D: "Painel Mercedes-Benz eO500U / eCitaro"
- **Autor**: Carlos Reis (https://sketchfab.com/carlosreis) — agujas ("ponteiros") modeladas por Edson Antônio
- **Fuente**: https://sketchfab.com/3d-models/painel-mercedes-benz-eo500u-ecitaro-89042f693f2c4da6a7d2ad37b63cdeea
- **Licencia**: Creative Commons Attribution (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: ninguna en la malla (versión GLB 1K); en el proyecto se animan las agujas y el volante por código y la pantalla central muestra el GPS.

### Modelo 3D: "Gear Selector/Gear shifter/Gear Switch (CVT)"
- **Autor**: fikridjojo (https://sketchfab.com/DtestING)
- **Fuente**: https://sketchfab.com/3d-models/gear-selectorgear-shiftergear-switch-cvt-5c078ceab6984dcc882d66db3011ec5e
- **Licencia**: Creative Commons Attribution (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones**: se desactivó el plano de suelo que trae el modelo; en el proyecto se reescaló y rotó para montarla sobre la consola del camión; la palanca gira por código (agarre) y las letras (P-R-N-D-L) se iluminan según la marcha activa.

### Modelo 3D: "Travel View - Portable Monitor"
- **Autor**: Innerscene (https://www.innerscene.com/)
- **Fuente**: https://www.innerscene.com/tools/library/3d-parts/travel-view-portable-monitor-85a9906d
- **Licencia**: CC0 1.0 Universal (dominio público) — https://creativecommons.org/publicdomain/zero/1.0/ — **no exige atribución** (se acredita igualmente por trazabilidad).
- **Modificaciones**: ninguna en la malla; en el proyecto se coloca sobre el tablero del camión (misma posición/rotación, corrida a la derecha) y muestra el GPS de la misión por código.

### Kit de ciudad: "Downtown City MegaKit" (Standard)
- **Autor**: Quaternius (https://quaternius.com)
- **Fuente**: https://quaternius.com/packs/downtowncitymegakit.html
- **Licencia**: **CC0 1.0 Universal** (dominio público) — https://creativecommons.org/publicdomain/zero/1.0/ — **no exige atribución** (se acredita igualmente por trazabilidad).
- **Contenido incluido en el repositorio**: los modelos FBX y texturas usados para la ciudad (calles, veredas, decals viales, edificios y muros) en `Assets/Models/DowntownCity/`.
- **Versión completa (opcional)**: el kit completo (300+ modelos, ~234 MB) se descarga gratis desde el enlace (versión Standard, CC0). El repositorio incluye solo las piezas utilizadas para no inflarlo; si necesitas más variedad, descarga el kit y copia los FBX/texturas que falten en la misma carpeta.
- **Modificaciones**: ninguna en las mallas; se importan a Unity y se usan para construir la ciudad (escala en metros del kit).

### Kit de naturaleza: "Ultimate Stylized Nature Pack"
- **Autor**: Quaternius (https://quaternius.com)
- **Fuente**: https://quaternius.com/packs/ultimatestylizednature.html
- **Espejo**: https://poly.pizza/bundle/Ultimate-Stylized-Nature-Pack-zyIyYd9yGr
- **Licencia**: **CC0 1.0 Universal** (dominio público) — https://creativecommons.org/publicdomain/zero/1.0/ — **no exige atribución** (se acredita igualmente por trazabilidad).
- **Contenido incluido en el repositorio**: **pack completo** (63 modelos FBX y 21 texturas) en `Assets/Models/StylizedNature/` + `LICENCIA.txt` (~20 MB con texturas a 1024 px).
- **Modificaciones**: texturas de corteza reducidas de 2048 a 1024 px para Quest; se crearon materiales Standard (corteza con normal map; hojas con alpha cutout a doble cara) y prefabs con los materiales asignados.
- **Nota de uso**: los prefabs conservan su transformación original del FBX; al colocarlos se respeta esa orientación y se aplica el giro deseado sobre ella.

### Modelos de terceros NO incluidos en este repositorio

- **"Cartoon Stylized American Fire Truck"** — autor: ZippyLee (CGTrader)
  - Fuente: https://www.cgtrader.com/3d-models/vehicle/truck/cartoon-stylized-american-fire-truck
  - Licencia: **Royalty Free de CGTrader** — permite el uso del modelo dentro del proyecto (juego/simulación/VR), renders y capturas, pero **prohíbe la redistribución de los archivos**.
  - Por esa razón, el modelo **no se incluye** en este repositorio. Para usarlo, adquiérelo en el enlace (carpeta local esperada: `Assets/Models/CamionBomberos/`).

- **"Lockers"** (pack de casilleros) — autor: TridentGame (itch.io)
  - Fuente: https://tridentgame.itch.io/lockers
  - Licencia: **no declarada** por el autor (asset gratuito en itch.io) — sin permiso explícito de redistribución.
  - Por esa razón, el asset **no se incluye** en este repositorio (uso local, carpeta `Assets/Models/Casilleros/`). Si el autor otorga permiso, puede incluirse con atribución.

- **"Rugged Tablet 0.1"** — autor: dengxiart (Sketchfab)
  - Fuente: https://sketchfab.com/3d-models/rugged-tablet-01-7df208afa36a41d0a7592ae83cde8979
  - Licencia: **"Free Standard" de Sketchfab** — permite el uso dentro del proyecto (incluso comercial), pero **prohíbe redistribuir el archivo del modelo** como archivo independiente y es **Single-Seat**.
  - Por esa razón, el asset **no se incluye** en este repositorio (uso local, carpeta `Assets/Models/Tablet_Rugged/`).

#### Cómo agregar los assets locales (para que la escena se vea completa)

1. Consigue cada asset desde su enlace (el camión se compra; los casilleros y la tablet son gratuitos: itch.io y Sketchfab).
2. Coloca su contenido en la carpeta indicada — `Assets/Models/CamionBomberos/`, `Assets/Models/Casilleros/` o `Assets/Models/Tablet_Rugged/` (tablet: `rugged_tablet_0.1.glb`) — **respetando los nombres de archivo originales**.
3. Abre Unity: los objetos se **re-vinculan automáticamente** en la escena (los archivos `.meta` versionados en este repositorio conservan los mismos identificadores/GUIDs).

#### Cómo instalar el Meta Avatars SDK (para el avatar del jugador)

1. Unity: **Window → Package Manager → My Assets** (con tu cuenta Unity).
2. Instala **"Meta Avatars SDK"** (40.0.1, gratis) y **"Meta Avatars SDK Sample Assets"** (dependencia).
3. En el SDK: pestaña **Samples** → **"Sample Scenes"** → **Import**.
4. Cuando aparezca el diálogo de presets: **"Include All"** (los presets se generan localmente y NO van al repositorio).
5. Ejecuta el menú **"Juego Bomberos → Preparar avatares (copiar presets)"** (copia los zips Light a StreamingAssets; ~165 MB, tampoco van al repositorio).
6. Los assets generados (`Assets/Oculus/Avatar2*`, `Assets/StreamingAssets/*`, `Assets/Samples/`) están excluidos del repositorio por tamaño y licencia del SDK.

**Notas del avatar:**
- El avatar usa el **avatar de la cuenta Meta** (CDN) si hay app-id configurado (`Meta → Platform → Edit Settings`); si no, cae al **preset local** (funciona igual: brazos/manos con poses de dedos por controladores).
- El rig del jugador es el Building Block de la cámara (`[BuildingBlock] Camera Rig`); los scripts propios (`AvatarJugador`, `AvatarEntradaJugador`, `AvatarTrackingDelegate`, `AvatarControlDelegate`) viven en `Assets/Scripts/Juego/` y no dependen de `Assets/Samples`.

> Si los assets no están presentes, el proyecto **compila y ejecuta igual**; solo se muestran como referencias vacías ("Missing") en la escena.

### Tecnologías y SDKs
- **Meta XR SDK / Oculus Integration** — © Meta Platforms, bajo su licencia de SDK (Meta Platform Technologies SDK License).
- **TextMesh Pro** — © Unity Technologies, bajo la Unity Companion License.
- **Scripts, mesa y taburete**: contenido propio del proyecto (trabajo académico).
