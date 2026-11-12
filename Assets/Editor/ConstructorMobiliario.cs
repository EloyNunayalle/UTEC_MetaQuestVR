using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Etapa 4 de la ciudad unica: mobiliario urbano y senaletica.
/// Cada prop es UNA malla (submallas por material) guardada como asset y reutilizada: sin sombras,
/// sin luces en tiempo real, estatico (static batching). Nada se coloca en calzadas de giro (ochavas),
/// puertas, portones, la rampa, zonas de carga, la zona segura ni los puntos de estacionamiento.
/// </summary>
public static class ConstructorMobiliario
{
    const string Raiz = "Mobiliario_Urbano";
    const string Dir = "Assets/Models/CiudadUnica/Props";
    static readonly float[][] NS = ConstructorCiudadUnica.CallesNS;
    const float Av0 = ConstructorCiudadUnica.AvZ0, Av1 = ConstructorCiudadUnica.AvZ1;
    const float Sur0 = ConstructorCiudadUnica.SurZ0, Sur1 = ConstructorCiudadUnica.SurZ1;

    static Transform _raiz;
    static Mesh _cubo, _cil;
    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    [MenuItem("UTEC/Ciudad Unica/4 Mobiliario urbano y carteles")]
    public static void Etapa4()
    {
        if (EditorApplication.isPlaying) return;
        var escena = SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogError("[Mobiliario] Abre Ciudad_Tile_00_Estacion."); return; }
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Models/CiudadUnica", "Props");
        var viejo = GameObject.Find(Raiz);
        if (viejo != null) Object.DestroyImmediate(viejo);
        _raiz = new GameObject(Raiz).transform;
        _cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        _cil = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

        Farolas();
        Hidrantes();
        BancosYTachos();
        Autos();
        Senales();
        Carteles();
        Misiones();

        foreach (var t in _raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        foreach (var r in _raiz.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Debug.Log("[Mobiliario] " + _raiz.GetComponentsInChildren<MeshRenderer>().Length + " renderers.");
    }

    // ------------------------------------------------------------------ mallas

    static Material M(string nombre, Color c, Color? emision = null)
    {
        if (_mats.TryGetValue(nombre, out var m) && m != null) return m;
        m = ConstructorCiudadUnica.Material(nombre, c);
        if (emision.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emision.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        return _mats[nombre] = m;
    }

    class Prop
    {
        public readonly List<(Material mat, List<CombineInstance> partes)> grupos = new List<(Material, List<CombineInstance>)>();
        public Prop P(Mesh malla, Material mat, Vector3 c, Vector3 s, Vector3? rot = null)
        {
            var g = grupos.Find(x => x.mat == mat);
            if (g.partes == null) { g = (mat, new List<CombineInstance>()); grupos.Add(g); }
            g.partes.Add(new CombineInstance { mesh = malla, transform = Matrix4x4.TRS(c, Quaternion.Euler(rot ?? Vector3.zero), s) });
            return this;
        }
    }

    static readonly Dictionary<string, (Mesh malla, Material[] mats)> _cache = new Dictionary<string, (Mesh, Material[])>();

    static (Mesh, Material[]) Construir(string nombre, Prop p)
    {
        if (_cache.TryGetValue(nombre, out var c) && c.malla != null) return c;
        var subs = new List<CombineInstance>();
        var mats = new List<Material>();
        foreach (var g in p.grupos)
        {
            var sub = new Mesh();
            sub.CombineMeshes(g.partes.ToArray(), true, true);
            subs.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
            mats.Add(g.mat);
        }
        var m = new Mesh { name = nombre };
        m.CombineMeshes(subs.ToArray(), false, false);
        m.RecalculateBounds();
        string ruta = Dir + "/" + nombre + ".asset";
        var vieja = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (vieja != null) { EditorUtility.CopySerialized(m, vieja); m = vieja; }
        else AssetDatabase.CreateAsset(m, ruta);
        return _cache[nombre] = (m, mats.ToArray());
    }

    static GameObject Poner(string tipo, Prop def, Vector3 pos, float rotY, Transform padre, Vector3? colision = null, Vector3? centroCol = null)
    {
        var (malla, mats) = Construir(tipo, def);
        var g = new GameObject(tipo);
        g.transform.SetParent(padre, false);
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        g.AddComponent<MeshFilter>().sharedMesh = malla;
        g.AddComponent<MeshRenderer>().sharedMaterials = mats;
        if (colision.HasValue)
        {
            var bc = g.AddComponent<BoxCollider>();
            bc.size = colision.Value;
            bc.center = centroCol ?? new Vector3(0f, colision.Value.y * 0.5f, 0f);
        }
        return g;
    }

    static Transform Grupo(string nombre)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(_raiz, false);
        return t;
    }

    static bool CercaDeCruce(float x, float margen)
    {
        foreach (var c in NS) if (x > c[0] - margen && x < c[1] + margen) return true;
        return false;
    }

    // ------------------------------------------------------------------ definiciones

    static Prop DefFarola()
    {
        var metal = M("Poste_Metal", new Color(0.35f, 0.36f, 0.38f));
        var luz = M("Farola_Luz", new Color(1f, 0.92f, 0.75f), new Color(1.2f, 0.95f, 0.65f));
        return new Prop()
            .P(_cil, metal, new Vector3(0f, 2.6f, 0f), new Vector3(0.14f, 2.6f, 0.14f))
            .P(_cil, metal, new Vector3(0f, 0.15f, 0f), new Vector3(0.26f, 0.15f, 0.26f))
            .P(_cubo, metal, new Vector3(0f, 5.15f, 0.6f), new Vector3(0.08f, 0.08f, 1.3f))
            .P(_cubo, metal, new Vector3(0f, 5.1f, 1.2f), new Vector3(0.28f, 0.1f, 0.5f))
            .P(_cubo, luz, new Vector3(0f, 5.03f, 1.2f), new Vector3(0.22f, 0.04f, 0.42f));
    }

    static Prop DefHidrante()
    {
        var rojo = M("Hidrante_Rojo", new Color(0.78f, 0.10f, 0.08f));
        var metal = M("Poste_Metal", new Color(0.35f, 0.36f, 0.38f));
        return new Prop()
            .P(_cil, rojo, new Vector3(0f, 0.32f, 0f), new Vector3(0.22f, 0.32f, 0.22f))
            .P(_cil, rojo, new Vector3(0f, 0.68f, 0f), new Vector3(0.27f, 0.04f, 0.27f))
            .P(_cil, rojo, new Vector3(0f, 0.76f, 0f), new Vector3(0.12f, 0.05f, 0.12f))
            .P(_cil, metal, new Vector3(0.15f, 0.45f, 0f), new Vector3(0.09f, 0.06f, 0.09f), new Vector3(0f, 0f, 90f))
            .P(_cil, metal, new Vector3(-0.15f, 0.45f, 0f), new Vector3(0.09f, 0.06f, 0.09f), new Vector3(0f, 0f, 90f))
            .P(_cil, metal, new Vector3(0f, 0.45f, 0.15f), new Vector3(0.12f, 0.06f, 0.12f), new Vector3(90f, 0f, 0f));
    }

    static Prop DefBanco()
    {
        var madera = M("Banco_Madera", new Color(0.55f, 0.36f, 0.20f));
        var metal = M("Poste_Metal", new Color(0.35f, 0.36f, 0.38f));
        return new Prop()
            .P(_cubo, madera, new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.06f, 0.45f))
            .P(_cubo, madera, new Vector3(0f, 0.75f, -0.22f), new Vector3(1.7f, 0.3f, 0.05f), new Vector3(-10f, 0f, 0f))
            .P(_cubo, metal, new Vector3(-0.7f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.42f))
            .P(_cubo, metal, new Vector3(0.7f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.42f));
    }

    static Prop DefTacho()
    {
        var verde = M("Tacho_Verde", new Color(0.18f, 0.40f, 0.26f));
        var metal = M("Poste_Metal", new Color(0.35f, 0.36f, 0.38f));
        return new Prop()
            .P(_cil, verde, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.45f, 0.45f))
            .P(_cil, metal, new Vector3(0f, 0.92f, 0f), new Vector3(0.48f, 0.03f, 0.48f));
    }

    static Prop DefCono()
    {
        var naranja = M("Cono_Naranja", new Color(1f, 0.42f, 0.05f));
        var blanco = M("Senal_Blanco", new Color(0.95f, 0.95f, 0.93f));
        return new Prop()
            .P(_cubo, naranja, new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.04f, 0.38f))
            .P(_cil, naranja, new Vector3(0f, 0.2f, 0f), new Vector3(0.24f, 0.16f, 0.24f))
            .P(_cil, blanco, new Vector3(0f, 0.4f, 0f), new Vector3(0.17f, 0.05f, 0.17f))
            .P(_cil, naranja, new Vector3(0f, 0.55f, 0f), new Vector3(0.11f, 0.1f, 0.11f));
    }

    static Prop DefValla()
    {
        var rojo = M("Senal_Rojo", new Color(0.80f, 0.12f, 0.10f));
        var blanco = M("Senal_Blanco", new Color(0.95f, 0.95f, 0.93f));
        var metal = M("Poste_Metal", new Color(0.35f, 0.36f, 0.38f));
        var p = new Prop()
            .P(_cubo, metal, new Vector3(-0.9f, 0.5f, 0f), new Vector3(0.05f, 1f, 0.05f))
            .P(_cubo, metal, new Vector3(0.9f, 0.5f, 0f), new Vector3(0.05f, 1f, 0.05f))
            .P(_cubo, metal, new Vector3(-0.9f, 0.02f, 0f), new Vector3(0.08f, 0.04f, 0.5f))
            .P(_cubo, metal, new Vector3(0.9f, 0.02f, 0f), new Vector3(0.08f, 0.04f, 0.5f));
        for (int i = 0; i < 6; i++)
            p.P(_cubo, i % 2 == 0 ? rojo : blanco, new Vector3(-0.75f + i * 0.3f, 0.85f, 0f), new Vector3(0.3f, 0.2f, 0.03f));
        return p;
    }

    static Prop DefAuto(string color, Color c)
    {
        var carro = M("Auto_" + color, c);
        var vidrio = M("Garita_Vidrio", new Color(0.18f, 0.26f, 0.32f));
        var goma = M("Goma", new Color(0.07f, 0.07f, 0.08f));
        var p = new Prop()
            .P(_cubo, carro, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.6f, 4.2f))
            .P(_cubo, carro, new Vector3(0f, 1.12f, -0.25f), new Vector3(1.6f, 0.46f, 2.2f))
            .P(_cubo, vidrio, new Vector3(0f, 1.12f, -0.25f), new Vector3(1.62f, 0.34f, 2.0f))
            .P(_cubo, vidrio, new Vector3(0f, 1.1f, 0.87f), new Vector3(1.5f, 0.36f, 0.05f), new Vector3(-35f, 0f, 0f));
        foreach (var x in new[] { -0.86f, 0.86f })
            foreach (var z in new[] { -1.35f, 1.35f })
                p.P(_cil, goma, new Vector3(x, 0.33f, z), new Vector3(0.64f, 0.12f, 0.64f), new Vector3(0f, 0f, 90f));
        return p;
    }

    static Prop DefPoste(float alto)
    {
        var metal = M("Poste_Metal", new Color(0.35f, 0.36f, 0.38f));
        return new Prop().P(_cil, metal, new Vector3(0f, alto * 0.5f, 0f), new Vector3(0.07f, alto * 0.5f, 0.07f));
    }

    // ------------------------------------------------------------------ colocacion

    static void Farolas()
    {
        var g = Grupo("Farolas");
        var def = DefFarola();
        var col = new Vector3(0.25f, 5.3f, 0.25f);
        // Avenida: sur (brazo hacia +Z) y norte (brazo hacia -Z), a 0.5 m del cordon.
        for (float x = -96f; x <= 88f; x += 24f)
        {
            if (!CercaDeCruce(x, 6f) && !(x > -11f && x < 1f)) // libre el porton y la rampa
                Poner("Farola", def, new Vector3(x, 0.02f, Av0 - 0.5f), 0f, g, col, new Vector3(0f, 2.65f, 0f));
            if (x + 12f <= 92f && !(x + 12f > -92f && x + 12f < -70f)) // puerta de M3 y plazoleta de la zona segura
                Poner("Farola", def, new Vector3(x + 12f, 0.02f, Av1 + 0.5f), 180f, g, col, new Vector3(0f, 2.65f, 0f));
        }
        // Calles N-S: vereda oeste, brazo hacia +X.
        foreach (var c in NS)
            for (float z = -62f; z <= 6f; z += 22f)
                Poner("Farola", def, new Vector3(c[0] - 0.5f, 0.02f, z), 90f, g, col, new Vector3(0f, 2.65f, 0f));
        // Calle sur: vereda norte de las manzanas.
        for (float x = -84f; x <= 80f; x += 30f)
            if (!CercaDeCruce(x, 6f)) Poner("Farola", def, new Vector3(x, 0.02f, Sur1 + 0.5f), 180f, g, col, new Vector3(0f, 2.65f, 0f));
        // Malecon: frente a la baranda, iluminando el paseo.
        for (float x = -100f; x <= 90f; x += 20f)
            Poner("Farola", def, new Vector3(x, 0.02f, 50.6f), 180f, g, col, new Vector3(0f, 2.65f, 0f));
    }

    static void Hidrantes()
    {
        var g = Grupo("Hidrantes");
        var def = DefHidrante();
        var col = new Vector3(0.3f, 0.8f, 0.3f);
        var puntos = new[]
        {
            new Vector3(1.6f, 0f, 9f),        // Calle Estacion, junto al HUB
            new Vector3(12.3f, 0f, -44.5f),   // M1, al norte de la fachada en llamas
            new Vector3(57.3f, 0f, -24f),     // M2, frente a la vecina
            new Vector3(-95.5f, 0f, 27.3f),   // M3, al oeste de la casa (lejos de la zona segura)
            new Vector3(-50f, 0f, 27.3f),
            new Vector3(40f, 0f, -72.7f),
            new Vector3(-95.6f, 0f, -40f),
        };
        foreach (var p in puntos) Poner("Hidrante", def, p + Vector3.up * 0.02f, 0f, g, col);
    }

    static void BancosYTachos()
    {
        var g = Grupo("Bancos_Tachos");
        var banco = DefBanco();
        var tacho = DefTacho();
        // Malecon: bancos mirando al mar, con tacho al lado.
        for (float x = -90f; x <= 80f; x += 30f)
        {
            Poner("Banco", banco, new Vector3(x, 0.02f, 49.2f), 0f, g, new Vector3(1.7f, 0.9f, 0.5f));
            Poner("Tacho", tacho, new Vector3(x + 1.5f, 0.02f, 49.2f), 0f, g, new Vector3(0.45f, 0.95f, 0.45f));
        }
        // Veredas de la avenida (lado norte) y frente al parque de la estacion.
        foreach (var x in new[] { -40f, 26f, 62f })
            Poner("Tacho", tacho, new Vector3(x, 0.02f, Av1 + 0.6f), 0f, g, new Vector3(0.45f, 0.95f, 0.45f));
        Poner("Banco", banco, new Vector3(-8f, 0.02f, -30f), 180f, g, new Vector3(1.7f, 0.9f, 0.5f));
        Poner("Banco", banco, new Vector3(-8f, 0.02f, -50f), 180f, g, new Vector3(1.7f, 0.9f, 0.5f));
        Poner("Tacho", tacho, new Vector3(-6f, 0.02f, -30f), 0f, g, new Vector3(0.45f, 0.95f, 0.45f));
        // Macetas del kit sobre las veredas anchas de la avenida (lado sur, lejos de la rampa).
        foreach (var x in new[] { -33f, -27f, 20f, 32f, 64f, 76f })
            ConstructorCiudadUnica.Pieza("Prop_Planter_Single", g, new Vector3(x, 0.02f, Av0 - 2.1f), 0f).name = "Maceta";
    }

    static void Autos()
    {
        var g = Grupo("Autos_Estacionados");
        var defs = new[]
        {
            DefAuto("Azul", new Color(0.16f, 0.30f, 0.55f)), DefAuto("Blanco", new Color(0.88f, 0.88f, 0.86f)),
            DefAuto("Gris", new Color(0.45f, 0.47f, 0.50f)), DefAuto("Verde", new Color(0.25f, 0.42f, 0.30f))
        };
        string[] nom = { "Auto_Azul", "Auto_Blanco", "Auto_Gris", "Auto_Verde" };
        var col = new Vector3(1.8f, 1.4f, 4.2f);
        // Junto al cordon, en el carril derecho: quedan > 6.5 m libres para el camion.
        var autos = new (Vector3 p, float r)[]
        {
            (new Vector3(48.8f, 0.02f, -40f), 180f), (new Vector3(48.8f, 0.02f, -52f), 180f), (new Vector3(55.5f, 0.02f, 6f), 0f),
            (new Vector3(-50.2f, 0.02f, -20f), 180f), (new Vector3(-50.2f, 0.02f, -36f), 180f),
            (new Vector3(-30f, 0.02f, Sur1 - 1.15f), 90f), (new Vector3(66f, 0.02f, Sur1 - 1.15f), 90f),
            (new Vector3(-97.5f, 0.02f, -55f), 180f),
        };
        for (int i = 0; i < autos.Length; i++)
            Poner(nom[i % 4], defs[i % 4], autos[i].p, autos[i].r, g, col);
    }

    static void Senales()
    {
        var g = Grupo("Senales_Transito");
        var rojo = M("Senal_Rojo", new Color(0.80f, 0.12f, 0.10f));
        // PARE en cada calle N-S, 5 m antes de la avenida (fuera de la ochava), lado derecho del que sube.
        foreach (var c in NS)
        {
            Poner("Poste_Senal", DefPoste(2.4f), new Vector3(c[1] + 0.6f, 0.02f, Av0 - 5f), 0f, g, new Vector3(0.1f, 2.4f, 0.1f));
            ConstructorLimiteCostero.Cartel(g, "Pare", new Vector3(c[1] + 0.6f, 2.35f, Av0 - 5.04f), 180f, new Vector2(0.7f, 0.7f), rojo, "PARE", Color.white);
        }
    }

    static void Carteles()
    {
        var g = Grupo("Carteles");
        var verde = M("Cartel_Verde", new Color(0.08f, 0.38f, 0.22f));
        var azul = M("Cartel_Azul", new Color(0.10f, 0.25f, 0.55f));
        var poste = DefPoste(3.3f);
        void Cartel(string nombre, Vector3 pos, float rotY, string texto, Material mat, float ancho = 3.4f, float alto = 0.75f)
        {
            Vector3 lateral = Quaternion.Euler(0f, rotY, 0f) * Vector3.right;
            Poner("Poste_Cartel", poste, pos - lateral * (ancho * 0.45f), 0f, g, new Vector3(0.12f, 3.3f, 0.12f));
            Poner("Poste_Cartel", poste, pos + lateral * (ancho * 0.45f), 0f, g, new Vector3(0.12f, 3.3f, 0.12f));
            ConstructorLimiteCostero.Cartel(g, nombre, pos + Vector3.up * (2.55f + alto * 0.5f), rotY, new Vector2(ancho, alto), mat, texto, Color.white);
        }
        // Saliendo del porton (mirando al norte): oeste a la izquierda, este a la derecha.
        Cartel("Cartel_Salida_Porton", new Vector3(-5.25f, 0.02f, Av1 + 1.4f), 180f, "<align=left>« BARRIO OESTE</align>\n<align=right>INDUSTRIA · RESIDENCIAL »</align>", verde, 4.2f, 1.1f);
        // Hacia el este por la avenida: girar a la derecha (sur).
        Cartel("Cartel_Industrial", new Vector3(1f, 0.02f, Av1 + 1.4f), 270f, "ZONA INDUSTRIAL »", verde, 2.4f);
        Cartel("Cartel_Residencial", new Vector3(42f, 0.02f, Av1 + 1.4f), 270f, "BARRIO RESIDENCIAL »", verde, 2.4f);
        // Hacia el oeste: sigue recto por la avenida.
        Cartel("Cartel_Oeste", new Vector3(-30f, 0.02f, Av1 + 1.4f), 90f, "BARRIO OESTE · MALECÓN", verde, 2.4f);
        // Estacion, visible desde la avenida.
        Cartel("Cartel_Estacion", new Vector3(-20f, 0.02f, Av0 - 1.4f), 0f, "ESTACIÓN DE BOMBEROS", M("Senal_Rojo", new Color(0.80f, 0.12f, 0.10f)), 4.2f);
        // Casas de mision (numero de calle, sin delatar la mision).
        Cartel("Cartel_Casas_Residencial", new Vector3(58.6f, 0.02f, Av0 - 1.4f), 0f, "CALLE RESIDENCIAL · CASAS", azul, 4f);
        Cartel("Cartel_Casa_Oeste", new Vector3(-99.5f, 0.02f, Av1 + 1.4f), 180f, "CASAS 1 - 9", azul, 2.6f);
    }

    /// <summary>Enmarca las misiones: estacionamiento pintado para el camion, conos y "ZONA RESTRINGIDA".</summary>
    static void Misiones()
    {
        var g = Grupo("Marcos_Mision");
        var amarillo = M("Pintura_Amarilla", new Color(0.98f, 0.80f, 0.10f));
        var rojo = M("Senal_Rojo", new Color(0.80f, 0.12f, 0.10f));
        var cono = DefCono();
        var valla = DefValla();

        void Estacionamiento(string nombre, Vector3 centro, bool aLoLargoX)
        {
            float L = 11f, W = 3.4f, e = 0.15f;
            var p = new Prop();
            Vector3 a = aLoLargoX ? new Vector3(L, 0.01f, e) : new Vector3(e, 0.01f, L);
            Vector3 b = aLoLargoX ? new Vector3(e, 0.01f, W) : new Vector3(W, 0.01f, e);
            Vector3 dA = aLoLargoX ? new Vector3(0f, 0f, W * 0.5f) : new Vector3(W * 0.5f, 0f, 0f);
            Vector3 dB = aLoLargoX ? new Vector3(L * 0.5f, 0f, 0f) : new Vector3(0f, 0f, L * 0.5f);
            p.P(_cubo, amarillo, dA, a).P(_cubo, amarillo, -dA, a).P(_cubo, amarillo, dB, b).P(_cubo, amarillo, -dB, b);
            Poner("Estacionamiento_Camion", p, centro + Vector3.up * 0.025f, 0f, g);
            var t = new GameObject(nombre + "_Texto");
            t.transform.SetParent(g, false);
            t.transform.position = centro + Vector3.up * 0.04f;
            t.transform.rotation = Quaternion.Euler(90f, aLoLargoX ? 0f : 90f, 0f);
            var tmp = t.AddComponent<TextMeshPro>();
            tmp.text = "BOMBEROS"; tmp.fontSize = 9f; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.98f, 0.80f, 0.10f);
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
        }
        // Coinciden con Mision3Controller.puntoEstacionar y con las calles frente a M1 y M2.
        Estacionamiento("Est_M3", new Vector3(-85f, 0f, 23.4f), true);
        Estacionamiento("Est_M2", new Vector3(54.4f, 0f, -14f), false);
        Estacionamiento("Est_M1", new Vector3(9.4f, 0f, -55f), false);

        // Conos en los extremos de cada estacionamiento (sobre la linea, no dentro del cajon).
        void Conos(Vector3 c, bool x)
        {
            Vector3 d = x ? new Vector3(6.2f, 0f, 1.2f) : new Vector3(1.2f, 0f, 6.2f);
            foreach (var s in new[] { -1f, 1f })
                Poner("Cono", cono, c + new Vector3(d.x * (x ? s : 1f), 0.02f, d.z * (x ? 1f : s)), 0f, g);
        }
        Conos(new Vector3(-85f, 0f, 23.4f), true);
        Conos(new Vector3(54.4f, 0f, -14f), false);
        Conos(new Vector3(9.4f, 0f, -55f), false);

        // M1 (zona industrial): vallas y "ZONA RESTRINGIDA" en las esquinas de la vereda, sin tapar la fachada.
        foreach (var z in new[] { -62.5f, -46f })
            Poner("Valla", valla, new Vector3(13.3f, 0.02f, z), 90f, g, new Vector3(1.9f, 1.1f, 0.3f));
        ConstructorLimiteCostero.Cartel(g, "Restringida_M1", new Vector3(13.3f, 1.6f, -63.6f), 90f, new Vector2(1.6f, 0.5f), rojo, "ZONA RESTRINGIDA", Color.white);
        Poner("Poste_Senal", DefPoste(2.4f), new Vector3(13.3f, 0.02f, -63.6f), 0f, g, new Vector3(0.1f, 2.4f, 0.1f));
        // M3: cartel al oeste de la casa (la zona segura queda libre al este).
        ConstructorLimiteCostero.Cartel(g, "Restringida_M3", new Vector3(-92.9f, 1.6f, 27.6f), 180f, new Vector2(1.6f, 0.5f), rojo, "ZONA RESTRINGIDA", Color.white);
        Poner("Poste_Senal", DefPoste(2.4f), new Vector3(-92.9f, 0.02f, 27.65f), 0f, g, new Vector3(0.1f, 2.4f, 0.1f));
        // Obra en la vereda de la calle sur: vallas + conos (narrativa de emergencia).
        for (int i = 0; i < 3; i++) Poner("Valla", valla, new Vector3(24f + i * 2f, 0.02f, Sur0 - 1.5f), 0f, g, new Vector3(1.9f, 1.1f, 0.3f));
        for (int i = 0; i < 4; i++) Poner("Cono", cono, new Vector3(23f + i * 2f, 0.02f, Sur0 - 0.6f), 0f, g);
    }
}
