# Paleta de partes de edificios

Materiales generados para personalizar por partes. **No están aplicados a la escena ni a prefabs.**

## Grupos generados

| Grupo | Carpeta | Textura base |
|---|---|---|
| Trim | `PaletaPartes/Trim/` | T_Trim_BaseColor |
| Cornice | `PaletaPartes/Cornice/` | T_Trim_BaseColor |
| Frame | `PaletaPartes/Frame/` | T_Trim_BaseColor |
| Roof | `PaletaPartes/Roof/` | T_RoofSlate_BaseColor |
| Door | `PaletaPartes/Door/` | T_MetalConcrete_BaseColor |
| Foundation | `PaletaPartes/Foundation/` | T_Concrete_BaseColor |
| Stairs | `PaletaPartes/Stairs/` | T_Concrete_BaseColor |
| InteriorWall | `PaletaPartes/InteriorWall/` | T_RedBrick_BaseColor |
| InteriorFloor | `PaletaPartes/InteriorFloor/` | T_MarbleFloor_BaseColor |
| Fake | `PaletaPartes/Fake/` | T_lit_interior_1/2, T_dark_interior |
| Glass | `PaletaPartes/Glass/` | sin textura |
| BrickExtra | `PaletaPartes/BrickExtra/` | T_Brick_Gris |

## Colores base

Blanco, Crema, Arena, Beige, GrisClaro, Gris, Marron, Ocre, Terracota, RojoOscuro, VerdeOscuro, VerdeClaro, AzulGris, Negro.

Los 8 ladrillos originales siguen en `Assets/Models/CiudadNueva/Materials/M_Brick_*`.

## Fake

Neutral, Calido, Frio, Oscuro, Tenue.

## Vidrio

Clear, Azul, Verde, Ahumado, Bronce, Lechoso.

## Flujo

1. Asignar estos materiales a los submeshes/materiales separados del edificio editable.
2. Personalizar los colores/texturas por edificio.
3. Cuando esté definido, hornear el resultado en atlas (opaco + vidrio; fake aparte si requiere propiedades).
4. El atlas final queda fijo; conservar la versión editable para futuros cambios.
