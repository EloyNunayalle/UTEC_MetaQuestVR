using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel del camion (UI diegetica, segun el mockup "camion.png" del informe):
///  - Tablero del conductor: medidor de velocidad (numero grande + barra), marcha y
///    ruta, y chips de estado de luces y sirena.
///  - Pantalla GPS de la tablet: mapa con la ruta, conexion y ETA.
/// Se crea solo al dar Play (no hay que tocar la escena). Si no hay camion, se apaga solo.
/// Ajustes finos en el Inspector (en Play): ajustePanel / ajusteGps / inclinacionPanel.
/// </summary>
public class PanelCamion : MonoBehaviour
{
    [Header("Panel del conductor (tablero)")]
    public bool conductorIzquierda = true;
    [Tooltip("Ajustes finos en ejes del camion: X derecha, Y arriba, Z frente.")]
    public Vector3 ajustePanel = Vector3.zero;
    public float inclinacionPanel = 25f;
    public string nombreRuta = "Almacen Central";

    [Header("Pantalla GPS (tablet del tablero)")]
    public Vector3 ajusteGps = Vector3.zero;
    [Tooltip("Separacion del contenido respecto a la cara de la pantalla (m).")]
    public float separacionGps = 0.012f;

    [Header("ETA")]
    public Vector3 puntoDestino = new Vector3(-5.19f, 0f, -1f);
    public float velocidadMinimaEta = 3f;

    // Paleta del mockup.
    static readonly Color ColFondo = new Color(0.055f, 0.08f, 0.13f, 0.94f);
    static readonly Color ColBorde = new Color(0.28f, 0.42f, 0.58f, 0.85f);
    static readonly Color ColCyan = new Color(0.40f, 0.75f, 0.95f, 1f);
    static readonly Color ColBlanco = new Color(0.93f, 0.95f, 0.98f, 1f);
    static readonly Color ColGris = new Color(0.60f, 0.66f, 0.72f, 1f);
    static readonly Color ColAmbar = new Color(0.86f, 0.68f, 0.16f, 1f);
    static readonly Color ColTextoActivo = new Color(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color ColVerde = new Color(0.30f, 0.80f, 0.35f, 1f);
    static readonly Color ColRojo = new Color(0.85f, 0.32f, 0.26f, 1f);
    static readonly Color ColChipOff = new Color(0.13f, 0.16f, 0.20f, 0.92f);
    static readonly Color ColRuta = new Color(0.55f, 0.75f, 0.95f, 1f);
    static readonly Color ColGrid = new Color(0.25f, 0.35f, 0.50f, 0.55f);

    CamionBomberos _camion;
    Transform _panel;
    Transform _gps;
    Transform _barra;
    TextMeshPro _vel, _marcha, _luces, _sirena, _eta;
    Material _matBarra, _matChipLuces, _matChipSirena;
    FieldInfo _campoLuces;
    float _tiempoSinCamion;
    float _reloj;
    int _pasadasDebug;
    int _ultimaVel = -1;
    string _ultimaEta = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arranque()
    {
        var go = new GameObject("PanelCamion");
        go.AddComponent<PanelCamion>();
    }

    void Update()
    {
        if (_camion == null)
        {
            _tiempoSinCamion += Time.deltaTime;
            if (_tiempoSinCamion > 0.5f)
            {
                _tiempoSinCamion = 0f;
                BuscarCamion();
            }
            if (_camion == null && Time.timeSinceLevelLoad > 30f) enabled = false;
            return;
        }

        Actualizar();

        // Limpieza: la etiqueta debug "PlayerController" del SDK (3 pasadas).
        _reloj += Time.deltaTime;
        if (_pasadasDebug < 3 && _reloj > _pasadasDebug * 2.5f)
        {
            _pasadasDebug++;
            OcultarEtiquetasDebug();
        }
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
        var camion = go.GetComponent<CamionBomberos>();
        if (camion == null) return;
        Construir(camion);
    }

    // ------------------------------------------------------------------ construccion

    void Construir(CamionBomberos camion)
    {
        if (_panel != null) return;
        _camion = camion;
        var raiz = camion.transform;

        Bounds b;
        if (!VisualesVR.Limites(raiz, out b)) return;

        // ----- Panel del conductor: sobre el tablero, frente al asiento.
        float lado = conductorIzquierda ? -1f : 1f;
        Vector3 localPanel = new Vector3(lado * b.extents.x * 0.45f, b.extents.y * 1.30f, b.extents.z * 0.80f) + ajustePanel;
        _panel = new GameObject("PanelConductor").transform;
        _panel.SetParent(raiz, false);
        _panel.position = raiz.TransformPoint(localPanel);
        _panel.rotation = raiz.rotation * Quaternion.Euler(inclinacionPanel, 180f, 0f);
        ConstruirPanelConductor();

        // ----- GPS: delante de la pantalla de la tablet (si existe).
        var tablet = BuscarHijo(raiz, "Tablet_Rugged");
        if (tablet != null)
        {
            Bounds tb;
            if (VisualesVR.Limites(tablet, out tb))
            {
                _gps = new GameObject("PantallaGPS").transform;
                _gps.SetParent(raiz, false);
                _gps.position = new Vector3(tb.center.x, tb.center.y, tb.min.z - separacionGps) + ajusteGps;
                _gps.rotation = raiz.rotation * Quaternion.Euler(0f, 180f, 0f);
                ConstruirGps();
            }
        }

        _campoLuces = typeof(CamionBomberos).GetField("_lucesOn", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    void ConstruirPanelConductor()
    {
        Quad("Fondo", _panel, 0.42f, 0.30f, 0f, 0f, 0f, VisualesVR.MaterialSinLuz(ColFondo));
        Borde(_panel, 0.42f, 0.30f);

        var titulo = Texto("Titulo", _panel, 0.0115f, new Vector2(0.40f, 0.03f));
        titulo.transform.localPosition = new Vector3(0f, 0.126f, 0.004f);
        titulo.text = "TRAYECTO AL ESCENARIO";
        titulo.color = ColCyan;

        _vel = Texto("Velocidad", _panel, 0.060f, new Vector2(0.24f, 0.10f));
        _vel.transform.localPosition = new Vector3(-0.035f, 0.045f, 0.005f);
        _vel.fontStyle = FontStyles.Bold;
        _vel.text = "0";

        var unidad = Texto("Unidad", _panel, 0.016f, new Vector2(0.07f, 0.03f));
        unidad.transform.localPosition = new Vector3(0.105f, 0.018f, 0.005f);
        unidad.text = "km/h";
        unidad.color = ColGris;

        Quad("BarraFondo", _panel, 0.32f, 0.012f, -0.02f, -0.005f, 0.004f,
            VisualesVR.MaterialSinLuz(new Color(0.12f, 0.15f, 0.20f, 0.9f)));
        _matBarra = VisualesVR.MaterialSinLuz(ColCyan);
        var barra = Quad("BarraRelleno", _panel, 0.32f, 0.012f, -0.18f, -0.005f, 0.005f, _matBarra);
        _barra = barra.transform;
        _barra.localScale = new Vector3(0.0001f, 0.012f, 1f);

        _marcha = Texto("Marcha", _panel, 0.0145f, new Vector2(0.38f, 0.03f));
        _marcha.transform.localPosition = new Vector3(0f, -0.035f, 0.005f);
        _marcha.text = "Marcha D - Ruta: " + nombreRuta;

        // Chips de estado (luces / sirena).
        _matChipLuces = VisualesVR.MaterialSinLuz(ColChipOff);
        Quad("ChipLuces", _panel, 0.155f, 0.036f, -0.083f, -0.095f, 0.003f, _matChipLuces);
        _luces = Texto("TextoLuces", _panel, 0.0115f, new Vector2(0.15f, 0.03f));
        _luces.transform.localPosition = new Vector3(-0.083f, -0.095f, 0.006f);
        _luces.text = "Luces activas";
        _luces.color = ColGris;

        _matChipSirena = VisualesVR.MaterialSinLuz(ColChipOff);
        Quad("ChipSirena", _panel, 0.155f, 0.036f, 0.083f, -0.095f, 0.003f, _matChipSirena);
        _sirena = Texto("TextoSirena", _panel, 0.0115f, new Vector2(0.15f, 0.03f));
        _sirena.transform.localPosition = new Vector3(0.083f, -0.095f, 0.006f);
        _sirena.text = "Sirena activa";
        _sirena.color = ColGris;
    }

    void ConstruirGps()
    {
        Quad("Fondo", _gps, 0.132f, 0.22f, 0f, 0f, 0f, VisualesVR.MaterialSinLuz(ColFondo));
        Borde(_gps, 0.132f, 0.22f);

        var titulo = Texto("Titulo", _gps, 0.0085f, new Vector2(0.12f, 0.02f));
        titulo.transform.localPosition = new Vector3(0f, 0.095f, 0.004f);
        titulo.text = "RUTA ACTIVA";
        titulo.color = ColCyan;

        var mapa = new GameObject("Mapa").transform;
        mapa.SetParent(_gps, false);
        mapa.localPosition = new Vector3(0f, 0.008f, 0f);

        // Cuadricula.
        for (int i = -1; i <= 1; i++)
            Quad("GridV", mapa, 0.0012f, 0.096f, i * 0.033f, 0.005f, 0.003f, VisualesVR.MaterialSinLuz(ColGrid));
        for (int j = -1; j <= 1; j++)
            Quad("GridH", mapa, 0.096f, 0.0012f, 0f, 0.005f + j * 0.035f, 0.003f, VisualesVR.MaterialSinLuz(ColGrid));

        // Ruta (polilinea con guiones), de la estacion (abajo-izq) al almacen (arriba-der).
        Vector2[] ruta = new Vector2[]
        {
            new Vector2(-0.038f, -0.035f),
            new Vector2(-0.012f, -0.018f),
            new Vector2(0.012f, -0.006f),
            new Vector2(0.030f, 0.022f),
            new Vector2(0.038f, 0.048f)
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
                var go = Quad("Guion", mapa, 0.010f, 0.0035f, p.x, p.y, 0.004f, VisualesVR.MaterialSinLuz(ColRuta));
                go.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        Punto(mapa, ruta[0], ColVerde, 0.006f);
        Punto(mapa, ruta[ruta.Length - 1], ColRojo, 0.006f);

        var est = Texto("Estacion", mapa, 0.007f, new Vector2(0.06f, 0.016f));
        est.transform.localPosition = new Vector3(-0.020f, -0.054f, 0.004f);
        est.text = "Estacion";
        est.color = ColBlanco;

        var alm = Texto("Almacen", mapa, 0.007f, new Vector2(0.06f, 0.016f));
        alm.transform.localPosition = new Vector3(0.022f, 0.062f, 0.004f);
        alm.text = "Almacen Central";
        alm.color = ColBlanco;

        // Estado: conexion y ETA.
        Punto(_gps, new Vector2(-0.048f, -0.072f), ColVerde, 0.0035f);
        var conex = Texto("Conexion", _gps, 0.0055f, new Vector2(0.06f, 0.014f));
        conex.transform.localPosition = new Vector3(-0.015f, -0.072f, 0.004f);
        conex.text = "Conexion estable";
        conex.color = ColGris;

        Punto(_gps, new Vector2(0.022f, -0.072f), ColAmbar, 0.0035f);
        _eta = Texto("Eta", _gps, 0.0055f, new Vector2(0.045f, 0.014f));
        _eta.transform.localPosition = new Vector3(0.042f, -0.072f, 0.004f);
        _eta.text = "ETA: --";
        _eta.color = ColAmbar;
    }

    // ------------------------------------------------------------------ actualizacion

    void Actualizar()
    {
        float kmh = _camion.VelocidadKmh;

        int v = Mathf.RoundToInt(kmh);
        if (v != _ultimaVel && _vel != null)
        {
            _ultimaVel = v;
            _vel.text = v.ToString();
        }

        float vms = _camion.Velocidad;
        string linea = "Marcha " + (vms < -0.15f ? "R" : "D") + " - Ruta: " + nombreRuta;
        if (_marcha != null && _marcha.text != linea) _marcha.text = linea;

        float pct = Mathf.Clamp01(kmh / 30f);
        if (_barra != null)
        {
            var s = _barra.localScale;
            s.x = Mathf.Max(0.0001f, 0.32f * pct);
            _barra.localScale = s;
            var p = _barra.localPosition;
            p.x = -0.18f + 0.16f * pct;
            _barra.localPosition = p;
        }

        bool luces = LeerLuces();
        bool sirena = _camion.SirenaEncendida;
        if (_matChipLuces != null) _matChipLuces.color = luces ? ColAmbar : ColChipOff;
        if (_luces != null) _luces.color = luces ? ColTextoActivo : ColGris;
        if (_matChipSirena != null) _matChipSirena.color = sirena ? ColAmbar : ColChipOff;
        if (_sirena != null) _sirena.color = sirena ? ColTextoActivo : ColGris;

        if (_eta != null)
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
                _eta.text = txt;
            }
        }
    }

    bool LeerLuces()
    {
        if (_campoLuces == null || _camion == null) return false;
        object v = _campoLuces.GetValue(_camion);
        return v is bool && (bool)v;
    }

    void OcultarEtiquetasDebug()
    {
        var textos = Object.FindObjectsOfType<TMP_Text>(true);
        foreach (var t in textos)
        {
            if (t == null || t.text == null) continue;
            if (t.text.Trim() == "PlayerController" && t.gameObject.activeSelf)
                t.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------ utilidades

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    static GameObject Quad(string nombre, Transform padre, float ancho, float alto, float x, float y, float z, Material mat)
    {
        var go = VisualesVR.Primitiva(PrimitiveType.Quad, nombre, padre, mat);
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
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0.004f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    static void Borde(Transform padre, float ancho, float alto)
    {
        float t = 0.0022f;
        var mat = VisualesVR.MaterialSinLuz(ColBorde);
        Quad("BordeSup", padre, ancho, t, 0f, alto * 0.5f - t * 0.5f, 0.001f, mat);
        Quad("BordeInf", padre, ancho, t, 0f, -alto * 0.5f + t * 0.5f, 0.001f, mat);
        Quad("BordeIzq", padre, t, alto, -ancho * 0.5f + t * 0.5f, 0f, 0.001f, mat);
        Quad("BordeDer", padre, t, alto, ancho * 0.5f - t * 0.5f, 0f, 0.001f, mat);
    }
}
