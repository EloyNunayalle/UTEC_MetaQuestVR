using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Construye la ciudad de tile unico (Ciudad_Tile_00_Estacion) sobre una grilla de 9 m que calza
/// con el kit DowntownCity (asfalto 9x9, veredas 3x3). La estacion NO se mueve.
///
///  Avenida (E-O)  z [16.65, 25.65]      Calle Sur (E-O) z [-82.35, -73.35]
///  Calles N-S     x [-105.35,-96.35] Oeste | [-51.35,-42.35] Plaza | [2.65,11.65] Estacion
///                 x [47.65,56.65] Residencial | [83.65,92.65] Este
///
/// Etapa 1 (layout): calles, veredas, marcas, edificios de mision reubicados y relleno urbano.
/// Es idempotente: borra lo generado antes y vuelve a construir.
/// </summary>
public static class ConstructorCiudadUnica
{
    // ---------- Grilla ----------
    public const float AvZ0 = 16.65f, AvZ1 = 25.65f;
    public const float SurZ0 = -82.35f, SurZ1 = -73.35f;
    public static readonly float[][] CallesNS =
    {
        new[] { -105.35f, -96.35f }, new[] { -51.35f, -42.35f }, new[] { 2.65f, 11.65f },
        new[] { 47.65f, 56.65f }, new[] { 83.65f, 92.65f }
    };
    public const float XMin = -105.35f, XMax = 92.65f; // borde exterior de la calzada
    public const float Vereda = 3f;

    // ---------- Misiones (fuente unica de las coordenadas nuevas) ----------
    public static readonly Vector3 M1Viejo = new Vector3(13.96f, 0f, -27.31f), M1Nuevo = new Vector3(16.96f, 0f, -55f);
    public static readonly Vector3 M2Viejo = new Vector3(17.65f, 0f, -14f), M2Nuevo = new Vector3(67.65f, 0f, -14f);
    public static readonly Vector3 M3Viejo = new Vector3(-39.11f, 0f, 27.96f), M3Nuevo = new Vector3(-85f, 0f, 30.96f);

    const float YAsfalto = 0.17f, YVereda = 0.02f, YMarca = 0.165f;
    const string Kit = "Assets/Models/DowntownCity/FBX/";
    const string Paleta = "Assets/Prefabs/Ciudad/Edificios/A2/Paleta/";
    const string RaizRelleno = "Relleno_Urbano";

    static Transform _calles, _veredas, _marcas;

    [MenuItem("UTEC/Ciudad Unica/1 Layout (calles, veredas, edificios)")]
    public static void Etapa1Layout()
    {
        if (EditorApplication.isPlaying) return;
        var escena = SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogError("[CiudadUnica] Abre Ciudad_Tile_00_Estacion."); return; }

        var tile = GameObject.Find("Ciudad_Tile_00").transform;
        _calles = Limpiar(tile, "Calles");
        _veredas = Limpiar(tile, "Veredas");
        _marcas = Limpiar(tile, "Marcas");
        Limpiar(tile, "Props");

        // Las mallas combinadas viejas se regeneran en la etapa de optimizacion.
        Borrar("Props_Combinados");
        DesagruparArboles();

        Suelo();
        Calles();
        Veredas();
        Marcas();
        Edificios();
        Vegetacion(tile.Find("Vegetacion"));

        foreach (Transform t in new[] { _calles, _veredas, _marcas })
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.isStatic = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("[CiudadUnica] Layout reconstruido.");
    }

    // ------------------------------------------------------------------ util

    static Transform Limpiar(Transform padre, string nombre)
    {
        var t = padre.Find(nombre);
        if (t == null) { t = new GameObject(nombre).transform; t.SetParent(padre, false); }
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        return t;
    }

    static void Borrar(string nombre)
    {
        var g = GameObject.Find(nombre);
        if (g != null) Object.DestroyImmediate(g);
    }

    /// <summary>Quita Arboles_Celda y reactiva los arboles originales (los reagrupa la optimizacion).</summary>
    static void DesagruparArboles()
    {
        Borrar("Arboles_Celda");
        foreach (var lg in Object.FindObjectsOfType<LODGroup>(true))
        {
            if (!lg.gameObject.name.ToLowerInvariant().Contains("tree")) continue;
            lg.enabled = true;
            foreach (var lod in lg.GetLODs())
                foreach (var r in lod.renderers)
                    if (r != null) r.enabled = true;
        }
    }

    public static GameObject Pieza(string fbx, Transform padre, Vector3 pos, float rotY, Vector3? escala = null)
    {
        var a = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + fbx + ".fbx");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(a, padre);
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        if (escala.HasValue) g.transform.localScale = escala.Value;
        g.isStatic = true;
        return g;
    }

    static bool EnCalleNS(float x0, float x1)
    {
        foreach (var c in CallesNS) if (x0 < c[1] - 0.01f && x1 > c[0] + 0.01f) return true;
        return false;
    }

    // ------------------------------------------------------------------ suelo

    static void Suelo()
    {
        // Tierra firme: x [-135, 125], z [-110, 52] (al norte empieza el mar en la etapa 2).
        var plane = GameObject.Find("Plane").transform;
        plane.position = new Vector3(-5f, 0f, -29f);
        plane.localScale = new Vector3(26f, 1f, 16.2f);
        var mat = plane.GetComponent<Renderer>().sharedMaterial;
        if (mat != null && mat.name == "M_Suelo_Ciudad") { mat.mainTextureScale = new Vector2(65f, 40.5f); EditorUtility.SetDirty(mat); }
    }

    // ------------------------------------------------------------------ calles

    static void Calles()
    {
        // Asfalto 9x9: pivote en la esquina (x0, z1), cubre x+9 y z-9.
        void Rect(float x0, float x1, float z0, float z1)
        {
            for (float x = x0; x < x1 - 0.01f; x += 9f)
                for (float z = z0; z < z1 - 0.01f; z += 9f)
                    Pieza("Street_Asphalt_9x9", _calles, new Vector3(x, YAsfalto, z + 9f), 0f).name = "Asfalto";
        }
        Rect(XMin, XMax, AvZ0, AvZ1);    // Avenida
        Rect(XMin, XMax, SurZ0, SurZ1);  // Calle Sur
        foreach (var c in CallesNS) Rect(c[0], c[1], SurZ1, AvZ0);
        Rect(2.65f, 11.65f, SurZ0 - 9f, SurZ0); // acceso cerrado al sur (control de salida, etapa 2)
    }

    // ------------------------------------------------------------------ veredas

    static void Veredas()
    {
        // rot: la cara de cordon del modulo apunta a +Z con rot 0 -> 0:+Z, 90:+X, 180:-Z, 270:-X
        void Tramo(float x0, float x1, float z0, float z1, float rot, bool sinCordon = false)
        {
            for (float x = x0; x < x1 - 0.01f; x += Vereda)
                for (float z = z0; z < z1 - 0.01f; z += Vereda)
                {
                    float cx = x + 1.5f, cz = z + 1.5f;
                    if (cz > 13.6f && cz < 16.7f && cx > -8.3f && cx < -2.2f) continue; // rampa del porton
                    Pieza(sinCordon ? "Sidewalk_NoCurb_3m" : "Sidewalk_Straight_3m", _veredas,
                          new Vector3(cx, YVereda, cz), rot).name = "Vereda";
                }
        }

        // Anillo exterior.
        Tramo(XMin - 3f, XMax + 3f, AvZ1, AvZ1 + 3f, 180f);                 // norte de la avenida
        Tramo(XMin - 3f, 2.65f, SurZ0 - 3f, SurZ0, 0f);                    // sur de la calle sur
        Tramo(11.65f, XMax + 3f, SurZ0 - 3f, SurZ0, 0f);
        Tramo(XMin - 3f, XMin, SurZ0, AvZ1, 90f);                          // oeste
        Tramo(XMax, XMax + 3f, SurZ0, AvZ1, 270f);                         // este

        // Manzanas interiores: anillo de 3 m con el cordon hacia la calle.
        for (int i = 0; i < CallesNS.Length - 1; i++)
        {
            float x0 = CallesNS[i][1], x1 = CallesNS[i + 1][0];
            float z0 = SurZ1, z1 = AvZ0;
            Tramo(x0 + 3f, x1 - 3f, z1 - 3f, z1, 0f);   // frente a la avenida
            Tramo(x0 + 3f, x1 - 3f, z0, z0 + 3f, 180f); // frente a la calle sur
            Tramo(x0, x0 + 3f, z0 + 3f, z1 - 3f, 270f); // calle al oeste
            Tramo(x1 - 3f, x1, z0 + 3f, z1 - 3f, 90f);  // calle al este
            foreach (var p in new[] { new Vector2(x0, z0), new Vector2(x1 - 3f, z0), new Vector2(x0, z1 - 3f), new Vector2(x1 - 3f, z1 - 3f) })
                Tramo(p.x, p.x + 3f, p.y, p.y + 3f, 0f, true); // ochavas
        }
    }

    // ------------------------------------------------------------------ marcas

    static void Marcas()
    {
        // Linea discontinua central: tramos de 6 m cada 12 m, sin pisar los cruces.
        void LineaEO(float z, float x0, float x1)
        {
            for (float x = x0 + 6f; x + 3f < x1 - 3f; x += 12f)
            {
                if (EnCalleNS(x - 3f - 4f, x + 3f + 4f)) continue;
                Pieza("Decal_BrokenLine_Straight", _marcas, new Vector3(x, YMarca, z), 0f).name = "Linea";
            }
        }
        void LineaNS(float x, float z0, float z1)
        {
            for (float z = z0 + 7.5f; z + 3f < z1 - 4.5f; z += 12f)
                Pieza("Decal_BrokenLine_Straight", _marcas, new Vector3(x, YMarca, z), 90f).name = "Linea";
        }
        LineaEO((AvZ0 + AvZ1) * 0.5f, XMin, XMax);
        LineaEO((SurZ0 + SurZ1) * 0.5f, XMin, XMax);
        foreach (var c in CallesNS) LineaNS((c[0] + c[1]) * 0.5f, SurZ1, AvZ0);

        // Cruces peatonales (estirados al ancho de 9 m) junto a las esquinas clave.
        var ancho = new Vector3(1f, 1f, 9f / 5.42f);
        void CruceEO(float x) => Pieza("Decal_Crosswalk", _marcas, new Vector3(x, YMarca + 0.005f, (AvZ0 + AvZ1) * 0.5f), 0f, ancho).name = "Cruce";
        void CruceNS(float x, float z) => Pieza("Decal_Crosswalk", _marcas, new Vector3(x, YMarca + 0.005f, z), 90f, ancho).name = "Cruce";
        foreach (var c in CallesNS)
        {
            float cx = (c[0] + c[1]) * 0.5f;
            if (c[0] > XMin) CruceEO(c[0] - 3f);
            if (c[1] < XMax) CruceEO(c[1] + 3f);
            CruceNS(cx, AvZ0 - 3f);
            CruceNS(cx, SurZ1 + 3f);
        }
        CruceEO(-13f); // frente al porton de la estacion, fuera de la rampa

        // Flechas de carril antes de los cruces de la avenida.
        foreach (var c in CallesNS)
        {
            if (c[0] > XMin) Pieza("Decal_ArrowStraight", _marcas, new Vector3(c[0] - 10f, YMarca, AvZ0 + 2.25f), 90f).name = "Flecha";
            if (c[1] < XMax) Pieza("Decal_ArrowStraight", _marcas, new Vector3(c[1] + 10f, YMarca, AvZ1 - 2.25f), 270f).name = "Flecha";
        }
    }

    // ------------------------------------------------------------------ edificios

    static GameObject RaizCercana(Vector3 p, string prefijo)
    {
        GameObject mejor = null; float d = 2f;
        foreach (var g in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!g.name.StartsWith(prefijo)) continue;
            Vector3 v = g.transform.position - p; v.y = 0f;
            if (v.magnitude < d) { d = v.magnitude; mejor = g; }
        }
        return mejor;
    }

    static void Mover(Vector3 viejo, Vector3 nuevo, string prefijo)
    {
        var g = RaizCercana(viejo, prefijo) ?? RaizCercana(nuevo, prefijo);
        if (g == null) { Debug.LogWarning($"[CiudadUnica] No encontre {prefijo} en {viejo}"); return; }
        g.transform.position = nuevo;
    }

    static void Edificios()
    {
        // Misiones (traslacion pura: los interiores aditivos se corren con el mismo delta).
        Mover(M1Viejo, M1Nuevo, "Building_Small_1_LOD");
        Mover(M2Viejo, M2Nuevo, "Rowhouse_A_Combined_LOD");
        Mover(M3Viejo, M3Nuevo, "Building_Small_1_LOD");
        // Fachada norte de la avenida: +3 m por el ensanche a 9 m.
        Mover(new Vector3(-24.85f, 0f, 26.22f), new Vector3(-24.85f, 0f, 29.22f), "Building_Medium_2_001_LOD");
        Mover(new Vector3(-8.59f, 0f, 27.96f), new Vector3(-8.59f, 0f, 30.96f), "Building_Small_1_LOD");
        // Local de esquina: Avenida x Calle Estacion, frente a la calle.
        Mover(new Vector3(17.65f, 0f, 1f), new Vector3(22.65f, 0f, 7.52f), "Corner_Shop_B_Combined_LOD");

        Borrar(RaizRelleno);
        var raiz = new GameObject(RaizRelleno).transform;

        // Norte de la avenida (fachadas a -Z sobre z = 28.65).
        float norte = AvZ1 + Vereda;
        Edificio("Rowhouse_Marron", raiz, Vector3.back, norte, -69f); // deja una plazoleta junto a M3 (zona segura)
        Edificio("Rowhouse_Marron", raiz, Vector3.back, norte, -61f);
        Edificio("Small_Blanco", raiz, Vector3.back, norte, -45f);
        Edificio("Corner_RedBrick", raiz, Vector3.back, norte, 8f);
        Edificio("Rowhouse_Marron", raiz, Vector3.back, norte, 20f);
        Edificio("Small_Gris", raiz, Vector3.back, norte, 33f);
        Edificio("Rowhouse_Marron", raiz, Vector3.back, norte, 46f);
        Edificio("Rowhouse_Marron", raiz, Vector3.back, norte, 54f);
        Edificio("Corner_RedBrick", raiz, Vector3.back, norte, 68f);
        Edificio("Rowhouse_Marron", raiz, Vector3.back, norte, 84f);

        // Barrio oeste (sur de la avenida, fachadas a +Z sobre z = 13.65).
        float surAv = AvZ0 - Vereda;
        Edificio("Rowhouse_Marron", raiz, Vector3.forward, surAv, -88f);
        Edificio("Rowhouse_Marron", raiz, Vector3.forward, surAv, -80f);
        Edificio("Corner_RedBrick", raiz, Vector3.forward, surAv, -63f);
        Edificio("Rowhouse_Marron", raiz, Vector3.forward, surAv, -34f);
        // Barrio oeste, frente a la calle sur.
        Edificio("Rowhouse_Marron", raiz, Vector3.back, SurZ1 + Vereda, -84f);
        Edificio("Rowhouse_Marron", raiz, Vector3.back, SurZ1 + Vereda, -64f);

        // Calle residencial (M2): casas vecinas mirando al oeste sobre x = 59.65.
        float resid = 56.65f + Vereda;
        Edificio("Rowhouse_Marron", raiz, Vector3.left, resid, 4f);
        Edificio("Rowhouse_Marron", raiz, Vector3.left, resid, -32f);
        Edificio("Rowhouse_Marron", raiz, Vector3.left, resid, -48f);

        // Zona industrial (manzana de M1): galpones bajos frente a la Calle Estacion y la calle sur.
        Galpon(raiz, new Vector3(30f, 0f, -30f), new Vector3(24f, 6.5f, 16f), new Color(0.62f, 0.66f, 0.70f));
        Galpon(raiz, new Vector3(38f, 0f, -63.5f), new Vector3(16f, 5.5f, 13f), new Color(0.72f, 0.60f, 0.42f));
        Galpon(raiz, new Vector3(-28f, 0f, -55f), new Vector3(18f, 6f, 20f), new Color(0.58f, 0.64f, 0.58f));
    }

    /// <summary>Instancia un edificio de la paleta con la fachada mirando a 'frente', alineada a la linea 'linea'.</summary>
    static GameObject Edificio(string tipo, Transform padre, Vector3 frente, float linea, float lateral)
    {
        var a = AssetDatabase.LoadAssetAtPath<GameObject>(Paleta + "EDF_A2_Paleta_" + tipo + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(a, padre);
        g.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
        g.transform.position = Vector3.zero;
        var lg = g.GetComponent<LODGroup>();
        var rs = lg != null ? lg.GetLODs()[0].renderers : g.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) if (r) b.Encapsulate(r.bounds);

        Vector3 p = Vector3.zero;
        if (frente.x != 0f)
        {
            float cara = frente.x < 0f ? b.min.x : b.max.x;
            p.x = linea - cara + (frente.x < 0f ? 0.01f : -0.01f);
            p.z = lateral - b.center.z;
        }
        else
        {
            float cara = frente.z < 0f ? b.min.z : b.max.z;
            p.z = linea - cara + (frente.z < 0f ? 0.01f : -0.01f);
            p.x = lateral - b.center.x;
        }
        g.transform.position = p;
        g.name = "EDF_" + tipo;
        g.isStatic = true;
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        return g;
    }

    static Material MatColor(string nombre, Color c)
    {
        const string dir = "Assets/Materials/CiudadUnica";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Materials", "CiudadUnica");
        string ruta = dir + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, ruta);
        }
        m.color = c;
        m.SetFloat("_Glossiness", 0.15f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }
    public static Material Material(string nombre, Color c) => MatColor(nombre, c);

    static void Galpon(Transform padre, Vector3 centroBase, Vector3 tam, Color color)
    {
        var g = new GameObject("Galpon").transform;
        g.SetParent(padre, false);
        g.position = centroBase;
        var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cuerpo.name = "Cuerpo";
        cuerpo.transform.SetParent(g, false);
        cuerpo.transform.localPosition = new Vector3(0f, tam.y * 0.5f, 0f);
        cuerpo.transform.localScale = tam;
        cuerpo.GetComponent<Renderer>().sharedMaterial = MatColor("Galpon_" + ColorUtility.ToHtmlStringRGB(color), color);
        var techo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        techo.name = "Techo";
        Object.DestroyImmediate(techo.GetComponent<Collider>());
        techo.transform.SetParent(g, false);
        techo.transform.localPosition = new Vector3(0f, tam.y + 0.15f, 0f);
        techo.transform.localScale = new Vector3(tam.x + 0.6f, 0.3f, tam.z + 0.6f);
        techo.GetComponent<Renderer>().sharedMaterial = MatColor("Galpon_Techo", new Color(0.35f, 0.37f, 0.40f));
        var porton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        porton.name = "Porton";
        Object.DestroyImmediate(porton.GetComponent<Collider>());
        porton.transform.SetParent(g, false);
        porton.transform.localPosition = new Vector3(-tam.x * 0.5f - 0.02f, 2.2f, 0f);
        porton.transform.localScale = new Vector3(0.05f, 4.4f, Mathf.Min(6f, tam.z * 0.5f));
        porton.GetComponent<Renderer>().sharedMaterial = MatColor("Galpon_Porton", new Color(0.82f, 0.45f, 0.18f));
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
    }

    // ------------------------------------------------------------------ vegetacion

    /// <summary>Reubica los arboles (con su colision de tronco) y la vegetacion baja en jardines.</summary>
    static void Vegetacion(Transform veg)
    {
        if (veg == null) return;
        var rnd = new System.Random(1234);

        // Jardines y franjas verdes (x0, x1, z0, z1), lejos de calles, puertas y triggers.
        var jardines = new[]
        {
            new Rect(-93f, -55f, -40f, -6f),    // patios del barrio oeste
            new Rect(-93f, -55f, -52f, -46f),
            new Rect(-39f, -26f, -15f, -3f),    // lateral oeste de la estacion
            new Rect(-15f, -1f, -70f, -26f),    // parque al sur de la estacion
            new Rect(75f, 80f, -70f, 8f),       // fondo de la calle residencial
            new Rect(14f, 44f, -45f, -40f),     // franja verde de la zona industrial
        };
        var franjaArboles = new[]
        {
            new Rect(-90f, -56f, -45f, -10f),
            new Rect(-38f, -27f, -14f, -4f),
            new Rect(-14f, -2f, -68f, -28f),
            new Rect(75.5f, 79.5f, -68f, 6f),
        };

        var arboles = new List<Transform>();
        var troncos = new List<Transform>();
        var resto = new List<Transform>();
        foreach (Transform c in veg)
        {
            if (c.GetComponent<LODGroup>() != null && c.name.ToLowerInvariant().Contains("tree")) arboles.Add(c);
            else if (c.name.StartsWith("Colision_Tronco")) troncos.Add(c);
            else if (c.name == "Cesped_Patio") continue;
            else resto.Add(c);
        }

        // Empareja cada tronco con su arbol antes de mover.
        var tronco = new Dictionary<Transform, Transform>();
        foreach (var a in arboles)
        {
            Transform mejor = null; float d = 1.5f;
            foreach (var t in troncos)
            {
                if (tronco.ContainsValue(t)) continue;
                Vector3 v = t.position - a.position; v.y = 0f;
                if (v.magnitude < d) { d = v.magnitude; mejor = t; }
            }
            if (mejor != null) tronco[a] = mejor;
        }

        var usados = new List<Vector2>();
        Vector2 Punto(Rect[] zonas, float sep)
        {
            for (int intento = 0; intento < 60; intento++)
            {
                var z = zonas[rnd.Next(zonas.Length)];
                var p = new Vector2(z.xMin + (float)rnd.NextDouble() * z.width, z.yMin + (float)rnd.NextDouble() * z.height);
                bool libre = true;
                foreach (var u in usados) if ((u - p).sqrMagnitude < sep * sep) { libre = false; break; }
                if (libre) { usados.Add(p); return p; }
            }
            var zz = zonas[0];
            return zz.center;
        }

        foreach (var a in arboles)
        {
            var p = Punto(franjaArboles, 6f);
            Vector3 off = tronco.TryGetValue(a, out var t) ? t.position - a.position : Vector3.zero;
            a.position = new Vector3(p.x, a.position.y, p.y);
            if (t != null) t.position = a.position + off;
        }
        foreach (var r in resto)
        {
            var p = Punto(jardines, 1.2f);
            r.position = new Vector3(p.x, r.position.y, p.y);
        }
    }

    struct Rect
    {
        public float xMin, yMin, width, height;
        public Rect(float x0, float x1, float z0, float z1) { xMin = x0; yMin = z0; width = x1 - x0; height = z1 - z0; }
        public Vector2 center => new Vector2(xMin + width * 0.5f, yMin + height * 0.5f);
    }
}
