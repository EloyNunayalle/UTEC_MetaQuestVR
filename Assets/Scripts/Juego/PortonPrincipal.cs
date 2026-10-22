using TMPro;
using UnityEngine;

/// <summary>
/// Porton principal del cerco del patio (el primero que se abre en la mision).
/// Es corredizo: primero se separa del muro hacia el patio y luego se desliza
/// a un costado, detras del muro, con movimiento suave (smoothstep).
///
/// Activacion: SOLO con el boton verde inmersivo del poste fijo (se presiona tocandolo
/// con la mano o la punta del control; no usa teclas ni botones del control).
/// Feedback: pitido, vibracion, motor mientras se mueve, baliza ambar y golpe al terminar.
/// Seguridad: no se cierra si el jugador o el camion estan en el paso.
///
/// Lo agrega solo JuegoBomberos al objeto "Porton_Principal" de la escena de la ciudad.
/// </summary>
public class PortonPrincipal : MonoBehaviour
{
    public enum Estado { Cerrado, Abriendo, Abierto, Cerrando }

    [Header("Movimiento")]
    public float duracion = 5f;
    [Tooltip("Desliza hacia la izquierda (eje -X del porton). Desmarcar = hacia la derecha.")]
    public bool deslizarIzquierda = true;
    [Tooltip("Cuanto se separa del muro hacia el patio antes de deslizarse (m).")]
    public float separacion = 0.28f;

    [Header("Boton inmersivo")]
    [Tooltip("Nombre del poste fijo donde va el boton (vacio = junto al porton).")]
    public string nombrePosteBoton = "Poste_Porton_R";
    public float radioBoton = 0.12f;

    public Estado estado = Estado.Cerrado;
    public bool EstaAbierto => estado == Estado.Abierto;
    public bool AbiertoOAbriendo => estado == Estado.Abierto || estado == Estado.Abriendo;
    public Vector3 PuntoBoton => _boton != null ? _boton.position : transform.position;

    /// <summary>Se llama al presionar el boton; si devuelve false no se mueve.</summary>
    public System.Func<bool> puedeAccionar;

    Vector3 _posCerrado, _haciaDentro, _haciaLado;
    float _recorrido, _t;
    Transform _boton, _cara;
    Vector3 _botonReposo;
    bool _botonArmado = true;
    float _proximoPresionar;
    Renderer _baliza;
    AudioSource _motor;
    OVRCameraRig _rig;
    CamionBomberos _camion;
    float _proximaBusqueda;
    Bounds _paso;

    /// <summary>puntoInterior: cualquier punto dentro del patio (para saber hacia donde separarse).</summary>
    public void Inicializar(Vector3 puntoInterior)
    {
        _posCerrado = transform.position;

        Vector3 normal = transform.forward; normal.y = 0f; normal.Normalize();
        Vector3 aInterior = puntoInterior - _posCerrado; aInterior.y = 0f;
        _haciaDentro = Vector3.Dot(normal, aInterior) >= 0f ? normal : -normal;
        Vector3 derecha = transform.right; derecha.y = 0f; derecha.Normalize();
        _haciaLado = deslizarIzquierda ? -derecha : derecha;

        // El porton es un cubo escalado: su ancho es la escala en X.
        float ancho = Mathf.Abs(transform.lossyScale.x);
        _recorrido = ancho + 0.2f;

        // Zona de paso (para la seguridad anti-aplastamiento).
        // Zona de paso: el ancho del porton (+0.5 m por lado) y 2 m hacia cada lado del muro.
        Vector3 alLargo = new Vector3(Mathf.Abs(derecha.x), 0f, Mathf.Abs(derecha.z)) * (ancho + 1f);
        Vector3 alTraves = new Vector3(Mathf.Abs(_haciaDentro.x), 0f, Mathf.Abs(_haciaDentro.z)) * 4f;
        _paso = new Bounds(_posCerrado, alLargo + alTraves + Vector3.up * 3f);

        CrearBoton(ancho);

        _motor = SonidosVR.CrearBucle(transform, SonidosVR.Motor, 0.45f, 30f);
        _motor.pitch = 1.3f;
    }

    void CrearBoton(float ancho)
    {
        Transform poste = null;
        if (!string.IsNullOrEmpty(nombrePosteBoton))
        {
            var go = GameObject.Find(nombrePosteBoton);
            if (go != null) poste = go.transform;
        }
        Vector3 basePos = poste != null ? poste.position : _posCerrado - _haciaLado * (ancho * 0.5f + 0.4f);
        float grosorPoste = poste != null ? Mathf.Max(Mathf.Abs(poste.lossyScale.x), Mathf.Abs(poste.lossyScale.z)) : 0.2f;

        // Caja de control en la cara del poste que mira al patio, a 1.2 m de altura.
        _cara = new GameObject("Control_Porton_Principal").transform;
        _cara.position = new Vector3(basePos.x, _posCerrado.y - transform.lossyScale.y * 0.5f + 1.2f, basePos.z) + _haciaDentro * (grosorPoste * 0.5f + 0.03f);
        _cara.rotation = Quaternion.LookRotation(-_haciaDentro, Vector3.up); // +Z hacia el muro: se ve desde el patio
        // Se cuelga del poste (no del porton, que se mueve).
        if (poste != null) _cara.SetParent(poste, true);

        var caja = VisualesVR.Primitiva(PrimitiveType.Cube, "Caja", _cara, VisualesVR.MaterialOpaco(new Color(0.25f, 0.27f, 0.3f)));
        SetEscalaMundo(caja.transform, new Vector3(0.16f, 0.24f, 0.05f));

        var boton = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Boton", _cara, VisualesVR.MaterialOpaco(new Color(0.1f, 0.75f, 0.2f)));
        boton.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        SetEscalaMundo(boton.transform, new Vector3(0.08f, 0.02f, 0.08f));
        boton.transform.position = _cara.position - _cara.forward * 0.035f;
        _boton = boton.transform;
        _botonReposo = _boton.localPosition;

        var etiqueta = VisualesVR.Texto("Etiqueta", _cara, 0.035f, new Vector2(0.5f, 0.12f));
        etiqueta.transform.position = _cara.position + Vector3.up * 0.2f - _cara.forward * 0.03f;
        etiqueta.transform.rotation = _cara.rotation;
        SetEscalaMundo(etiqueta.transform, Vector3.one);
        etiqueta.fontStyle = FontStyles.Bold;
        etiqueta.outlineWidth = 0.2f;
        etiqueta.outlineColor = Color.black;
        etiqueta.text = "PORTÓN PRINCIPAL\n<size=70%>Toca el botón</size>";

        // Baliza ambar arriba del poste: parpadea mientras el porton se mueve.
        var baliza = VisualesVR.Primitiva(PrimitiveType.Sphere, "Baliza", null, VisualesVR.MaterialSinLuz(new Color(0.4f, 0.25f, 0f)));
        float alturaPoste = poste != null ? poste.position.y + Mathf.Abs(poste.lossyScale.y) * 0.5f : _posCerrado.y + 1.4f;
        baliza.transform.position = new Vector3(basePos.x, alturaPoste + 0.08f, basePos.z);
        baliza.transform.localScale = Vector3.one * 0.14f;
        if (poste != null) baliza.transform.SetParent(poste, true);
        _baliza = baliza.GetComponent<Renderer>();
    }

    /// <summary>Fija la escala en el mundo aunque el padre (poste) este escalado.</summary>
    static void SetEscalaMundo(Transform t, Vector3 escalaMundo)
    {
        Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(escalaMundo.x / Mathf.Max(1e-4f, Mathf.Abs(p.x)),
                                   escalaMundo.y / Mathf.Max(1e-4f, Mathf.Abs(p.y)),
                                   escalaMundo.z / Mathf.Max(1e-4f, Mathf.Abs(p.z)));
    }

    // ------------------------------------------------------------------

    public void Alternar()
    {
        if (puedeAccionar != null && !puedeAccionar()) return;
        if (estado == Estado.Cerrado || estado == Estado.Cerrando) Abrir();
        else Cerrar();
    }

    public void Abrir()
    {
        if (estado == Estado.Abierto) return;
        estado = Estado.Abriendo;
    }

    public void Cerrar()
    {
        if (estado == Estado.Cerrado) return;
        estado = PasoOcupado() ? Estado.Abierto : Estado.Cerrando;
    }

    void Update()
    {
        if (_boton == null) return;
        Buscar();

        RevisarBoton();

        // Avance del movimiento.
        float paso = Time.deltaTime / Mathf.Max(0.1f, duracion);
        Estado antes = estado;
        if (estado == Estado.Abriendo)
        {
            _t = Mathf.Min(1f, _t + paso);
            if (_t >= 1f) estado = Estado.Abierto;
        }
        else if (estado == Estado.Cerrando)
        {
            if (PasoOcupado()) estado = Estado.Abriendo; // seguridad: vuelve a abrir
            else
            {
                _t = Mathf.Max(0f, _t - paso);
                if (_t <= 0f) estado = Estado.Cerrado;
            }
        }

        // Primero se separa del muro (15 % del recorrido), luego se desliza.
        float sep = Suave(Mathf.Clamp01(_t / 0.15f));
        float lado = Suave(Mathf.Clamp01((_t - 0.15f) / 0.85f));
        transform.position = _posCerrado + _haciaDentro * (separacion * sep) + _haciaLado * (_recorrido * lado);

        bool moviendo = estado == Estado.Abriendo || estado == Estado.Cerrando;
        if (moviendo && !_motor.isPlaying) _motor.Play();
        if (!moviendo && _motor.isPlaying) _motor.Stop();
        if (antes != estado && (estado == Estado.Abierto || estado == Estado.Cerrado))
            SonidosVR.ReproducirEn(SonidosVR.Puerta, transform.position, 1f, 30f);

        if (_baliza != null)
        {
            bool on = moviendo && (int)(Time.time * 3f) % 2 == 0;
            _baliza.sharedMaterial.color = on ? new Color(1f, 0.65f, 0f) : new Color(0.4f, 0.25f, 0f);
        }
    }

    static float Suave(float x) => x * x * (3f - 2f * x);

    void Buscar()
    {
        if (Time.unscaledTime < _proximaBusqueda) return;
        _proximaBusqueda = Time.unscaledTime + 1f;
        if (_rig == null) _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : FindObjectOfType<OVRCameraRig>();
        if (_camion == null) _camion = FindObjectOfType<CamionBomberos>();
    }

    void RevisarBoton()
    {
        int mano = -1;
        if (_rig != null)
        {
            if (_rig.leftHandAnchor != null && Vector3.Distance(_rig.leftHandAnchor.position, _boton.position) < radioBoton) mano = 0;
            else if (_rig.rightHandAnchor != null && Vector3.Distance(_rig.rightHandAnchor.position, _boton.position) < radioBoton) mano = 1;
        }

        // Animacion del boton hundiendose.
        Vector3 hundido = _botonReposo + Vector3.forward * (0.015f / Mathf.Max(1e-4f, Mathf.Abs(_cara.lossyScale.z)));
        _boton.localPosition = Vector3.Lerp(_boton.localPosition, mano >= 0 ? hundido : _botonReposo, Time.deltaTime * 15f);

        if (mano < 0)
        {
            // Se rearma cuando la mano se aleja un poco.
            bool lejos = _rig == null ||
                ((_rig.leftHandAnchor == null || Vector3.Distance(_rig.leftHandAnchor.position, _boton.position) > radioBoton * 1.5f) &&
                 (_rig.rightHandAnchor == null || Vector3.Distance(_rig.rightHandAnchor.position, _boton.position) > radioBoton * 1.5f));
            if (lejos) _botonArmado = true;
            return;
        }
        if (_botonArmado) { _botonArmado = false; Presionar(mano); }
    }

    void Presionar(int mano)
    {
        if (Time.time < _proximoPresionar) return;
        _proximoPresionar = Time.time + 0.8f;
        SonidosVR.ReproducirEn(SonidosVR.Clic, PuntoBoton, 1f);
        EntradaVR.Vibrar(mano == 0, 0.6f, 0.08f, 0.8f);
        Alternar();
    }

    bool CamionCerca(float margen, bool soloManejando)
    {
        if (_camion == null || (soloManejando && !_camion.JugadorSentado)) return false;
        Bounds b = _camion.Limites;
        b.Expand(margen * 2f);
        return b.Intersects(new Bounds(_posCerrado, new Vector3(_recorrido, 3f, _recorrido)));
    }

    bool PasoOcupado()
    {
        if (CamionCerca(0.3f, false)) return true;
        if (_rig == null || _rig.centerEyeAnchor == null) return false;
        Vector3 c = _rig.centerEyeAnchor.position;
        return _paso.Contains(new Vector3(c.x, _paso.center.y, c.z));
    }
}
