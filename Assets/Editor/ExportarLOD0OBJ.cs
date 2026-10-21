using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exporta a OBJ el LOD0 de cada prefab final A1/A2 para decimarlo en Blender.
/// Conserva grupos por submesh; no exporta texturas.
/// </summary>
public static class ExportarLOD0OBJ
{
    private const string Salida = "Assets/Models/CiudadNueva/LOD1Trabajo";

    [MenuItem("UTEC/LOD1/1 Exportar LOD0 a OBJ")]
    public static void Exportar()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/CiudadNueva/LOD1Trabajo"));
        var prefabs = new List<string>();
        prefabs.AddRange(Directory.GetFiles("Assets/Prefabs/Ciudad/Edificios/A1", "*.prefab", SearchOption.AllDirectories));
        prefabs.AddRange(Directory.GetFiles("Assets/Prefabs/Ciudad/Edificios/A2", "*.prefab", SearchOption.AllDirectories));
        prefabs.Sort();
        int n = 0;
        foreach (var p in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace("\\", "/"));
            if (prefab == null) continue;
            Mesh mesh = null; string nombre = Path.GetFileNameWithoutExtension(p);
            var lod = prefab.GetComponent<LODGroup>();
            if (lod != null)
            {
                var niveles = lod.GetLODs();
                if (niveles.Length > 0 && niveles[0].renderers.Length > 0)
                {
                    var mf = niveles[0].renderers[0].GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
            }
            if (mesh == null) continue;
            Escribir(mesh, Path.Combine(Salida, nombre + ".obj"));
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log("[LOD1] OBJ exportados=" + n);
    }

    private static void Escribir(Mesh m, string ruta)
    {
        var inv = CultureInfo.InvariantCulture;
        var v = m.vertices; var n = m.normals; var uv = m.uv;
        var sb = new System.Text.StringBuilder();
        string baseName = Path.GetFileNameWithoutExtension(ruta);
        sb.AppendLine("# LOD0 " + m.name);
        sb.AppendLine("mtllib " + baseName + ".mtl");
        for (int i = 0; i < v.Length; i++)
            sb.AppendLine("v " + v[i].x.ToString("R", inv) + " " + v[i].y.ToString("R", inv) + " " + v[i].z.ToString("R", inv));
        for (int i = 0; i < uv.Length; i++)
            sb.AppendLine("vt " + uv[i].x.ToString("R", inv) + " " + uv[i].y.ToString("R", inv));
        for (int i = 0; i < n.Length; i++)
            sb.AppendLine("vn " + n[i].x.ToString("R", inv) + " " + n[i].y.ToString("R", inv) + " " + n[i].z.ToString("R", inv));
        for (int s = 0; s < m.subMeshCount; s++)
        {
            sb.AppendLine("g sub" + s);
            sb.AppendLine("usemtl M_" + s);
            var tr = m.GetTriangles(s);
            for (int i = 0; i < tr.Length; i += 3)
            {
                int a = tr[i] + 1, b = tr[i + 1] + 1, c = tr[i + 2] + 1;
                sb.AppendLine("f " + a + "/" + a + "/" + a + " " + b + "/" + b + "/" + b + " " + c + "/" + c + "/" + c);
            }
        }
        File.WriteAllText(ruta, sb.ToString());
        var mtl = new System.Text.StringBuilder();
        for (int s = 0; s < m.subMeshCount; s++)
        {
            mtl.AppendLine("newmtl M_" + s);
            mtl.AppendLine("Kd 0.5 0.5 0.5");
            mtl.AppendLine("Ka 0.1 0.1 0.1");
            mtl.AppendLine();
        }
        File.WriteAllText(Path.ChangeExtension(ruta, ".mtl"), mtl.ToString());
    }
}
