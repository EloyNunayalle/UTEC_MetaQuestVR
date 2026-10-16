using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// Interior realista del camion (segun la referencia del informe y el mockup camion.png):
///  - Volante redondo (malla de toro generada por codigo) que reemplaza al low-poly, agarrable.
///  - Tablero empotrado: cluster con velocimetro y tacometro (agujas reales), km/h digital,
///    indicadores de marcha/luces/sirena, y pantalla central con el GPS (mapa, ruta, ETA).
///  - Palanca de cambios P-R-N-D agarrable en la consola.
///  - Luz tenue de cabina. Nada flotante: todo va montado al camion.
/// Funciona tambien en el EDITOR (preview, no se guarda) para afinar posiciones.
/// </summary>
[ExecuteAlways]
public class InteriorCamion : MonoBehaviour
{
    [Header("Volante")]
    public Vector3 centroVolante = new Vector3(-5.902f, 1.893f, -6.387f);
    public float radioVolante = 0.275f;
    public float grosorAro = 0.042f;
    [Range(0f, 90f)] public float inclinacionVolante = 65f;

    [Header("Tablero (cara frontal)")]
    public Vector3 centroTablero = new Vector3(-5.40f, 1.70f, -6.40f);
    [Range(0f, 60f)] public float inclinacionTablero = 26f;
    public Vector2 tamanoTablero = new Vector2(1.95f, 0.62f);

    [Header("Cluster y pantalla (empotrados en la cara del tablero)")]
    public Vector2 offsetCluster = new Vector2(-0.25f, 0.07f);
    public Vector2 offsetPantalla = new Vector2(0.45f, 0.05f);
    public Vector2 tamanoPantalla = new Vector2(0.30f, 0.17f);

    [Header("Palanca P-R-N-D")]
    public Vector3 centroPalanca = new Vector3(-5.48f, 1.10f, -6.39f);
    [Range(0f, 60f)] public float inclinacionPalanca = 0f;

    [Header("Extras")]
    public bool luzCabina = true;
    [Tooltip("Usar el panel+volante Mercedes del modelo (desactiva los procedurales).")]
    public bool usarPanelMercedes = true;
    public string nombreRuta = "Almacen Central";
    public Vector3 puntoDestino = new Vector3(-5.19f, 0f, -1f);
    public float velocidadMinimaEta = 3f;

    // Paleta.
    static readonly Color ColFondo = new Color(0.055f, 0.08f, 0.13f, 0.96f);
    static readonly Color ColBorde = new Color(0.28f, 0.42f, 0.58f, 0.85f);
    static readonly Color ColCyan = new Color(0.40f, 0.75f, 0.95f, 1f);
    static readonly Color ColBlanco = new Color(0.93f, 0.95f, 0.98f, 1f);
    static readonly Color ColGris = new Color(0.60f, 0.66f, 0.72f, 1f);
    static readonly Color ColAmbar = new Color(0.86f, 0.68f, 0.16f, 1f);
    static readonly Color ColVerde = new Color(0.30f, 0.80f, 0.35f, 1f);
    static readonly Color ColRojo = new Color(0.85f, 0.32f, 0.26f, 1f);
    static readonly Color ColChipOff = new Color(0.13f, 0.16f, 0.20f, 0.92f);
    static readonly Color ColRuta = new Color(0.55f, 0.75f, 0.95f, 1f);
    static readonly Color ColGrid = new Color(0.25f, 0.35f, 0.50f, 0.55f);
    static readonly Color ColGoma = new Color(0.09f, 0.09f, 0.10f, 1f);
    static readonly Color ColMetal = new Color(0.17f, 0.17f, 0.19f, 1f);

    Transform _raiz;
    CamionBomberos _camion;
    Transform _tablero, _cluster, _pantalla, _palancaPivote;
    Transform _agujaVel, _agujaRpm;
    TextMeshPro _velTxt, _marchaTxt, _lucesTxt, _sirenaTxt, _etaTxt;
    TextMeshPro[] _letras = new TextMeshPro[4];
    Material _matChipLuces, _matChipSirena;
    FieldInfo _campoLuces;
    float _tiempoBusqueda;
    bool _reconstruir;
    bool _construido;
    int _ultimaVel = -1;
    string _ultimaEta = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arranque()
    {
        if (Object.FindObjectOfType<InteriorCamion>() != null) return;
        var go = new GameObject("InteriorCamion");
        go.AddComponent<InteriorCamion>();
    }

    void Update()
    {
        if (_reconstruir)
        {
            _reconstruir = false;
            Destruir();
        }

        if (_raiz == null)
        {
            _tiempoBusqueda += Time.deltaTime;
            if (_tiempoBusqueda > 0.5f)
            {
                _tiempoBusqueda = 0f;
                BuscarCamion();
            }
            if (Application.isPlaying && _raiz == null && Time.timeSinceLevelLoad > 30f) enabled = false;
            return;
        }

        if (Application.isPlaying)
        {
            if (_camion == null) _camion = _raiz.GetComponent<CamionBomberos>();
            if (_camion != null) Actualizar();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) _reconstruir = true;
    }

    void BuscarCamion()
    {
        var go = GameObject.Find("Camion_Bomberos");
        if (go == null)
        {
            var c = Object.FindObjectOfType<CamionBomberos>();
            if (c != null) go = c.gameObject;
        }
        if (go == null) return;
        _raiz = go.transform;
        _camion = go.GetComponent<CamionBomberos>();
        Construir();
    }

    void Destruir()
    {
        if (_raiz != null)
        {
            for (int i = _raiz.childCount - 1; i >= 0; i--)
            {
                var t = _raiz.GetChild(i);
                if (t.name.StartsWith("Interior_")) Object.DestroyImmediate(t.gameObject);
            }
        }
        _tablero = _cluster = _pantalla = _palancaPivote = null;
        _construido = false;
        _agujaVel = _agujaRpm = null;
        _velTxt = _marchaTxt = _lucesTxt = _sirenaTxt = _etaTxt = null;
        for (int i = 0; i < _letras.Length; i++) _letras[i] = null;
        _matChipLuces = _matChipSirena = null;
        _raiz = null;
        _camion = null;
        _ultimaVel = -1;
        _ultimaEta = "";
    }

    // ------------------------------------------------------------------ construccion

    void Construir()
    {
        if (_construido || _raiz == null) return;
        _construido = true;
        var raiz = _raiz;

        if (!usarPanelMercedes)
        {
            ConstruirVolante(raiz);
            ConstruirTablero(raiz);
            ConstruirCluster(raiz);
            ConstruirPantalla(raiz);
        }
        // Si la palanca nueva (modelo importado) esta en la escena, no se construye la procedural.
        if (GameObject.Find("Palanca_Cambios") == null) ConstruirPalanca(raiz);
        if (luzCabina) ConstruirLuz(raiz);

        _campoLuces = typeof(CamionBomberos).GetField("_lucesOn", BindingFlags.NonPublic | BindingFlags.Instance);

        // En el editor el preview no se guarda en la escena.
        if (!Application.isPlaying)
        {
            for (int i = 0; i < raiz.childCount; i++)
            {
                var t = raiz.GetChild(i);
                if (!t.name.StartsWith("Interior_")) continue;
                foreach (var d in t.GetComponentsInChildren<Transform>(true)) d.gameObject.hideFlags = HideFlags.DontSave;
            }
        }
    }

    static Quaternion RotacionHaciaConductor(float inclinacion)
    {
        // El texto/mallas se leen por el lado -Z: +Z apunta hacia el parabrisas.
        return Quaternion.LookRotation(Quaternion.Euler(-inclinacion, 0f, 0f) * Vector3.forward, Vector3.up);
    }

    Transform NuevaRaiz(Transform padre, string nombre, Vector3 pos, float inclinacion)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.transform.rotation = padre.rotation * RotacionHaciaConductor(inclinacion);
        return go.transform;
    }

    // ---------------- Volante redondo ----------------

    void ConstruirVolante(Transform raiz)
    {
        // Volante recostado como el original: la parte de arriba hacia el parabrisas.
        var pivote = new GameObject("Interior_Volante").transform;
        pivote.SetParent(raiz, false);
        pivote.position = centroVolante;
        float rad = inclinacionVolante * Mathf.Deg2Rad;
        pivote.rotation = raiz.rotation * Quaternion.LookRotation(new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad)), Vector3.up);
        var matGoma = VisualesVR.MaterialOpaco(ColGoma);
        matGoma.SetFloat("_Glossiness", 0.35f);
        var matMetal = VisualesVR.MaterialOpaco(ColMetal);
        matMetal.SetFloat("_Glossiness", 0.6f);

        VisualesVR.ObjetoMalla("Aro", pivote, Toro(radioVolante, grosorAro, 40, 12), matGoma);

        // Cubreespacio oscuro detras del volante (tapa el volante viejo del modelo).
        var shroud = VisualesVR.ObjetoMalla("Shroud", pivote, VisualesVR.Disco(0.24f), VisualesVR.MaterialOpaco(new Color(0.06f, 0.06f, 0.07f)));
        shroud.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        shroud.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // 3 radios (izquierda, derecha, abajo).
        float[] angulos = new float[] { 0f, 180f, 270f };
        foreach (float a in angulos)
        {
            float radEsp = a * Mathf.Deg2Rad;
            var radio = Prim(PrimitiveType.Cube, "Radio", pivote, matMetal);
            float largo = radioVolante - 0.03f;
            radio.transform.localPosition = new Vector3(Mathf.Cos(radEsp) * (radioVolante + 0.045f) * 0.5f,
                                                        Mathf.Sin(radEsp) * (radioVolante + 0.045f) * 0.5f, 0f);
            radio.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            radio.transform.localScale = new Vector3(largo, 0.034f, 0.024f);
        }

        // Centro (hub).
        var hub = Prim(PrimitiveType.Cylinder, "Hub", pivote, matMetal);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hub.transform.localScale = new Vector3(0.14f, 0.02f, 0.14f);

        var vol = pivote.gameObject.AddComponent<VolanteInteractivo>();
        vol.camion = _camion;
        vol.radio = radioVolante;
    }

    static Mesh Toro(float R, float r, int segU, int segV)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= segU; i++)
        {
            float u = i / (float)segU * Mathf.PI * 2f;
            float cu = Mathf.Cos(u), su = Mathf.Sin(u);
            for (int j = 0; j <= segV; j++)
            {
                float v = j / (float)segV * Mathf.PI * 2f;
                float rv = R + r * Mathf.Cos(v);
                verts.Add(new Vector3(cu * rv, su * rv, r * Mathf.Sin(v)));
                if (i < segU && j < segV)
                {
                    int a = i * (segV + 1) + j;
                    int b = a + segV + 1;
                    tris.AddRange(new int[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
        }
        var m = new Mesh { name = "Toro" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ---------------- Tablero (cara) ----------------

    void ConstruirTablero(Transform raiz)
    {
        // Panel inclinado como el tablero original: la parte de arriba hacia el parabrisas.
        _tablero = new GameObject("Interior_Tablero").transform;
        _tablero.SetParent(raiz, false);
        _tablero.position = centroTablero;
        float radT = inclinacionTablero * Mathf.Deg2Rad;
        _tablero.rotation = raiz.rotation * Quaternion.LookRotation(
            new Vector3(0f, -Mathf.Sin(radT), Mathf.Cos(radT)),
            new Vector3(0f, Mathf.Cos(radT), Mathf.Sin(radT)));

        var fondo = Quad("Fondo", _tablero, tamanoTablero.x, tamanoTablero.y, 0f, 0f, 0f, VisualesVR.MaterialSinLuz(ColFondo));
        fondo.GetComponent<Renderer>().sharedMaterial.SetFloat("_Glossiness", 0f);
        Borde(_tablero, tamanoTablero.x, tamanoTablero.y);

        // Fila de botones + boton de emergencia + salidas de aire (decorativos).
        for (int i = 0; i < 5; i++)
            Quad("Boton", _tablero, 0.034f, 0.020f, 0.235f + i * 0.055f, -0.150f, -0.004f,
                VisualesVR.MaterialSinLuz(new Color(0.16f, 0.18f, 0.22f, 1f)));
        Quad("Emergencia", _tablero, 0.042f, 0.026f, 0.20f, -0.150f, -0.005f, VisualesVR.MaterialSinLuz(ColRojo));
        Quad("Vent1", _tablero, 0.085f, 0.045f, 0.62f, 0.06f, -0.004f, VisualesVR.MaterialSinLuz(new Color(0.10f, 0.12f, 0.15f, 1f)));
        Quad("Vent2", _tablero, 0.085f, 0.045f, 0.62f, -0.01f, -0.004f, VisualesVR.MaterialSinLuz(new Color(0.10f, 0.12f, 0.15f, 1f)));

        // Parches locales para tapar restos del tablero original en zonas que sobresalen.
        Quad("Parche1", _tablero, 0.64f, 0.44f, -0.44f, 0.15f, -0.20f, VisualesVR.MaterialSinLuz(ColFondo));
        Quad("Parche2", _tablero, 0.75f, 0.44f, -0.80f, 0.10f, -0.20f, VisualesVR.MaterialSinLuz(ColFondo));
        // Consola central (cubre la zona mas salida del tablero original).
        Quad("Consola", _tablero, 1.15f, 0.44f, 0.10f, -0.04f, -0.20f, VisualesVR.MaterialSinLuz(ColFondo));
        Quad("ConsolaBoton", _tablero, 0.05f, 0.025f, 0.02f, -0.15f, -0.205f, VisualesVR.MaterialSinLuz(new Color(0.16f, 0.18f, 0.22f, 1f)));
        Quad("ConsolaBoton2", _tablero, 0.05f, 0.025f, 0.09f, -0.15f, -0.205f, VisualesVR.MaterialSinLuz(new Color(0.16f, 0.18f, 0.22f, 1f)));
        Quad("ParcheIzq", _tablero, 0.75f, 0.50f, -1.0f, 0.10f, -0.20f, VisualesVR.MaterialSinLuz(ColFondo));
        // Faldon inferior: tapa la banda de textura vieja justo bajo el volante.
        Quad("Faldon", _tablero, 0.95f, 0.24f, -0.30f, -0.24f, -0.26f, VisualesVR.MaterialSinLuz(ColFondo));
    }

    // ---------------- Cluster (agujas) ----------------

    void ConstruirCluster(Transform raiz)
    {
        _cluster = new GameObject("Interior_Cluster").transform;
        _cluster.SetParent(_tablero, false);
        _cluster.localPosition = new Vector3(offsetCluster.x, offsetCluster.y, -0.16f);

        // Placa oscura del cluster, al ras de la cara del tablero (donde estaban los simbolos).
        Quad("Cara", _cluster, 0.85f, 0.26f, 0f, 0f, -0.002f, VisualesVR.MaterialSinLuz(ColFondo));

        _agujaVel = Dial(new Vector2(-0.145f, 0.01f), ColBlanco, "Vel");
        _agujaRpm = Dial(new Vector2(0.145f, 0.01f), ColBlanco, "Rpm");

        _velTxt = Texto("Velocidad", _cluster, 0.026f, new Vector2(0.10f, 0.04f));
        _velTxt.transform.localPosition = new Vector3(0f, 0.022f, -0.008f);
        _velTxt.fontStyle = FontStyles.Bold;
        _velTxt.text = "0";

        var unidad = Texto("Unidad", _cluster, 0.009f, new Vector2(0.10f, 0.02f));
        unidad.transform.localPosition = new Vector3(0f, -0.008f, -0.008f);
        unidad.text = "km/h";
        unidad.color = ColGris;

        _marchaTxt = Texto("Marcha", _cluster, 0.016f, new Vector2(0.06f, 0.03f));
        _marchaTxt.transform.localPosition = new Vector3(0f, -0.062f, -0.008f);
        _marchaTxt.fontStyle = FontStyles.Bold;
        _marchaTxt.text = "D";
        _marchaTxt.color = ColCyan;

        // Indicador de combustible (E-F), como en el Atego real.
        Quad("CombFondo", _cluster, 0.085f, 0.010f, 0f, -0.098f, -0.006f, VisualesVR.MaterialSinLuz(new Color(0.12f, 0.15f, 0.20f, 0.9f)));
        Quad("CombRelleno", _cluster, 0.060f, 0.010f, -0.012f, -0.098f, -0.007f, VisualesVR.MaterialSinLuz(ColAmbar));
        var eTxt = Texto("E", _cluster, 0.007f, new Vector2(0.02f, 0.012f));
        eTxt.transform.localPosition = new Vector3(-0.054f, -0.098f, -0.008f);
        eTxt.text = "E";
        eTxt.color = ColGris;
        var fTxt = Texto("F", _cluster, 0.007f, new Vector2(0.02f, 0.012f));
        fTxt.transform.localPosition = new Vector3(0.054f, -0.098f, -0.008f);
        fTxt.text = "F";
        fTxt.color = ColGris;

        // Indicadores de luces y sirena.
        _matChipLuces = VisualesVR.MaterialSinLuz(ColChipOff);
        Quad("ChipLuces", _cluster, 0.085f, 0.024f, -0.175f, -0.088f, -0.004f, _matChipLuces);
        _lucesTxt = Texto("TextoLuces", _cluster, 0.008f, new Vector2(0.08f, 0.02f));
        _lucesTxt.transform.localPosition = new Vector3(-0.175f, -0.088f, -0.008f);
        _lucesTxt.text = "Luces";
        _lucesTxt.color = ColGris;

        _matChipSirena = VisualesVR.MaterialSinLuz(ColChipOff);
        Quad("ChipSirena", _cluster, 0.085f, 0.024f, 0.175f, -0.088f, -0.004f, _matChipSirena);
        _sirenaTxt = Texto("TextoSirena", _cluster, 0.008f, new Vector2(0.08f, 0.02f));
        _sirenaTxt.transform.localPosition = new Vector3(0.175f, -0.088f, -0.008f);
        _sirenaTxt.text = "Sirena";
        _sirenaTxt.color = ColGris;
    }

    Transform Dial(Vector2 centro, Color colorAguja, string nombre)
    {
        var matCara = VisualesVR.MaterialSinLuz(new Color(0.08f, 0.10f, 0.14f, 1f));
        var disco = VisualesVR.ObjetoMalla(nombre + "Cara", _cluster, VisualesVR.Disco(0.088f), matCara);
        disco.transform.localPosition = new Vector3(centro.x, centro.y, -0.005f);
        disco.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var anillo = VisualesVR.ObjetoMalla(nombre + "Anillo", _cluster, VisualesVR.Anillo(0.088f, 0.098f), VisualesVR.MaterialSinLuz(ColBorde));
        anillo.transform.localPosition = new Vector3(centro.x, centro.y, -0.006f);
        anillo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var marcas = VisualesVR.ObjetoMalla(nombre + "Centro", _cluster, VisualesVR.Disco(0.014f), VisualesVR.MaterialSinLuz(ColBorde));
        marcas.transform.localPosition = new Vector3(centro.x, centro.y, -0.007f);
        marcas.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var aguja = new GameObject(nombre + "Aguja").transform;
        aguja.SetParent(_cluster, false);
        aguja.localPosition = new Vector3(centro.x, centro.y, -0.009f);
        var palito = Quad("Palito", aguja, 0.011f, 0.075f, 0f, 0.033f, 0f, VisualesVR.MaterialSinLuz(colorAguja));
        palito.transform.localPosition = new Vector3(0f, 0.033f, 0f);
        return aguja;
    }

    // ---------------- Pantalla GPS ----------------

    void ConstruirPantalla(Transform raiz)
    {
        _pantalla = new GameObject("Interior_Pantalla").transform;
        _pantalla.SetParent(_tablero, false);
        _pantalla.localPosition = new Vector3(offsetPantalla.x, offsetPantalla.y, -0.09f);

        var marco = Prim(PrimitiveType.Cube, "Marco", _pantalla, VisualesVR.MaterialOpaco(new Color(0.05f, 0.06f, 0.08f)));
        marco.transform.localPosition = new Vector3(0f, 0f, 0.010f);
        marco.transform.localScale = new Vector3(tamanoPantalla.x + 0.05f, tamanoPantalla.y + 0.05f, 0.022f);

        Quad("Pantalla", _pantalla, tamanoPantalla.x, tamanoPantalla.y, 0f, 0f, -0.004f, VisualesVR.MaterialSinLuz(ColFondo));

        var titulo = Texto("Titulo", _pantalla, 0.0075f, new Vector2(0.26f, 0.018f));
        titulo.transform.localPosition = new Vector3(0f, 0.072f, -0.008f);
        titulo.text = "RUTA ACTIVA";
        titulo.color = ColCyan;

        var mapa = new GameObject("Mapa").transform;
        mapa.SetParent(_pantalla, false);
        mapa.localPosition = new Vector3(0f, 0.008f, -0.008f);

        for (int i = -1; i <= 1; i++)
            Quad("GridV", mapa, 0.0012f, 0.096f, i * 0.045f, 0f, 0f, VisualesVR.MaterialSinLuz(ColGrid));
        for (int j = -1; j <= 1; j++)
            Quad("GridH", mapa, 0.130f, 0.0012f, 0f, j * 0.033f, 0f, VisualesVR.MaterialSinLuz(ColGrid));

        Vector2[] ruta = new Vector2[]
        {
            new Vector2(-0.055f, -0.030f),
            new Vector2(-0.020f, -0.014f),
            new Vector2(0.016f, -0.002f),
            new Vector2(0.040f, 0.020f),
            new Vector2(0.052f, 0.040f)
        };
        for (int i = 0; i < ruta.Length - 1; i++)
        {
            Vector2 a = ruta[i];
            Vector2 d = ruta[i + 1] - a;
            float len = d.magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.011f));
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int k = 0; k <= n; k++)
            {
                Vector2 p = a + d * (k / (float)n);
                var go = Quad("Guion", mapa, 0.010f, 0.0035f, p.x, p.y, -0.001f, VisualesVR.MaterialSinLuz(ColRuta));
                go.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        Punto(mapa, ruta[0], ColVerde, 0.006f);
        Punto(mapa, ruta[ruta.Length - 1], ColRojo, 0.006f);

        var est = Texto("Estacion", mapa, 0.0065f, new Vector2(0.06f, 0.014f));
        est.transform.localPosition = new Vector3(-0.028f, -0.046f, -0.001f);
        est.text = "Estacion";
        est.color = ColBlanco;

        var alm = Texto("Almacen", mapa, 0.0065f, new Vector2(0.07f, 0.014f));
        alm.transform.localPosition = new Vector3(0.028f, 0.055f, -0.001f);
        alm.text = "Almacen Central";
        alm.color = ColBlanco;

        Punto(_pantalla, new Vector2(-0.115f, -0.068f), ColVerde, 0.0035f);
        var conex = Texto("Conexion", _pantalla, 0.005f, new Vector2(0.09f, 0.013f));
        conex.transform.localPosition = new Vector3(-0.055f, -0.068f, -0.008f);
        conex.text = "Conexion estable";
        conex.color = ColGris;

        Punto(_pantalla, new Vector2(0.055f, -0.068f), ColAmbar, 0.0035f);
        _etaTxt = Texto("Eta", _pantalla, 0.005f, new Vector2(0.07f, 0.013f));
        _etaTxt.transform.localPosition = new Vector3(0.105f, -0.068f, -0.008f);
        _etaTxt.text = "ETA: --";
        _etaTxt.color = ColAmbar;
    }

    // ---------------- Palanca P-R-N-D ----------------

    void ConstruirPalanca(Transform raiz)
    {
        _palancaPivote = NuevaRaiz(raiz, "Interior_Palanca", centroPalanca, inclinacionPalanca);

        // Base fija.
        var basePal = Prim(PrimitiveType.Cube, "Base", _palancaPivote, VisualesVR.MaterialOpaco(new Color(0.08f, 0.09f, 0.11f)));
        basePal.transform.localPosition = new Vector3(0f, -0.01f, 0f);
        basePal.transform.localScale = new Vector3(0.10f, 0.05f, 0.16f);

        // Palanca movil (pivote propio para que PalancaCambios la gire).
        var pivote = new GameObject("Pivote").transform;
        pivote.SetParent(_palancaPivote, false);
        pivote.localRotation = Quaternion.identity;

        // Mas larga que la palanca original (la supera un poco).
        var palo = Prim(PrimitiveType.Cylinder, "Palo", pivote, VisualesVR.MaterialOpaco(new Color(0.10f, 0.10f, 0.12f)));
        palo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        palo.transform.localScale = new Vector3(0.024f, 0.25f, 0.024f);

        var pomo = Prim(PrimitiveType.Sphere, "Pomo", pivote, VisualesVR.MaterialOpaco(new Color(0.05f, 0.05f, 0.06f)));
        pomo.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        pomo.transform.localScale = new Vector3(0.058f, 0.052f, 0.058f);

        // Letras grabadas en el pomo (se ilumina la del modo actual).
        string[] letras = new string[] { "P", "R", "N", "D" };
        float[] xs = new float[] { -0.028f, -0.0095f, 0.0095f, 0.028f };
        for (int i = 0; i < 4; i++)
        {
            var t = Texto("Letra" + letras[i], pivote, 0.010f, new Vector2(0.02f, 0.016f));
            t.transform.localPosition = new Vector3(xs[i], 0.52f, -0.027f);
            t.text = letras[i];
            t.color = i == 3 ? new Color(0.45f, 0.9f, 1f) : new Color(0.13f, 0.14f, 0.16f);
            _letras[i] = t;
        }

        var pal = pivote.gameObject.AddComponent<PalancaCambios>();
        pal.camion = _camion;
        pal.radioPomo = 0.52f;
    }

    // ---------------- Luz de cabina ----------------

    void ConstruirLuz(Transform raiz)
    {
        var go = new GameObject("Interior_Luz");
        go.transform.SetParent(raiz, false);
        go.transform.position = raiz.TransformPoint(new Vector3(-0.25f, 2.28f, 3.1f));
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 3.6f;
        l.intensity = 0.40f;
        l.color = new Color(1f, 0.96f, 0.88f);
        l.shadows = LightShadows.None;
    }

    // ------------------------------------------------------------------ actualizacion

    void Actualizar()
    {
        float kmh = _camion.VelocidadKmh;
        int v = Mathf.RoundToInt(kmh);
        if (v != _ultimaVel && _velTxt != null)
        {
            _ultimaVel = v;
            _velTxt.text = v.ToString();
        }

        // Agujas (barrido de -120 a +120 grados).
        if (_agujaVel != null)
            _agujaVel.localRotation = Quaternion.AngleAxis(Mathf.Lerp(-120f, 120f, Mathf.Clamp01(kmh / 120f)), Vector3.forward);
        if (_agujaRpm != null)
        {
            float rpm = 0.8f + Mathf.Clamp01(kmh / 30f) * 2.2f;
            _agujaRpm.localRotation = Quaternion.AngleAxis(Mathf.Lerp(-120f, 120f, rpm / 4f), Vector3.forward);
        }

        // Marcha: la manda la palanca si esta instalada.
        if (_marchaTxt != null)
        {
            string m;
            var pal = _palancaPivote != null ? _palancaPivote.GetComponentInChildren<PalancaCambios>() : null;
            if (pal != null) m = pal.NombreMarcha;
            else m = _camion.Velocidad < -0.15f ? "R" : "D";
            if (_marchaTxt.text != m) _marchaTxt.text = m;
        }

        bool luces = LeerLuces();
        bool sirena = _camion.SirenaEncendida;
        if (_matChipLuces != null) _matChipLuces.color = luces ? ColAmbar : ColChipOff;
        if (_lucesTxt != null) _lucesTxt.color = luces ? new Color(0.10f, 0.10f, 0.12f) : ColGris;
        if (_matChipSirena != null) _matChipSirena.color = sirena ? ColAmbar : ColChipOff;
        if (_sirenaTxt != null) _sirenaTxt.color = sirena ? new Color(0.10f, 0.10f, 0.12f) : ColGris;

        // Letras del pomo: se ilumina la del modo actual.
        {
            var palL = _palancaPivote != null ? _palancaPivote.GetComponentInChildren<PalancaCambios>() : null;
            int mAct = palL != null ? palL.marcha : (_camion.Velocidad < -0.15f ? 1 : 3);
            for (int i = 0; i < _letras.Length; i++)
            {
                if (_letras[i] == null) continue;
                _letras[i].color = i == mAct ? new Color(0.45f, 0.9f, 1f) : new Color(0.13f, 0.14f, 0.16f);
            }
        }

        if (_etaTxt != null)
        {
            Vector3 p = _camion.transform.position; p.y = 0f;
            Vector3 d = puntoDestino; d.y = 0f;
            float dist = Vector3.Distance(p, d);
            float vel = Mathf.Max(kmh / 3.6f, velocidadMinimaEta);
            int min = Mathf.Max(1, Mathf.CeilToInt(dist / vel / 60f));
            string txt = min <= 1 ? "ETA: <1 min" : "ETA: " + min + " min";
            if (txt != _ultimaEta)
            {
                _ultimaEta = txt;
                _etaTxt.text = txt;
            }
        }
    }

    bool LeerLuces()
    {
        if (_campoLuces == null || _camion == null) return false;
        object v = _campoLuces.GetValue(_camion);
        return v is bool && (bool)v;
    }

    // ------------------------------------------------------------------ utilidades

    static GameObject Prim(PrimitiveType tipo, string nombre, Transform padre, Material mat)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(padre, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    static GameObject Quad(string nombre, Transform padre, float ancho, float alto, float x, float y, float z, Material mat)
    {
        var go = Prim(PrimitiveType.Quad, nombre, padre, mat);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localScale = new Vector3(ancho, alto, 1f);
        return go;
    }

    static TextMeshPro Texto(string nombre, Transform padre, float alturaLetra, Vector2 caja)
    {
        return VisualesVR.Texto(nombre, padre, alturaLetra, caja);
    }

    static void Punto(Transform padre, Vector2 pos, Color color, float radio)
    {
        var go = VisualesVR.ObjetoMalla("Punto", padre, VisualesVR.Disco(radio), VisualesVR.MaterialSinLuz(color));
        go.transform.localPosition = new Vector3(pos.x, pos.y, -0.001f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    static void Borde(Transform padre, float ancho, float alto)
    {
        float t = 0.0022f;
        var mat = VisualesVR.MaterialSinLuz(ColBorde);
        Quad("BordeSup", padre, ancho, t, 0f, alto * 0.5f - t * 0.5f, -0.002f, mat);
        Quad("BordeInf", padre, ancho, t, 0f, -alto * 0.5f + t * 0.5f, -0.002f, mat);
        Quad("BordeIzq", padre, t, alto, -ancho * 0.5f + t * 0.5f, 0f, -0.002f, mat);
        Quad("BordeDer", padre, t, alto, ancho * 0.5f - t * 0.5f, 0f, -0.002f, mat);
    }
}
