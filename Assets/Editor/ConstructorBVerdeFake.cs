using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Candidato B: conserva el atlas de detalle original del B (verde) y añade
/// la geometría fake desde la fuente, todo en 3 materiales (ladrillo, detalle, vidrio).
/// No crea prefabs.
/// </summary>
public static class ConstructorBVerdeFake
{
    private const string Carpeta = "Assets/Models/CiudadNueva/BVerdeFake";

    private class Dato
    {
        public LODGroup grupo;
        public Renderer renderer;
        public Mesh mallaB;
        public Mesh fuente;
        public Material[] actuales;
        public Material ladrillo;
    }

    [MenuItem("UTEC/Escenas/B verde + fake")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) return;
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!escena.name.Contains("B_FakeFix")) { Debug.LogWarning("[BVF] Abre B_FakeFix"); return; }
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "BVerdeFake");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/CiudadNueva/BVerdeFake"));

        var datos = new List<Dato>();
        foreach (var g in Object.FindObjectsOfType<LODGroup>())
        {
            string n = g.gameObject.name; if (n.Contains("Tree") || n.Contains("Maple") || n.Contains("Pine")) continue;
            var l = g.GetLODs(); if (l.Length == 0 || l[0].renderers.Length == 0) continue;
            var r = l[0].renderers[0]; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 3) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(r) as Renderer;
            if (source == null) continue;
            var smf = source.GetComponent<MeshFilter>(); if (smf == null || smf.sharedMesh == null) continue;
            datos.Add(new Dato { grupo = g, renderer = r, mallaB = mf.sharedMesh, fuente = smf.sharedMesh, actuales = ArrayActual(n, g.transform.position), ladrillo = r.sharedMaterials.Length > 0 ? r.sharedMaterials[0] : null });
        }
        if (datos.Count == 0) return;

        // Atlas: atlas de detalle B completo + texturas fake de la fuente.
        var claves = new List<string>(); var texturas = new List<Texture2D>();
        var bdet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/CiudadNueva/Atlas/T_Atlas_Detalles.png");
        claves.Add("__BDETALLE__"); texturas.Add(Copiar(bdet, 2048, "BDETALLE"));
        var fakeMats = new Dictionary<string, Material>();
        foreach (var d in datos)
            foreach (var m in d.actuales)
            {
                if (m == null || !EsFake(m)) continue;
                if (!fakeMats.ContainsKey(m.name)) fakeMats[m.name] = m;
            }
        foreach (var par in fakeMats)
        {
            var m = par.Value;
            if (m.mainTexture is Texture2D tex) { claves.Add("FAKE:" + m.name); texturas.Add(Copiar(tex, 512, "BVF_" + m.name)); }
            else
            {
                var solid = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var c = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                var px = new Color[64]; for (int i = 0; i < px.Length; i++) px[i] = new Color(c.r, c.g, c.b, 1f);
                solid.SetPixels(px); solid.Apply(); solid.name = "BVF_SOLID_" + m.name;
                claves.Add("FAKE:" + m.name); texturas.Add(solid);
            }
        }
        var atlas = new Texture2D(4096, 4096, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(texturas.ToArray(), 4, 4096, false); atlas.Apply();
        var mapa = new Dictionary<string, Rect>(); for (int i = 0; i < claves.Count; i++) mapa[claves[i]] = rects[i];
        string abs = Path.Combine(Application.dataPath, "Models/CiudadNueva/BVerdeFake/T_Atlas_BVerdeFake.png");
        File.WriteAllBytes(abs, atlas.EncodeToPNG());
        string rel = Carpeta + "/T_Atlas_BVerdeFake.png"; AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(rel);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = false;
        imp.mipmapEnabled = true; imp.maxTextureSize = 4096; imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
        var texAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
        string rutaMat = Carpeta + "/M_Detalle_BVerdeFake.mat";
        var detalle = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (detalle == null) { detalle = new Material(Shader.Find("Standard")); detalle.name = "M_Detalle_BVerdeFake"; AssetDatabase.CreateAsset(detalle, rutaMat); }
        detalle.shader = Shader.Find("Standard"); detalle.mainTexture = texAtlas; detalle.SetColor("_Color", Color.white); detalle.SetFloat("_Glossiness", 0.15f); detalle.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(detalle);
        var vidrio = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Glass_Transparente.mat");

        foreach (var d in datos)
        {
            var malla = Construir(d, mapa, detalle, vidrio);
            if (malla == null) continue;
            string ruta = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/BVF_" + d.grupo.gameObject.name + "_LOD0.asset");
            AssetDatabase.CreateAsset(malla, ruta);
            d.renderer.GetComponent<MeshFilter>().sharedMesh = malla;
            d.renderer.sharedMaterials = new[] { d.ladrillo, detalle, vidrio };
            // LOD0 + caja
            var borrar = new List<GameObject>(); foreach (Transform t in d.grupo.transform) if (t.name == "LOD1_B" || t.name == "LOD1_Decimado") borrar.Add(t.gameObject);
            foreach (var go in borrar) Object.DestroyImmediate(go);
            Renderer caja = null; foreach (Transform t in d.grupo.transform) if (t.name.Contains("Caja")) { caja = t.GetComponent<Renderer>(); break; }
            var niveles = new List<LOD>(); niveles.Add(new LOD(0.25f, new Renderer[] { d.renderer }));
            if (caja != null) niveles.Add(new LOD(0.10f, new Renderer[] { caja }));
            niveles.Add(new LOD(0.03f, new Renderer[0]));
            d.grupo.SetLODS(niveles.ToArray()); d.grupo.RecalculateBounds();
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena);
        Debug.Log("[BVF] verde + fake, datos=" + datos.Count);
    }

    private static Mesh Construir(Dato d, Dictionary<string, Rect> mapa, Material detalle, Material vidrio)
    {
        var b = d.mallaB;
        var fuente = d.fuente;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var ncol = new List<Color>();
        var bv = b.vertices; var bn = b.normals; var bt = b.tangents; var buv = b.uv; var buv2 = b.uv2; var bc = b.colors;
        bool bhn = bn != null && bn.Length == bv.Length, bht = bt != null && bt.Length == bv.Length, bhuv2 = buv2 != null && buv2.Length == bv.Length, bhc = bc != null && bc.Length == bv.Length;
        var brick = new List<int>(); var detail = new List<int>(); var glass = new List<int>();
        var mapB = new Dictionary<int, int>(); var mapD = new Dictionary<long, int>(); var mapG = new Dictionary<int, int>();
        var idxMat = new Dictionary<string, int>(); int sig = 0;

        // Brick y glass del B.
        for (int s = 0; s < b.subMeshCount; s++)
        {
            if (s == 0)
            {
                foreach (int orig in b.GetTriangles(0))
                {
                    int ni; if (!mapB.TryGetValue(orig, out ni)) { ni = nv.Count; mapB[orig] = ni; nv.Add(bv[orig]); if (bhn) nn.Add(bn[orig]); if (bht) nt.Add(bt[orig]); nuv.Add(buv[orig]); if (bhuv2) nuv2.Add(buv2[orig]); if (bhc) ncol.Add(bc[orig]); }
                    brick.Add(ni);
                }
            }
            else if (s == 2)
            {
                foreach (int orig in b.GetTriangles(2))
                {
                    int ni; if (!mapG.TryGetValue(orig, out ni)) { ni = nv.Count; mapG[orig] = ni; nv.Add(bv[orig]); if (bhn) nn.Add(bn[orig]); if (bht) nt.Add(bt[orig]); nuv.Add(buv[orig]); if (bhuv2) nuv2.Add(buv2[orig]); if (bhc) ncol.Add(bc[orig]); }
                    glass.Add(ni);
                }
            }
        }

        // Detalle B completo (verde) como una región del atlas.
        Rect rb = mapa["__BDETALLE__"];
        foreach (int orig in b.GetTriangles(1))
        {
            long k = ((long)orig << 32) | 1; int ni;
            if (!mapD.TryGetValue(k, out ni)) { ni = nv.Count; mapD[k] = ni; nv.Add(bv[orig]); if (bhn) nn.Add(bn[orig]); if (bht) nt.Add(bt[orig]); nuv.Add(new Vector2(rb.x + buv[orig].x * rb.width, rb.y + buv[orig].y * rb.height)); if (bhuv2) nuv2.Add(buv2[orig]); if (bhc) ncol.Add(bc[orig]); }
            detail.Add(ni);
        }

        // Añadir fake desde la fuente.
        var fv = fuente.vertices; var fn = fuente.normals; var ft = fuente.tangents; var fuv = fuente.uv; var fuv2 = fuente.uv2; var fc = fuente.colors;
        bool fhn = fn != null && fn.Length == fv.Length, fht = ft != null && ft.Length == fv.Length, fhuv2 = fuv2 != null && fuv2.Length == fv.Length, fhc = fc != null && fc.Length == fv.Length;
        for (int s = 0; s < fuente.subMeshCount && s < d.actuales.Length; s++)
        {
            var m = d.actuales[s]; if (m == null || !EsFake(m)) continue;
            string clave = "FAKE:" + m.name; if (!mapa.ContainsKey(clave)) continue;
            if (!idxMat.ContainsKey(clave)) idxMat[clave] = sig++;
            int idx = idxMat[clave]; Rect rf = mapa[clave];
            foreach (int orig in fuente.GetTriangles(s))
            {
                long k = ((long)orig << 32) | (uint)(100 + idx); int ni;
                if (!mapD.TryGetValue(k, out ni))
                {
                    ni = nv.Count; mapD[k] = ni; nv.Add(fv[orig]); if (fhn) nn.Add(fn[orig]); if (fht) nt.Add(ft[orig]);
                    nuv.Add(new Vector2(rf.x + fuv[orig].x * rf.width, rf.y + fuv[orig].y * rf.height));
                    if (fhuv2) nuv2.Add(fuv2[orig]); if (fhc) ncol.Add(fc[orig]);
                }
                detail.Add(ni);
            }
        }

        var malla = new Mesh(); malla.name = "BVF_" + d.grupo.gameObject.name;
        if (nv.Count > 65535) malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malla.SetVertices(nv); if (nn.Count == nv.Count) malla.SetNormals(nn); if (nt.Count == nv.Count) malla.SetTangents(nt); malla.SetUVs(0, nuv); if (nuv2.Count == nv.Count) malla.SetUVs(1, nuv2); if (ncol.Count == nv.Count) malla.SetColors(ncol);
        malla.subMeshCount = 3; malla.SetTriangles(brick, 0); malla.SetTriangles(detail, 1); malla.SetTriangles(glass, 2); malla.RecalculateBounds(); return malla;
    }

    private static bool EsFake(Material m) { return m != null && m.name.Contains("FakeInterior"); }
    private static Material[] ArrayActual(string nombre, Vector3 pos)
    {
        System.Func<string, Material> M = BuscarMaterial;
        if (nombre.Contains("Medium")) return new[] { M("MI_InteriorFloor"), M("M_Trim_VerdeOscuro"), M("MI_FakeInterior_1"), M("MI_InteriorWall"), M("M_Trim_VerdeOscuro"), M("MI_Glass"), M("MI_Trim_Dark"), M("M_Brick_Ocre"), M("MI_FakeInterior_3"), M("MI_FakeInterior_2"), M("MI_FakeInterior_4"), M("MI_Asphalt") };
        if (nombre.Contains("Corner")) return new[] { M("MI_RedBrick"), M("M_Trim_Blanco"), M("MI_FakeInterior"), M("MI_Glass"), M("MI_Trim_MetalConcrete"), M("MI_Concrete"), M("MI_Asphalt"), M("MI_InteriorFloor") };
        if (nombre.Contains("Rowhouse")) return new[] { M("M_Trim_Crema"), M("MI_Glass"), M("MI_FakeInterior"), M("M_Brick_Marron"), M("MI_Trim_MetalConcrete"), M("MI_Concrete"), M("MI_Asphalt"), M("MI_InteriorFloor") };
        if (pos.x < -20) return new[] { M("M_Trim_Negro"), M("M_Brick_Gris"), M("MI_InteriorWall"), M("MI_Trim_MetalConcrete"), M("MI_Glass"), M("MI_FakeInterior_2"), M("MI_Trim_Dark"), M("MI_FakeInterior_1"), M("MI_Concrete"), M("MI_FakeInterior_4"), M("MI_FakeInterior_3"), M("MI_Asphalt"), M("MI_InteriorFloor") };
        if (pos.x < 0) return new[] { M("M_Trim_Blanco"), M("M_Brick_Arena"), M("M_Brick_Arena"), M("MI_Trim_MetalConcrete"), M("MI_Glass"), M("MI_FakeInterior_2"), M("M_Trim_Blanco"), M("MI_FakeInterior_1"), M("MI_Concrete"), M("MI_FakeInterior_4"), M("MI_FakeInterior_1"), M("MI_Asphalt"), M("MI_InteriorFloor") };
        return new[] { M("M_Trim_Blanco"), M("M_Brick_Blanco"), M("MI_InteriorWall"), M("MI_Trim_MetalConcrete"), M("MI_Glass"), M("MI_FakeInterior_2"), M("MI_Trim_Dark"), M("MI_FakeInterior_1"), M("MI_Concrete"), M("MI_FakeInterior_4"), M("MI_FakeInterior_3"), M("MI_Asphalt"), M("MI_InteriorFloor") };
    }
    private static Material BuscarMaterial(string nombre)
    {
        string directo = "Assets/Models/CiudadNueva/Materials/" + nombre + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(directo); if (m != null) return m;
        foreach (var gu in AssetDatabase.FindAssets(nombre + " t:Material")) { string path = AssetDatabase.GUIDToAssetPath(gu); foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path)) { var mm = a as Material; if (mm != null && mm.name == nombre) return mm; } }
        return null;
    }
    private static Texture2D Copiar(Texture2D origen, int tamano, string nombre)
    {
        var rt = RenderTexture.GetTemporary(tamano, tamano, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(origen, rt); var previo = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, tamano, tamano), 0, 0); tex.Apply();
        RenderTexture.active = previo; RenderTexture.ReleaseTemporary(rt); tex.name = nombre; return tex;
    }
}
