# Estado de trabajo — LOD1, caras y fake (21/10/2026)

## Qué está hecho
- **22 prefabs finales** en `Assets/Prefabs/Ciudad/Edificios/` (A1/A2 Paleta y Original, incluye Large) con **LOD0 + LOD1 decimado + caja + culled**.
- **LOD1Final**: 22 FBX decimados al 50 % en Blender + mallas `Mesh_*.asset` corregidas del espejo X.
- **Escena principal** `Ciudad_Tile_00_Estacion`: los 6 edificios ahora son **instancias de prefabs A2** con LOD1 (ya no son objetos sueltos).
- **A1 actual**: atlas `T_Atlas_A1_Actual.png` con la región fake rellenada de vidrio opaco; las ventanas ya no muestran fake.
- **A1 original**: `T_Atlas_A1_Final.png` con los cuadros de fake conocidos enmascarados.
- **Caras traseras/laterales actuales**: UV remapeadas a un punto de ladrillo en `2MatMain` y `FinalA1_Actual` para que el detalle/fake no manche paredes ciegas.
- Scripts nuevos: `RevisorPrefabs`, `CorrectorCarasActual`, `ExportarLOD0OBJ`, `AgregarLOD1Prefabs`, `CorregirEspejoLOD1`.
- Script Blender nuevo: `Herramientas/Blender/decimar_lote.py`.

## Pendiente inmediato al retomar
1. **Regenerar el LOD1 de los 12 actuales** (o los 22) porque el último `CorrectorCarasActual` cambió las UV del LOD0 **después** de la última generación del LOD1. Pipeline:
   1. Menú `UTEC/LOD1/1 Exportar LOD0 a OBJ`.
   2. Ejecutar Blender:
      `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Herramientas/Blender/decimar_lote.py -- <obj> <fbx> 0.5 ...` (ver `decimar_lote.py`; se puede generar la lista con los `.obj` de `LOD1Trabajo`).
   3. Refrescar Unity.
   4. Menú `UTEC/LOD1/2 Insertar LOD1 en prefabs`.
   5. Menú `UTEC/LOD1/3 Corregir espejo X de LOD1`.
   6. Borrar `Assets/Models/CiudadNueva/LOD1Trabajo` al terminar.
2. **Revisar** con `UTEC/Revisar prefabs (4 vistas)`; los triángulos oscuros del Medium A2 en trasera/laterales corresponden a **geometría real** de laterales de bay windows/trims, no a triángulos nuevos de la fusión de materiales.
3. **Actualizar capturas** definitivas y `OPTIMIZACION_RENDIMIENTO.md` si hace falta.
4. **Commit final** del trabajo.

## Datos útiles
- LOD1 actuales (tris): Small ~9–10K, Medium 30K, Corner 7.8K, Rowhouse 2K; Large original 22K.
- Umbrales LODGroup: `0.25 / 0.12 / 0.05 / 0.02`.
- `LOD1Final` conserva UVs y materiales del LOD0; el FBX viene espejado en X y se corrige en Unity.
- La escena principal sigue en Build Settings junto con `Persistente` y `Estacion_Interior`.
- B (`B_FakeFix`, `B_Balanceado`) y sus LOD1 viejos en `Atlas/LOD/` se conservan como respaldo.

## Estado Git
- Commit base: `492c1ce` (prefabs A1/A2, atlas 2mat y limpieza).
- Este estado se guarda en un commit WIP para poder retomar.
- **No push**.
