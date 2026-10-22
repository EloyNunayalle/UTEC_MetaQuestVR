using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chorro de agua de la boquilla.
/// - ParticleSystem en espacio de mundo, con gravedad y colision con el mundo (World Collision);
///   al chocar, un sub-emisor genera salpicaduras.
/// - Control con el control que sostiene la boquilla:
///     Gatillo (analogico) = valvula / caudal (0 a 100 %).
///     A / X               = presion: BAJA -> MEDIA -> ALTA.
///     B / Y               = patron: CHORRO directo / NIEBLA (abanico).
///   Teclado (editor): F = agua, T = presion, N = patron.
/// - Expone <see cref="Empuje"/> (0-1) para el retroceso, la vibracion y la rigidez de la manguera.
/// - Evento <see cref="AlMojar"/>: para apagar fuego u otras reacciones al agua.
/// </summary>
public class LanzaAgua : MonoBehaviour
{
    [Header("Presion (velocidad de salida en m/s)")]
    public float velocidadMinima = 7f;
    public float velocidadMaxima = 22f;
    public float[] nivelesPresion = { 0.4f, 0.7f, 1f };
    public string[] nombresPresion = { "BAJA", "MEDIA", "ALTA" };

    [Header("Caudal (particulas por segundo a valvula abierta)")]
    public float particulasMinimas = 120f;
    public float particulasMaximas = 320f;
    [Tooltip("Que tan rapido abre/cierra la valvula (1/s).")]
    public float velocidadValvula = 4f;

    [Header("Fisica del agua")]
    public float gravedad = 0.9f;
    public float tiempoVida = 1.8f;
    [Tooltip("Angulo del cono en modo chorro y en modo niebla.")]
    public float anguloChorro = 1.5f;
    public float anguloNiebla = 22f;
    [Tooltip("Capas con las que choca el agua (por defecto todo menos la manguera).")]
    public LayerMask chocaCon = ~(1 << MangueraBomberos.CapaManguera);

    /// <summary>Caudal real 0-1 (valvula, suavizado).</summary>
    public float Caudal { get; private set; }
    public float Presion => nivelesPresion[Mathf.Clamp(_nivel, 0, nivelesPresion.Length - 1)];
    public string NombrePresion => nombresPresion[Mathf.Clamp(_nivel, 0, nombresPresion.Length - 1)];
    public bool Niebla { get; private set; }
    /// <summary>Fuerza relativa del chorro (caudal x presion): retroceso, vibracion, rigidez.</summary>
    public float Empuje => Caudal * Presion * (Niebla ? 0.5f : 1f);

    /// <summary>(objeto mojado, punto de impacto). Se dispara como maximo ~10 veces por segundo por objeto.</summary>
    public event Action<GameObject, Vector3> AlMojar;

    MangueraBomberos _manguera;
    ParticleSystem _ps, _salpicadura;
    AudioSource _sonido;
    int _nivel = 1;
    readonly List<ParticleCollisionEvent> _eventos = new List<ParticleCollisionEvent>();
    readonly Dictionary<GameObject, float> _ultimoMojado = new Dictionary<GameObject, float>();

    static Material _material;

    public void Inicializar(MangueraBomberos manguera)
    {
        _manguera = manguera;
        _ps = CrearChorro();
        _salpicadura = CrearSalpicadura(_ps.transform);

        var sub = _ps.subEmitters;
        sub.enabled = true;
        sub.AddSubEmitter(_salpicadura, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, 0.35f);

        _sonido = gameObject.AddComponent<AudioSource>();
        _sonido.clip = SonidosVR.Agua;
        _sonido.loop = true;
        _sonido.playOnAwake = false;
        _sonido.spatialBlend = 1f;
        _sonido.minDistance = 1f;
        _sonido.maxDistance = 20f;
        _sonido.rolloffMode = AudioRolloffMode.Linear;
        _sonido.volume = 0f;

        _ps.Play();
    }

    // ------------------------------------------------------------------
    // Construccion de los sistemas de particulas
    // ------------------------------------------------------------------

    ParticleSystem CrearChorro()
    {
        // El ParticleSystem va en este mismo objeto para recibir OnParticleCollision.
        var ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = tiempoVida;
        main.startSpeed = velocidadMinima;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startColor = new Color(0.78f, 0.9f, 1f, 0.6f);
        main.gravityModifier = gravedad;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // el agua no sigue a la boquilla al moverla
        main.maxParticles = 700;

        var emision = ps.emission;
        emision.rateOverTime = 0f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone; // el cono apunta al +Z local (hacia donde mira la boquilla)
        forma.angle = anguloChorro;
        forma.radius = 0.012f;

        // El chorro se abre y se desvanece al avanzar.
        var tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.6f));
        var colorVida = ps.colorOverLifetime;
        colorVida.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorVida.color = g;

        var col = ps.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision3D;
        col.collidesWith = chocaCon;
        col.dampen = 0.45f;
        col.bounce = 0.12f;
        col.lifetimeLoss = 0.35f;
        col.minKillSpeed = 0.4f;
        col.radiusScale = 0.5f;
        col.quality = ParticleSystemCollisionQuality.Medium; // buen balance en Quest
        col.maxCollisionShapes = 48;
        col.enableDynamicColliders = false;
        col.sendCollisionMessages = true;

        var r = GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; // gotas alargadas segun su velocidad
        r.velocityScale = 0.035f;
        r.lengthScale = 1.3f;
        r.sharedMaterial = MaterialAgua();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    ParticleSystem CrearSalpicadura(Transform padre)
    {
        var go = new GameObject("Salpicadura");
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(0.85f, 0.94f, 1f, 0.7f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 4) });

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Hemisphere;
        forma.radius = 0.03f;

        var colorVida = ps.colorOverLifetime;
        colorVida.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        colorVida.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = MaterialAgua();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    /// <summary>Material de gota: circulo suave generado por codigo (Sprites/Default siempre va en el build).</summary>
    static Material MaterialAgua()
    {
        if (_material != null) return _material;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Gota" };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        _material = VisualesVR.MaterialSinLuz(Color.white);
        _material.mainTexture = tex;
        return _material;
    }

    // ------------------------------------------------------------------
    // Control
    // ------------------------------------------------------------------

    void Update()
    {
        if (_ps == null) return;
        int mano = _manguera != null ? _manguera.Mano : -1;
        float valvula = 0f;

        // Solo sale agua mientras alguien sostiene la boquilla (valvula de "hombre muerto").
        if (mano >= 0)
        {
            var c = mano == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            valvula = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c);
            if (Input.GetKey(KeyCode.F)) valvula = 1f;

            if (OVRInput.GetDown(OVRInput.Button.One, c) || Input.GetKeyDown(KeyCode.T))
            {
                _nivel = (_nivel + 1) % nivelesPresion.Length;
                SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position, 0.8f);
                EntradaVR.Vibrar(mano == 0, 0.4f, 0.06f, 0.9f);
                JuegoBomberos.Instancia?.Aviso("Presión " + NombrePresion, JuegoBomberos.TipoAviso.Info, 1f);
            }
            if (OVRInput.GetDown(OVRInput.Button.Two, c) || Input.GetKeyDown(KeyCode.N))
            {
                Niebla = !Niebla;
                SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position, 0.8f);
                EntradaVR.Vibrar(mano == 0, 0.4f, 0.06f, 0.9f);
                JuegoBomberos.Instancia?.Aviso(Niebla ? "Niebla (abanico)" : "Chorro directo", JuegoBomberos.TipoAviso.Info, 1f);
            }
        }
        if (valvula < 0.05f) valvula = 0f;

        // La valvula abre/cierra con una pequena inercia (golpe de ariete suave).
        Caudal = Mathf.MoveTowards(Caudal, valvula, Time.deltaTime * velocidadValvula);

        float presion = Presion;
        var main = _ps.main;
        main.startSpeed = Mathf.Lerp(velocidadMinima, velocidadMaxima, presion) * Mathf.Lerp(0.65f, 1f, Caudal) * (Niebla ? 0.5f : 1f);
        main.startSize = Niebla ? new ParticleSystem.MinMaxCurve(0.1f, 0.2f) : new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startLifetime = Niebla ? tiempoVida * 0.55f : tiempoVida;

        var emision = _ps.emission;
        emision.rateOverTime = Caudal * Mathf.Lerp(particulasMinimas, particulasMaximas, presion) * (Niebla ? 1.3f : 1f);

        var forma = _ps.shape;
        forma.angle = Niebla ? anguloNiebla : Mathf.Lerp(anguloChorro * 2f, anguloChorro, presion);

        // Sonido: mas fuerte con el caudal, mas agudo con la presion.
        if (Caudal > 0.01f)
        {
            if (!_sonido.isPlaying) _sonido.Play();
            _sonido.volume = Caudal * 0.8f;
            _sonido.pitch = 0.8f + 0.5f * presion + (Niebla ? 0.25f : 0f);
        }
        else if (_sonido.isPlaying) _sonido.Stop();
    }

    void OnParticleCollision(GameObject otro)
    {
        if (AlMojar == null || otro == null) return;
        if (_ultimoMojado.TryGetValue(otro, out float t) && Time.time - t < 0.1f) return;
        int n = _ps.GetCollisionEvents(otro, _eventos);
        if (n == 0) return;
        _ultimoMojado[otro] = Time.time;
        AlMojar(otro, _eventos[0].intersection);
    }

    void OnDisable()
    {
        Caudal = 0f;
        if (_sonido != null) _sonido.Stop();
    }
}
