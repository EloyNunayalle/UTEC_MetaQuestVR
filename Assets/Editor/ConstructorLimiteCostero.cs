using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Etapa 2 de la ciudad unica: limite fisico "pueblo costero".
///  - Norte: malecon de hormigon con baranda (con colision) y mar abierto (UTEC/AguaSimple).
///  - Oeste / Este / Sur: laderas low-poly con roca al pie (MeshCollider, pendiente &gt; 50 grados).
///  - Acceso sur: corte con muros de contencion, barrera cerrada, barreras New Jersey y garita de control.
///  - Suelo de seguridad: caja de 1 m bajo el Plane (el Plane no tiene grosor).
/// El limite logico (LimiteCiudad.cs, en Persistente) usa el mismo rectangulo.
/// </summary>
public static class ConstructorLimiteCostero
{
    // Bordes interiores (deben coincidir con LimiteCiudad).
    const float BordeOeste = -108.6f, BordeEste = 95.9f, BordeSur = -85.6f, Malecon = 52f;
    const float AccesoX0 = 0.4f, AccesoX1 = 16.4f, AccesoFondo = -95f, Barrera = -85.9f;
    const string Raiz = "Limite_Costero";
    const string DirMallas = "Assets/Models/CiudadUnica";

    [MenuItem("UTEC/Ciudad Unica/2 Limite costero (mar, laderas, control)")]
    public static void Etapa2()
    {
        if (EditorApplication.isPlaying) return;
        var escena = SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogError("[Limite] Abre Ciudad_Tile_00_Estacion."); return; }
        if (!AssetDatabase.IsValidFolder(DirMallas)) AssetDatabase.CreateFolder("Assets/Models", "CiudadUnica");

        var vieja = GameObject.Find(Raiz);
        if (vieja != null) Object.DestroyImmediate(vieja);
        var raiz = new GameObject(Raiz).transform;

        SueloSeguridad(raiz);
        Mar(raiz);
        MaleconYBaranda(raiz);
        var ladera = MatShader("Ladera", "UTEC/LaderaVertexColor", Color.white);
        // Oeste y este: entran al mar como cabos (z hasta 120, se pierden en la niebla).
        Ladera(raiz, "Ladera_Oeste", new Vector3(BordeOeste, 0f, -112f), new Vector3(BordeOeste, 0f, 120f), Vector3.left, ladera, 11);
        Ladera(raiz, "Ladera_Este", new Vector3(BordeEste, 0f, -112f), new Vector3(BordeEste, 0f, 120f), Vector3.right, ladera, 23);
        Ladera(raiz, "Ladera_Sur_O", new Vector3(-140f, 0f, BordeSur), new Vector3(AccesoX0, 0f, BordeSur), Vector3.back, ladera, 37);
        Ladera(raiz, "Ladera_Sur_E", new Vector3(AccesoX1, 0f, BordeSur), new Vector3(130f, 0f, BordeSur), Vector3.back, ladera, 41);
        Ladera(raiz, "Ladera_Sur_Fondo", new Vector3(AccesoX0 - 2f, 0f, AccesoFondo), new Vector3(AccesoX1 + 2f, 0f, AccesoFondo), Vector3.back, ladera, 53);
        RocasAlPie(raiz);
        ControlSur(raiz);

        foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Debug.Log("[Limite] Limite costero construido.");
    }

    // ------------------------------------------------------------------ materiales y mallas

    static Material MatShader(string nombre, string shader, Color c)
    {
        var m = ConstructorCiudadUnica.Material(nombre, c);
        var sh = Shader.Find(shader);
        if (sh != null && m.shader != sh) { m.shader = sh; m.color = c; }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh GuardarMalla(Mesh m, string nombre)
    {
        string ruta = DirMallas + "/" + nombre + ".asset";
        var vieja = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (vieja != null) { EditorUtility.CopySerialized(m, vieja); return vieja; }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static GameObject Objeto(string nombre, Transform padre, Mesh malla, Material mat, bool colision, bool sombras = false)
    {
        var g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        g.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = sombras ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = sombras;
        if (colision) g.AddComponent<MeshCollider>().sharedMesh = malla;
        return g;
    }

    static GameObject Caja(string nombre, Transform padre, Vector3 centro, Vector3 tam, Material mat, bool colision = true, float rotY = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = nombre;
        g.transform.SetParent(padre, false);
        g.transform.position = centro;
        g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        g.transform.localScale = tam;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!colision) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    // ------------------------------------------------------------------ piezas

    static void SueloSeguridad(Transform raiz)
    {
        var g = new GameObject("Suelo_Seguridad");
        g.transform.SetParent(raiz, false);
        g.transform.position = new Vector3(-5f, -0.5f, -29f);
        var bc = g.AddComponent<BoxCollider>();
        bc.size = new Vector3(280f, 1f, 170f);
    }

    static void Mar(Transform raiz)
    {
        // Gran quad a -0.8 m desde el malecon hasta el horizonte (la niebla lo funde con el cielo).
        var m = new Mesh { name = "Mar" };
        float x0 = -700f, x1 = 700f, z0 = Malecon - 0.5f, z1 = 900f, y = -0.8f;
        m.vertices = new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.RecalculateBounds();
        var agua = MatShader("Agua_Mar", "UTEC/AguaSimple", Color.white);
        Objeto("Mar", raiz, GuardarMalla(m, "Mar"), agua, false);
    }

    static void MaleconYBaranda(Transform raiz)
    {
        var hormigon = MatShader("Malecon_Hormigon", "Standard", new Color(0.70f, 0.68f, 0.63f));
        var metal = MatShader("Baranda_Metal", "Standard", new Color(0.93f, 0.93f, 0.90f));
        float x0 = BordeOeste - 4f, x1 = BordeEste + 4f, largo = x1 - x0, cx = (x0 + x1) * 0.5f;

        // Muro del malecon (cara hacia el mar) y coronamiento.
        Caja("Muro_Malecon", raiz, new Vector3(cx, -1.3f, Malecon + 0.5f), new Vector3(largo, 2.9f, 1f), hormigon, true);
        Caja("Coronamiento", raiz, new Vector3(cx, 0.12f, Malecon + 0.35f), new Vector3(largo, 0.24f, 1.3f), hormigon, false);

        // Baranda: postes cada 2.5 m + pasamanos + travesano, combinados en una malla.
        var piezas = new List<CombineInstance>();
        var cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        void Pieza(Vector3 c, Vector3 s) => piezas.Add(new CombineInstance { mesh = cubo, transform = Matrix4x4.TRS(c, Quaternion.identity, s) });
        float zB = Malecon + 0.15f;
        for (float x = x0; x <= x1 + 0.01f; x += 2.5f) Pieza(new Vector3(x, 0.78f, zB), new Vector3(0.07f, 1.1f, 0.07f));
        Pieza(new Vector3(cx, 1.30f, zB), new Vector3(largo, 0.07f, 0.09f));
        Pieza(new Vector3(cx, 0.80f, zB), new Vector3(largo, 0.05f, 0.05f));
        var malla = new Mesh { name = "Baranda_Malecon", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.CombineMeshes(piezas.ToArray(), true, true);
        var baranda = Objeto("Baranda_Malecon", raiz, GuardarMalla(malla, "Baranda_Malecon"), metal, false);
        var bc = baranda.AddComponent<BoxCollider>();
        bc.center = new Vector3(cx, 1.0f, zB);
        bc.size = new Vector3(largo, 2.2f, 0.4f); // alto para que no se pueda saltar ni pasar con el camion
    }

    /// <summary>Franja de ladera low-poly: perfil abrupto al pie (&gt;50 grados) que sube a 15-17 m.</summary>
    static void Ladera(Transform raiz, string nombre, Vector3 a, Vector3 b, Vector3 afuera, Material mat, int semilla)
    {
        float[] dist = { 0f, 1.4f, 4f, 8f, 14f, 22f, 34f, 50f };
        float[] alto = { -0.3f, 3.2f, 6.8f, 9.8f, 12.6f, 15f, 16.5f, 17f };
        Vector3 dir = b - a; float largo = dir.magnitude; dir /= largo;
        int segs = Mathf.Max(2, Mathf.CeilToInt(largo / 4f));
        int filas = dist.Length;

        var rnd = new System.Random(semilla);
        float ox = (float)rnd.NextDouble() * 100f, oz = (float)rnd.NextDouble() * 100f;
        var grid = new Vector3[segs + 1, filas];
        for (int i = 0; i <= segs; i++)
            for (int j = 0; j < filas; j++)
            {
                float s = largo * i / segs;
                Vector3 p = a + dir * s + afuera * dist[j];
                float ruido = (Mathf.PerlinNoise(ox + s * 0.07f, oz + j * 0.6f) - 0.5f);
                float h = alto[j] + (j == 0 ? 0f : ruido * (j == 1 ? 1.4f : 4f));
                Vector3 jitter = j == 0 ? Vector3.zero : afuera * ruido * (j == 1 ? 0.3f : 1.5f);
                grid[i, j] = p + jitter + Vector3.up * h;
            }

        // Facetado: vertices por triangulo, color por altura/pendiente.
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        Color roca = new Color(0.50f, 0.46f, 0.41f), rocaOsc = new Color(0.40f, 0.37f, 0.34f);
        Color pasto = new Color(0.42f, 0.52f, 0.27f), monte = new Color(0.30f, 0.42f, 0.22f);
        bool invertir = Vector3.Cross(dir, afuera).y < 0f; // cara visible hacia arriba
        void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            if (invertir) { var x = p1; p1 = p2; p2 = x; }
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            float pend = 1f - n.y; // 0 plano, 1 vertical
            float hm = (p0.y + p1.y + p2.y) / 3f;
            Color col = pend > 0.35f ? Color.Lerp(roca, rocaOsc, (float)rnd.NextDouble())
                                     : Color.Lerp(pasto, monte, Mathf.InverseLerp(6f, 16f, hm) * 0.7f + (float)rnd.NextDouble() * 0.3f);
            int k = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); c.Add(col); c.Add(col); c.Add(col);
            t.Add(k); t.Add(k + 1); t.Add(k + 2);
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < filas - 1; j++)
            {
                Tri(grid[i, j], grid[i + 1, j], grid[i, j + 1]);
                Tri(grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]);
            }

        var m = new Mesh { name = nombre, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        Objeto(nombre, raiz, GuardarMalla(m, nombre), mat, true, true);
    }

    static void RocasAlPie(Transform raiz)
    {
        var padre = new GameObject("Rocas_Pie").transform;
        padre.SetParent(raiz, false);
        var rnd = new System.Random(77);
        var rocas = new List<GameObject>();
        for (int i = 1; i <= 5; i++)
        {
            var r = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/StylizedNature/Prefabs/Rock_" + i + ".prefab");
            if (r != null) rocas.Add(r);
        }
        if (rocas.Count == 0) return;

        void Linea(Vector3 a, Vector3 b, Vector3 afuera, float paso)
        {
            Vector3 d = b - a; float L = d.magnitude; d /= L;
            for (float s = 2f; s < L - 2f; s += paso * (0.7f + (float)rnd.NextDouble() * 0.6f))
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(rocas[rnd.Next(rocas.Count)], padre);
                float esc = 2.2f + (float)rnd.NextDouble() * 2.8f;
                g.transform.position = a + d * s + afuera * (1.2f + (float)rnd.NextDouble() * 1.5f) + Vector3.down * 0.3f;
                g.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                g.transform.localScale = Vector3.one * esc;
                foreach (var rr in g.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        Linea(new Vector3(BordeOeste, 0f, -84f), new Vector3(BordeOeste, 0f, 50f), Vector3.left, 7f);
        Linea(new Vector3(BordeEste, 0f, -84f), new Vector3(BordeEste, 0f, 50f), Vector3.right, 7f);
        Linea(new Vector3(BordeOeste, 0f, BordeSur), new Vector3(AccesoX0 - 1f, 0f, BordeSur), Vector3.back, 7f);
        Linea(new Vector3(AccesoX1 + 1f, 0f, BordeSur), new Vector3(BordeEste, 0f, BordeSur), Vector3.back, 7f);
    }

    static void ControlSur(Transform raiz)
    {
        var p = new GameObject("Control_Sur").transform;
        p.SetParent(raiz, false);
        var hormigon = MatShader("Muro_Contencion", "Standard", new Color(0.62f, 0.60f, 0.56f));
        var rojo = MatShader("Senal_Rojo", "Standard", new Color(0.80f, 0.12f, 0.10f));
        var blanco = MatShader("Senal_Blanco", "Standard", new Color(0.95f, 0.95f, 0.93f));
        var garitaMat = MatShader("Garita", "Standard", new Color(0.86f, 0.82f, 0.70f));
        var vidrio = MatShader("Garita_Vidrio", "Standard", new Color(0.18f, 0.26f, 0.32f));
        var poste = MatShader("Poste_Metal", "Standard", new Color(0.35f, 0.36f, 0.38f));
        float cx = (2.65f + 11.65f) * 0.5f;

        // Muros de contencion del corte.
        Caja("Muro_Corte_O", p, new Vector3(AccesoX0 + 0.5f, 2f, (BordeSur + AccesoFondo) * 0.5f), new Vector3(1f, 4f, BordeSur - AccesoFondo + 1f), hormigon);
        Caja("Muro_Corte_E", p, new Vector3(AccesoX1 - 0.5f, 2f, (BordeSur + AccesoFondo) * 0.5f), new Vector3(1f, 4f, BordeSur - AccesoFondo + 1f), hormigon);

        // Barrera (brazo a franjas) + colision de todo el ancho de la calle.
        Caja("Barrera_Poste", p, new Vector3(2.3f, 0.55f, Barrera), new Vector3(0.35f, 1.1f, 0.35f), poste);
        for (int i = 0; i < 9; i++)
            Caja("Barrera_Brazo", p, new Vector3(2.9f + i + 0.5f, 1.0f, Barrera), new Vector3(1f, 0.14f, 0.12f), i % 2 == 0 ? rojo : blanco, false);
        var bloqueo = new GameObject("Barrera_Colision");
        bloqueo.transform.SetParent(p, false);
        bloqueo.transform.position = new Vector3(cx, 1.1f, Barrera - 0.2f);
        bloqueo.AddComponent<BoxCollider>().size = new Vector3(AccesoX1 - AccesoX0, 2.2f, 0.4f);

        // Barreras New Jersey a franjas, cerrando el paso detras del brazo.
        for (int i = 0; i < 5; i++)
        {
            float x = 3.2f + i * 2.1f;
            Caja("NewJersey", p, new Vector3(x, 0.42f, -88.3f), new Vector3(1.95f, 0.84f, 0.6f), i % 2 == 0 ? rojo : blanco);
        }

        // Garita de control.
        float gx = 14.1f, gz = -89.8f;
        Caja("Garita_Cuerpo", p, new Vector3(gx, 1.3f, gz), new Vector3(2.2f, 2.6f, 2.4f), garitaMat);
        Caja("Garita_Techo", p, new Vector3(gx, 2.72f, gz), new Vector3(2.8f, 0.2f, 3.0f), blanco, false);
        Caja("Garita_Ventana_N", p, new Vector3(gx, 1.7f, gz + 1.21f), new Vector3(1.6f, 0.8f, 0.03f), vidrio, false);
        Caja("Garita_Ventana_O", p, new Vector3(gx - 1.11f, 1.7f, gz), new Vector3(0.03f, 0.8f, 1.8f), vidrio, false);
        Caja("Garita_Franja", p, new Vector3(gx, 2.45f, gz), new Vector3(2.22f, 0.18f, 2.42f), rojo, false);

        // Carteles.
        Cartel(p, "Cartel_Salida", new Vector3(cx, 3.4f, Barrera + 0.6f), 0f, new Vector2(7.5f, 1.7f), rojo,
               "SALIDA CERRADA\n<size=55%>La ciudad está cerrada por emergencia</size>", Color.white);
        Caja("Cartel_Salida_Poste_O", p, new Vector3(cx - 3.4f, 1.6f, Barrera + 0.65f), new Vector3(0.15f, 3.2f, 0.15f), poste);
        Caja("Cartel_Salida_Poste_E", p, new Vector3(cx + 3.4f, 1.6f, Barrera + 0.65f), new Vector3(0.15f, 3.2f, 0.15f), poste);
        Cartel(p, "Cartel_Control", new Vector3(gx, 3.2f, gz + 1.25f), 0f, new Vector2(2.4f, 0.55f), blanco, "CONTROL", new Color(0.15f, 0.15f, 0.15f));
    }

    /// <summary>Cartel: placa + texto TMP mirando hacia +Z (hacia la ciudad cuando rotY = 0).</summary>
    public static GameObject Cartel(Transform padre, string nombre, Vector3 pos, float rotY, Vector2 tam, Material placa, string texto, Color colorTexto)
    {
        var g = new GameObject(nombre).transform;
        g.SetParent(padre, false);
        g.position = pos;
        g.rotation = Quaternion.Euler(0f, rotY, 0f);
        var fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fondo.name = "Placa";
        Object.DestroyImmediate(fondo.GetComponent<Collider>());
        fondo.transform.SetParent(g, false);
        fondo.transform.localScale = new Vector3(tam.x, tam.y, 0.06f);
        fondo.GetComponent<Renderer>().sharedMaterial = placa;
        fondo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var txt = new GameObject("Texto");
        txt.transform.SetParent(g, false);
        txt.transform.localPosition = new Vector3(0f, 0f, 0.035f);
        txt.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TMP mira a -Z local
        var tmp = txt.AddComponent<TextMeshPro>();
        tmp.text = texto;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f; tmp.fontSizeMax = 40f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = colorTexto;
        tmp.rectTransform.sizeDelta = new Vector2(tam.x * 0.92f, tam.y * 0.85f);
        tmp.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.gameObject;
    }
}
