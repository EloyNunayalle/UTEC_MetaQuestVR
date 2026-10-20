# Plan de división de escenas — Ciudad + Estación (v1)

> **Estado**: 📋 PENDIENTE — NO implementado aún (solo diseño, para recordar luego)
> **Fecha**: 18 de octubre de 2026
> **Proyecto**: UTEC_MetaQuestVR (Unity 2022.3 · Meta Quest · Simulador XR)
> **Objetivo**: una ciudad manejable con la estación de bomberos como hub y casas
> explorables para las misiones, manteniendo 72 FPS estables en Quest.

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
- *(Si la ciudad crece → dividir en `Ciudad_Tile_00`, `_01`… por cuadrícula)*

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
| **Fase 1** | Crear `Persistente` (mover rig + avatar + HUD + managers + camión) + separar `Estacion_Interior` (garaje) + `Ciudad` (fachada de la estación + calle). *La más trabajosa, se hace una vez.* |
| **Fase 2** | Crecer la ciudad: calles, edificios de fondo, tiles + **Addressables** |
| **Fase 3** | Casas de misión: exterior en la Ciudad + interior aparte + triggers en puertas + integración con `JuegoBomberos` |

## 8. Herramientas

- **Inicio**: `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)` simple
- **Ciudad grande**: **Addressables** (carga/descarga por etiquetas) — referencia:
  el sample oficial de Meta `Unity-AssetStreaming` (quadtree LOD + Addressables +
  estados descargado → cargado-oculto → activado para evitar "pop-in")

## 9. Notas para la implementación

- Cuidado al separar `SampleScene`: hoy todo está junto (estación + camión +
  avatar + HUD). Revisar las referencias cruzadas actuales (rig ↔ camión, HUD,
  zona de subida) antes de mover nada.
- El camión **no** puede vivir en una escena que se descargue (por eso va en
  `Persistente`).
- Mantener el proyecto jugable después de cada fase (no migrar todo de golpe).
- Probar SIEMPRE con el Simulador XR + perfil de rendimiento antes de subir a la Quest.

---

*Documento de planificación — no implica cambios en el proyecto todavía.*
