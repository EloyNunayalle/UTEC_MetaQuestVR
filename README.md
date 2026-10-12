# UTEC MetaQuestVR — Estación de Bomberos VR

Proyecto universitario de realidad virtual (UTEC) desarrollado en **Unity 2022.3** con el **Meta XR SDK**.
La escena principal es una estación de bomberos interactiva con:

- **Puerta de entrada automática por proximidad**: se abre al acercarse y se cierra al alejarse.
- **Portón elevadizo (basculante)** con brazos hidráulicos, que se abre/cierra con un **botón de presión** (poke con la mano o la punta del controlador).
- **Locomoción VR**: caminar con joystick + teletransporte (Interaction SDK / Building Blocks).
- Interacciones con manos y controles (poke, ray, grab) sobre el rig de interacción moderno.

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

## Créditos y licencias

### Modelo 3D: "Fire Department Building"
- **Autor**: jimbogies (https://sketchfab.com/jimbogies)
- **Fuente**: https://sketchfab.com/3d-models/fire-department-building-6f95f738a4ed4ad1853b4185629b403b
- **Licencia**: Creative Commons Attribution 4.0 (**CC BY 4.0**) — https://creativecommons.org/licenses/by/4.0/
- **Modificaciones realizadas**: reescalado de las unidades originales (pulgadas) a metros reales, separación/duplicación y animación de las puertas (portón basculante y puerta automática por proximidad), colliders añadidos a las piezas y desactivación de piezas originales reemplazadas.

> La licencia CC BY 4.0 requiere acreditar al autor, enlazar la licencia e indicar los cambios realizados (cumplido en esta sección). El uso comercial está permitido.

### Modelos de terceros NO incluidos en este repositorio

- **"Cartoon Stylized American Fire Truck"** — autor: ZippyLee (CGTrader)
  - Fuente: https://www.cgtrader.com/3d-models/vehicle/truck/cartoon-stylized-american-fire-truck
  - Licencia: **Royalty Free de CGTrader** — permite el uso del modelo dentro del proyecto (juego/simulación/VR), renders y capturas, pero **prohíbe la redistribución de los archivos**.
  - Por esa razón, el modelo **no se incluye** en este repositorio. Para usarlo, adquiérelo en el enlace (carpeta local esperada: `Assets/Models/CamionBomberos/`).

### Tecnologías y SDKs
- **Meta XR SDK / Oculus Integration** — © Meta Platforms, bajo su licencia de SDK (Meta Platform Technologies SDK License).
- **TextMesh Pro** — © Unity Technologies, bajo la Unity Companion License.
- **Scripts, mesa y taburete**: contenido propio del proyecto (trabajo académico).
