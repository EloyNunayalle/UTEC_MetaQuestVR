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

> **Estado**: 🟢 FINAL TÉCNICO — A2 3mat y A1 2mat, LOD1 decimado, corrección de escalera Rowhouse/Corner y base (22/10/2026). Pendiente: bake y validación en dispositivo.
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
- Atlas de billboards de árboles (11 texturas → 1 material): **hecho** en la escena principal.
- LOD1 decimado de árboles: **hecho** en la escena principal (ver 2.8).
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

**Escena principal:** `Ciudad_Tile_00_Estacion` usa **A2 3mat** con 6 edificios, 26 árboles y **LOD0 + LOD1 decimado + caja**. Los 6 edificios son instancias de los prefabs A2; el Large no está colocado en este tile.

**Estructura final de prefabs** (`Assets/Prefabs/Ciudad/Edificios/`):

| Carpeta | Prefabs | Materiales | Vidrio | Fake |
|---|---:|---|---|---|
| `A2/Paleta/` | 6 actuales | **3: ladrillo + `M_Detalle_BVerdeFake` + `M_Glass_Transparente`** | Transparente | Visible |
| `A1/Paleta/` | 6 actuales | **2: ladrillo + `M_Atlas_A1_2Mat`** | Opaco | Oculto |
| `A2/Original/` | 5 (incluye Large) | **3: ladrillo original + `M_Atlas_Original_A2` + `M_Glass_Transparente`** | Transparente | Visible |
| `A1/Original/` | 5 (incluye Large) | **2: ladrillo original + `M_Atlas_Original_A1`** | Opaco | Oculto |

- **A2 actual:** reconstruido desde el B fix, conservando su atlas original de detalle/fake. **3 materiales**: ladrillo + detalle/fake + vidrio. Se eliminó el renderer/material extra de trim alterno para cumplir el máximo de 3.
- **A1 actual:** **2 materiales**: ladrillo + atlas opaco `M_Atlas_A1_2Mat`. El atlas elimina el fake y las caras de vidrio se mandan a un bloque de ventana opaca.
- **Escaleras Rowhouse/Corner:** se elevó el edificio **+1.006 m**, se dejó la escalera en el suelo y se agregó una base/fundación. El umbral de la puerta coincide con el último escalón.
- **Originales:** reconstruidos desde las mallas/materiales originales con la misma arquitectura: A2 3mat y A1 2mat. El fake quedó solo en las caras de ventana; techos, pisos y detalles usan su material propio.
- **Techos/tapas:** se agregó una tapa superior a los 22 prefabs para cerrar la vista desde arriba; se eliminaron los bordes invisibles y el cambio de color por coplanaridad/mipmaps.
- **Cajas:** usan el material de ladrillo; no se usan `MaterialPropertyBlock`.
- **LODs:** LOD0 + **LOD1 decimado 50 %** + caja + culled. Umbrales `0.25 / 0.12 / 0.05 / 0.02`.
- **LOD1:** 22 mallas generadas con Blender 5.2 (`decimar_lote.py`), FBX importado y corregido el espejo X del pipeline OBJ→Blender→FBX. El LOD1 conserva UVs y materiales del LOD0. Triángulos actuales: Small ~9–10K, Medium 30K, Corner 7.8K, Rowhouse 2K.
- **Large:** `EDF_A1_Original_Large` y `EDF_A2_Original_Large` listos con LOD0 + LOD1 + caja; no colocados en el tile.
- **Convención:** `EDF_<A1|A2>_<Paleta|Original>_<Tipo>[_<Color>]`.

#### Estado de escenas

Se conservan solo:
- `Assets/Scenes/Ciudad_Tile_00_Estacion.unity` — principal con A2 3mat.
- `Assets/Scenes/Ciudad_Tile_00_Estacion_B_FakeFix.unity` — última B con verde/fake/trims.
- `Assets/Scenes/Ciudad_Tile_00_Estacion_B_Balanceado.unity` — B original.

Se eliminaron las copias temporales/fallidas (`2Mat_v1`, `B_FakeFix_v1..v4`, `A1_VidrioOpaco`, `A2_*`, `B_TransportTemp`, etc.). La escena principal sigue en Build Settings junto con `Persistente` y `Estacion_Interior`.

#### Scripts y assets

- **Se mantienen:** `Constructor2MatMain.cs`, `ConstructorPrefabsFinales.cs`, `ConstructorBVerdeFake.cs`, `ConstructorPaletaPartes.cs`, `ConstructorAtlasArboles.cs`, `ConstructorAtlasArbolesLOD0.cs`, `ConfiguracionAndroid.cs`, `AjusteLODDispositivo.cs`, `RevisorPrefabs.cs`, `CorrectorCarasActual.cs`, `ConstructorFinal3Mat2Mat.cs`, `ExportarLOD0OBJ.cs`, `AgregarLOD1Prefabs.cs` y `CorregirEspejoLOD1.cs`.
- **Obsoletos movidos fuera de Assets:** `ConstructorA1A2Limpio.cs`, `ConstructorA2Actual.cs`, `CorrectorA2Actual.cs`, `ConstructorOriginalA2.cs`, `ConstructorA1OriginalLimpio.cs`, `ConstructorAtlasCompleto*.cs`, `ConstructorAtlasEdificios.cs`, `ConstructorB3Escena.cs`, `Exportador*.cs`, `ImportadorLOD1A2Original.cs` y `SincronizarLODs.cs` (respaldo en `%LOCALAPPDATA%/Temp/opencode/UTEC_Editor_Obsoletos_2026-10-21`).
- **Assets viejos eliminados:** `A2/`, `A2Original/`, `AtlasCompleto/`, `A1OriginalLimpio/`, `B3Fix/`, `FinalA1 1/`, `FinalA2 1/`, mallas duplicadas de `2MatMain`, `FinalA1`, `FinalA2`, `FinalA1_Actual`, `BVerdeFake` y `Atlas`.
- `Assets/Models/CiudadNueva` bajó de **~574 MB a ~207 MB** (incluye LOD1Final).
- **Commit base:** `492c1ce` (prefabs A1/A2, atlas 2mat y limpieza). Los cambios de LOD1/corrección de caras y fake se commitean al finalizar esta pasada.

#### Capturas

- Escena principal: `main_2mat_medium.png`, `main_2mat_east.png`.
- Prefab A1 actual (vidrio opaco, sin fake): `a1_actual_medium.png`.
- Prefab A2 actual (vidrio transparente, fake visible): `a2_actual_medium.png`.
- Prefab A2 original: `a2_original_medium.png`.
- B fix: `b_fake_fix_medium.png`, `b_fake_fix_east.png`.
- Trabajo/pruebas: `Assets/Screenshots/_trabajo/`.

### 2.7 Arquitectura final 3mat/2mat

- La escena principal usa las instancias **A2 3mat** con LOD0 + LOD1 + caja.
- **A2:** ladrillo separado + detalle/fake original del B fix + vidrio. No se repackea el fake, por eso desaparecen los triángulos/rectángulos negros del 2mat.
- **A1:** ladrillo separado + atlas opaco sin fake con ventanas opacas.
- **Rowhouse/Corner:** edificio subido 1.006 m, escalera en el suelo, base nueva y puerta/marco completos.
- **LOD1:** regenerado para los 12 prefabs actuales y los originales.
- **Respaldo previo:** `Backups/Ciudad_Antes_3Mat_2026-10-22/` (escena, prefabs A1/A2 y prefabs fuente Rowhouse/Corner), fuera de `Assets/` para no duplicar GUIDs.
- **Pendiente:** bake de luz y medición real en Quest 2/3.

---

### 2.8 LODs de árboles en la escena principal (25/10/2026)

- **LOD0:** malla completa con el atlas de árboles (1 material).
- **LOD1:** árbol decimado al **50 %** (BirchTree_3 al **70 %** para conservar la copa), conservando UVs y material del atlas. Mallas finales en `Assets/Models/StylizedNature/LOD1Final`.
- **LOD2:** billboard (atlas propio, a doble cara desde el fix anterior).
- **Culled:** umbral efectivo **0.02**.
- Umbrales: `0.18 / 0.06 / 0.02`. El LOD vacío anterior en 0.02 hacía que el árbol se cullara realmente a **0.06**; ahora el billboard es el último LOD visible y el culling es a 0.02.
- Triángulos de las 26 instancias: **140.364 (LOD0) → 75.622 (LOD1, −46 %)**; billboards 208 tris. El LOD1 copia sombras/probes del LOD0; el billboard no proyecta sombras.
- Pipeline: exportar la malla atlas a OBJ, decimar en Blender 5.2 headless (`Herramientas/Blender/decimar_lod.py`), importar el FBX, corregir el espejo X y crear las mallas finales. El script `UTEC > Árboles > 4 Insertar LOD1 decimado en escena` arma los `LODGroup` (idempotente).
- Nota: los edificios conservan su LOD vacío final; su caja se culla al 0.05, no al 0.02. Es un ajuste independiente que no se tocó en esta pasada.

### 2.9 Optimización de draw calls de la escena (25/10/2026)

- **Baseline en build Windows Development** con static batching real: 620–742 draw calls y 450–680K triángulos en las vistas principales.
- **Props combinados por material** (226 renderers → 6 combos): veredas, flores, pasto, asfalto, tierra y decals. Se conservan los GameObjects originales con colliders; solo se desactivan sus renderers.
- **Estación de bomberos**: 18 renderers estáticos → 14 combos por material; se agregó LODGroup `LOD0 29 / LOD1 15 / LOD2 1 caja`, manteniendo portón, puerta y mecanismos dinámicos.
- **Cerco**: 19 renderers estáticos → 2 combos; la rampa se marcó estática. Se preserva el portón dinámico.
- **Sombras**: 59 casters apagados (vegetación baja, vidrio/fake, props, cerco y mecanismos). Se mantiene sombra en edificios, árboles LOD0 y estructura principal de la estación. En Android, `AjusteLODDispositivo` fija **1 cascada** y hard shadows.
- **Perfil Quest (Medium, 1 cascada)**: 226–238 draw calls en vistas principales y **106** en la esquina lejana con LOD de estación; 399–488K triángulos.
- **Sin sombras**: 139–150 draw calls y 203–376K triángulos.
- **Descartado**: GPU instancing en árboles/billboards no redujo draw calls con LODGroup; el cambio de Read/Write en las mallas tampoco cambió el batching. Ambos se revirtieron.
- **Pendiente**: los draw calls 226–238 siguen por encima del objetivo Quest 3 (<200) y Quest 2 (<100). El siguiente trabajo es reducir árboles/edificios/estación en LOD0, evaluar sombras de árboles y avanzar con streaming/atlas de edificios.

### 2.10 Diagnóstico de batching y submeshes (25/10/2026)

- **El static batching del build sí funciona**: 115/150 renderers activos están en batches estáticos; árboles 51/52 y billboards 25/26. Probar los árboles como no estáticos empeora (233 → 239 draw calls) con los mismos triángulos; se mantiene el static batching.
- **Read/Write y GPU instancing**: descartados. El test de instancing anterior estaba contaminado porque los árboles seguían siendo estáticos.
- **Submeshes**: los meshes fuente de árboles, billboards, estación combinada y props ya son **1 submesh / 1 material**. Los 612/626 submeshes del log de build son submeshes internos de los static batches, no de los meshes fuente. **No hay que fusionar submeshes** en estos assets.
- **Medición del tile**: 150 renderers, main pass 139–150 draw calls, sombras ~94, total 226–238; 399–488K triángulos. La cámara del diagnóstico no reproduce bien la selección de LOD de árboles/billboards (todos aparecen activos; los edificios muestran LOD0); no se usa para concluir que los LODs están rotos. Falta medir con la cámara real del juego.
- **El juego completo no es solo el tile**: `GestorEscenas` carga `Persistente + Ciudad_Tile_00` de forma aditiva, `JuegoBomberos` instancia el camión y `Estacion_Interior` se carga por proximidad. La medición del tile no incluye camión, manos/controles ni interior; el presupuesto real es mayor.
- **Plan corregido**: (1) medir el juego completo con la cámara real; (2) agrupar los 26 árboles por celdas de 25 m → 7 LODGroups; (3) sombras; (4) camión/interior; (5) HLOD por tile y streaming multi-tile.
- **Resultado de la agrupación por celdas (25/10/2026)**: 26 árboles → **7 LODGroups**; renderers activos **150 → 93**; draw calls **226–238 → 194–205**; esquina lejana **106 → 98**; 403–490K triángulos. Los LOD0/LOD1/billboard se combinan por celda y los colliders originales se conservan. Captura: `Assets/Screenshots/Arboles_Celdas_25m.png`.
- **Estado**: el tile solo queda en ~200 draw calls (Quest 3 casi cumplido; Quest 2 sigue >100). El juego completo ya está medido en §2.11: **431–541 draw calls con sombras** y **429–539 con interior + optimizador**, todavía lejos de <200.

### 2.11 Juego completo: medición real (25/10/2026)

- Build **Persistente + Ciudad_Tile_00_Estacion + Estacion_Interior**, con cámara de diagnóstico y carga forzada del interior.
- **Con sombras (sin interior cargado)**: 431–541 draw calls y 460–817K triángulos. **Sin sombras**: 309–371 draw calls y 309–520K. La pasada de sombras cuesta ~**167 draw calls**.
- **Con interior + optimizador**: **429–539 draw calls** (centro 116; vistas principales 518–539; esquina 429) y **452–809K** triángulos; **296 renderers activos**. Grupos dominantes: **Camera Rig 87 → 52** tras el optimizador, **Camión 71**, **interior 35**, **HUD/selector/manguera/marcador 23**; el tile queda en ~93.
- **Optimizador de runtime** (`OptimizadorJuegoCompleto`): apaga sombras de rig/camión/HUD/interior y deja el rig en **52 renderers** (solo manos/controladores, sin rayos/retículas/glows/debug). No baja materialmente el total: el rig, el camión y el interior siguen siendo el cuello.
- **Conclusión**: el tile solo (194–205) no representa al juego. El objetivo <200 no se alcanza ni con el tile actual: hacen falta **camión combinado/LOD**, **rig simplificado**, **interior/HUD combinados** y **HLOD por tile**. Con el contenido actual, 3 tiles completos no son viables; el objetivo realista es 1 tile cercano + HLOD de vecinos.
- **Archivos de esta fase (sin commit)**: `ConstructorDiagnosticoJuegoCompleto.cs`, `OptimizadorJuegoCompleto.cs` y los cambios de `DiagnosticoRT.cs`. La escena temporal del tile se restaura automáticamente.

### 2.12 Presupuesto realista y comparación con las guías de Meta (25/10/2026)

> Investigación de referencia. No se modificó el proyecto en esta sección; solo se contrastó el presupuesto del plan con las guías oficiales de Meta y Unity.

#### 1. Rangos oficiales de Meta

Meta publica **rangos de ejemplo por frame**, no un límite duro ni un presupuesto por tile:

| Plataforma | “Busy simulation” | “Medium simulation” | “Light simulation” | Triángulos |
|---|---:|---:|---:|---:|
| Quest 2 / Quest Pro | **80–200** | 200–300 | 400–600 | **750K–1M** |
| Quest 3 / Quest 3S | **200–300** | 400–600 | 700–1000 | **1.3M–1.8M** |

- “Busy” incluye redes, VoIP, animación, skinning y muchos NPCs.
- “Light” incluye salas de escape, puzzles y cooperativos con pocos cambios de estado.
- Aplicaciones interactivas: **mínimo 72 FPS** (13,9 ms por frame).
- El rango depende de cambios de pipeline, hilos principal/render, API gráfica, uso de indirect draws, etc.

Fuente: [Meta - Testing and performance analysis](https://developers.meta.com/vr/documentation/unity/unity-perf/).

#### 2. Presupuesto del proyecto frente a Meta

| Métrica | Plan actual | Meta (referencia) | Lectura |
|---|---:|---|---|
| Draw calls Quest 2 | **<100** | 80–200 busy | Meta muy conservadora, en el borde bajo de “busy”. |
| Draw calls Quest 3 | **<200** | 200–300 busy | Por debajo de “busy”; conservadora. |
| Triángulos Quest 2 | **<750K** | 750K–1M | Alineado, en el límite bajo. |
| Triángulos Quest 3 | **<1.5M** | 1.3M–1.8M | Alineado, en el rango medio. |
| Tiles completos | **3–5** | No es una métrica oficial | Depende de HLOD/streaming, no de cargar todo. |
| Draw calls por tile | **25–40** | No es una métrica oficial | Solo realista para tiles lejanos en HLOD, no para un tile cercano completo. |

Medición actual del proyecto:

- **Tile solo**: 194–205 draw calls con sombras; 139–150 sin sombras.
- **Juego completo**: 431–541 con sombras; 309–371 sin sombras.
- **Triángulos**: 452–817K.
- **Sombras**: ~167 draw calls del juego completo.
- **Renderers del juego completo**: 296 activos tras el optimizador; Camera Rig 52, Camión 71, interior 35, HUD/selector/manguera/marcador 23; tile ~93.

Conclusión: **el juego completo actual cae en “light” de Quest 2 y “medium” de Quest 3** según la tabla de Meta, no en “busy”. Pero el proyecto tiene scripts, física, manos/controladores, camión e interior; no es una experiencia “light”. El número aislado no decide: hay que medir CPU/GPU en visor.

#### 3. ¿Es realista el presupuesto?

- **<100 en Quest 2** es una meta **más conservadora que la guía de Meta**. Es posible para un tile cercano muy optimizado + HLOD, sin sombras en tiempo real y con rig/camión simplificados; no con el juego completo actual de 431–541.
- **<200 en Quest 3** es también conservador (Meta pone “busy” en 200–300). Es alcanzable con **1 tile cercano + HLOD**, rig/camión/interior simplificados y sombras horneadas; no con 3 tiles completos.
- **25–40 draw calls por tile** no es un presupuesto de tile completo: es un presupuesto de **tile lejano/HLOD**. Un tile cercano con árboles, edificios, estación, props y sombras ronda los 100–200.
- **3–5 tiles completos** no es realista con la densidad actual. Con 194 draw calls por tile, 3 tiles ya son ~582 antes de sumar el juego completo. Para 3–5 tiles hay que usar **1–2 tiles cercanos completos + HLOD/impostores** para el resto.

#### 4. Mundo abierto en Quest 2

Meta lo dice explícitamente en su guía móvil:

> “The advice to early VR titles is to favor modest ‘scenes’ instead of ‘open worlds,’ and Occlusion Culling may be overkill for modest scenes.”

Traducción: para títulos tempranos de VR conviene favorecer **escenas modestas** antes que **mundos abiertos**; la oclusión puede ser excesiva para escenas pequeñas.

No significa que sea imposible. Significa que un mundo abierto en Quest 2 exige:

- Tiles pequeños/chunks y **streaming** con Addressables o escenas aditivas.
- **HLOD e impostores** para todo lo lejano.
- **Luces horneadas**, light probes y sombras estáticas; nada de sombras en tiempo real para la ciudad.
- **Oclusión** y distancias de culling por capa.
- **Niebla/far clip** agresivos.
- **Low-poly**, materiales compartidos, atlas y shaders mobile/unlit.
- **1–2 tiles cercanos** como máximo; el resto, proxies/impostores.

El caso oficial de Meta, **Dead & Buried 2**, usa LODs y carga/descarga por posición para gestionar el mundo; es el modelo a seguir, no cargar 3–5 tiles completos. Fuente: [Meta - Open World Games and Asset Streaming](https://developers.meta.com/vr/documentation/unity/po-assetstreaming/).

#### 5. Dónde optimizan más los proyectos Quest y qué falta aquí

1. **HLOD e impostores**: agrupar objetos lejanos en una malla/impostor por zona o material. Es lo que más reduce draw calls en mundo abierto. Aquí solo hay LOD por objeto y agrupación de árboles por celda; faltan edificios, estación, props y **HLOD por tile**.
2. **Sombras y luces horneadas**: Meta recomienda lightmaps, light probes, shaders mobile/unlit y evitar shadow buffers. Aquí la pasada de sombras del juego completo cuesta ~167 draw calls. Es la palanca más rápida.
3. **Oclusión, `layerCullDistances`, far clip y niebla**: la ciudad tiene edificios que ocluyen; hay que hornear oclusión y limitar props pequeños por distancia. Está pendiente.
4. **Juego completo, no solo el tile**: Camera Rig 52 (87 antes; 280 en total), Camión 71 (42 en prefab, 21 materiales, sin LOD), interior 35 y HUD/selector/manguera/marcador 23. El tile son solo ~93 renderers; el resto es el 55–60 % del coste del juego completo.
5. **URP + SRP Batcher y shaders mobile**: el proyecto usa Built-in RP, que **no tiene SRP Batcher**. El SRP Batcher no reduce el número de draw calls, pero reduce el coste CPU de prepararlos; URP también permite shaders mobile más baratos. El shader `Standard` de Built-in es pesado para Quest 2.
6. **GPU Instancing / BatchRendererGroup / atlas**: el static batching combina mallas estáticas del mismo material, pero no resuelve repeticiones con LODGroups ni materiales únicos. Para árboles, arbustos y props repetidos conviene instancing/indirect o HLOD. Además: camión 21 materiales, rig 26, edificios 3 por edificio; los atlas y la consolidación de materiales reducen cambios de estado.
7. **Streaming multi-tile y descarga**: `GestorEscenas` carga Persistente + tile y el interior por proximidad, pero no hay un gestor de 3–5 tiles con presupuesto dinámico ni descarga de la ciudad al entrar al interior.
8. **Funciones Quest**: FFR, Application SpaceWarp y Dynamic Resolution ayudan a GPU/fill rate, no a los draw calls de CPU. No sustituyen HLOD ni batching.
9. **Perfilado en dispositivo**: el build de Windows es un proxy. Hay que usar OVR Metrics Tool, RenderDoc Meta fork y Unity Profiler en Quest 2/3 para saber si el límite es CPU (draw calls/scripts) o GPU (overdraw/sombras).
10. **Dirección de arte/contenido**: low-poly ayuda a triángulos, pero no a draw calls. Hay que reducir **número de objetos, materiales y estados**, no solo polígonos. Un mundo abierto Quest 2 se construye con fachadas falsas, impostores, niebla y zonas pequeñas.

#### 6. Presupuesto recomendado

| Objetivo | Quest 2 | Quest 3 |
|---|---|---|
| Tiles cercanos completos | **1** | **1–2** |
| Tiles lejanos | **1–2 HLOD** | **2–3 HLOD** |
| Draw calls totales | **150–250** | **250–400** |
| Triángulos | **750K–1M** | **1.3M–1.8M** |
| Sombras | **Horneadas / blob** | **Horneadas + pocas dinámicas** |
| Rig | Manos/controladores simples | Manos/controladores simples |
| Camión | Combinado + LOD | Combinado + LOD |

- **<200 en Quest 3**: alcanzable solo con **1 tile cercano (~120–150) + HLOD + rig/camión/interior simplificados (~50–80)**.
- **<100 en Quest 2**: alcanzable solo con **1 tile muy optimizado + HLOD, sin sombras en tiempo real**, no con 3 tiles.
- **3–5 tiles**: solo si los lejanos son **HLOD/impostores**, no geometría completa.

#### 7. Fuentes

- Meta, *Testing and performance analysis* (tabla de draw calls/triángulos y FPS): https://developers.meta.com/vr/documentation/unity/unity-perf/
- Meta, *Mobile performance* (escenas modestas vs. mundos abiertos, luces horneadas, batching, shaders): https://developers.meta.com/vr/documentation/unity/unity-mobile-performance-intro/
- Meta, *Draw Call Cost Analysis for Meta Quest* (coste GPU de draws, materiales y vértices): https://developers.meta.com/vr/documentation/unity/po-draw-call-analysis/
- Meta, *Open World Games and Asset Streaming* (Dead & Buried 2, LOD y carga/descarga): https://developers.meta.com/vr/documentation/unity/po-assetstreaming/
- Meta, *Dynamic Resolution*: https://developers.meta.com/vr/documentation/unity/dynamic-resolution-unity/
- Unity, *Static batching*: https://docs.unity3d.com/Manual/static-batching.html
- Unity, *GPU instancing*: https://docs.unity3d.com/Manual/GPUInstancing.html
- Unity, *SRP Batcher*: https://docs.unity3d.com/Manual/SRPBatcher.html
- Unity, *Occlusion Culling*: https://docs.unity3d.com/Manual/OcclusionCulling.html

### 2.13 Plan de siguiente fase (28/10/2026)

> Anotación de planificación. No se modificó el proyecto en esta sección.

#### 1. Lectura de los datos actuales

- **431–541 draw calls** con sombras y **309–371** sin sombras en el juego completo; **452–817K triángulos**. Los triángulos están dentro del rango de Quest 2/3: el cuello son los draw calls, no la geometría.
- **Tile solo:** 194–205 con sombras (main ~100–110 + sombras ~94). El resto del juego es ~55–60 % del coste.
- **Camión:** 71 renderers, 21 materiales, sin LOD.
- **Camera Rig:** 52 renderers tras el optimizador (280 en total, 87 antes de optimizar).
- **Interior:** 35 renderers; se carga a 45 m aunque no se vea.
- **HUD/selector/manguera/marcador:** 23 renderers.
- **Sombras:** ~167 draw calls. El optimizador apaga las de camión/rig/interior, pero la medición solo baja ~2 draw calls: falta auditar el reparto real entre ciudad estática y objetos dinámicos.
- **Iluminación:** sin lightmaps (`m_LightingDataAsset: {fileID: 0}` en las tres escenas), sin occlusion bake, niebla desactivada y far clip en **1000**. Cuatro luces puntuales realtime y una directional `Mixed` sin bake.
- **UV2:** ningún modelo tiene `Generate Lightmap UVs` activado; es un requisito antes de hornear.
- **Interior:** no tiene luces propias; un bake de la ciudad puede dejarlo plano u oscuro.
- **Medición:** la cámara de diagnóstico no reproduce la selección de LOD; 431–541 es un techo, no el valor real del juego.

#### 2. Siguiente paso real

El siguiente paso **no es hornear ni HLOD**: es reducir el coste del juego completo que no es el tile (camión, rig, interior y HUD), y en paralelo auditar las sombras. El tile ya está cerca de su límite práctico; el 55–60 % del coste está fuera.

Hornear es la palanca más grande en teoría, pero también la de mayor riesgo (UV2, memoria de lightmaps, escenas aditivas, interior sin luces). Primero conviene quitar renderers, que además abaratan el pase de sombras.

#### 3. Fases

| Fase | Trabajo | Impacto esperado | Riesgo | Gate |
|---|---|---|---|---|
| **0. Baseline y auditoría** (0,5–1 día) | Medir con la cámara real; separar main/sombra por grupo (tile, camión, rig, interior, HUD); verificar que el optimizador aplica `shadowCastingMode=Off` antes de medir | Ninguno; da dirección | Bajo | Tabla de draw calls por grupo y pase |
| **1. Juego completo** (3–5 días) | Camión combinado por material + LOD; rig solo manos/controladores; interior combinado y desactivado fuera; HUD batcheado | −120 a −160 main | Medio | Camión ≤25, rig ≤8, interior ≤10; sin romper input/equipamiento |
| **2. Sombras** (2–4 días) | Sombras dinámicas off + blob; generar UV2; bake con directional `Mixed` + shadowmask y puntuales `Baked`; fallback sin sombras + AO | −80 a −140 | Medio-alto | Pase de sombras ≤30; lightmaps 8–16 MB; sin artefactos |
| **3. Tile main** (2–3 días) | Occlusion Culling; far clip 1000 → 150–200; niebla; `layerCullDistances` para pasto/flores/decals; HLOD de edificios lejanos | −20 a −40 en tile | Medio | Tile main ≤90; sin pop-in grave |
| **4. Multi-tile** (1–2 semanas) | 1 tile cercano completo + 1–2 vecinos en HLOD; carga/descarga por distancia y presupuesto | 3 tiles ~250–330 | Alto | 3 tiles ≤350; hitches <2 ms |
| **5. Visor** | Quest 2/3, OVR Metrics, Profiler, RenderDoc Meta; FFR/ASW/Dynamic Resolution | Validación real | — | 72 FPS estables; sin throttling |

Detalle de la Fase 1:

- **Camión:** combinar casco estático por material (21 → ~6), mantener ruedas/puertas/escalera/manguera/panel/palanca/GPS separados, LOD1/caja y sombra off o blob. **71 → 15–25.**
- **Rig:** conservar solo manos/controladores y desactivar el resto de visuales de OVRInteraction en build. **52 → 4–8.**
- **Interior:** combinar por material, cargar solo al entrar y desactivar renderers al salir (la escena puede seguir cargada para chaleco/extintor). Si se ve la calle por el portón, mantener fachada/HLOD exterior. **35 → 6–10; 0 fuera.**
- **HUD:** canvas batching y ocultar paneles no usados; no es la prioridad porque parte ya batchea.

#### 4. Presupuesto objetivo

| Métrica | Quest 2 | Quest 3 |
|---|---:|---:|
| Draw calls totales | **150–250** | **250–400** |
| Tile cercano | 80–90 main + 0–10 sombra | 90–110 main + 10–20 sombra |
| Camión | 15–25 | 15–25 |
| Rig | 4–8 | 4–8 |
| Interior | 6–10 (solo dentro) | 6–10 |
| Triángulos | 750K–1M | 1,3M–1,8M |
| Sombras | Horneadas/blob | Horneadas + pocas dinámicas |

- **<100 en Quest 2** no es realista con el contenido completo actual; el objetivo defendible es **150–250**.
- **<200 en Quest 3** sí es alcanzable después de las fases 1–3.
- **3–5 tiles completos** siguen sin ser viables; solo con 1 cercano + HLOD de vecinos.

#### 5. Qué no hacer ahora

- Migrar a URP: no reduce draw calls y no ataca el cuello actual.
- Seguir decimando árboles/edificios: los triángulos ya están en rango.
- Hornear sin auditar UV2 y el reparto de sombras.
- HLOD multi-tile antes de bajar el juego completo de ~500.
- Combinar todo en una sola malla: se pierde culling y LOD.

#### 6. Decisiones pendientes

1. Bake de luz o sombras fake (recomendación: auditar primero; bake solo si UV2 y memoria salen bien).
2. Prioridad Quest 2 o Quest 3.
3. ¿Se puede desactivar el interior fuera y la ciudad dentro, cuidando la vista por el portón?
4. ¿El camión puede tener ruedas/puertas/escalera separadas para combinarlo?
5. ¿Habrá visor pronto para validar en dispositivo?

> Nota: los cambios pendientes en `Persistente.unity` (preset Hombre) y `Estacion_Interior.unity` (objeto movido) son ediciones de escena del usuario, no forman parte de este plan y no se mezclaron con la optimización.

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
