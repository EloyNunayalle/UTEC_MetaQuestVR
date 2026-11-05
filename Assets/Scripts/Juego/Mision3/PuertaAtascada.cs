using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Puerta atascada de la Mision 3: no se abre al acercarse ni con la mano; hace falta la barra.
/// - Al llevar la barra aparece un anillo en el punto de palanca (junto a la cerradura).
/// - Acerca la PUNTA de la barra al anillo: se engancha y queda clavada ahi.
/// - Jala la barra hacia ti varias veces: solo cuenta el movimiento alejandose de la puerta. La puerta
///   cruje, cede de a pocos y salen astillas, hasta que se abre de golpe.
///   Teclado (editor) o gatillo: mantener para hacer palanca con la barra enganchada.
/// Convencion: pivote en la bisagra, +X hacia la cerradura, +Z hacia el lado del jugador.
/// </summary>
public class PuertaAtascada : MonoBehaviour
{
    [Tooltip("Punto de palanca en coordenadas locales de la puerta.")]
    public Vector3 puntoPalancaLocal = new Vector3(0.93f, 1.05f, 0.05f);
    [Tooltip("Grados que abre la puerta una vez forzada (positivo = hacia la habitacion).")]
    public float anguloAbierto = 95f;
    [Tooltip("Grados que llega a ceder mientras se hace palanca.")]
    public float anguloCedido = 5f;
    [Tooltip("Metros de jalon (sumados) necesarios para forzarla.")]
    public float recorridoNecesario = 0.7f;
    public float radioEnganche = 0.22f;
    public float distanciaMaximaMano = 1.15f;

    public bool Forzada { get; private set; }
    public float Progreso { get; private set; }
    public bool Enganchada { get; private set; }
    public Vector3 PuntoPalanca => _puntoPalanca;

    public event Action alForzar;
    /// <summary>Si devuelve false no se puede hacer palanca (el incendio bloquea el acceso).</summary>
    public Func<bool> accesoLibre;

    Vector3 _puntoPalanca, _normal;
    Quaternion _rotCerrada;
    Transform _anillo;
    Material _matAnillo;
    TextMeshPro _ayuda;
    ParticleSystem _astillas;
    OVRCameraRig _rig;
    float _angulo, _manoAnterior, _temblor, _proximoAviso, _hitoProgreso;

    void Awake()
    {
        _rotCerrada = transform.localRotation;
        _puntoPalanca = transform.TransformPoint(puntoPalancaLocal);
        _normal = transform.forward;

        CrearMarcador();
        CrearAstillas();
    }

    void CrearMarcador()
    {
        // Pletina de la cerradura: se ve el punto donde esta trabada.
        var pletina = VisualesVR.Primitiva(PrimitiveType.Cube, "Pletina", transform, VisualesVR.MaterialOpaco(new Color(0.3f, 0.3f, 0.32f)));
        pletina.transform.localPosition = new Vector3(0.93f, 1.05f, 0.03f);
        pletina.transform.localScale = new Vector3(0.12f, 0.3f, 0.015f);

        _anillo = new GameObject("Punto_Palanca").transform;
        _anillo.SetParent(transform, false);
        _anillo.localPosition = puntoPalancaLocal;
        _matAnillo = VisualesVR.MaterialSinLuz(new Color(1f, 0.8f, 0.1f, 0.9f));
        var malla = VisualesVR.ObjetoMalla("Anillo", _anillo, VisualesVR.Anillo(0.08f, 0.1f), _matAnillo);
        malla.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        _ayuda = VisualesVR.Texto("Ayuda", _anillo, 0.03f, new Vector2(0.7f, 0.12f));
        _ayuda.transform.localPosition = new Vector3(-0.2f, 0.22f, 0.02f);
        _ayuda.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TMP se lee desde su -Z
        _ayuda.fontStyle = FontStyles.Bold;
        _ayuda.outlineWidth = 0.2f;
        _ayuda.outlineColor = Color.black;
        _ayuda.color = new Color(1f, 0.85f, 0.25f);
        _ayuda.text = "Acerca aquí la punta de la barra";
        _anillo.gameObject.SetActive(false);
    }

    void CrearAstillas()
    {
        var go = new GameObject("Astillas");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = puntoPalancaLocal;
        _astillas = go.AddComponent<ParticleSystem>();
        _astillas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _astillas.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(0.45f, 0.3f, 0.18f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = _astillas.emission;
        em.rateOverTime = 0f;
        var forma = _astillas.shape;
        forma.shapeType = ParticleSystemShapeType.Hemisphere;
        forma.radius = 0.04f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ZonaFuego.MaterialParticula();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        _astillas.Play(); // emision 0: solo salen con Emit()
    }

    /// <summary>Deja la puerta abierta sin efectos (al recargar la escena si ya se habia forzado).</summary>
    public void DejarForzada()
    {
        Forzada = true;
        Progreso = 1f;
        _angulo = anguloAbierto;
        _anillo.gameObject.SetActive(false);
        Aplicar();
    }

    void Update()
    {
        if (_rig == null && JuegoBomberos.Instancia != null) _rig = JuegoBomberos.Instancia.rig;

        if (Forzada)
        {
            _angulo = Mathf.MoveTowards(_angulo, anguloAbierto, Time.deltaTime * 160f);
            Aplicar();
            return;
        }

        var h = Mision3Controller.Instancia != null ? Mision3Controller.Instancia.Herramienta : null;
        bool enMano = h != null && h.Agarrada;

        if (_anillo.gameObject.activeSelf != enMano) _anillo.gameObject.SetActive(enMano);
        if (enMano)
        {
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            _matAnillo.color = Enganchada ? new Color(0.3f, 1f, 0.4f, 0.9f) : new Color(1f, 0.8f, 0.1f, 0.5f + 0.4f * pulso);
            _ayuda.text = Enganchada ? "¡Jala la barra hacia ti!" : "Acerca aquí la punta de la barra";
        }

        if (!Enganchada)
        {
            if (enMano && Vector3.Distance(h.Punta, _puntoPalanca) <= radioEnganche)
            {
                if (accesoLibre == null || accesoLibre()) Enganchar(h);
                else if (Time.time >= _proximoAviso)
                {
                    _proximoAviso = Time.time + 3.5f;
                    SonidosVR.ReproducirEn(SonidosVR.Metal, _puntoPalanca, 0.5f, 10f);
                    JuegoBomberos.Instancia?.Aviso("¡Primero apaga el fuego!\n<size=55%>Forzar la puerta con fuego cerca alimentaría las llamas</size>", JuegoBomberos.TipoAviso.Error, 3f);
                }
            }
            else RevisarManoVacia(enMano);
        }
        else if (!enMano || Vector3.Distance(h.PosicionMano, _puntoPalanca) > distanciaMaximaMano) Desenganchar(h);
        else HacerPalanca(h);

        _temblor = Mathf.MoveTowards(_temblor, 0f, Time.deltaTime * 6f);
        _angulo = Progreso * anguloCedido + Mathf.Sin(Time.time * 60f) * _temblor;
        Aplicar();
    }

    /// <summary>Intento de abrir con la mano: no se puede, pero responde.</summary>
    void RevisarManoVacia(bool conBarra)
    {
        if (_rig == null || Time.time < _proximoAviso) return;
        Vector3 centro = transform.TransformPoint(0.5f, 1.1f, 0f);
        for (int i = 0; i < 2; i++)
        {
            var m = i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor;
            if (m == null || !m.gameObject.activeInHierarchy) continue;
            Vector3 d = m.position - centro;
            if (Mathf.Abs(Vector3.Dot(d, _normal)) > 0.2f || Mathf.Abs(d.y) > 1.1f || new Vector2(Vector3.Dot(d, transform.right), 0f).magnitude > 0.6f) continue;
            if (conBarra) return; // con la barra en la otra mano el anillo ya indica que hacer
            _proximoAviso = Time.time + 3.5f;
            _temblor = 0.8f;
            SonidosVR.ReproducirEn(SonidosVR.Golpes, centro, 0.5f, 10f);
            EntradaVR.Vibrar(i == 0, 0.5f, 0.12f, 0.3f);
            JuegoBomberos.Instancia?.Aviso("La puerta está atascada\n<size=55%>Necesitas la barra de rescate del camión</size>", JuegoBomberos.TipoAviso.Error, 2.5f);
            return;
        }
    }

    void Enganchar(HerramientaRescate h)
    {
        Enganchada = true;
        h.Enganchar(_puntoPalanca);
        _manoAnterior = Vector3.Dot(h.PosicionMano - _puntoPalanca, _normal);
        SonidosVR.ReproducirEn(SonidosVR.Metal, _puntoPalanca, 1f, 15f);
        EntradaVR.Vibrar(h.Mano == 0, 0.8f, 0.12f, 0.7f);
    }

    void Desenganchar(HerramientaRescate h)
    {
        Enganchada = false;
        if (h != null) h.Desenganchar();
    }

    void HacerPalanca(HerramientaRescate h)
    {
        // Solo cuenta jalar: al volver hacia la puerta no suma (se bombea la barra).
        float actual = Vector3.Dot(h.PosicionMano - _puntoPalanca, _normal);
        float jalon = Mathf.Max(0f, actual - _manoAnterior);
        _manoAnterior = actual;

        var c = h.Mano == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        bool sinMovimiento = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c) > 0.7f || Input.GetKey(KeyCode.G);
        if (sinMovimiento) jalon = Mathf.Max(jalon, Time.deltaTime * 0.22f); // alternativa para quien no puede jalar

        if (jalon <= 0f) return;
        Progreso = Mathf.Clamp01(Progreso + jalon / Mathf.Max(0.1f, recorridoNecesario));
        _temblor = Mathf.Max(_temblor, 0.5f);
        EntradaVR.Vibrar(h.Mano == 0, 0.2f + 0.5f * Progreso, 0.05f, 0.4f);

        // Cada cuarto del recorrido la puerta cruje y suelta astillas.
        if (Progreso >= _hitoProgreso + 0.25f && Progreso < 1f)
        {
            _hitoProgreso += 0.25f;
            _temblor = 1.4f;
            SonidosVR.ReproducirEn(SonidosVR.Metal, _puntoPalanca, 0.9f, 15f);
            SonidosVR.ReproducirEn(SonidosVR.Madera, _puntoPalanca, 0.8f, 15f);
            _astillas.Emit(8);
            EntradaVR.Vibrar(h.Mano == 0, 0.9f, 0.15f, 0.5f);
        }

        if (Progreso >= 1f) Ceder(h);
    }

    void Ceder(HerramientaRescate h)
    {
        Forzada = true;
        Enganchada = false;
        h.Desenganchar();
        _anillo.gameObject.SetActive(false);
        _astillas.Emit(30);
        SonidosVR.ReproducirEn(SonidosVR.Madera, _puntoPalanca, 1f, 25f);
        SonidosVR.ReproducirEn(SonidosVR.Metal, _puntoPalanca, 1f, 25f);
        SonidosVR.ReproducirEn(SonidosVR.Puerta, _puntoPalanca, 1f, 25f);
        EntradaVR.VibrarAmbas(1f, 0.3f, 0.5f);
        alForzar?.Invoke();
    }

    void Aplicar()
    {
        transform.localRotation = _rotCerrada * Quaternion.Euler(0f, _angulo, 0f);
    }
}
