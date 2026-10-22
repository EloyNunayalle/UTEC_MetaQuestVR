# Estado de trabajo — LOD1, caras y fake (actualizado 22/10/2026)

## Estado: LOD1 terminado

- **22 prefabs finales** en `Assets/Prefabs/Ciudad/Edificios/` (A1/A2 Paleta y Original, incluye Large) con **LOD0 + LOD1 decimado + caja + culled**.
- **LOD1Final** regenerado con las UV finales del LOD0:
  - 22 FBX decimados al 50 % en Blender.
  - 12 mallas `Mesh_*.asset` de A1/A2 Paleta actualizadas con la corrección de espejo X.
  - Originales sin cambios de geometría; FBX regenerados por consistencia.
- **Escena principal** `Ciudad_Tile_00_Estacion`: 6 edificios son instancias de prefabs A2 con LOD1.
- **A1 actual**: atlas con fake enmascarado; ventanas opacas sin fake.
- **A1 original**: atlas con los cuadros de fake conocidos enmascarados.
- **Caras traseras/laterales actuales**: UV remapeadas a un punto de ladrillo en `2MatMain` y `FinalA1_Actual`.
- Scripts: `RevisorPrefabs`, `CorrectorCarasActual`, `ExportarLOD0OBJ`, `AgregarLOD1Prefabs`, `CorregirEspejoLOD1`; Blender `decimar_lote.py`.

## Pipeline usado (por si hay que repetirlo)

1. `UTEC/LOD1/1 Exportar LOD0 a OBJ`.
2. Blender headless:
   `blender --background --factory-startup --python Herramientas/Blender/decimar_lote.py -- <obj> <fbx> 0.5 ...`
3. Refrescar Unity.
4. `UTEC/LOD1/2 Insertar LOD1 en prefabs`.
5. `UTEC/LOD1/3 Corregir espejo X de LOD1`.
6. Borrar `Assets/Models/CiudadNueva/LOD1Trabajo`.

## Datos

- Triángulos LOD1: Small ~9–10K, Medium 30K, Corner 7.8K, Rowhouse 2K; Large original 22K.
- Umbrales LODGroup: `0.25 / 0.12 / 0.05 / 0.02`.
- Los triángulos oscuros del Medium A2 en trasera/laterales son geometría real (laterales de bay windows/trims), no artefactos nuevos de la fusión de materiales.
- B (`B_FakeFix`, `B_Balanceado`) y sus LOD1 viejos en `Atlas/LOD/` se conservan como respaldo.

## Pendiente

- Bake de luz.
- Medición real de FPS/draw calls en Quest 2/3.
- Decidir si A2 2mat o B fix 3–4 materiales es la versión visual definitiva.
