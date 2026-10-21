# Optimización de rendimiento — Tile 00 y ciudad

> **Estado**: 🟡 EN CURSO — Fases 1/2, LODs y atlas implementados; streaming, bake y validación en dispositivo pendientes.
> **Fecha**: 20/10/2026 (actualizado 21/10/2026)
> **Proyecto**: UTEC_MetaQuestVR (Unity 2022.3 · Meta Quest · Built-in RP)
> **Contexto**: el tile 00 de la ciudad está construido y commiteado (`0527611`).

---

## 1. Mediciones base (Editor, sin build)

| Escena | Triángulos | Renderers | Estáticos | Con sombra | Materiales únicos |
|---|---|---|---|---|---|
| `Ciudad_Tile_00_Estacion` | **321.792** | 323 | 272 | **323 (todos)** | **82** |
| `Persistente` (Play) | **99.463** | 49 | 0 | 214 (Editor) | 41 |
| `Estacion_Interior` | **27.238** | 35 | **0** | 35 | 21 |

- `Persistente` en Editor = 502K por las manos/controles del Camera Rig; en Play bajan a **99K** (camión = 73.640).
- **Draw calls medidos en Play (Editor): 720** · batches 616 · setPass 417.
- El valor del Editor incluye probablemente la Scene View y la pasada de sombras → **hay que confirmar con Frame Debugger** antes de tomar decisiones definitivas.

### Materiales con más renderers (Tile 00)
| Material | Renderers |
|---|---|
| `MI_Trim_MetalConcrete` (veredas) | **115** |
| `M_Flowers` | 46 |
| `MI_Asphalt` | 33 |
| `M_Grass` | 31 |
| `MI_Dirt` | 18 |
| `M_Birch_Leaves` / `M_Birch_Bark` | 18 + 18 |
| `Standard` | 15 |
| `MI_StreetDecals` | 14 |

---

## 2. Problemas identificados

1. **Todo proyecta sombras.** La direccional renderiza una segunda pasada de los 323 renderers del tile y los 35 del interior.
2. **Materiales duplicados.** El tile usa 82 materiales, pero el kit tiene ~21 únicos. El import de Quaternius crea una copia de material por cada FBX (mismo nombre, instancia distinta) → rompe el batching.
3. **El interior no es estático** (0/35) → no se batchea.

---

## 3. Fase 1 — Recorte inmediato (implementar)

### 3.1 Consolidar materiales duplicados
- Agrupar materiales por **shader + textura principal + color + tiling/offset + keywords**.
- Crear/reutilizar un material canónico por grupo y reasignarlo a todos los renderers.
- Objetivo: **82 → ~21 materiales** en el tile.
- Riesgo: no fusionar por nombre a ciegas; verificar textura y shader para no cambiar la apariencia.

### 3.2 Quitar sombra a lo que no la necesita
- `ShadowCastingMode.Off` para:
  - Veredas (`MI_Trim_MetalConcrete`), asfalto (`MI_Asphalt`), decals (`MI_StreetDecals`).
  - Flores, pasto, arbustos y plantas (`M_Flowers`, `M_Grass`, `M_Bush_Leaves`, hojas de árboles).
  - Props pequeños y planos (tapas, desagües, rampa, césped).
- Mantener sombra en: edificios, estación, cerco, camión y troncos de árboles (si el coste lo permite).
- Objetivo: reducir drásticamente la pasada de sombras.

### 3.3 Marcar el interior como estático
- Marcar estáticos los objetos de `Estacion_Interior` (decoración, marcas, piso).
- Verificar que también lo estén los props del tile (272/323 ya lo están; revisar los 51 restantes).

### 3.4 Ajustar sombras
- `QualitySettings.shadowDistance`: bajar a **30–40 m**.
- Revisar cascadas y resolución de sombra para móvil.

---

## 4. Fase 2 — Verificar / forzar batching

1. **Frame Debugger** en Play: contar draw calls reales del Game view (no confiar en `UnityStats`).
2. Si el static batching no agrupa:
   - `StaticBatchingUtility.Combine(root)` sobre el tile.
   - O combinar mallas por material con una herramienta de editor (veredas 115 → 1–2 draw calls).
3. Confirmar en un **build Android** (aunque no haya Quest, se puede revisar el batching del build).

---

## 5. Fase 3 — Atlas de texturas (solo si hace falta)

- Unir texturas del kit y remapear UVs → bajar de ~21 a 3–5 materiales.
- Necesario solo si 3 tiles no entran en el presupuesto de draw calls tras Fase 1+2.

---

## 6. Objetivos numéricos

| Escenario | Triángulos | Draw calls objetivo |
|---|---|---|
| 1 tile + Persistente + Interior | ~448K | **< 120** (Quest 3), < 80 (Quest 2) |
| 3 tiles + Persistente + Interior | ~1.09M | **< 200** (Quest 3) |
| 3 tiles + casa de misión | ~1.12M | < 200 (Quest 3) |

- **Triángulos**: 3 tiles es viable en Quest 3 (~1.1M de 1.5M); en Quest 2 solo 1 tile.
- **Draw calls**: es el cuello de botella real; sin Fase 1+2, 3 tiles no entra.

---

## 7. Orden de ejecución

1. Fase 1 (barata, sin assets nuevos).
2. Medir con Frame Debugger.
3. Decidir si hace falta Fase 3 (atlas) o combinar mallas.
4. LODs de edificios y árboles.
5. Streaming de tiles (3 en Quest 3; 1–2 en Quest 2).
6. Bake de luz + occlusion culling.

---

## 8. Notas

- Los valores del Editor **no equivalen a un build**; toda medición final debe hacerse en build Android (y en Quest cuando esté disponible).
- La cuadrícula de tiles y el techo de recursos están en `PLAN_CIUDAD.md` §11 y §12 (repo).
- No modificar la apariencia de la ciudad en Fase 1: solo materiales equivalentes, sombras y flags estáticos.

---

## 9. Resultados Fase 1 (20/10/2026)

### Cambios aplicados
| Acción | Antes | Después |
|---|---|---|
| Materiales únicos — Tile 00 | 82 | **45** |
| Materiales únicos — Estacion_Interior | 21 | **16** |
| Renderers con sombra — Tile 00 | 323 | **49** |
| Renderers con sombra — Estacion_Interior | 35 | **0** |
| Estáticos — Estacion_Interior | 0 | **35 (todos)** |
| Estáticos — edificio de la estación | 0 | **21 objetos** |
| `shadowDistance` | 150 m | **40 m** (6 niveles de calidad) |
| Cascadas de sombra | 4 | **2** |

### Medición en Play (Editor)
| Métrica | Antes | Después | Variación |
|---|---|---|---|
| **Draw calls** | 720 | **452** | **−37 %** |
| Batches | 616 | 415 | −33 % |
| setPass | 417 | 330 | −21 % |
| Triángulos renderizados | 704.354 | 527.544 | −25 % |
| Renderers activos | 414 | 414 | — |
| Materiales únicos (3 escenas) | ~102 | 95 | −7 % |

### Nota sobre el batching
- El static batching de Unity **no se aplica en el Editor** para mallas no legibles (los FBX del kit); se aplica **en el build**. Android tiene `StaticBatching` habilitado en Project Settings.
- `StaticBatchingUtility.Combine` en runtime no cambió nada (mallas no legibles).
- Por eso el número real de draw calls debe medirse en un **build Android** (y en Quest cuando esté disponible). El 452 del Editor es pesimista.

### Pendiente (Fase 2/3)
- 452 draw calls sigue por encima del objetivo (<200 en Quest 3).
- Siguiente: **Frame Debugger en build** + combinar mallas por material o atlas de texturas.
- Los 3 tiles + Persistente + interiores siguen siendo el escenario objetivo (~1.1M triángulos).

---

## 10. Resultados Fase 2 — combinación de mallas (20/10/2026)

Se probaron dos variantes de `Mesh.CombineMeshes` sobre los objetos estáticos (excluyendo puertas/portones animados):

| Variante | Renderers tile | Draw calls (Editor) | Triángulos renderizados | Veredicto |
|---|---|---|---|---|
| Base (Fase 1) | 323 | 452 | 527.544 | — |
| Combine total (por material) | **112** | 420 | **639.578 (+21 %)** | ❌ pierde culling |
| Combine por celda 20 m | **166** | **452** | **593.870 (+12 %)** | ❌ sin ganancia en draw calls |

**Decisión: revertido.** La combinación reduce renderers pero:
- No baja los draw calls en el Editor (el número parece incluir la Scene View).
- Aumenta los triángulos renderizados al perder granularidad de frustum culling.

**Conclusión**: la combinación de mallas queda pendiente de validar en un **build Android** (donde el static batching sí se aplica). No debe repetirse a ciegas.

### Estado actual tras revertir
- Tile: **323 renderers**, 452 draw calls (Editor), 527K triángulos renderizados — igual que al final de Fase 1.
- `Assets/Generated/` eliminado.

---

# Anexo: LODs, streaming y edificios (Quest 2 / Quest 3)

# Plan de LODs, streaming y edificios nuevos — Quest 2 / Quest 3

> **Estado**: 🟡 CASI FINAL — escena principal 2mat con 6 edificios; prefabs finales A1/A2 para actuales y originales (incluido Large); limpieza de escenas, scripts y assets viejos hecha (21/10/2026). Pendiente: LOD1, bake y validación en dispositivo.
> **Fecha**: 20/10/2026 (actualizado 21/10/2026)
> **Proyecto**: UTEC_MetaQuestVR (Unity 2022.3 · Meta Quest · Built-in RP)

---

## 1. Presupuestos por dispositivo (objetivo seguro)

| Métrica | Quest 2 | Quest 3 |
|---|---|---|
| FPS objetivo | 72 | 72–90 |
| Triángulos renderizados | **<750K** | **<1.5M** |
| Draw calls | **<100** | **<200** |
| Tiles cargados | **3 normales / 4 en esquinas** | **4–5** |
| Triángulos por tile (renderizados) | **~120–150K** | **~150–200K** |
| Draw calls por tile | **≤25** | **≤40** |

> Nota: 750K/100 no son límites oficiales de Meta; son presupuestos conservadores del proyecto para Quest 2.

---

## 2. LODs

### 2.1 Perfiles por dispositivo

| Perfil | LOD0 | LOD1 | LOD2 | Culled / descarga |
|---|---|---|---|---|
| **Quest 2** | 0–15 m | 15–35 m | 35–60 m | >70–80 m |
| **Quest 3** | 0–20 m | 20–50 m | 50–90 m | >90–110 m |

- **3 LODs + culled** para edificios y árboles. No hace falta LOD3 salvo un objeto “hero” concreto.
- **Sin LODs** para props pequeños (macetas, bolardos, tapas) y para calles/veredas/suelo.
- **Backdrop** lejano (skyline) aparte, no como LOD.

### 2.2 Objetos
| Objeto | LOD0 | LOD1 | LOD2 |
|---|---|---|---|
| Edificios | Completo | ~50 % | ~15–20 % o caja texturizada |
| Árboles | Completo | Copa simplificada | Billboard / impostor |
| Props | — | — | — (culling por distancia) |
| Calles/suelo | — | — | — (mipmaps) |

### 2.3 Pipeline
1. Modelo base en Unity (módulos del kit) → malla combinada por material.
2. Exportar/decimar en **Blender headless** (`--background --python`) para LOD1 y LOD2.
3. Importar a Unity y crear `LODGroup` con las distancias del perfil.
4. Medir triángulos y verificar visualmente.

### 2.4 Versión B (balanceada) — atlas de 3 materiales + LOD1 Blender

> Esta versión queda como **respaldo** en `Assets/Scenes/Ciudad_Tile_00_Estacion_B_Balanceado.unity` (no está en Build Settings). La versión **A** (máxima optimización) está en la sección 2.6.

**Estado:** 32 `LODGroup` en el tile: 6 edificios + 26 árboles. Edificios con **LOD0 completo**, **LOD1 decimado al 50 %** (Small/Medium) y **caja LOD2**; árboles con billboard. El atlas de detalles ya está aplicado.

| Objeto | LOD0 | LOD1 | LOD2 / culled | Umbrales (screen-relative) |
|---|---|---|---|---|
| Small + Medium (4) | Malla completa · **3 materiales** | **Decimada 50 %** · 3 materiales | Caja de 12 tris · 1 material | `0.25 / 0.15 / 0.08 / 0.03` |
| Rowhouse + Corner (2) | Malla completa · **3 materiales** | — | Caja de 12 tris · 1 material | `0.25 / 0.10 / 0.03` |
| Árboles (26) | Árbol completo · 2 materiales | Billboard 4 tris · 1 material | Culled | `0.18 / 0.06 / 0.02` |

- **Distancias aproximadas** (FOV 60, referencia):
  - Small 17 m: LOD0 <59 m; LOD1 59–98 m; caja 98–184 m; culled >184 m.
  - Medium 25 m: LOD0 <87 m; LOD1 87–145 m; caja 145–272 m.
  - Rowhouse/Corner 10 m: LOD0 <35 m; caja 35–87 m; culled >87 m.
  - Árbol 5,5 m: LOD0 <26 m; billboard 26–79 m; culled >79 m.
- Los umbrales son **fracciones de altura en pantalla**, no metros. Para Quest 3, `QualitySettings.lodBias = 1.5` mantiene el detalle más lejos (`AjusteLODDispositivo.cs`); Quest 2 queda en 1.0.

#### Atlas balanceado

- **13 texturas de detalle** empaquetadas en `T_Atlas_Detalles.png` (2048×2048, PNG ~2,6 MB): trims (blanco, negro, verde, crema), concreto, asfalto, mármol, ladrillo interior y falsos interiores. El vidrio no entra: se mantiene aparte por transparencia.
- Cada edificio usa **3 materiales**: ladrillo (intercambiable) + `M_Atlas_Detalles` + `MI_Glass` (transparente).
- Los **8 colores de ladrillo** siguen siendo materiales separados; el atlas **no fija** el color del ladrillo. Los detalles del atlas sí quedan fijos: si se cambia un trim/techo, hay que regenerar el atlas.
- El vidrio se dejó aparte por transparencia; por eso son **3 materiales**, no 2. Aun así, los draw calls de edificios bajan de **~67 a 18**.
- Si se cambia el ladrillo de una instancia, ejecutar `UTEC > Sincronizar materiales LOD de edificios` (`Assets/Editor/SincronizarLODs.cs`): actualiza la caja **y** el LOD1 decimado.
- **Regenerar el atlas:** el constructor omite edificios que ya tienen `M_Atlas_Detalles` para no corromperlos. Para rehacer detalles hay que restaurar las mallas originales (prefab fuente) o ampliar el script para reemplazar regiones; hoy los detalles quedan fijos una vez aplicados.

#### LOD1 en Blender

- Pipeline: exportar la malla LOD0 con atlas a **OBJ** (`UTEC > Atlas > Exportar OBJ...`), decimar al **50 %** en **Blender 5.2 headless** con `Herramientas/Blender/decimar_lod.py`, importar el FBX y corregir el **espejo en X** que introduce el paso OBJ→Blender→FBX (se invierte X y se restaura el winding).
- Resultados: Small **18.152 → 9.076 tris**; Medium **25.612 → 12.806 tris**. La malla conserva las UVs del atlas y los 3 materiales.
- Rowhouse y Corner (4.008 / 9.490 tris) no se decimaron: ya son baratos y la caja es suficiente.
- Se descartó el LOD1 “sin interiores falsos” en Unity: ahorraba solo **1–5 %**. El LOD1 real se hizo con Blender + atlas.

#### Resultados de malla y draw calls

| Concepto | LOD0 | LOD1 (mid) | Lejano |
|---|---:|---:|---:|
| Small ×3 | 54.456 tris | 27.228 tris | 36 tris |
| Medium ×1 | 25.612 tris | 12.806 tris | 12 tris |
| Rowhouse + Corner | 13.498 tris | 13.498 tris (sin decimar) | 24 tris |
| **Edificios total** | **93.566 tris** | **53.532 tris** | **72 tris** |
| Árboles | 140.364 tris | — | 104 tris |
| **Total LOD** | **233.930 tris** | — | **176 tris** |

- **Draw calls de edificios:** ~67 (LOD0 original) → **18** (3 materiales × 6) → **6** (cajas). Sin static batching de build; los números reales pueden ser menores.
- **Tile completo:** 295.772 tris con LOD0; ~61,8K son calles, props y vegetación baja.
- **Atlas:** 2048×2048, 13 detalles; PNG ~2,6 MB (`Assets/Models/CiudadNueva/Atlas/T_Atlas_Detalles.png`).

#### Capturas
- `Assets/Screenshots/atlas_este.png` — fila este con atlas y LOD0.
- `Assets/Screenshots/atlas_norte.png` — fila norte con atlas y LOD0.
- `Assets/Screenshots/lod1_forzado.png` — LOD1 forzado del Small este.
- `Assets/Screenshots/lod1_medium_forzado.png` — LOD1 forzado del Medium.
- `Assets/Screenshots/lod1_small.png` — Small a ~64 m (LOD1).
- `Assets/Screenshots/lod_arboles.png` — árboles LOD0 + billboards.

#### Pendiente de esta fase
- Atlas de billboards de árboles (11 texturas → 1 material) para bajar draw calls de árboles.
- Decimar Rowhouse/Corner solo si se quiere una vista media aún más barata (son low-poly).
- Convertir los `LODGroup` montados en la escena en **prefabs/variantes reutilizables** para los demás tiles.
- Validar transiciones/pop y FPS en Quest 2/3 (sin visor disponible).
- Streaming de tiles, bake de luz y occlusion culling.

### 2.5 Caja, atlas y Blender: qué hace cada uno y decisión

| Herramienta | Qué optimiza | Qué no optimiza | ¿Fija el color? |
|---|---|---|---|
| **Caja LOD** | Triángulos y draw calls a distancia; 1 material | No reduce draw calls cerca; pierde ventanas/trims | **No**: usa el material de ladrillo compartido; si se cambia por instancia, ejecutar el sincronizador |
| **Atlas de texturas** | Draw calls: 12–13 materiales → 1–3 por edificio (con vidrio aparte) | No reduce triángulos por sí solo | **Sí** si es atlas completo: el color queda en las UVs/textura. El balanceado deja el ladrillo aparte y solo fija los detalles |
| **Blender (decimación)** | Triángulos del LOD1/LOD2 | No reduce draw calls si se mantienen los materiales | No, si se reasignan los mismos materiales |
| **Blender (bake de atlas)** | Draw calls + triángulos, si se combina con decimación | Más trabajo y menos flexibilidad de color | Depende del bake; normalmente queda fijo |

- **Caja**: bloque de 12 triángulos con el material de ladrillo. No es un atlas ni una textura horneada; es una simplificación geométrica para lejos.
- **Atlas**: junta texturas en una sola imagen y reubica UVs para que varias partes usen un solo material. Es la pieza que ataca el cuello de botella de **draw calls** de Quest 2.
- **Blender**: decima geometría y puede hornear el atlas. Decimar solo baja triángulos; si el edificio conserva 12–13 materiales, los draw calls no bajan.
- **Billboards de árboles**: la textura está horneada; si se cambia el color de un árbol, hay que regenerar su billboard.

#### Opciones y decisión

| Opción | Draw calls LOD0 (6 edificios) | Triángulos | Vista a 20–40 m | Colores | Trabajo |
|---|---:|---|---|---|---|
| Antes (solo caja/billboard) | ~67 | 93K | Caja | Muy flexible | Bajo |
| Solo Blender | ~67 | ~50K | Decimada | Flexible | Medio |
| Solo atlas | ~6–18 | 93K | Caja | Menos flexible | Medio |
| **Balanceado: 2–3 materiales + Blender** | **~18** | **~54K** | **Decimada** | **Flexible en ladrillo** | **Medio-alto** |
| Atlas completo + shader + Blender | ~6 | ~50K | Decimada | Parámetro por instancia | Alto |

**Decisión:** implementar la opción **balanceada (segunda mejor optimizada)**:

1. Mantener los **8 materiales de ladrillo** como materiales separados e intercambiables por instancia.
2. Crear un **atlas de detalles** (trims, techo, puertas, interiores) y un material compartido.
3. Cada edificio queda con **2–3 materiales**: ladrillo + atlas de detalles + vidrio (transparente, aparte); los 3 Small comparten el atlas de detalles y conservan su ladrillo.
4. El LOD1 se decima en **Blender** usando esos 3 materiales; la caja sigue como LOD2/culled.
5. Las texturas de detalle quedan fijas en el atlas; si se cambia un detalle, hay que actualizar el atlas. El ladrillo sigue siendo flexible. La caja y el LOD1 sincronizan el ladrillo con el menú.

### 2.6 Prefabs finales A1/A2 (21/10/2026)

**Escena principal:** `Ciudad_Tile_00_Estacion` usa **A2 2mat** con 6 edificios, 26 árboles y **LOD0 + caja**. El Large no está colocado en este tile.

**Estructura final de prefabs** (`Assets/Prefabs/Ciudad/Edificios/`):

| Carpeta | Prefabs | Materiales | Vidrio | Fake |
|---|---:|---|---|---|
| `A2/Paleta/` | 6 actuales | 2: `M_Atlas_2Mat` + `M_Glass_Transparente` | Transparente | Visible |
| `A1/Paleta/` | 6 actuales | 1: `M_Atlas_A1_Actual` | Opaco | Oculto |
| `A2/Original/` | 5 (incluye Large) | 2: `M_Atlas_A2_Final` + `M_Glass_Transparente` | Transparente | Visible |
| `A1/Original/` | 5 (incluye Large) | 1: `M_Atlas_A1_Final` | Opaco | Oculto |

- **A2 actual:** atlas 2mat con ladrillo, detalle, fake y trims alternos; el vidrio queda separado y transparente.
- **A1 actual:** atlas propio `T_Atlas_A1_Actual.png` (2048×4096) que incluye la región 2mat + un bloque de vidrio opaco; **1 material**. Las ventanas ya no muestran el fake.
- **Cajas LOD1:** las UV se hornearon a un color de ladrillo del atlas; no se usan `MaterialPropertyBlock` porque no se serializan al guardar el prefab. A1/A2 actuales y originales usan la misma textura de su atlas.
- **LODs:** LOD0 + caja + culled; sin LOD1 decimado en esta versión.
- **Large:** `EDF_A1_Original_Large` y `EDF_A2_Original_Large` listos, LOD0 + caja; no colocados en el tile.
- **Convención:** `EDF_<A1|A2>_<Paleta|Original>_<Tipo>[_<Color>]`.

#### Estado de escenas

Se conservan solo:
- `Assets/Scenes/Ciudad_Tile_00_Estacion.unity` — principal 2mat.
- `Assets/Scenes/Ciudad_Tile_00_Estacion_B_FakeFix.unity` — última B con verde/fake/trims.
- `Assets/Scenes/Ciudad_Tile_00_Estacion_B_Balanceado.unity` — B original.

Se eliminaron las copias temporales/fallidas (`2Mat_v1`, `B_FakeFix_v1..v4`, `A1_VidrioOpaco`, `A2_*`, `B_TransportTemp`, etc.). La escena principal sigue en Build Settings junto con `Persistente` y `Estacion_Interior`.

#### Scripts y assets

- **Se mantienen:** `Constructor2MatMain.cs`, `ConstructorPrefabsFinales.cs`, `ConstructorBVerdeFake.cs`, `ConstructorPaletaPartes.cs`, `ConstructorAtlasArboles.cs`, `ConstructorAtlasArbolesLOD0.cs`, `ConfiguracionAndroid.cs` y `AjusteLODDispositivo.cs`.
- **Obsoletos movidos fuera de Assets:** `ConstructorA1A2Limpio.cs`, `ConstructorA2Actual.cs`, `CorrectorA2Actual.cs`, `ConstructorOriginalA2.cs`, `ConstructorA1OriginalLimpio.cs`, `ConstructorAtlasCompleto*.cs`, `ConstructorAtlasEdificios.cs`, `ConstructorB3Escena.cs`, `Exportador*.cs`, `ImportadorLOD1A2Original.cs` y `SincronizarLODs.cs` (respaldo en `%LOCALAPPDATA%/Temp/opencode/UTEC_Editor_Obsoletos_2026-10-21`).
- **Assets viejos eliminados:** `A2/`, `A2Original/`, `AtlasCompleto/`, `A1OriginalLimpio/`, `B3Fix/`, `FinalA1 1/`, `FinalA2 1/`, mallas duplicadas de `2MatMain`, `FinalA1`, `FinalA2`, `FinalA1_Actual`, `BVerdeFake` y `Atlas`.
- `Assets/Models/CiudadNueva` bajó de **~574 MB a ~200 MB**.
- **No hay commits nuevos.**

#### Capturas

- Escena principal: `main_2mat_medium.png`, `main_2mat_east.png`.
- Prefab A1 actual (vidrio opaco, sin fake): `a1_actual_medium.png`.
- Prefab A2 actual (vidrio transparente, fake visible): `a2_actual_medium.png`.
- Prefab A2 original: `a2_original_medium.png`.
- B fix: `b_fake_fix_medium.png`, `b_fake_fix_east.png`.
- Trabajo/pruebas: `Assets/Screenshots/_trabajo/`.

### 2.7 Prueba 2mat y decisiones

- La escena principal usa el atlas `M_Atlas_2Mat` (ladrillo + detalle/fake + trims alternos) y `M_Glass_Transparente`; 12 draw calls de edificios en LOD0 + 6 cajas.
- Las UVs de ladrillo/trims se hornearon con **`Mathf.Repeat`** para evitar manchas y bandas por UVs fuera de 0–1.
- La caja LOD1 no usa property blocks; el color se resolvió con UVs dentro del atlas.
- **Pendiente:** LOD1 decimado, bake de luz, medición real en Quest 2/3 y decidir si A2 2mat o B fix 3–4 materiales es la versión visual definitiva.

---

## 3. Streaming de tiles

- Tiles de **80×65 m**, 4×4 = 16.
- **Prioridad de carga**:
  1. Tile actual (siempre).
  2. Tile en la dirección de movimiento.
  3. Tile dentro del frustum.
  4. Tile anterior (gracia).
- **Precarga**: ~25–30 m antes de cruzar el borde.
- **Descarga**: ~100 m del centro del tile o cuando el presupuesto se exceda.
- **Máximo**: 3 tiles en Quest 2 (4 en esquinas si el presupuesto lo permite); 4–5 en Quest 3.
- **Mitigaciones visuales**:
  - Fog a ~120–150 m del color del cielo.
  - **Suelo global** (un plano que cubra toda la ciudad, no por tile).
  - Prefetch para evitar pop-in.
- En esquinas de 4 tiles: cargar actual + adelante + lateral visible; descargar el de atrás.

---

## 4. Edificios nuevos y variantes

### 4.1 Dos modelos base
| Modelo | Dimensiones | Estilo |
|---|---|---|
| **Rowhouse_A** | ~9 × 12 m · ~12.5 m alto | Boston rowhouse de 3 pisos, ventana bay, cornisa, techo de pizarra |
| **Corner_Shop_B** | ~12 × 12 m · ~14 m alto | Esquina con local/escaparate en planta baja y 2 pisos de ladrillo |

- Ensamblados en **Unity** con módulos del kit (`Brick_*`, `Cornice_*`, `Roof_*`, `Door_*`, `Entrance_*`).
- Materiales del kit (`MI_RedBrick`, `MI_Trim`, `MI_Ornaments`, `MI_Glass`, `MI_Roof_Slate`).
- Malla combinada por material, **1 BoxCollider**, pivote al suelo, fachada a **+Z**.
- Presupuesto: **≤10K triángulos**, **≤5 materiales** por modelo.

### 4.2 Variantes de los 3 existentes
| Variante | Cambio |
|---|---|
| `Building_Small_1_Pale` | `MI_RedBrick` → `MI_RedBrick_Pale` |
| `Building_Medium_2_001_Pale` | Ídem + puerta/entrada distinta |
| `Building_Large_2_Pale` | Ídem |
| `Building_Small_1_Mirror` (opcional) | Fachada espejada en Blender |

### 4.3 Colocación
- **Fila este**: `Building_Small_1` (sur) · `Rowhouse_A` (centro) · `Corner_Shop_B` (norte/esquina).
- **Fila norte**: `Building_Medium_2_001_Pale` · `Building_Small_1_Pale` · `Building_Large_2` (o mirror).

### 4.4 Orden
1. Rowhouse_A.
2. Corner_Shop_B.
3. Variantes pale/mirror de los existentes.
4. Reemplazar instancias repetidas en el tile.
5. Medir triángulos/materiales y capturas.
6. LODs con los perfiles Quest 2 / Quest 3.
7. Draw calls: combinar mallas por material/celda + atlas de texturas.
8. Streaming con presupuesto dinámico.
9. Bake de luz + occlusion.

---

## 5. Criterios de aceptación
- Estilo coherente con el kit.
- Sin huecos, z-fighting ni piezas flotantes.
- Colisión y escala correctas.
- Presupuesto de triángulos/materiales/draw calls respetado.
- LODs sin “pop” exagerado.
- Prefabs reutilizables en otros tiles.

---

## 6. Blender
- Blender 5.2 instalado en `C:\Program Files\Blender Foundation\Blender 5.2`.
- Uso en **headless** para espejar y decimar; **no requiere MCP**.
