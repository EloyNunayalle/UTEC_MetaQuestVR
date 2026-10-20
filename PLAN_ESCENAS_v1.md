# Plan de división de escenas — Ciudad + Estación (v1)

> **Estado**: 🟡 **FASE 1 PARCIAL** — `Persistente` implementada y probada (19/10/2026)
> **Fecha**: 18 de octubre de 2026 · actualizado 19/10/2026
> **Proyecto**: UTEC_MetaQuestVR (Unity 2022.3 · Meta Quest · Simulador XR)
> **Objetivo**: una ciudad manejable con la estación de bomberos como hub y casas
> explorables para las misiones, manteniendo 72 FPS estables en Quest.

---

## 0. Estado de implementación (19/10/2026)

| Ítem | Estado |
|---|---|
| `Persistente.unity` (rig + avatar + `JuegoBomberos` + HUD + camión + luz de cabina) | ✅ **HECHA** y probada en Play |
| Separación del jugador/managers/camión de la escena del mundo | ✅ **HECHA** (el tile de la ciudad ya no los contiene) |
| Carga aditiva automática (`Assets/Scripts/Juego/GestorEscenas.cs`) | ✅ **HECHA** (editor + build + reinicio) |
| `Estacion_Interior.unity` (decoración + marcas de piso + chaleco/extintor) | ✅ **CONTENIDO SEPARADO** — se carga/descarga por proximidad (45 m / 60 m) |
| Build Settings | ✅ `[Persistente (0), Ciudad_Tile_00_Estacion (1), Estacion_Interior (2)]` |
| Prueba en editor | ✅ 1 avatar, 1 manager, 1 rig, camión; 4 tareas; descarga/recarga y reinicio OK |
| Malla del edificio partida (exterior/interior) | ⏳ **PENDIENTE** — el edificio es una sola malla Collada; requiere cirugía en Blender |
| `Ciudad` | ⏳ **PENDIENTE** — no iniciada (decisión del usuario) |
| Casas de misión | ⏳ **PENDIENTE** (Fase 3) |

**Cómo se prueba en el editor**: abrir **solo** `Assets/Scenes/Ciudad_Tile_00_Estacion.unity` y dar Play;
`GestorEscenas` agrega la `Persistente` y, si el jugador está cerca, la `Estacion_Interior`.
⚠️ No abrir la `Persistente` también en el editor, porque el Play multi-escena la cargaría dos veces.

**Cómo funciona en build**: `Persistente` es el índice 0 → `GestorEscenas` agrega la
`Ciudad_Tile_00_Estacion` (el mundo) encima. El jugador y el camión nunca se recargan al cambiar de mundo.

**Interior por proximidad**: `GestorEscenas` lo carga a ≤45 m del centro de la estación y lo
descarga a >60 m (histéresis), solo si `JuegoBomberos` lo permite (misión sin equipamiento
pendiente). Al recargar, `JuegoBomberos.RevisarEquipamiento()` re-engancha chaleco/extintor.
`GestorEscenas` sobrevive a las recargas (DontDestroyOnLoad): si `Reiniciar` recarga el mundo
en Single, repone la `Persistente` y el interior antes de los `Start` del siguiente frame.

**Luz de cabina**: `TEMP_Luz_Cabina` quedó **emparentada al camión** para que viaje con él.

**Corrección por la división**: `PuertaAutomatica` y `PortonElevadizo` tenían asignado el
`CenterEyeAnchor` del rig (que ahora vive en `Persistente`), por lo que la referencia quedó
vacía y la puerta dejó de abrir/cerrar. Ambos scripts ahora **buscan el rig automáticamente**
si la referencia está vacía (probado: abre a <2.2 m, cierra a >3.2 m).

---

## 1. Decisión clave

| Opción | ¿Viable? |
|---|---|
| ❌ Una sola escena con toda la ciudad | **NO** — memoria (Quest 2 = 6 GB / Quest 3 = 8 GB), tiempos de carga (crecen linealmente con el nº de GameObjects) y draw calls |
| ✅ **Escenas aditivas + carga/descarga por proximidad (streaming)** | **SÍ** — es el patrón oficial de Meta para mundos abiertos en Quest |

**La estación y las casas "pertenecen" a la ciudad** porque TODAS las escenas
comparten **las mismas coordenadas del mundo**: el exterior está en la escena de
la Ciudad y el interior en su propia escena, en el mismo lugar exacto. El jugador
cruza la puerta y no nota la separación (es solo técnica).

---

## 2. Las escenas

### 1️⃣ `Persistente` — siempre cargada, nunca se descarga
- **Jugador**: `[BuildingBlock] Camera Rig` + `AvatarJugador` + `AvatarSDK`
  - Nota: el avatar está **emparentado al TrackingSpace del rig** → sigue al
    jugador automáticamente, incluso entre cambios de escena.
- **Camión 🚒 completo** (`CamionBomberos`): incluye volante (`VolanteInteractivo`),
  palanca (`PalancaCambios`), panel + agujas, pantallas GPS (`Pantalla_GPS`) y la
  cabina (`InteriorCamion`). La zona de subida (círculo amarillo) ya es hija del
  camión y viaja con él.
  - *Viaja con el jugador por toda la ciudad → nunca debe descargarse.*
- **HUD** (`HudBomberos` — avisos/instrucciones)
- **Managers**: `JuegoBomberos` (singleton), `EntradaVR`, sonidos/visuales
- **Pantalla de carga inicial** + precompilado de shaders (tip oficial de Meta)

### 2️⃣ `Ciudad` — el exterior del mundo
- Calles, veredas, piso/terreno, edificios de fondo (baja calidad)
- **Exterior de la estación**: fachada "FIRE DEPARTMENT", puertas rojas, patio
- **Exteriores de las casas de misión** (fachada + puerta)
- Luces **bakeadas**
- *(Tiles: el hub es `Ciudad_Tile_00_Estacion`; los demás siguen `Ciudad_Tile_01_...`, `Ciudad_Tile_02_...` por cuadrícula)*

### 3️⃣ `Estacion_Interior` — lo de adentro de la estación
- Garaje, paredes interiores, decoración
- Se **carga** al acercarse/entrar · se **descarga** al salir

### 4️⃣ `Casa_Mision_01`, `Casa_Mision_02`… — una por casa
- Cada interior con su contenido de misión
- Se **carga** al acercarse a esa puerta · se **descarga** al salir
- Cada una con su propia luz bakeada

---

## 3. Ciclo de carga (ejemplo real)

```
Jugador en el garaje:
  [Persistente] + [Ciudad] + [Estacion_Interior]

Sale a la calle → se aleja:
  [Persistente] + [Ciudad]                    ← Estacion_Interior se descarga

Conduce hacia una casa de misión:
  [Persistente] + [Ciudad]
  …al acercarse:
  [Persistente] + [Ciudad] + [Casa_Mision_01] ← se carga ANTES de llegar

Sigue de largo:
  [Persistente] + [Ciudad]                    ← Casa_Mision_01 se descarga
```

---

## 4. Portales (triggers en las puertas)

| Paso | Qué pasa |
|---|---|
| 1 | El jugador se **acerca** (10-15 m antes) → `LoadSceneAsync(..., Additive)` en segundo plano |
| 2 | La escena carga **oculta/deshabilitada** (sin tirón) y se **activa** al cruzar |
| 3 | Si la carga es grande → **fade corto a negro** (mejor que un tirón) |
| 4 | Al salir y alejarse → `UnloadSceneAsync` + `Resources.UnloadUnusedAssets()` |

**Ciudad al conducir (tiles)**: cargar los tiles **alrededor del camión** con
**prefetch** (el de adelante primero, porque vas en esa dirección).

---

## 5. Reglas de oro (doc oficial de Meta)

1. **Escenas pequeñas**: pocos GameObjects (referencia actual: la estación =
   318 objetos ✓ — una ciudad entera = miles ✗ → por eso se divide)
2. **Sin referencias directas entre escenas** → comunicarse por los managers
   persistentes (`JuegoBomberos.Instancia`) o eventos
3. **Luz bakeada por escena** (nunca realtime) · **Occlusion culling por escena**
4. Objetos **deshabilitados** al cargar + **activación escalonada** por frames
5. **Shaders precompilados** al inicio (escena "Precarga" tras el splash)
6. **Throttle**: máx. ~4 operaciones de carga/descarga por frame
7. Tras descargar: `Resources.UnloadUnusedAssets()` (las texturas no se liberan solas)

## 6. Presupuesto de rendimiento (Quest)

| Métrica | Quest 2 | Quest 3 |
|---|---|---|
| FPS | 72 | 72-90 |
| Draw calls | < 100 | < 200 |
| Triángulos por frame | < 750K | < 1.5M |
| Memoria RAM | 6 GB | 8 GB |

---

## 7. Plan de migración por fases (cuando se decida hacerlo)

| Fase | Qué se hace |
|---|---|
| **Fase 1** | 🟡 **Casi completa (19/10/2026)**: `Persistente` ✅ + `Estacion_Interior` (contenido: decoración, marcas, equipamiento) ✅ con carga/descarga por proximidad. Falta partir la malla del edificio en Blender (exterior/interior) y `Ciudad` (aún no iniciada). |
| **Fase 2** | Crecer la ciudad: calles, edificios de fondo, tiles + **Addressables** |
| **Fase 3** | Casas de misión: exterior en la Ciudad + interior aparte + triggers en puertas + integración con `JuegoBomberos` |

## 8. Herramientas

- **Inicio**: `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)` simple
- **Ciudad grande**: **Addressables** (carga/descarga por etiquetas) — referencia:
  el sample oficial de Meta `Unity-AssetStreaming` (quadtree LOD + Addressables +
  estados descargado → cargado-oculto → activado para evitar "pop-in")

## 9. Notas para la implementación

- Cuidado al separar la escena del mundo: hoy el tile `Ciudad_Tile_00_Estacion` contiene la estación completa (edificio + patio + cerco). Los demás tiles seguirán la convención `Ciudad_Tile_01_...`, `Ciudad_Tile_02_...`.
  avatar + HUD). Revisar las referencias cruzadas actuales (rig ↔ camión, HUD,
  zona de subida) antes de mover nada.
- El camión **no** puede vivir en una escena que se descargue (por eso va en
  `Persistente`).
- Mantener el proyecto jugable después de cada fase (no migrar todo de golpe).
- Probar SIEMPRE con el Simulador XR + perfil de rendimiento antes de subir a la Quest.

---

*Documento de planificación — no implica cambios en el proyecto todavía.*
