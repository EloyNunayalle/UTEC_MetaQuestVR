using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reconstruye A2 OriginalSmall/Medium/Corner/Rowhouse siguiendo el camino del B fix:
/// se usa la malla BVF (fake ya integrado y correcto) y se cambia solo el ladrillo
/// al material original. Se conservan las texturas del atlas de detalle original.
/// </summary>
public static class ConstructorA2OriginalBPath
{
    private const string Out = "Assets/Models/CiudadNueva/FinalOriginalBPath";

    private class Item
    {
        public string tipo;
        public string bvf;
        public string brick;
        public bool stairs;
    }

    [MenuItem("UTEC/Edificios/Refazer A2 Original por camino B fix")]
    public static void Ejecutar()
    {
        var items = new List<Item>
        {
            new Item { tipo="Small", bvf="Assets/Models/CiudadNueva/BVerdeFake/BVF_Building_Small_1_LOD_LOD0 6.asset", brick="M_Brick_RojoOscuro", stairs=false },
            new Item { tipo="Medium", bvf="Assets/Models/CiudadNueva/BVerdeFake/BVF_Building_Medium_2_001_LOD_LOD0 2.asset", brick="M_Brick_Ocre", stairs=false },
            new Item { tipo="Corner", bvf="Assets/Models/CiudadNueva/BVerdeFake/BVF_Corner_Shop_B_Combined_LOD_LOD0 2.asset", brick="M_Brick_RojoOscuro", stairs=true },
            new Item { tipo="Rowhouse", bvf="Assets/Models/CiudadNueva/BVerdeFake/BVF_Rowhouse_A_Combined_LOD_LOD0 2.asset", brick="M_Brick_Marron", stairs=true },
        };
        if (!AssetDatabase.IsValidFolder(Out)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "FinalOriginalBPath");
        var detail = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Final3Mat2Mat/M_Detalle_BVerdeFake_Doble.mat");
        if (detail == null) detail = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/BVerdeFake/M_Detalle_BVerdeFake.mat");
        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Glass_Transparente.mat");
        int n = 0;
        foreach (var it in items)
        {
            var src = AssetDatabase.LoadAssetAtPath<Mesh>(it.bvf);
            var brick = Buscar(it.brick);
            if (src == null || brick == null || detail == null || glass == null) continue;
            var mesh = it.stairs ? CorregirEscalera(src, it.tipo) : Instancear(src, "BPath_" + it.tipo);
            string prefPath = "Assets/Prefabs/Ciudad/Edificios/A2/Original/EDF_A2_Original_" + it.tipo + ".prefab";
            var root = new GameObject("EDF_A2_Original_" + it.tipo);
            var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false); l0.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = l0.AddComponent<MeshRenderer>(); mr.sharedMaterials = new[] { brick, detail, glass };
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(box.GetComponent<Collider>()); box.name = "LOD2_Caja"; box.transform.SetParent(root.transform, false); box.transform.localPosition = mesh.bounds.center; box.transform.localScale = mesh.bounds.size; box.GetComponent<MeshRenderer>().sharedMaterial = brick; box.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bc = root.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size;
            var lg = root.AddComponent<LODGroup>(); lg.SetLODs(new[] { new LOD(0.25f, new Renderer[] { mr }), new LOD(0.05f, new Renderer[] { box.GetComponent<Renderer>() }), new LOD(0.02f, new Renderer[0]) }); lg.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, prefPath); Object.DestroyImmediate(root); n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[A2OriginalBPath] " + n);
    }

    private static Mesh Instancear(Mesh src, string nombre)
    {
        var m = Object.Instantiate(src); m.name = nombre; string p = AssetDatabase.GenerateUniqueAssetPath(Out + "/" + nombre + ".asset"); AssetDatabase.CreateAsset(m, p); return m;
    }

    private static Mesh CorregirEscalera(Mesh src, string tipo)
    {
        string prefPath = tipo == "Rowhouse" ? "Assets/Models/CiudadNueva/Rowhouse_A.prefab" : "Assets/Models/CiudadNueva/Corner_Shop_B.prefab";
        var pref = AssetDatabase.LoadAssetAtPath<GameObject>(prefPath); Transform st = null;
        if (pref != null) foreach (var t in pref.GetComponentsInChildren<Transform>(true)) if (t.name == "Stairs_Entrance_Concrete") { st = t; break; }
        var mesh = Object.Instantiate(src); mesh.name = tipo + "_BPath_StairsFix";
        if (st != null)
        {
            var inv = pref.transform.worldToLocalMatrix; var smf = st.GetComponent<MeshFilter>(); var set = new HashSet<string>(); float maxY = 0f;
            foreach (var pos in smf.sharedMesh.vertices) { var w = inv.MultiplyPoint3x4(st.transform.localToWorldMatrix.MultiplyPoint3x4(pos)); set.Add(w.x.ToString("F3") + "," + w.y.ToString("F3") + "," + w.z.ToString("F3")); maxY = Mathf.Max(maxY, w.y); }
            if (maxY < 0.5f) maxY = 1.0067f;
            var v = mesh.vertices; for (int i = 0; i < v.Length; i++) { string k = v[i].x.ToString("F3") + "," + v[i].y.ToString("F3") + "," + v[i].z.ToString("F3"); if (!set.Contains(k)) v[i].y += maxY; } mesh.vertices = v;
        }
        string p = AssetDatabase.GenerateUniqueAssetPath(Out + "/" + tipo + "_BPath_StairsFix.asset"); AssetDatabase.CreateAsset(mesh, p); return mesh;
    }

    private static Material Buscar(string nombre)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/" + nombre + ".mat"); if (m != null) return m;
        foreach (var g in AssetDatabase.FindAssets(nombre + " t:Material")) foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g))) { var mm = a as Material; if (mm != null && mm.name == nombre) return mm; }
        return null;
    }
}