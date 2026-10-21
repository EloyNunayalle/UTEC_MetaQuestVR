using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renderiza 4 vistas de cada prefab A1/A2 para revisar fake, UVs y caras traseras.
/// Solo genera capturas en Assets/Screenshots/_trabajo/review; no modifica prefabs.
/// </summary>
public static class RevisorPrefabs
{
    private const string Salida = "Assets/Screenshots/_trabajo/review";
    private const int Tam = 512;

    [MenuItem("UTEC/Revisar prefabs (4 vistas)")]
    public static void Revisar()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Screenshots/_trabajo/review"));
        var prefabs = new List<string>();
        prefabs.AddRange(Directory.GetFiles("Assets/Prefabs/Ciudad/Edificios/A1", "*.prefab", SearchOption.AllDirectories));
        prefabs.AddRange(Directory.GetFiles("Assets/Prefabs/Ciudad/Edificios/A2", "*.prefab", SearchOption.AllDirectories));
        prefabs.Sort();

        var ambientePrevio = RenderSettings.ambientLight;
        var modoPrevio = RenderSettings.ambientMode;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.85f, 0.85f, 0.85f);

        var raiz = new GameObject("__RevisorPrefabs");
        var camObj = new GameObject("__ReviewCamera");
        camObj.transform.SetParent(raiz.transform);
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;

        var luzObj = new GameObject("__ReviewLight");
        luzObj.transform.SetParent(raiz.transform);
        var luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.45f;
        luz.color = new Color(1f, 0.96f, 0.88f);
        luz.shadows = LightShadows.None;
        luzObj.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

        int total = 0;
        try
        {
            foreach (var rutaPrefab in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab.Replace("\\", "/"));
                if (prefab == null) continue;
                var go = Object.Instantiate(prefab);
                go.transform.position = new Vector3(10000f, 0f, 10000f);
                go.transform.rotation = Quaternion.identity;
                var lod = go.GetComponent<LODGroup>();
                if (lod != null) lod.ForceLOD(0);
                var principal = BuscarLod0(go);
                if (principal == null) { Object.DestroyImmediate(go); continue; }
                var b = principal.bounds;
                float distancia = Mathf.Max(4f, b.extents.magnitude * 2.4f);
                string nombre = Path.GetFileNameWithoutExtension(rutaPrefab);
                for (int i = 0; i < 4; i++)
                {
                    float yaw = i * 90f;
                    cam.transform.position = b.center - Quaternion.Euler(14f, yaw, 0f) * Vector3.forward * distancia;
                    cam.transform.LookAt(b.center + Vector3.up * (b.size.y * 0.03f));
                    var rt = RenderTexture.GetTemporary(Tam, Tam, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    var previo = RenderTexture.active;
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(Tam, Tam, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, Tam, Tam), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(Application.dataPath, "Screenshots/_trabajo/review", nombre + "_" + yaw.ToString("000") + ".png"), tex.EncodeToPNG());
                    cam.targetTexture = null;
                    RenderTexture.active = previo;
                    RenderTexture.ReleaseTemporary(rt);
                    Object.DestroyImmediate(tex);
                    total++;
                }
                Object.DestroyImmediate(go);
            }
        }
        finally
        {
            Object.DestroyImmediate(raiz);
            RenderSettings.ambientLight = ambientePrevio;
            RenderSettings.ambientMode = modoPrevio;
            AssetDatabase.Refresh();
        }
        Debug.Log("[RevisorPrefabs] capturas=" + total + " en " + Salida);
    }

    private static Renderer BuscarLod0(GameObject root)
    {
        var lod = root.GetComponent<LODGroup>();
        if (lod != null)
        {
            var niveles = lod.GetLODs();
            if (niveles.Length > 0 && niveles[0].renderers.Length > 0) return niveles[0].renderers[0];
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.subMeshCount >= 1 && !r.name.Contains("Caja")) return r;
        }
        return null;
    }
}
