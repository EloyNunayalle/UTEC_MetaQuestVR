# Estado de trabajo — A2 3mat, A1 2mat y LOD1 (22/10/2026)

## Versiones finales actuales

### A2 — 3 materiales
- **Ladrillo** por edificio (`M_Brick_*` / `MI_RedBrick`).
- **Detalle + fake** con el atlas original del B fix, `M_Detalle_BVerdeFake`.
- **Vidrio** transparente, `M_Glass_Transparente`.
- Se eliminó el renderer/material extra de trim alterno para respetar el máximo de 3.

### A1 — 2 materiales
- **Ladrillo** por edificio.
- **Atlas opaco** `M_Atlas_A1_2Mat` con detalles + ventanas opacas, sin fake.
- No usa vidrio transparente.

### Originales A1/A2
- Reconstruidos con las mallas/materiales originales.
- A2 Original: 3 materiales y fake solo en ventanas.
- A1 Original: 2 materiales sin fake.
- Techos, pisos y detalles ya no comparten UV con el fake.

## Escaleras Rowhouse / Corner
- El edificio se elevó **1.006 m**.
- La escalera queda en el suelo y su último escalón coincide con el umbral.
- Se agregó una base/fundación de 0 a 1.006 m.
- La puerta y el marco dejan de verse cortados.

## Techos
- Se agregó una **tapa superior** a los 22 prefabs para cerrar la vista desde arriba.
- Evita que desaparezcan los bordes por backface culling.
- Tapa el posible cambio de color por caras coplanares/mipmaps del techo.

## LODs
- **LOD0 + LOD1 decimado 50 % + caja + culled**.
- Umbrales: `0.25 / 0.12 / 0.05 / 0.02`.
- **A2:** 3 materiales y 3 submeshes.
- **A1:** 2 materiales y 2 submeshes.
- LOD1 regenerado desde las mallas finales; espejo X corregido.

## Escena
- `Assets/Scenes/Ciudad_Tile_00_Estacion.unity` usa las **instancias A2 3mat** nuevas.
- B (`B_FakeFix`, `B_Balanceado`) queda intacto.

## Respaldo
- `Backups/Ciudad_Antes_3Mat_2026-10-22/`:
  - escena anterior,
  - prefabs A1/A2 anteriores,
  - prefabs fuente Rowhouse/Corner anteriores.
- Está **fuera de `Assets/`** para no duplicar GUIDs.

## Pendiente
- Bake de luz.
- Medición real de FPS/draw calls en Quest 2/3.
- Validar visualmente las 6 instancias en la escena y las esquinas Rowhouse/Corner.
