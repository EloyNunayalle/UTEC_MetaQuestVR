using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Comprobador de tension (Mision 2). No habia uno en el proyecto: se arma con primitivas
/// (cuerpo amarillo, punta metalica, pantalla y LED).
/// - Agarrar: acerca la mano y presiona GRIP. Se sostiene como un lapiz: la punta apunta hacia adelante.
/// - Medir: SOLO hay que tocar el tablero con la punta (aparece un anillo verde donde apoyarlo).
///   Mide solo en medio segundo, con pitidos; no hace falta apretar nada. Teclado (editor): T = medir.
/// - El resultado sale del estado LOGICO del tablero (PanelElectrico.Comprobar), no de lo que se ve:
///   verde "SIN TENSION" o rojo "PELIGRO: CIRCUITO ENERGIZADO".
/// Al soltarlo cae sobre la mesa o el piso.
/// </summary>
public class ComprobadorTension : MonoBehaviour
{
    public PanelElectrico panel;
    public float radioAgarre = 0.12f;

    public bool Agarrado => _mano >= 0;
    public Vector3 Centro => transform.position;
    public Vector3 Punta => _punta != null ? _punta.position : transform.position;

    /// <summary>(sinTension) medicion valida tocando el tablero.</summary>
    public event Action<bool> alMedir;
    /// <summary>Se intento medir sin tocar el tablero.</summary>
    public event Action alMedirSinContacto;
    public event Action alAgarrar;

    Transform _punta;
    TextMeshPro _pantalla;
    Material _matLed;
    Renderer[] _renders;
    Collider _col;
    OVRCameraRig _rig;
    Vector3 _posInicial, _caidaDesde, _caidaHasta;
    Quaternion _rotInicial;
    int _mano = -1, _hoverPrev = -1;
    float _caidaT = 1f, _finResultado, _contacto, _proximoPitido;
    bool _gatilloPrev, _preparado, _peligro, _medidoEnContacto;

    static readonly Color Gris = new Color(0.25f, 0.25f, 0.25f);

    public void Preparar()
    {
        if (_preparado) return;
        _preparado = true;

        // Cuerpo a lo largo de +Z (la punta va hacia +Z).
        var cuerpo = VisualesVR.Primitiva(PrimitiveType.Cube, "Cuerpo", transform, VisualesVR.MaterialOpaco(new Color(1f, 0.78f, 0.05f)));
        cuerpo.transform.localScale = new Vector3(0.04f, 0.028f, 0.15f);
        var goma = VisualesVR.Primitiva(PrimitiveType.Cube, "Agarre", transform, VisualesVR.MaterialOpaco(new Color(0.1f, 0.1f, 0.1f)));
        goma.transform.localScale = new Vector3(0.043f, 0.031f, 0.06f);
        goma.transform.localPosition = new Vector3(0f, 0f, -0.045f);
        var cono = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Cabezal", transform, VisualesVR.MaterialOpaco(new Color(0.1f, 0.1f, 0.1f)));
        cono.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cono.transform.localScale = new Vector3(0.024f, 0.012f, 0.024f);
        cono.transform.localPosition = new Vector3(0f, 0f, 0.087f);
        var punta = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Punta", transform, VisualesVR.MaterialOpaco(new Color(0.8f, 0.82f, 0.85f)));
        punta.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        punta.transform.localScale = new Vector3(0.005f, 0.02f, 0.005f);
        punta.transform.localPosition = new Vector3(0f, 0f, 0.115f);
        _punta = new GameObject("Extremo").transform;
        _punta.SetParent(transform, false);
        _punta.localPosition = new Vector3(0f, 0f, 0.135f);

        var pantallaFondo = VisualesVR.Primitiva(PrimitiveType.Cube, "Pantalla", transform, VisualesVR.MaterialOpaco(new Color(0.05f, 0.08f, 0.06f)));
        pantallaFondo.transform.localScale = new Vector3(0.032f, 0.002f, 0.045f);
        pantallaFondo.transform.localPosition = new Vector3(0f, 0.0145f, 0.02f);
        _pantalla = VisualesVR.Texto("Lectura", transform, 0.007f, new Vector2(0.03f, 0.04f));
        _pantalla.transform.localPosition = new Vector3(0f, 0.0162f, 0.02f);
        // Texto acostado sobre la cara superior, legible mirando desde atras (como se sostiene).
        _pantalla.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _pantalla.fontStyle = FontStyles.Bold;

        _matLed = VisualesVR.MaterialSinLuz(Gris);
        var led = VisualesVR.Primitiva(PrimitiveType.Sphere, "LED", transform, _matLed);
        led.transform.localScale = Vector3.one * 0.012f;
        led.transform.localPosition = new Vector3(0f, 0.016f, 0.058f);

        var etiqueta = VisualesVR.Texto("Marca", transform, 0.006f, new Vector2(0.035f, 0.012f));
        etiqueta.transform.localPosition = new Vector3(0f, 0.0145f, -0.03f);
        etiqueta.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        etiqueta.text = "TESTER";
        etiqueta.color = Color.white;

        // Collider para apoyarlo y para que no se pierda (no bloquea al jugador por su tamano).
        var col = gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(0.045f, 0.032f, 0.2f);
        col.center = new Vector3(0f, 0f, 0.01f);
        _col = col;

        _renders = GetComponentsInChildren<Renderer>();
        _posInicial = transform.position;
        _rotInicial = transform.rotation;
        MostrarReposo();
    }

    void MostrarReposo()
    {
        _peligro = false;
        _pantalla.text = "LISTO\n<size=70%>--- V</size>";
        _pantalla.color = new Color(0.6f, 1f, 0.7f);
        _matLed.color = Gris;
    }

    Transform ManoT(int i)
    {
        if (_rig == null) _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : null;
        if (_rig == null) return null;
        var m = i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor;
        return m != null && m.gameObject.activeInHierarchy ? m : null;
    }

    void Update()
    {
        if (!_preparado) return;
        bool permitido = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;

        if (_finResultado > 0f && Time.time >= _finResultado) { _finResultado = 0f; MostrarReposo(); }
        if (_peligro) _matLed.color = Mathf.Repeat(Time.time, 0.3f) > 0.15f ? new Color(1f, 0.1f, 0.05f) : Gris;

        if (_mano < 0)
        {
            Caer();
            int hover = -1;
            if (permitido)
                for (int i = 0; i < 2; i++)
                {
                    var m = ManoT(i);
                    if (m == null || Vector3.Distance(m.position, transform.position) > radioAgarre) continue;
                    hover = i;
                    if (EntradaVR.AgarreDown(i == 0)) { Agarrar(i); return; }
                }
            if (hover != _hoverPrev && hover >= 0) EntradaVR.Vibrar(hover == 0, 0.25f, 0.05f, 0.8f);
            _hoverPrev = hover;
            VisualesVR.Tenir(_renders, new Color(1f, 0.85f, 0.1f), hover >= 0 ? 0.3f + 0.3f * Mathf.Sin(Time.time * 8f) : 0f);
            return;
        }

        var mano = ManoT(_mano);
        if (mano == null || !permitido || !EntradaVR.Agarrando(_mano == 0))
        {
            if (panel != null) panel.MostrarObjetivo(false);
            Soltar();
            return;
        }

        // Como un lapiz: un poco delante de la mano, la punta hacia adelante.
        transform.SetPositionAndRotation(mano.TransformPoint(new Vector3(0f, -0.01f, 0.05f)), mano.rotation);
        if (panel != null) panel.MostrarObjetivo(!panel.Verificado);

        // Medicion automatica: tocar el tablero con la punta durante medio segundo.
        bool tocando = panel != null && panel.EnZonaPrueba(Punta);
        if (tocando && !_medidoEnContacto)
        {
            _contacto += Time.deltaTime;
            if (Time.time >= _proximoPitido)
            {
                _proximoPitido = Time.time + 0.12f;
                SonidosVR.ReproducirEn(SonidosVR.Clic, Punta, 0.5f, 4f);
                EntradaVR.Vibrar(_mano == 0, 0.25f, 0.05f, 0.9f);
                _pantalla.text = "MIDIENDO...";
                _pantalla.color = new Color(1f, 0.9f, 0.4f);
            }
            if (_contacto >= 0.5f) { _medidoEnContacto = true; Medir(); }
        }
        else if (!tocando)
        {
            _contacto = 0f;
            _medidoEnContacto = false; // al despegarlo se puede volver a medir
        }

        // Tambien se puede medir a mano (gatillo / T), por si acaso.
        var c = _mano == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        bool gatillo = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c) > 0.6f || Input.GetKey(KeyCode.T);
        if (gatillo && !_gatilloPrev) Medir();
        _gatilloPrev = gatillo;
    }

    void Medir()
    {
        SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position, 0.6f, 5f);
        bool sinTension = false;
        bool enBornes = panel != null && panel.Comprobar(Punta, out sinTension);
        if (!enBornes)
        {
            _pantalla.text = "TOCA EL\nTABLERO";
            _pantalla.color = new Color(0.8f, 0.8f, 0.8f);
            _matLed.color = Gris;
            _peligro = false;
            _finResultado = Time.time + 1.5f;
            alMedirSinContacto?.Invoke();
            return;
        }

        if (sinTension)
        {
            _pantalla.text = "SIN TENSIÓN\n<size=70%>0 V</size>";
            _pantalla.color = new Color(0.3f, 1f, 0.4f);
            _matLed.color = new Color(0.2f, 1f, 0.3f);
            _peligro = false;
            SonidosVR.ReproducirEn(SonidosVR.Pitido, transform.position, 0.8f, 6f);
            SonidosVR.ReproducirEn(SonidosVR.Exito, transform.position, 0.6f, 6f);
            if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.3f, 0.08f, 0.9f);
        }
        else
        {
            _pantalla.text = "¡TENSIÓN!\n<size=70%>230 V</size>";
            _pantalla.color = new Color(1f, 0.25f, 0.2f);
            _peligro = true;
            SonidosVR.ReproducirEn(SonidosVR.Error, transform.position, 0.9f, 8f);
            SonidosVR.ReproducirEn(SonidosVR.Chispa, Punta, 0.6f, 8f);
            if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 1f, 0.35f, 0.9f);
        }
        _finResultado = Time.time + 4f;
        alMedir?.Invoke(sinTension);
    }

    void Agarrar(int i)
    {
        _mano = i;
        _hoverPrev = -1;
        _caidaT = 1f;
        _gatilloPrev = true;
        _col.enabled = false; // en la mano no choca con el jugador
        VisualesVR.Tenir(_renders, Color.white, 0f);
        EntradaVR.Vibrar(i == 0, 0.6f, 0.1f, 0.6f);
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position, 0.8f);
        alAgarrar?.Invoke();
    }

    void Soltar()
    {
        if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.3f, 0.05f);
        _mano = -1;
        _col.enabled = true;
        // Cae acostado (pantalla hacia arriba) sobre lo que tenga debajo.
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        _caidaDesde = transform.position;
        _caidaHasta = _posInicial;
        foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 0.05f, Vector3.down, 3f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (_rig != null && h.collider.transform.IsChildOf(_rig.transform)) continue;
            if (_caidaHasta == _posInicial || h.point.y + 0.016f > _caidaHasta.y) _caidaHasta = h.point + Vector3.up * 0.016f;
        }
        _caidaT = 0f;
    }

    void Caer()
    {
        if (_caidaT >= 1f) return;
        _caidaT = Mathf.Min(1f, _caidaT + Time.deltaTime * 3f);
        transform.position = Vector3.Lerp(_caidaDesde, _caidaHasta, _caidaT * _caidaT);
    }

    /// <summary>Vuelve a la mesa (reinicio).</summary>
    public void Restablecer()
    {
        Preparar();
        _mano = -1;
        _col.enabled = true;
        _caidaT = 1f;
        _finResultado = 0f;
        _contacto = 0f;
        _medidoEnContacto = false;
        if (panel != null) panel.MostrarObjetivo(false);
        transform.SetPositionAndRotation(_posInicial, _rotInicial);
        MostrarReposo();
    }
}
