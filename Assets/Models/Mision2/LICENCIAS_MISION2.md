# Misión 2 — Incendio eléctrico: assets y licencias

Fecha de incorporación: 28/10/2026

Assets incorporados para la Misión 2:

| Asset | Archivo | Autor | Fuente | Licencia |
|---|---|---|---|---|
| Tablero eléctrico | `TableroElectrico/vr-ready_fuse_box.glb` | Jimmy Johansson | [Sketchfab](https://sketchfab.com/3d-models/vr-ready-fuse-box-e7ffc35358af4ae0b981f4bd2eadb38a) | **CC BY 4.0** |
| Extintor CO₂ | `ExtintorCO2/co2_fire_extinguisher_low_poly.glb` | TheAppXR team / Oobe-Xr Srl | [Fab](https://www.fab.com/listings/871bfbbd-90a2-48dd-8e9c-02541572cc57) | **Fab Standard License** (Personal/Professional, gratuita) |
| Equipo que se incendia | `EquipoIncendio/Ac_Units.fbx` | abistar | [CGTrader](https://www.cgtrader.com/items/5046685) | **CGTrader Royalty-Free** (modelo gratuito) |

## Cómo completar los assets locales

Los dos assets cuya licencia prohíbe redistribuir el archivo original no se
incluyen en el repositorio. Para que la escena los encuentre:

1. Descarga el extintor desde el enlace de Fab y coloca el `.glb` en
   `Assets/Models/Mision2/ExtintorCO2/co2_fire_extinguisher_low_poly.glb`.
2. Descarga el equipo AC desde CGTrader y coloca el `.fbx` en
   `Assets/Models/Mision2/EquipoIncendio/Ac_Units.fbx`.
3. Abre Unity: los `.meta` versionados conservan los GUIDs y los modelos se
   re-vinculan automáticamente.

## Notas

- Los tres modelos se importan **sin modificaciones de malla**. Los cambios de
  escala, materiales, colliders, fuego, chispas y animaciones se harán en Unity.
- Las licencias permiten usar los assets dentro de este proyecto, pero **no**
  redistribuir los archivos originales por separado ni venderlos como asset pack.
- El proyecto es académico y no comercial; aun así se acredita a los autores.
- El detalle completo de cada licencia está en el `ATRIBUCION.txt` de su carpeta.
- El asset `Ac units - Game Ready` no incluye texturas; usa materiales Phong
  simples. Si se necesita más detalle, se puede texturizar en el proyecto.
- El tablero de Sketchfab incluye un nodo de **interruptor principal**
  (`FuseBox_FuseBox_MainPowerSwitch`), útil para animar el corte de energía.
- El extintor de Fab incluye partes móviles (`Pin`, `Hose`, `Moveable01-03`),
  útiles para el pasador, la manguera y el gatillo.
