# Plan de la ciudad — "Downtown UTEC" (v1)

> **Estado**: 🟡 **EN CONSTRUCCIÓN** — Tile 00 (estación) con calles, veredas, marcas y detalles
> **Fecha**: 19 de octubre de 2026 · actualizado 20/10/2026
> **Inspiración real**: **Boston** (Beacon Hill / North End: rowhouses de ladrillo, calles estrechas, manzanas irregulares, plazas) + **NYC** (Broadway: avenida principal curva que corta la cuadrícula). El kit Quaternius es literalmente estilo "Boston/NYC".
> **Mapa visual**: abrir `PLANO_CIUDAD.svg` en el navegador.

---

## 0. Estado de construcción (20/10/2026)

### ✅ Tile 00 — `Ciudad_Tile_00_Estacion`

La escena de la estación ya tiene su fragmento de ciudad:

| Elemento | Detalle |
|---|---|
| **Calle Sur** (E–O) | 6 m de ancho, z 16.65..22.65; 26 losas de asfalto `Street_Asphalt_6x6`; línea discontinua central |
| **Calle Oeste** (N–S) | 6 m de ancho, x 2.65..8.65; misma construcción; tramo norte de 4 m hasta el borde del tile |
| **Cruce** | 6×6 m de asfalto en la intersección + **4 cruces peatonales** `Decal_Crosswalk` |
| **Veredas** | 88 piezas `Sidewalk_Straight_3m` (3 m de ancho) en las 4 franjas + remates escalados en los bordes del tile |
| **Rampa del portón** | Asfalto 6×3 m frente a la puerta real (x −7.5..−3.0, z 13.65..16.65) a y=0.032 |
| **Edificios** | **6 edificios pre-armados** del kit: 3 al este de la Calle Oeste (fachadas al oeste, dentro del tile) + 3 al norte de la Calle Sur (fachadas al sur). Todos con **BoxCollider** |
| **Vegetación** | **26 árboles**: 16 de vereda en jardineras (abedules) + 10 en el **parque del patio** (abedules, arces, árboles normales y un pino). Además: 5 arbustos, flores, plantas, pasto, 3 rocas y un **césped de 23×38 m** en el patio oeste. *Pack completo de 63 modelos — Ultimate Stylized Nature Pack (Quaternius, CC0)* |
| **Suelo del tile** | Plano 120×110 m con material `M_Suelo_Ciudad` (concreto, tiling 30×28) |
| **Detalles** | 3 tapas de registro · 3 desagües · **16 jardineras** · 4 bolardos en las esquinas del cruce · flores/pasto · parque del patio |
| **Colliders** | `Plane` (MeshCollider) · estación (22) · `Piso_Patio` (Box) · **edificios (Box)** · **jardineras (Box)** · **bolardos (Box)** · **troncos de árboles (Capsule vertical)** · **rocas (Box)** · postes del cerco (ya tenían). Calles/veredas/decals/rampa/arbustos/flores son visuales |
| **Organización** | Root `Ciudad_Tile_00` → `Calles` (26) · `Veredas` (88) · `Marcas` (14) · `Props` (13) · `Detalles` (1) · `Edificios` (6) · `Vegetacion` (jardineras, 26 árboles, arbustos, flores, pasto, rocas, césped y colliders) |

### 📌 Notas técnicas del kit (importante)

1. **`Street_2Lane` / `Street_4Lane` NO sirven como calzada directamente**: su malla tiene la **acera de concreto arriba** (submesh `MI_Trim_MetalConcrete`) y el asfalto como quad plano en la parte **inferior** (y=−0.15). Por eso se ven grises/blancas si se usan como calle.
2. **Solución usada**: losas `Street_Asphalt_6x6` (quad de asfalto limpio, 1 submesh) colocadas a **y=0.165** → superficie a y=0.015. Las aceras van aparte con `Sidewalk_Straight_3m` a y=0.02.
3. **Decals** (`Decal_BrokenLine_Straight`, `Decal_Crosswalk`): su plano está en y≈−0.13/−0.15 → se colocan a **y=0.147 / 0.167** (2 mm sobre el asfalto).
4. **Piezas `*_noSidewalk`**: traen líneas blancas en los bordes que se repetirían cada 6/12 m → no se usaron en tramos largos.
5. Las piezas del kit **no traen colliders**; el camión rueda sobre el plano del suelo (el desnivel visual es de ~2 cm). Los edificios/macetas/bolardos sí llevan collider.
6. **Vegetación**: el kit de ciudad **no incluye plantas** (solo jardineras vacías). Se importó el **pack completo** del **Ultimate Stylized Nature Pack** (Quaternius, **CC0**, 63 modelos y 21 texturas) en `Assets/Models/StylizedNature/`, con texturas a 1024 px (~21 MB). Los prefabs conservan su transformación original del FBX (escala y orientación) y se instancian respetándola.
   - **Altura del sustrato de las jardineras**: y=0.08 (las flores/pasto dentro de las jardineras van a esa altura).
   - Arbustos, plantas y pasto: apoyados sobre el césped/vereda con `posición.y = superficie - vértice más bajo`.

### ⏳ Siguiente
- Casas de misión M1 (bloque E), M2 (bloque C), M3 (bloque K) + sus fachadas.
- Bloques de relleno (rowhouses Boston, tiendas, plaza) — tiles 01+.
- Portón principal del cerco: sigue estático (decisión pendiente); cuando se abra, la rampa ya está lista.

> **Nota de streaming**: los 3 edificios del norte de la Calle Sur sobresalen del tile de 65 m de profundidad. El frente de calle se considera parte del tile de la estación; al hacer streaming, el siguiente tile debe empezar **detrás** de esos edificios (no duplicarlos).

---

## 1. Visión general

- La ciudad rodea a la **estación de bomberos** (el hub, ya construida: recinto de 22.8 × 33.3 m con portón al norte).
- El camión sale por el portón y recorre la ciudad hasta las **3 casas de misión** (distancias escalonadas: cerca / media / lejos).
- Escala total: **320 × 260 m** (cruzar ≈ 1 min en camión a 8 m/s ≈ 29 km/h).
- La ciudad se divide en **tiles de ~80 × 65 m (4×4 = 16)** para streaming por proximidad (ver `PLAN_ESCENAS v1` del proyecto).

## 2. Sistema de coordenadas del plano

- `x`: 0 (oeste) → 320 (este), en metros.
- `y`: 0 (sur) → 260 (norte), en metros.
- Todos los números de este documento están en metros y corresponden a este plano.

## 3. Las calles

| Calle | Ancho | Dirección | Posición | Rol |
|---|---|---|---|---|
| **Avenida Curva** (Broadway) | **12 m** | SO→NE (curva suave) | banda curva entre y≈135 y y≈153 | **Avenida principal** (4 carriles) — el eje del recorrido |
| **Avenida Norte** | 12 m | E–O | y = 224..236 | Cierre norte de la ciudad |
| **Calle Sur** | 6 m | E–O | y = 55..61 | Frente de la estación (el portón da aquí) |
| **Calle Oeste** | 6 m | N–S | x = 55..61 | Conecta sur con la avenida curva y el norte |
| **Calle Centro** | 6 m | N–S | x = 145..151 | Divide M1 / plaza |
| **Calle Este** | 6 m | N–S | x = 245..251 | Divide plaza / M3 |
| **Veredas** | 3 m | — | a ambos lados de todas las calles | Del kit: `Sidewalk_Straight_3m` + esquinas |

Todas las intersecciones usan las piezas del kit (`Street_Asphalt_6x6` / `9x9`, `Street_Curve_*`).

## 4. Los bloques (manzanas)

| Bloque | x (m) | y (m) | Contenido | Prioridad |
|---|---|---|---|---|
| **A** | 0..55 | 0..55 | **ESTACIÓN DE BOMBEROS** (el recinto actual; portón al norte, hacia la Calle Sur) | ✅ Ya construida |
| **B** | 61..145 | 0..50 | Rowhouses de ladrillo (Boston) | Fase 4 |
| **C** | 151..245 | 0..50 | **M2 — Incendio eléctrico** (edificio pre-armado + puerta) | Fase 3 |
| **D** | 0..55 | 61..135 | Locales / tiendas (plantas bajas) | Fase 4 |
| **E** | 61..145 | 61..135 | **M1 — Incendio convencional** (edificio pre-armado + puerta) | Fase 3 |
| **F** | 151..245 | 61..135 | **PLAZA** (planters, bancas, árboles si hay) | Fase 5 |
| **G** | 251..320 | 61..135 | Edificios de relleno | Fase 4 |
| **H** | 0..55 | 153..224 | Rowhouses (casco antiguo) | Fase 4 |
| **I** | 61..145 | 153..224 | Rowhouses / tiendas | Fase 4 |
| **J** | 151..245 | 153..224 | Edificios pre-armados (filler) | Fase 4 |
| **K** | 251..320 | 153..224 | **M3 — Rescate** (edificio pre-armado + puerta) | Fase 3 |
| — | 0..320 | 236..260 | Fondo norte: edificios bajos (telón urbano) | Fase 4 |

## 5. Las casas de misión (distancias desde el portón de la estación)

| Misión | Bloque | Tema | Distancia (aprox.) | Tiempo en camión |
|---|---|---|---|---|
| **M1** | E | Incendio convencional | **~86 m** | ~15 s |
| **M2** | C | Riesgo eléctrico | **~173 m** | ~30 s |
| **M3** | K | Rescate de persona | **~290 m** | ~50 s |

- Cada casa de misión: **fachada del kit** + **puerta** (interior = escena aparte, se carga al entrar).
- La M1 = cerca (tutorial), M2 = media, M3 = lejos (la más épica).

## 6. Mapeo del kit Quaternius (Assets/Models/DowntownCity/)

| Uso | Piezas |
|---|---|
| Avenida 12 m | `Street_4Lane`, `Street_Curve_4Lane_Short`, `Street_Asphalt_9x9` |
| Calles 6 m | `Street_2Lane`, `Street_Curve_2Lane_Curb`, `Street_Asphalt_6x6` |
| Veredas | `Sidewalk_Straight_3m`, `Sidewalk_Corner_*`, `Sidewalk_Planter` |
| Marcas viales | `Decal_Crosswalk`, `Decal_Arrow*`, `Decal_Curve_*`, `T_Street_Decals` |
| Casas de misión | `Building_Small_1`, `Building_Medium_2_001`, `Building_Large_2` + `Door*` |
| Rowhouses (Boston) | módulos `Brick_*` (paredes), `Brick_Window_*`, `Cornice_*`, `Roof_*`, `Door*`, `Entrance*`, `Stairs*` |
| Plaza | `Sidewalk_Planter` + props (bancas, tachos) |
| Materiales | texturas `T_RedBrick_*`, `T_Concrete_*`, `T_Concrete_Asphalt_BaseColor`, `T_RoofSlate_*`, `T_Trim_*`, `T_Ornaments_*` |

## 7. Escenas / rendimiento (Quest 2/3)

- Tiles de ~80 × 65 m: **4 × 4 = 16 tiles** (`Ciudad_Tile_00` … `Ciudad_Tile_15`).
- Streaming: cargar el tile actual + 2–4 vecinos; descargar el resto (prefetch adelante en la dirección del camión).
- Luces **bakeadas** por tile; occlusion culling por tile; LODs en los edificios.
- Presupuesto: < 100–200 draw calls, < 750K–1.5M triángulos, 72 fps.

## 8. Orden de construcción (fases)

1. **Esqueleto de calles**: ✅ **Tile 00 hecho** — Calle Sur + Calle Oeste + cruce + veredas + marcas. (Avenida curva y demás calles: pendientes.)
2. **La manzana de la estación**: ✅ **Hecho** — veredas alrededor del recinto + rampa del portón a la Calle Sur + suelo de concreto.
3. **Las 3 casas de misión** (M1 → M2 → M3): fachadas + puertas (interiores = después).
4. **Bloques de relleno**: 🟡 **Parcial** — 6 edificios pre-armados ya colocados en el tile 00; faltan rowhouses modulares (Boston), tiendas y la plaza.
5. **La plaza** + decals + props + luces de calle.
6. **Bake + prueba de manejo**: la vuelta completa (estación → M1 → M2 → M3 → estación).

## 9. Criterios de terminado

- Se puede conducir de la estación a las 3 casas por calles coherentes (sin salir del mapa).
- Las distancias se sienten reales (~15 s / ~30 s / ~50 s).
- 72 fps estables en el simulador y en la Quest (perfil de rendimiento).
- La ciudad se ve "Boston/NYC" (ladrillo, rowhouses, avenida curva, plaza), no un grid genérico.

## 10. Decisiones pendientes

- [ ] ¿La avenida curva (Broadway) se queda? ✅ propuesta
- [ ] ¿Las posiciones M1/E, M2/C, M3/K se quedan?
- [ ] ¿El tamaño 320 × 260 m se queda o crece?
- [ ] ¿El portón del cerco se hace deslizante/interactivo al construir la ciudad? (ahora queda estático)
