# Optimización de rendimiento — Tile 00 y ciudad

> **Estado**: 📋 PLAN — pendiente de implementar (Fase 1)
> **Fecha**: 20/10/2026
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

> **Estado**: 📋 PLAN — pendiente de implementar
> **Fecha**: 20/10/2026
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
