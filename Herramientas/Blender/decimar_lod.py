# Decima una malla LOD0 exportada desde Unity (OBJ) y la exporta a FBX.
# Uso:
#   blender --background --factory-startup --python decimar_lod.py -- entrada.obj salida.fbx 0.5
# El FBX resultante se importa en Unity, se corrige el espejo en X del pipeline
# OBJ -> Blender -> FBX y se le asignan los 3 materiales (ladrillo, atlas, vidrio).

import bpy
import sys
import os

argv = sys.argv[sys.argv.index("--") + 1:]
obj_path = argv[0]
fbx_path = argv[1]
ratio = float(argv[2])

bpy.ops.wm.read_factory_settings(use_empty=True)

try:
    bpy.ops.wm.obj_import(filepath=obj_path)
except Exception:
    bpy.ops.import_scene.obj(filepath=obj_path)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise RuntimeError("No se importó ninguna malla")

for obj in meshes:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    mod = obj.modifiers.new(name="Decimate", type='DECIMATE')
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)

bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]

os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={'MESH'},
    path_mode='AUTO',
    embed_textures=False,
    apply_scale_options='FBX_SCALE_ALL',
    apply_unit_scale=True,
    axis_forward='-Z',
    axis_up='Y'
)

total_tris = sum(len(o.data.polygons) for o in meshes)
total_verts = sum(len(o.data.vertices) for o in meshes)
print("DECIMATED verts=%d tris=%d ratio=%s -> %s" % (total_verts, total_tris, ratio, fbx_path))
