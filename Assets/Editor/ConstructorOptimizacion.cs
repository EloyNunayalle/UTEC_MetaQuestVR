using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Etapa 5 (final) de la ciudad unica: optimizacion para Quest con la geometria ya definida.
/// 1. Combina por CELDA (48 m) y MATERIAL los renderers estaticos pequenos (calles, veredas, marcas,
///    vegetacion baja, mobiliario, rocas, galpones). Conserva el frustum culling por celda y los
///    colliders originales; solo desactiva sus renderers. Idempotente.
/// 2. Agrupa los arboles por celda (herramienta existente UTEC/Optimizacion/5).
/// 3. Luces del patio a "Not Important" (por vertice): sin pases aditivos por pixel.
/// 4. Sin sombras en los LOD1/LOD2 de edificios ni en props combinados.
/// </summary>
public static class ConstructorOptimizacion
{
    const string Raiz = "Ciudad_Combinada";
    const string Dir = "Assets/Models/CiudadUnica/Combinado";
    const float Celda = 48f;
    static readonly string[] Fuentes =
    {
        "Ciudad_Tile_00/Calles", "Ciudad_Tile_00/Veredas", "Ciudad_Tile_00/Marcas", "Ciudad_Tile_00/Vegetacion",
        "Ciudad_Tile_00/Props", "Ciudad_Tile_00/Detalles", "Mobiliario_Urbano", "Limite_Costero/Rocas_Pie",
        "Limite_Costero/Control_Sur", "Relleno_Urbano"
    };

    [MenuItem("UTEC/Ciudad Unica/5 Optimizar (combinar por celda, arboles, luces, sombras)")]
    public static void Etapa5()
    {
        if (EditorApplication.isPlaying) return;
        var escena = SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogError("[Optimizacion] Abre Ciudad_Tile_00_Estacion."); return; }
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Models/CiudadUnica", "Combinado");

        var vieja = GameObject.Find(Raiz);
        if (vieja != null) Object.DestroyImmediate(vieja);

        // ---- 1. Combinar por celda y material
        var grupos = new Dictionary<(int, int, Material), List<CombineInstance>>();
        // Materiales de color plano (props propios): se hornean a color por vertice y comparten 1 material.
        var colorVertice = new Dictionary<(int, int), List<(Mesh malla, int sub, Matrix4x4 m, Color c)>>();
        var matVC = ConstructorCiudadUnica.Material("Props_ColorVertice", Color.white);
        matVC.shader = Shader.Find("UTEC/LaderaVertexColor");
        matVC.color = Color.white;
        matVC.enableInstancing = true;
        EditorUtility.SetDirty(matVC);
        var originales = new List<MeshRenderer>();
        foreach (var ruta in Fuentes)
        {
            var t = Buscar(ruta);
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.activeInHierarchy || !r.gameObject.isStatic) continue;
                if (r.GetComponent<TMP_Text>() != null) continue;              // texto: malla de TMP
                if (r.GetComponentInParent<LODGroup>() != null) continue;      // edificios/arboles con LOD
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = r.sharedMaterials;
                if (mats.Length == 0 || mf.sharedMesh.subMeshCount < mats.Length) continue;
                bool ok = true;
                foreach (var m in mats) if (m == null) { ok = false; break; }
                if (!ok) continue;

                Vector3 c = r.bounds.center;
                int cx = Mathf.FloorToInt(c.x / Celda), cz = Mathf.FloorToInt(c.z / Celda);
                for (int s = 0; s < mats.Length; s++)
                {
                    if (ColorPlano(mats[s]))
                    {
                        if (!colorVertice.TryGetValue((cx, cz), out var lvc)) colorVertice[(cx, cz)] = lvc = new List<(Mesh, int, Matrix4x4, Color)>();
                        lvc.Add((mf.sharedMesh, s, r.transform.localToWorldMatrix, mats[s].color.linear));
                        continue;
                    }
                    var k = (cx, cz, mats[s]);
                    if (!grupos.TryGetValue(k, out var lista)) grupos[k] = lista = new List<CombineInstance>();
                    lista.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = r.transform.localToWorldMatrix });
                }
                originales.Add(r);
            }
        }

        foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { Dir }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

        var raiz = new GameObject(Raiz).transform;
        var celdas = new Dictionary<(int, int), Transform>();
        long tris = 0; int combos = 0;
        var usados = new HashSet<string>();
        foreach (var par in grupos)
        {
            var (cx, cz, mat) = par.Key;
            if (!celdas.TryGetValue((cx, cz), out var celda))
            {
                celda = new GameObject($"Celda_{cx}_{cz}").transform;
                celda.SetParent(raiz, false);
                celdas[(cx, cz)] = celda;
            }
            string nombre = $"C_{cx}_{cz}_{mat.name}";
            for (int i = 2; !usados.Add(nombre); i++) nombre = $"C_{cx}_{cz}_{mat.name}_{i}"; // materiales homonimos
            var malla = new Mesh { name = nombre, indexFormat = IndexFormat.UInt32 };
            malla.CombineMeshes(par.Value.ToArray(), true, true);
            malla.RecalculateBounds();
            malla.UploadMeshData(false);
            AssetDatabase.CreateAsset(malla, $"{Dir}/{malla.name}.asset");
            tris += malla.triangles.Length / 3;

            var go = new GameObject("Combo_" + mat.name);
            go.transform.SetParent(celda, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.isStatic = true;
            combos++;
        }
        foreach (var par in colorVertice)
        {
            var (cx, cz) = par.Key;
            if (!celdas.TryGetValue((cx, cz), out var celda))
            {
                celda = new GameObject($"Celda_{cx}_{cz}").transform;
                celda.SetParent(raiz, false);
                celdas[(cx, cz)] = celda;
            }
            var v = new List<Vector3>(); var n = new List<Vector3>(); var col = new List<Color>(); var tri = new List<int>();
            foreach (var (mallaO, sub, m, c) in par.Value)
            {
                int baseV = v.Count;
                var vs = mallaO.vertices; var ns = mallaO.normals;
                var nm = m.inverse.transpose;
                for (int i = 0; i < vs.Length; i++)
                {
                    v.Add(m.MultiplyPoint3x4(vs[i]));
                    n.Add(ns.Length == vs.Length ? nm.MultiplyVector(ns[i]).normalized : Vector3.up);
                    col.Add(c);
                }
                foreach (var idx in mallaO.GetTriangles(sub)) tri.Add(baseV + idx);
            }
            var malla = new Mesh { name = $"C_{cx}_{cz}_ColorVertice", indexFormat = IndexFormat.UInt32 };
            malla.SetVertices(v); malla.SetNormals(n); malla.SetColors(col); malla.SetTriangles(tri, 0);
            malla.RecalculateBounds();
            AssetDatabase.CreateAsset(malla, $"{Dir}/{malla.name}.asset");
            tris += tri.Count / 3;
            var go = new GameObject("Combo_ColorVertice");
            go.transform.SetParent(celda, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matVC;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.isStatic = true;
            combos++;
        }
        foreach (var r in originales) r.enabled = false;

        // ---- 3. Luces del patio por vertice
        int luces = 0;
        foreach (var l in Object.FindObjectsOfType<Light>())
            if (l.type != LightType.Directional) { l.renderMode = LightRenderMode.ForceVertex; l.shadows = LightShadows.None; luces++; }

        // ---- 4. Sin sombras en LODs lejanos
        int sinSombra = 0;
        foreach (var lg in Object.FindObjectsOfType<LODGroup>())
        {
            var lods = lg.GetLODs();
            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null && r.shadowCastingMode != ShadowCastingMode.Off) { r.shadowCastingMode = ShadowCastingMode.Off; sinSombra++; }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        // ---- 2. Arboles por celda (la herramienta existente guarda la escena)
        EditorApplication.ExecuteMenuItem("UTEC/Optimizacion/5 Agrupar arboles por celda (25 m)");

        Debug.Log($"[Optimizacion] {originales.Count} renderers -> {combos} combos en {celdas.Count} celdas ({tris} tris); " +
                  $"luces por vertice={luces}; LODs sin sombra={sinSombra}.");
    }

    /// <summary>Material propio de color plano (Standard sin textura en Materials/CiudadUnica).</summary>
    static bool ColorPlano(Material m) =>
        m.shader != null && m.shader.name == "Standard" && m.mainTexture == null &&
        AssetDatabase.GetAssetPath(m).StartsWith("Assets/Materials/CiudadUnica/");

    static Transform Buscar(string ruta)
    {
        var partes = ruta.Split('/');
        foreach (var g in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (g.name != partes[0]) continue;
            var t = g.transform;
            for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
            if (t != null) return t;
        }
        return null;
    }
}
