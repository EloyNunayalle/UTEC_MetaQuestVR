using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Utilidades para crear por codigo las piezas visuales del juego
/// (materiales sin iluminacion, anillos, flechas, textos 3D y tinte de resaltado).
/// </summary>
public static class VisualesVR
{
    static Shader _sinLuz;

    /// <summary>Material sin iluminacion, admite transparencia (Sprites/Default siempre va en el build).</summary>
    public static Material MaterialSinLuz(Color color)
    {
        if (_sinLuz == null)
            _sinLuz = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        var m = new Material(_sinLuz) { color = color };
        return m;
    }

    public static Material MaterialOpaco(Color color)
    {
        var sh = Shader.Find("Standard");
        if (sh == null) return MaterialSinLuz(color);
        return new Material(sh) { color = color };
    }

    public static GameObject Primitiva(PrimitiveType tipo, string nombre, Transform padre, Material mat)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(padre, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    public static GameObject ObjetoMalla(string nombre, Transform padre, Mesh malla, Material mat)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    /// <summary>Anillo plano en el plano XZ (normal +Y).</summary>
    public static Mesh Anillo(float radioInterior, float radioExterior, int segmentos = 48)
    {
        var v = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= segmentos; i++)
        {
            float a = i / (float)segmentos * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * radioInterior);
            v.Add(dir * radioExterior);
            if (i < segmentos)
            {
                int b = i * 2;
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
        }
        var m = new Mesh { name = "Anillo" };
        m.SetVertices(v);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Disco plano (XZ) usado como relleno de progreso.</summary>
    public static Mesh Disco(float radio, int segmentos = 48) => Anillo(0f, radio, segmentos);

    /// <summary>Flecha (cono) apuntando hacia -Y, con la punta en el origen.</summary>
    public static Mesh FlechaAbajo(float radio = 0.12f, float alto = 0.25f, int segmentos = 16)
    {
        var v = new List<Vector3> { Vector3.zero, new Vector3(0f, alto, 0f) };
        var tris = new List<int>();
        for (int i = 0; i < segmentos; i++)
        {
            float a = i / (float)segmentos * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * radio, alto, Mathf.Sin(a) * radio));
        }
        for (int i = 0; i < segmentos; i++)
        {
            int a = 2 + i, b = 2 + (i + 1) % segmentos;
            tris.AddRange(new[] { 0, b, a });   // lados
            tris.AddRange(new[] { 1, a, b });   // tapa
        }
        var m = new Mesh { name = "Flecha" };
        m.SetVertices(v);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Texto 3D (TextMeshPro). tamano ~ altura de letra en metros.</summary>
    public static TextMeshPro Texto(string nombre, Transform padre, float alturaLetra, Vector2 caja,
        TextAlignmentOptions alineacion = TextAlignmentOptions.Center)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshPro>();
        // En TextMeshPro 3D: 1 punto de fuente ~ 0.1 m de alto de linea.
        t.fontSize = alturaLetra * 10f;
        t.rectTransform.sizeDelta = caja;
        t.alignment = alineacion;
        t.enableWordWrapping = true;
        t.richText = true;
        t.color = Color.white;
        var r = t.GetComponent<Renderer>();
        if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    /// <summary>Limites combinados de todos los renderers (activos) bajo un transform.</summary>
    public static bool Limites(Transform raiz, out Bounds b, Transform excluir = null)
    {
        b = default;
        bool hay = false;
        foreach (var r in raiz.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (excluir != null && r.transform.IsChildOf(excluir)) continue;
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    // ---------------- Resaltado (tinte) ----------------

    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int IdBaseColorFactor = Shader.PropertyToID("baseColorFactor");
    static MaterialPropertyBlock _mpb;

    /// <summary>Tine los renderers (intensidad 0 = quitar el tinte).</summary>
    public static void Tenir(Renderer[] renders, Color color, float intensidad)
    {
        if (renders == null) return;
        _mpb ??= new MaterialPropertyBlock();
        foreach (var r in renders)
        {
            if (r == null) continue;
            if (intensidad <= 0.001f)
            {
                r.SetPropertyBlock(null);
                continue;
            }
            r.GetPropertyBlock(_mpb);
            Color c = Color.Lerp(Color.white, color, intensidad);
            _mpb.SetColor(IdColor, c);
            _mpb.SetColor(IdBaseColor, c);
            _mpb.SetColor(IdBaseColorFactor, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
