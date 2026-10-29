using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fuego del equipo electrico (Mision 2). Independiente de ZonaFuego (que es de la Mision 1 y se apaga
/// con agua): este fuego se combate con CO2 y su comportamiento lo controla el estado electrico.
/// - <see cref="aporteElectrico"/> = true (instalacion energizada): el CO2 lo baja un momento, pero
///   no lo apaga y se reaviva enseguida.
/// - <see cref="extincionPermitida"/> = true (corte verificado): el CO2 lo apaga de forma progresiva.
/// - Al extinguirse, las llamas se detienen y el humo residual se va apagando poco a poco.
/// Las particulas se crean una sola vez y solo se cambia su emision (sin instanciar/destruir).
/// </summary>
public class FuegoElectrico : MonoBehaviour
{
    [Tooltip("Segundos de CO2 directo (a potencia 1) para apagarlo cuando ya esta permitido.")]
    public float tiempoExtincion = 5f;
    [Tooltip("Vida minima a la que baja con CO2 si todavia no se puede extinguir.")]
    public float vidaMinimaBloqueada = 0.35f;
    [Tooltip("Radio (m) del foco: el chorro de CO2 cuenta si apunta dentro de este radio.")]
    public float radio = 0.55f;
    public float anchoFoco = 0.9f;

    /// <summary>La instalacion sigue alimentando el fuego (se reaviva rapido).</summary>
    public bool aporteElectrico = true;
    /// <summary>El CO2 puede apagarlo definitivamente (corte confirmado).</summary>
    public bool extincionPermitida;

    public float Vida { get; private set; }
    public bool Encendido { get; private set; }
    public bool Extinguido => !Encendido && _fueEncendido;
    /// <summary>Quedan llamas o humo visibles (para la verificacion final).</summary>
    public bool HumoResidual => _humoResidual > 0.05f;
    public Vector3 Centro => transform.position + Vector3.up * 0.25f;
    /// <summary>Transform cuyos colliders no tapan el chorro de CO2 (el propio equipo).</summary>
    public Transform ignorarColisiones;

    /// <summary>Se aplico CO2 pero el fuego no se puede extinguir todavia.</summary>
    public event Action alCO2Bloqueado;
    public event Action alExtinguirse;

    static readonly List<FuegoElectrico> _activos = new List<FuegoElectrico>();
    public static IReadOnlyList<FuegoElectrico> Activos => _activos;

    ParticleSystem _llamas, _humo;
    Light _luz;
    AudioSource _crepitar;
    float _ultimoCO2 = -100f, _humoResidual, _semilla;
    bool _fueEncendido, _creado;

    void OnEnable() { if (!_activos.Contains(this)) _activos.Add(this); }
    void OnDisable() { _activos.Remove(this); }

    void Crear()
    {
        if (_creado) return;
        _creado = true;
        _semilla = UnityEngine.Random.value * 100f;
        _llamas = CrearLlamas();
        _humo = CrearHumo();

        _luz = new GameObject("Luz_Fuego").AddComponent<Light>();
        _luz.transform.SetParent(transform, false);
        _luz.transform.localPosition = new Vector3(0f, 0.5f, 0.3f);
        _luz.type = LightType.Point;
        _luz.color = new Color(1f, 0.55f, 0.2f);
        _luz.range = 5f;
        _luz.shadows = LightShadows.None;
        _luz.enabled = false;

        _crepitar = SonidosVR.CrearBucle(transform, SonidosVR.Fuego, 0.7f, 18f);
    }

    ParticleSystem Sistema(string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ZonaFuego.MaterialParticula();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    ParticleSystem CrearLlamas()
    {
        var ps = Sistema("Llamas");
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.3f), new Color(1f, 0.4f, 0.06f));
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 110;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(anchoFoco, 0.15f, 0.35f);
        forma.rotation = new Vector3(-90f, 0f, 0f); // emite hacia arriba

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f), new GradientColorKey(new Color(0.5f, 0.07f, 0.02f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.5f;
        ruido.frequency = 1.4f;
        ruido.quality = ParticleSystemNoiseQuality.Low;

        ps.Play();
        return ps;
    }

    ParticleSystem CrearHumo()
    {
        var ps = Sistema("Humo");
        ps.transform.localPosition = Vector3.up * 0.55f;
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.08f, 0.075f, 0.07f, 0.6f); // humo negro de plastico y aislante quemado
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(anchoFoco * 0.7f, 0.15f, 0.2f);
        forma.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.8f));

        // El humo se acumula bajo el techo: se frena al subir.
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 0.6f;
        lim.dampen = 0.15f;

        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------

    public void Encender()
    {
        Crear();
        Vida = 1f;
        Encendido = true;
        _fueEncendido = true;
        _humoResidual = 1f;
        _ultimoCO2 = -100f;
        _luz.enabled = true;
        if (!_crepitar.isPlaying) _crepitar.Play();
    }

    /// <summary>Apaga todo al instante (reinicio o fin de la mision).</summary>
    public void Apagar()
    {
        Crear();
        Encendido = false;
        _fueEncendido = false;
        Vida = 0f;
        _humoResidual = 0f;
        _luz.enabled = false;
        _crepitar.Stop();
        var em = _llamas.emission; em.rateOverTime = 0f;
        var emH = _humo.emission; emH.rateOverTime = 0f;
    }

    /// <summary>
    /// CO2 sobre el foco durante dt segundos (potencia 0-1 segun caudal y distancia).
    /// Devuelve true si el fuego bajo; false si ya estaba en el minimo permitido (bloqueado).
    /// </summary>
    public bool AplicarCO2(float potencia, float dt)
    {
        if (!Encendido) return false;
        _ultimoCO2 = Time.time;
        float minimo = extincionPermitida ? 0f : vidaMinimaBloqueada;
        float antes = Vida;
        Vida = Mathf.Max(minimo, Vida - potencia * dt / Mathf.Max(0.5f, tiempoExtincion));
        if (!extincionPermitida && Vida <= minimo + 0.001f) alCO2Bloqueado?.Invoke();
        if (Vida <= 0f) Extinguir();
        return Vida < antes;
    }

    void Extinguir()
    {
        Encendido = false;
        Vida = 0f;
        _luz.enabled = false;
        _crepitar.Stop();
        // Bocanada blanca del CO2 y vapor; el humo residual se apaga en Update.
        _humo.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.85f, 0.86f, 0.88f, 0.55f), applyShapeToPosition = true }, 10);
        SonidosVR.ReproducirEn(SonidosVR.Descarga, transform.position, 0.5f, 12f);
        alExtinguirse?.Invoke();
    }

    void Update()
    {
        if (!_creado) return;

        if (Encendido)
        {
            // Reavivado: rapido mientras la instalacion sigue energizada, lento si no.
            float espera = aporteElectrico ? 0.8f : 3f;
            float velocidad = aporteElectrico ? 0.45f : 0.06f;
            if (Vida < 1f && Time.time - _ultimoCO2 > espera)
                Vida = Mathf.Min(1f, Vida + velocidad * Time.deltaTime);
            _humoResidual = 1f;
        }
        else
        {
            // Humo residual que disminuye poco a poco (~12 s).
            _humoResidual = Mathf.MoveTowards(_humoResidual, 0f, Time.deltaTime / 12f);
        }

        float v = Encendido ? Mathf.Lerp(0.12f, 1f, Vida) : 0f;
        var em = _llamas.emission;
        em.rateOverTime = 70f * v;
        var main = _llamas.main;
        float f = Mathf.Lerp(0.5f, 1f, Vida);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f * f, 0.55f * f);

        var emH = _humo.emission;
        emH.rateOverTime = Encendido ? 10f * Mathf.Lerp(0.4f, 1f, Vida) : 6f * _humoResidual * _humoResidual;

        if (Encendido)
        {
            float parpadeo = Mathf.PerlinNoise(Time.time * 7f, _semilla) * 0.6f + 0.7f;
            _luz.intensity = 1.8f * v * parpadeo;
            _crepitar.volume = 0.7f * v;
        }
    }
}
