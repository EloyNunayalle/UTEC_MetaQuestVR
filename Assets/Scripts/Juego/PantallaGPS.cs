using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Pantalla de GPS montada en un vehiculo (camion o moto), con el mapa real de la ciudad (MapaGPS):
/// - Mapa orientado segun la marcha (el vehiculo siempre apunta hacia arriba) y zoom segun la velocidad.
/// - En una mision: traza la ruta por las calles hasta el destino, con la siguiente indicacion
///   ("En 40 m, gira a la derecha"), distancia restante y tiempo estimado. La ruta se recalcula cada segundo.
/// - En Explorar (y sin mision): no hay ruta; solo la ubicacion, el rumbo y la velocidad.
/// Todo se construye por codigo. Solo se actualiza cuando el jugador esta cerca.
/// </summary>
public class PantallaGPS : MonoBehaviour
{
    public Transform vehiculo;
    public Func<float> velocidadKmh;
    public Vector2 tamano = new Vector2(0.33f, 0.19f);
    [Tooltip("Metros de mapa a lo ancho de la pantalla, detenido y a 50 km/h.")]
    public float zoomMin = 70f, zoomMax = 130f;
    public float distanciaLlegada = 15f;

    static readonly Color ColAzul = new Color(0.16f, 0.55f, 1f);
    static readonly Color ColBarra = new Color(0.04f, 0.06f, 0.09f, 0.88f);
    static readonly Color ColTexto = new Color(0.94f, 0.96f, 1f);
    static readonly Color ColGris = new Color(0.65f, 0.71f, 0.78f);
    static readonly Color ColVerde = new Color(0.35f, 0.9f, 0.45f);

    Mesh _malla;
    Material _matMapa;
    readonly Vector2[] _uv = new Vector2[4];
    readonly List<Vector3> _ruta = new List<Vector3>();
    readonly List<Vector3> _tramo = new List<Vector3>();
    readonly LineRenderer[] _lineas = new LineRenderer[3];
    Transform _pin, _icono, _norte;
    Material _matIcono;
    TextMeshPro _instruccion, _velocidad, _eta, _destinoTxt, _aviso;
    Rect _zona;      // parte visible del mapa (entre las barras), en metros de pantalla
    Vector2 _ancla;  // donde se dibuja el vehiculo
    Vector2 _centroBrujula;
    Vector3 _pos, _adel, _der, _destino;
    string _nombreDestino;
    float _k, _yaw, _proximaRuta, _proximoTexto, _s;
    bool _hayRuta, _conDestino, _inicioYaw;
    Transform _cabeza;

    public static PantallaGPS Crear(Transform padre, Vector3 posicion, Quaternion rotacion, Vector2 tamano, Transform vehiculo, Func<float> velocidad)
    {
        var go = new GameObject("GPS_Mapa");
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(posicion, rotacion);
        Vector3 l = padre.lossyScale;
        float escala = (Mathf.Abs(l.x) + Mathf.Abs(l.y) + Mathf.Abs(l.z)) / 3f;
        go.transform.localScale = Vector3.one / Mathf.Max(0.0001f, escala); // medidas en metros reales
        var p = go.AddComponent<PantallaGPS>();
        p.tamano = tamano;
        p.vehiculo = vehiculo;
        p.velocidadKmh = velocidad;
        p.Construir();
        return p;
    }

    // ------------------------------------------------------------------ construccion

    void Construir()
    {
        float W = tamano.x, H = tamano.y;
        _s = W / 0.33f; // las medidas de abajo estan pensadas para la pantalla del camion
        float altoSup = H * 0.21f, altoInf = H * 0.15f;
        _zona = new Rect(-W * 0.5f, -H * 0.5f + altoInf, W, H - altoSup - altoInf);
        _ancla = new Vector2(0f, _zona.yMin + _zona.height * 0.27f);
        _centroBrujula = new Vector2(W * 0.5f - 0.02f * _s, _zona.yMax - 0.02f * _s);

        // Mapa: un quad cuyas UV se giran y desplazan cada cuadro.
        _malla = new Mesh { name = "GPS_Mapa" };
        _malla.MarkDynamic();
        _malla.vertices = new[]
        {
            new Vector3(-W * 0.5f, -H * 0.5f, 0f), new Vector3(W * 0.5f, -H * 0.5f, 0f),
            new Vector3(-W * 0.5f, H * 0.5f, 0f), new Vector3(W * 0.5f, H * 0.5f, 0f)
        };
        _malla.uv = _uv;
        _malla.triangles = new[] { 0, 3, 1, 3, 0, 2 };
        _malla.RecalculateBounds();
        _matMapa = Mat(new Color(0.08f, 0.1f, 0.13f), 3000);
        VisualesVR.ObjetoMalla("Mapa", transform, _malla, _matMapa);

        // Ruta.
        var matRuta = Mat(ColAzul, 3002);
        for (int i = 0; i < _lineas.Length; i++)
        {
            var go = new GameObject("Ruta_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.sharedMaterial = matRuta;
            lr.widthMultiplier = 0.0055f * _s;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            _lineas[i] = lr;
        }

        // Destino.
        _pin = new GameObject("Destino").transform;
        _pin.SetParent(transform, false);
        Disco(_pin, 0.0068f * _s, new Color(0.9f, 0.18f, 0.15f), 3003, 0f);
        Disco(_pin, 0.0026f * _s, Color.white, 3004, -0.0002f);
        _pin.gameObject.SetActive(false);

        // Vehiculo: flecha azul con borde blanco, siempre hacia arriba.
        var flecha = new GameObject("Vehiculo").transform;
        flecha.SetParent(transform, false);
        flecha.localPosition = new Vector3(_ancla.x, _ancla.y, -0.0025f);
        Malla(flecha, "Borde", Flecha(0.0105f * _s), Color.white, 3004, 0f);
        Malla(flecha, "Relleno", Flecha(0.0072f * _s), ColAzul, 3005, -0.0002f);

        // Barras de informacion.
        var matBarra = Mat(ColBarra, 3006);
        Rectangulo("Barra_Superior", W, altoSup, 0f, H * 0.5f - altoSup * 0.5f, matBarra);
        Rectangulo("Barra_Inferior", W, altoInf, 0f, -H * 0.5f + altoInf * 0.5f, matBarra);

        _icono = new GameObject("Icono").transform;
        _icono.SetParent(transform, false);
        _icono.localPosition = new Vector3(-W * 0.5f + altoSup * 0.55f, H * 0.5f - altoSup * 0.5f, -0.004f);
        _matIcono = Mat(ColTexto, 3008);
        VisualesVR.ObjetoMalla("Flecha", _icono, Flecha(altoSup * 0.32f), _matIcono);

        _instruccion = Texto("Instruccion", 0.0135f, new Vector2(W - altoSup * 1.25f, altoSup), TextAlignmentOptions.Left);
        _instruccion.transform.localPosition = new Vector3(altoSup * 0.55f, H * 0.5f - altoSup * 0.5f, -0.004f);

        float yInf = -H * 0.5f + altoInf * 0.5f;
        _velocidad = Texto("Velocidad", 0.011f, new Vector2(W * 0.26f, altoInf), TextAlignmentOptions.Left);
        _velocidad.transform.localPosition = new Vector3(-W * 0.5f + W * 0.15f, yInf, -0.004f);
        _destinoTxt = Texto("Destino", 0.0085f, new Vector2(W * 0.42f, altoInf), TextAlignmentOptions.Center);
        _destinoTxt.transform.localPosition = new Vector3(-W * 0.03f, yInf, -0.004f);
        _destinoTxt.color = ColGris;
        _eta = Texto("Eta", 0.0095f, new Vector2(W * 0.3f, altoInf), TextAlignmentOptions.Right);
        _eta.transform.localPosition = new Vector3(W * 0.5f - W * 0.165f, yInf, -0.004f);

        // Brujula: la N gira alrededor de un circulo, porque el mapa sigue el rumbo.
        var brujula = new GameObject("Brujula").transform;
        brujula.SetParent(transform, false);
        brujula.localPosition = new Vector3(_centroBrujula.x, _centroBrujula.y, -0.003f);
        Disco(brujula, 0.0125f * _s, ColBarra, 3006, 0f);
        _norte = Texto("N", 0.0085f, new Vector2(0.02f * _s, 0.02f * _s), TextAlignmentOptions.Center).transform;
        _norte.GetComponent<TextMeshPro>().text = "<b>N</b>";
        _norte.GetComponent<TextMeshPro>().color = new Color(1f, 0.45f, 0.4f);

        _aviso = Texto("Aviso", 0.012f, new Vector2(W * 0.9f, H * 0.3f), TextAlignmentOptions.Center);
        _aviso.transform.localPosition = new Vector3(0f, _zona.center.y, -0.004f);
        _aviso.text = "Cargando mapa...";
    }

    static Material Mat(Color c, int cola)
    {
        var m = VisualesVR.MaterialSinLuz(c);
        m.renderQueue = cola; // capas fijas: mapa < ruta < marcas < barras < textos
        return m;
    }

    void Rectangulo(string nombre, float ancho, float alto, float x, float y, Material mat)
    {
        var q = VisualesVR.Primitiva(PrimitiveType.Quad, nombre, transform, mat);
        q.transform.localPosition = new Vector3(x, y, -0.003f);
        q.transform.localScale = new Vector3(ancho, alto, 1f);
    }

    static void Disco(Transform padre, float radio, Color color, int cola, float z)
    {
        var go = VisualesVR.ObjetoMalla("Disco", padre, VisualesVR.Disco(radio, 24), Mat(color, cola));
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    static void Malla(Transform padre, string nombre, Mesh malla, Color color, int cola, float z)
    {
        var go = VisualesVR.ObjetoMalla(nombre, padre, malla, Mat(color, cola));
        go.transform.localPosition = new Vector3(0f, 0f, z);
    }

    /// <summary>Flecha de navegacion apuntando a +Y.</summary>
    static Mesh Flecha(float t)
    {
        var m = new Mesh { name = "GPS_Flecha" };
        m.vertices = new[] { new Vector3(0f, t, 0f), new Vector3(-0.75f * t, -0.8f * t, 0f), new Vector3(0f, -0.38f * t, 0f), new Vector3(0.75f * t, -0.8f * t, 0f) };
        m.triangles = new[] { 0, 3, 2, 0, 2, 1 };
        m.RecalculateBounds();
        return m;
    }

    TextMeshPro Texto(string nombre, float alturaLetra, Vector2 caja, TextAlignmentOptions alineacion)
    {
        var t = VisualesVR.Texto(nombre, transform, alturaLetra * _s, caja, alineacion);
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.color = ColTexto;
        t.fontMaterial.renderQueue = 3008;
        return t;
    }

    // ------------------------------------------------------------------ actualizacion

    void LateUpdate()
    {
        if (vehiculo == null) return;
        if (_cabeza == null)
        {
            var rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : null;
            if (rig != null) _cabeza = rig.centerEyeAnchor;
        }
        // Nadie la mira: no se gasta nada.
        if (_cabeza != null && (_cabeza.position - transform.position).sqrMagnitude > 12f * 12f) return;

        if (!MapaGPS.Listo)
        {
            MapaGPS.IntentarConstruir();
            if (!MapaGPS.Listo) return;
        }
        if (_matMapa.mainTexture == null)
        {
            _matMapa.mainTexture = MapaGPS.Textura;
            _matMapa.color = Color.white;
            _aviso.text = "";
        }

        float v = velocidadKmh != null ? velocidadKmh() : 0f;
        Vector3 f = vehiculo.forward;
        f.y = 0f;
        if (f.sqrMagnitude > 1e-4f)
        {
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            _yaw = _inicioYaw ? Mathf.LerpAngle(_yaw, yaw, 1f - Mathf.Exp(-Time.deltaTime * 6f)) : yaw;
            _inicioYaw = true;
        }
        float rad = _yaw * Mathf.Deg2Rad;
        _adel = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        _der = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
        _pos = vehiculo.position;
        float kObjetivo = Mathf.Lerp(zoomMin, zoomMax, Mathf.Clamp01(v / 50f)) / tamano.x;
        _k = _k <= 0f ? kObjetivo : Mathf.Lerp(_k, kObjetivo, 1f - Mathf.Exp(-Time.deltaTime * 1.5f));

        ActualizarMapa();
        ActualizarRuta();
        Vector2 norte = new Vector2(_der.z, _adel.z); // el norte del mundo (+Z) en la pantalla
        _norte.localPosition = new Vector3(_centroBrujula.x + norte.x * 0.0075f * _s, _centroBrujula.y + norte.y * 0.0075f * _s, -0.0045f);

        if (Time.time >= _proximoTexto)
        {
            _proximoTexto = Time.time + 0.25f;
            ActualizarTextos(v);
        }
    }

    void ActualizarMapa()
    {
        float W = tamano.x * 0.5f, H = tamano.y * 0.5f;
        _uv[0] = MapaGPS.UV(DePantalla(-W, -H));
        _uv[1] = MapaGPS.UV(DePantalla(W, -H));
        _uv[2] = MapaGPS.UV(DePantalla(-W, H));
        _uv[3] = MapaGPS.UV(DePantalla(W, H));
        _malla.uv = _uv;
    }

    Vector3 DePantalla(float x, float y) => _pos + _der * ((x - _ancla.x) * _k) + _adel * ((y - _ancla.y) * _k);

    Vector2 APantalla(Vector3 mundo)
    {
        Vector3 d = mundo - _pos;
        return new Vector2(Vector3.Dot(d, _der) / _k + _ancla.x, Vector3.Dot(d, _adel) / _k + _ancla.y);
    }

    void ActualizarRuta()
    {
        var juego = JuegoBomberos.Instancia;
        Vector3 destino = default;
        string nombre = null;
        bool conDestino = juego != null && juego.DestinoGPS(out destino, out nombre) && MapaGPS.HayCalles;
        if (conDestino && (!_conDestino || (destino - _destino).sqrMagnitude > 1f)) _proximaRuta = 0f; // nuevo destino
        _conDestino = conDestino;
        if (conDestino)
        {
            _destino = destino;
            _nombreDestino = nombre;
            if (Time.time >= _proximaRuta)
            {
                _proximaRuta = Time.time + 1f;
                _hayRuta = MapaGPS.Ruta(_pos, _destino, _ruta);
            }
            if (_hayRuta && _ruta.Count > 0) _ruta[0] = _pos;
        }
        else
        {
            _hayRuta = false;
            _ruta.Clear();
        }
        DibujarRuta();

        _pin.gameObject.SetActive(conDestino);
        if (conDestino)
        {
            Vector2 p = APantalla(_destino);
            float m = 0.008f * _s;
            p.x = Mathf.Clamp(p.x, _zona.xMin + m, _zona.xMax - m);
            p.y = Mathf.Clamp(p.y, _zona.yMin + m, _zona.yMax - m);
            _pin.localPosition = new Vector3(p.x, p.y, -0.002f);
        }
    }

    void DibujarRuta()
    {
        int linea = 0;
        _tramo.Clear();
        if (_hayRuta)
            for (int i = 0; i < _ruta.Count - 1; i++)
            {
                Vector2 a = APantalla(_ruta[i]), b = APantalla(_ruta[i + 1]);
                if (!Recortar(ref a, ref b, _zona)) continue;
                if (_tramo.Count > 0 && ((Vector2)_tramo[_tramo.Count - 1] - a).sqrMagnitude > 1e-8f)
                {
                    Volcar(linea++);
                    _tramo.Clear();
                }
                if (_tramo.Count == 0) _tramo.Add(new Vector3(a.x, a.y, -0.001f));
                _tramo.Add(new Vector3(b.x, b.y, -0.001f));
            }
        if (_tramo.Count > 1) Volcar(linea++);
        for (; linea < _lineas.Length; linea++) _lineas[linea].positionCount = 0;
    }

    void Volcar(int i)
    {
        if (i >= _lineas.Length) return;
        _lineas[i].positionCount = _tramo.Count;
        for (int k = 0; k < _tramo.Count; k++) _lineas[i].SetPosition(k, _tramo[k]);
    }

    /// <summary>Recorta un segmento al rectangulo (Liang-Barsky). False si queda afuera.</summary>
    static bool Recortar(ref Vector2 a, ref Vector2 b, Rect r)
    {
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        if (!Borde(-d.x, a.x - r.xMin, ref t0, ref t1) || !Borde(d.x, r.xMax - a.x, ref t0, ref t1) ||
            !Borde(-d.y, a.y - r.yMin, ref t0, ref t1) || !Borde(d.y, r.yMax - a.y, ref t0, ref t1)) return false;
        Vector2 a0 = a;
        a = a0 + d * t0;
        b = a0 + d * t1;
        return true;
    }

    static bool Borde(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
        float t = q / p;
        if (p < 0f)
        {
            if (t > t1) return false;
            if (t > t0) t0 = t;
        }
        else
        {
            if (t < t0) return false;
            if (t < t1) t1 = t;
        }
        return true;
    }

    void ActualizarTextos(float v)
    {
        Poner(_velocidad, $"<b>{Mathf.RoundToInt(v)}</b> <size=70%>km/h</size>");

        if (!_conDestino)
        {
            bool explorar = JuegoBomberos.Instancia != null && JuegoBomberos.Instancia.ModoExplorar;
            Poner(_instruccion, (explorar ? "Explorar" : "GPS") + "  <color=#A6B5C7><size=80%>rumbo " + Rumbo(_yaw) + "</size></color>");
            Poner(_destinoTxt, "Sin destino");
            Poner(_eta, "");
            Icono(0f, ColGris);
            return;
        }

        Poner(_destinoTxt, _nombreDestino);
        Vector3 plano = _destino - _pos;
        plano.y = 0f;
        if (plano.magnitude <= distanciaLlegada)
        {
            Poner(_instruccion, "Has llegado a tu destino");
            Poner(_eta, "");
            Icono(0f, ColVerde);
            return;
        }
        if (!_hayRuta || _ruta.Count < 2)
        {
            Poner(_instruccion, "Buscando ruta...");
            Poner(_eta, "");
            Icono(0f, ColGris);
            return;
        }

        // Distancia total y siguiente giro.
        float total = 0f, distGiro = -1f;
        int lado = 0;
        for (int i = 1; i < _ruta.Count; i++)
        {
            Vector3 seg = _ruta[i] - _ruta[i - 1];
            seg.y = 0f;
            total += seg.magnitude;
            if (distGiro >= 0f || i >= _ruta.Count - 1 || total < 4f) continue;
            Vector3 sig = _ruta[i + 1] - _ruta[i];
            sig.y = 0f;
            if (seg.sqrMagnitude < 0.01f || sig.sqrMagnitude < 0.01f) continue;
            float ang = Vector3.SignedAngle(seg, sig, Vector3.up);
            if (Mathf.Abs(ang) < 35f) continue;
            distGiro = total;
            lado = ang > 0f ? 1 : -1;
        }

        if (distGiro >= 0f && distGiro < 300f)
        {
            string hacia = lado > 0 ? "derecha" : "izquierda";
            Poner(_instruccion, distGiro < 15f ? $"Gira a la {hacia} ahora" : $"En {Redondear(distGiro)} m, gira a la {hacia}");
            Icono(lado > 0 ? -90f : 90f, ColTexto);
        }
        else
        {
            Poner(_instruccion, $"Sigue recto {Redondear(total)} m");
            Icono(0f, ColTexto);
        }

        float ms = Mathf.Max(v / 3.6f, 6f); // ~22 km/h de referencia en ciudad
        float min = total / ms / 60f;
        Poner(_eta, $"{Redondear(total)} m  <color=#A6B5C7>{(min < 1f ? "<1" : Mathf.CeilToInt(min).ToString())} min</color>");
    }

    void Icono(float angulo, Color color)
    {
        _icono.localRotation = Quaternion.Euler(0f, 0f, angulo);
        _matIcono.color = color;
    }

    static void Poner(TextMeshPro t, string texto)
    {
        if (t.text != texto) t.text = texto;
    }

    static int Redondear(float m) => m < 100f ? Mathf.Max(5, Mathf.RoundToInt(m / 5f) * 5) : Mathf.RoundToInt(m / 10f) * 10;

    static string Rumbo(float yaw)
    {
        string[] nombres = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
        int i = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 45f) % 8;
        return nombres[i];
    }
}
