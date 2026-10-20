# Problemas: animación de piernas (intento 1)

> **Estado**: REVERTIDO — el proyecto volvió al estado del commit `c325d23`
> **Fecha**: 19 de octubre de 2026
> **Proyecto**: UTEC_MetaQuestVR (Unity 2022.3 · Meta Quest · Simulador XR)
> **Objetivo del intento**: piernas **caminando** + piernas **dobladas al sentarse** en el camión.

---

## 1. Qué se intentó

1. Se activó el **sistema de animación oficial del Meta Avatars SDK**:
   - `OvrAvatarAnimationBehavior` (comportamiento de animación del sample "LegsBehavior").
   - Rig de animación **`RTR00003`** (prefab + FBX + `RT_MecanimLegs.controller` + ~50 animaciones de los samples: caminar, girar, agacharse, sentarse).
   - `OvrAvatarLegsController` (eventos de velocidad de caminata y `TrackingUpperBlend`).
2. Se probaron muchas combinaciones de configuración:
   - Vista del avatar: `FirstPerson`, `ThirdPerson`, `All`.
   - Sub-mallas: con/sin cabeza, cabello, cara, etc. (`subMeshInclusionFlags`).
   - Blends del tren superior (`_enableUpperBodyBlended`, `_upperBodyBlendFactor`).
   - Reproducción de animación local (`_enableLocalAnimationPlayback`).
   - Activación/desactivación del sistema de comportamiento por estado (de pie/sentado).

## 2. Qué SÍ funcionó ✓

- **El ciclo de caminata se veía** ✓ (evidencia: `camina_c86` y `camina_c98` — zancadas alternas).
- **La pose de sentado con piernas dobladas** ✓ (evidencia: `piernas_w86`).
- El sistema se registraba correctamente (rig instanciado ✓, controlador encontrado ✓, animación reproduciéndose ✓).

## 3. Problemas encontrados ✗

### 3.1. La vista hacia abajo se rompe ("desde la nuca") ✗
- Al mirar hacia abajo se veía el **cuello/cabello/interior de la camisa** ✗ en vez de la ropa y los pies.
- El avatar de **3ª persona** (necesario para ver las piernas) **incluye cabeza/cuello** → la cámara queda dentro → se ve el interior del avatar ✗.

### 3.2. Limitación de diseño del SDK ✗
- **1ª persona** (malla `1stPerson`) = **brazos + torso**, vista limpia ✓ **PERO sin piernas** ✗.
- **3ª persona** (malla `combined`) = **cuerpo completo con piernas** ✓ **PERO la cabeza/cuello tapan la vista** al mirar hacia abajo ✗.
- **No existe "1ª persona con piernas"** en este SDK → las dos cosas son **incompatibles** ✗.

### 3.3. El prototipo de animación local del SDK ✗
- Su propia documentación advierte: *"reproducir la animación de locomoción en el avatar local puede hacer que las cosas se sientan incorrectas"* ✗.
- La animación desplaza la **cabeza/cuello hacia adelante** → empeora la vista ("nuca") ✗.

### 3.4. Bugs propios durante el intento ✗
- `_sistemaOn` se inicializaba en `false` → la primera llamada para apagar el sistema salía por el `return` y **nunca se aplicaba** ✗ (por eso el híbrido "de pie apagado / sentado prendido" no funcionaba en las pruebas).
- Una línea mal escrita al aplicar `_activeSubMeshes` (quedó en `All` en vez del valor deseado) ✗.
- Editar scripts **durante el Play** reinició la escena varias veces (domain reload) → pruebas confusas ✗.

## 4. Conclusiones

- **Se prioriza la vista limpia** (como el commit `c325d23`) ✓ → **se revirtió** el sistema de animación ✗.
- **Piernas al sentarse**: sin animación quedan **rectas** ✗ (el "crouch" que las doblaba era parte del sistema de animación ✗).
- **Caminar**: descartado por ahora ✗ (requiere un sistema procedural propio ✗).

## 5. Evidencia (capturas en `Assets/Screenshots/_trabajo/`)

| Captura | Muestra |
|---|---|
| `camina_c86.png` / `camina_c98.png` | Caminata funcionando (zancadas alternas) ✓ |
| `piernas_w86.png` | Sentado en el camión con piernas dobladas ✓ |
| `vFP_depie.png` | Vista de pie con la configuración final (1ª persona) ✓ |
| `vista_apagado.png` | Vista limpia con la animación desactivada (ropa + pies) ✓ |
| `cabeza_fuera.png` | Avatar sin cabello (prueba de sub-mallas) ✓ |

## 6. Investigación: cómo lo logran los proyectos reales

### 6.1. Meta (documentación oficial del Meta Avatars SDK)
- *"Normalmente las animaciones de locomoción **solo se ven en las instancias remotas (3ª persona)** del avatar y **no se reproducen en el avatar local de 1ª persona**, porque **pueden generar una sensación de 'desencarnación'** al verse desde esa perspectiva."*
- `LocalAnimationPlayback`: *"**no está pensada para uso general**, solo cuando la situación lo requiere"* (espejos, cámara selfie, pruebas).
- *"Para poses de **1ª persona**: **priorizar la precisión del tracking** sobre la calidad de la animación."*
- *"Para doblar las rodillas al agacharse: **usar IK**."*
- **Conclusión**: el SDK está diseñado para que el jugador **no** vea la animación de piernas en 1ª persona; las piernas animadas son para espejos/otros jugadores (por eso el sample `LegsNetworkLoopback` tiene un avatar local limpio + avatares espejo con piernas).

### 6.2. VRChat (la referencia real de "cuerpo completo en 1ª persona")
VRChat sí muestra el cuerpo completo, con 2 técnicas que no usamos en este intento:
1. **"Head Chop"**: **escala los huesos de la cabeza a ~0** solo para la cámara del jugador local → la cabeza desaparece de TU vista, pero sigue visible en espejos y para otros. (Se escala el **hueso**, no se excluye la malla: así no queda el "hueco del cuello" que nos apareció.)
2. **Locomoción procedural ("auto-footstep")**: las piernas **no usan un clip de caminata fijo**; con 3/4 puntos de tracking (cabeza + 2 manos) los pies dan **pasos procedurales** siguiendo el movimiento del cuerpo. Para 6 puntos (FBT) se fuerzan las animaciones de locomoción.

### 6.3. Proyectos open-source / assets
- **NYIK (MIT)**: *"**HideHeadInFirstPerson**: escala el hueso de la cabeza a ~0 durante el render de la cámara de 1ª persona; los espejos y otras cámaras renderizan la cabeza normal"*. Resuelve las piernas con **LegSolver IK** (cadera → rodilla → tobillo) en 3 puntos.
- **Final IK (VR IK)**: hasta 6 puntos de tracking + **locomoción procedural** integrada.

## 7. Opciones evaluadas para el proyecto

| Opción | Descripción | Esfuerzo | Resultado |
|---|---|---|---|
| **A** | Estado original (`c325d23`): 3ª persona sin animación | — | Vista limpia ✓ + piernas visibles **estáticas** ✗ (pies se deslizan al caminar ✗) |
| **B** | Solo manos y torso (1ª persona estándar, recomendación Meta) | — | Vista limpia ✓ + **sin piernas/pies locales** ✗ (pierde lo pedido ✗) |
| **C** | **Híbrido**: de pie = como A ✓ + **sentado en el camión = sistema de animación ON** (`IsSitting`) | **Bajo** ✓ | De pie: vista limpia + piernas estáticas ✓ · Sentado: **piernas dobladas** ✓ |
| **D** | **VRChat completo**: Head Chop (escalar hueso) + piernas IK procedurales + targets de sentado | **Alto** ✗ | Todo: vista limpia + caminar + sentado, como VRChat |

**Decisión final**: se probó la **Opción C (híbrido)** pero **se descartó** ✗ porque **dentro del camión no se ve bien** ✗ (la vista/pose sentado se siente incorrecta). El proyecto queda en la **Opción A** = el estado del commit `c325d23` (vista limpia al mirar hacia abajo ✓ + piernas visibles estáticas ✗ + sin doblarse al sentarse ✗). La Opción D (VRChat completo) queda como trabajo futuro si se retoma. La opción B se descarta (pierde las piernas locales).

### Notas de la Opción C (probada y descartada)
- Se implementó el toggle por estado (de pie = sistema de animación OFF / sentado = ON + `IsSitting`) en `PiernasJugador.cs`, leyendo el estado real (`OvrAvatarEntity.BehaviorSystemEnabled`) para evitar el bug de `_sistemaOn` del primer intento.
- **Problema al usarla**: dentro del camión la vista y la pose no se ven bien (la animación mueve cabeza/cuello y la sensación al estar sentado en la cabina es incorrecta).
- Por eso **se revirtió todo** al estado del commit `c325d23` (sin sistema de animación, sub-mallas = All, vista ThirdPerson limpia).



## 8. Opciones futuras (si se quiere retomar la Opción D)

1. **Head Chop** — escalar los huesos de la cabeza a ~0 en el render de la cámara local (como VRChat/NYIK), en lugar de excluir mallas (que deja el hueco).
2. **Piernas IK procedurales** — LegSolver (cadera→rodilla→tobillo) con pasos procedurales según el movimiento (NYIK MIT o Final IK), en lugar del clip de caminata del SDK.
3. **Sentado con IK** — targets de tobillos (el SDK ya expone *joint overrides* de tobillos en el sample `LocomotionJointOverridesExample`).


---

*Documento de registro técnico. El proyecto quedó en el estado del commit `c325d23`.*
