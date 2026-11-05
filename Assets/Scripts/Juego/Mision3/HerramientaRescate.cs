using System;
using UnityEngine;

/// <summary>
/// Barra Halligan de la Mision 3. No hay un modelo en el proyecto: se arma con primitivas
/// (mango de acero, horquilla en la punta y hacha + pico en el otro extremo).
/// - Agarrar: acerca la mano a la barra y presiona GRIP.
/// - Soltar cerca de la maletera abierta: queda cargada (ZonaCargaHerramienta). En otro lado cae al piso.
/// - PuertaAtascada la "engancha": la punta queda fija en el punto de palanca y la barra pivotea hacia la mano.
/// Vive en la escena persistente: sigue existiendo aunque se descargue el interior de la casa.
/// </summary>
public class HerramientaRescate : MonoBehaviour
{
    public float radioAgarre = 0.14f;

    const float LargoMedio = 0.38f;
    const float AgarreLocalZ = -0.18f;

    public bool Agarrada => _mano >= 0;
    public bool AgarradaAlgunaVez { get; private set; }
    public bool EnMaletera { get; private set; }
    public bool Enganchada => _enganche.HasValue;
    public int Mano => _mano;
    public Vector3 Centro => transform.position;
    public Vector3 Punta => _punta != null ? _punta.position : transform.position;
    /// <summary>Posicion de la mano que la sostiene.</summary>
    public Vector3 PosicionMano => ManoT(_mano) != null ? ManoT(_mano).position : transform.position;

    /// <summary>Zona de carga del camion (la asigna Mision3Controller).</summary>
    public ZonaCargaHerramienta zona;
    /// <summary>Opcional: si devuelve false esa mano ya lleva otra cosa (0 = izquierda, 1 = derecha).</summary>
    public Func<int, bool> manoLibre;
    public event Action alAgarrar;

    Transform _punta;
    Renderer[] _renders;
    OVRCameraRig _rig;
    Vector3? _enganche;
    Vector3 _posInicial, _caidaDesde, _caidaHasta;
    Quaternion _rotInicial;
    int _mano = -1, _hoverPrev = -1;
    float _caidaT = 1f;

    public static HerramientaRescate Crear(OVRCameraRig rig, Vector3 posicion, Quaternion rotacion)
    {
        var go = new GameObject("Barra_Halligan");
        go.transform.SetPositionAndRotation(posicion, rotacion);
        var h = go.AddComponent<HerramientaRescate>();
        h._rig = rig;
        h._posInicial = posicion;
        h._rotInicial = rotacion;
        h.Construir();
        return h;
    }

    void Construir()
    {
        var acero = VisualesVR.MaterialOpaco(new Color(0.42f, 0.45f, 0.5f));
        var oscuro = VisualesVR.MaterialOpaco(new Color(0.16f, 0.17f, 0.2f));
        var amarillo = VisualesVR.MaterialOpaco(new Color(1f, 0.75f, 0.05f));

        Pieza(PrimitiveType.Cylinder, "Mango", acero, Vector3.zero, new Vector3(0.032f, LargoMedio - 0.02f, 0.032f), new Vector3(90f, 0f, 0f));
        Pieza(PrimitiveType.Cylinder, "Agarre", amarillo, new Vector3(0f, 0f, AgarreLocalZ), new Vector3(0.038f, 0.1f, 0.038f), new Vector3(90f, 0f, 0f));

        // Horquilla (la punta que entra en la puerta).
        Pieza(PrimitiveType.Cube, "Horquilla_A", oscuro, new Vector3(0.02f, 0f, LargoMedio - 0.01f), new Vector3(0.022f, 0.012f, 0.1f), new Vector3(0f, -8f, 0f));
        Pieza(PrimitiveType.Cube, "Horquilla_B", oscuro, new Vector3(-0.02f, 0f, LargoMedio - 0.01f), new Vector3(0.022f, 0.012f, 0.1f), new Vector3(0f, 8f, 0f));

        // Hacha y pico en el otro extremo.
        Pieza(PrimitiveType.Cube, "Hacha", oscuro, new Vector3(0f, 0.05f, -LargoMedio), new Vector3(0.03f, 0.14f, 0.05f), Vector3.zero);
        Pieza(PrimitiveType.Cylinder, "Pico", oscuro, new Vector3(0f, -0.07f, -LargoMedio), new Vector3(0.022f, 0.05f, 0.022f), Vector3.zero);

        _punta = new GameObject("Punta").transform;
        _punta.SetParent(transform, false);
        _punta.localPosition = new Vector3(0f, 0f, LargoMedio + 0.05f);
        _renders = GetComponentsInChildren<Renderer>();
    }

    void Pieza(PrimitiveType tipo, string nombre, Material mat, Vector3 pos, Vector3 escala, Vector3 euler)
    {
        var p = VisualesVR.Primitiva(tipo, nombre, transform, mat);
        p.transform.localPosition = pos;
        p.transform.localScale = escala;
        p.transform.localRotation = Quaternion.Euler(euler);
    }

    Transform ManoT(int i)
    {
        if (_rig == null || i < 0) return null;
        var m = i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor;
        return m != null && m.gameObject.activeInHierarchy ? m : null;
    }

    /// <summary>Distancia de un punto al eje de la barra.</summary>
    float DistanciaBarra(Vector3 p)
    {
        Vector3 a = transform.TransformPoint(0f, 0f, -LargoMedio), b = transform.TransformPoint(0f, 0f, LargoMedio);
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(p, a + ab * t);
    }

    void Update()
    {
        bool permitido = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;

        if (_mano < 0)
        {
            Caer();
            bool sePuede = permitido && (!EnMaletera || (zona != null && zona.SePuedeSacar));
            int hover = -1;
            if (sePuede)
                for (int i = 0; i < 2; i++)
                {
                    var m = ManoT(i);
                    if (m == null || DistanciaBarra(m.position) > radioAgarre) continue;
                    if (manoLibre != null && !manoLibre(i)) continue;
                    hover = i;
                    if (EntradaVR.AgarreDown(i == 0)) { Agarrar(i); return; }
                }
            if (hover != _hoverPrev && hover >= 0) EntradaVR.Vibrar(hover == 0, 0.25f, 0.05f, 0.8f);
            _hoverPrev = hover;
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            VisualesVR.Tenir(_renders, new Color(1f, 0.85f, 0.1f), hover >= 0 ? 0.3f + 0.3f * pulso : 0f);
            return;
        }

        var mano = ManoT(_mano);
        if (mano == null || !permitido || !EntradaVR.Agarrando(_mano == 0)) { Soltar(); return; }

        if (_enganche.HasValue)
        {
            // Enganchada: la punta queda clavada y la barra apunta de la mano a la punta (hace palanca).
            Vector3 dir = _enganche.Value - mano.position;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.SetPositionAndRotation(_enganche.Value - rot * _punta.localPosition, rot);
            return;
        }

        // En la mano: agarrada por el mango, un poco atras del centro.
        transform.SetPositionAndRotation(mano.TransformPoint(new Vector3(0f, 0f, -AgarreLocalZ)), mano.rotation);
    }

    void Agarrar(int i)
    {
        _mano = i;
        _hoverPrev = -1;
        _caidaT = 1f;
        if (EnMaletera)
        {
            EnMaletera = false;
            transform.SetParent(null, true);
        }
        AgarradaAlgunaVez = true;
        VisualesVR.Tenir(_renders, Color.white, 0f);
        EntradaVR.Vibrar(i == 0, 0.7f, 0.12f, 0.6f);
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position, 0.9f);
        SonidosVR.ReproducirEn(SonidosVR.Metal, transform.position, 0.25f, 6f);
        alAgarrar?.Invoke();
    }

    void Soltar()
    {
        if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.3f, 0.05f);
        _mano = -1;
        _enganche = null;

        if (zona != null && zona.PuedeRecibir(transform.position))
        {
            zona.Cargar(this);
            return;
        }
        if (zona != null && zona.Cerca(transform.position)) zona.AvisarPuertaCerrada();

        // Cae acostada al piso; si no hay piso debajo, vuelve a su soporte.
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        _caidaDesde = transform.position;
        _caidaHasta = _posInicial;
        float mejor = float.MaxValue;
        foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 0.1f, Vector3.down, 4f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (_rig != null && h.collider.transform.IsChildOf(_rig.transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; _caidaHasta = h.point + Vector3.up * 0.09f; }
        }
        if (mejor == float.MaxValue) transform.rotation = _rotInicial;
        _caidaT = 0f;
    }

    void Caer()
    {
        if (_caidaT >= 1f) return;
        _caidaT = Mathf.Min(1f, _caidaT + Time.deltaTime * 3f);
        transform.position = Vector3.Lerp(_caidaDesde, _caidaHasta, _caidaT * _caidaT);
        if (_caidaT >= 1f) SonidosVR.ReproducirEn(SonidosVR.Metal, transform.position, 0.6f, 10f);
    }

    /// <summary>La deja guardada en la maletera (la llama ZonaCargaHerramienta).</summary>
    public void GuardarEn(Transform padre, Vector3 posLocal, Quaternion rotLocal)
    {
        _mano = -1;
        _enganche = null;
        _caidaT = 1f;
        EnMaletera = true;
        transform.SetParent(padre, false);
        transform.localPosition = posLocal;
        transform.localRotation = rotLocal;
        VisualesVR.Tenir(_renders, Color.white, 0f);
    }

    /// <summary>La punta queda fija en este punto mientras la barra siga en la mano.</summary>
    public void Enganchar(Vector3 punto) => _enganche = punto;
    public void Desenganchar() => _enganche = null;
}
