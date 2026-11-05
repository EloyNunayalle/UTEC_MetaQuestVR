using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Incendio dentro de la casa de la Mision 3. Hay que apagarlo con el extintor CO2 ANTES de poder
/// forzar la puerta: el foco del pasillo corta el paso hasta la habitacion de la persona.
/// - Focos: FuegoElectrico (el mismo fuego de la Mision 2: se apaga con CO2, no con agua), con llamas,
///   humo negro, crepitar y luz. Se reavivan si los dejas a medias.
/// - Ambiente mientras arde: capa de humo bajo el techo, niebla oscura al estar adentro (baja la
///   visibilidad), luces de la casa casi apagadas, hollin en paredes y techo, sofa quemado, escombros,
///   chispas, alarma de humo sonando y humo que sale por la puerta. El calor se siente en los controles.
/// Al apagarlo todo, el humo y la niebla se disipan poco a poco y vuelve la luz.
/// Pensado para Quest: una luz por foco sin sombras y pocas particulas grandes.
/// </summary>
public class IncendioCasa : MonoBehaviour
{
    [Header("Focos (coordenadas locales de este objeto)")]
    public Vector3 focoPasillo = new Vector3(0f, 1.05f, -6.55f);
    public Vector3 focoSala = new Vector3(-3.75f, 1.55f, -4.4f);

    [Header("Humo")]
    [Tooltip("Densidad de la niebla (Exp2) estando adentro con el fuego activo.")]
    public float densidadNiebla = 0.14f;
    public Color colorHumo = new Color(0.13f, 0.12f, 0.11f);
    [Tooltip("Intensidad de las luces de la casa mientras hay fuego (la luz de la instalacion falla).")]
    [Range(0f, 1f)] public float luzDuranteFuego = 0.3f;

    public int Total => _focos.Count;
    public int Apagados { get; private set; }
    public bool Apagado { get; private set; }

    public event Action alApagarFoco;
    public event Action alApagarTodo;

    readonly List<FuegoElectrico> _focos = new List<FuegoElectrico>();
    readonly List<GameObject> _bloqueos = new List<GameObject>();
    readonly List<Light> _lucesCasa = new List<Light>();
    readonly List<float> _intensidades = new List<float>();
    Transform _cabeza;
    EscenarioMision3 _escenario;
    ParticleSystem _capaHumo, _humoPuerta, _brasas;
    Material _matHollin;
    float _proximoPitido, _proximoCalor, _proximoAvisoCalor, _kHumo, _nieblaActual, _luzActual = 1f;
    bool _encendido, _nieblaOriginal, _guardoNiebla;
    Color _colorOriginal;
    FogMode _modoOriginal;
    float _densidadOriginal;

    /// <summary>Enciende el incendio (o lo deja apagado si ya se habia apagado antes de recargar la escena).</summary>
    public void Iniciar(Transform cabeza, EscenarioMision3 escenario, bool yaApagado)
    {
        if (_encendido) return;
        _encendido = true;
        _cabeza = cabeza;
        _escenario = escenario;
        Apagado = yaApagado;
        CrearDanos(); // el hollin y los escombros quedan aunque el fuego ya este apagado
        if (yaApagado) return;

        BuscarLuces();
        var pasillo = CrearFoco("Foco_Pasillo", focoPasillo, 1.7f, 1.45f, 0.95f, 6f);
        CrearBloqueo(pasillo.transform);
        CrearFoco("Foco_Sala", focoSala, 1.2f, 1.2f, 0.8f, 5f);

        _capaHumo = CrearCapaHumo();
        _humoPuerta = CrearHumoPuerta();
        _brasas = CrearBrasas();
        _kHumo = 1f;
        foreach (var f in _focos) f.Encender();
    }

    /// <summary>La casa es el padre de este objeto; el mobiliario y las luces cuelgan de "Interior".</summary>
    Transform EnCasa(string ruta) => transform.parent != null ? transform.parent.Find(ruta) : null;

    /// <summary>Lo que el fuego deja: hollin, escombros y el sofa quemado.</summary>
    void CrearDanos()
    {
        _matHollin = new Material(ZonaFuego.MaterialParticula()) { color = new Color(0f, 0f, 0f, 0.8f) };
        Hollin(new Vector3(0.92f, 2.2f, -6.55f), new Vector3(0f, 90f, 0f), 2.4f);
        Hollin(new Vector3(-0.92f, 2.2f, -6.55f), new Vector3(0f, -90f, 0f), 2.4f);
        Hollin(new Vector3(0f, 3.88f, -6.55f), new Vector3(-90f, 0f, 0f), 2.8f);
        Hollin(new Vector3(-4.77f, 2.4f, -4.4f), new Vector3(0f, -90f, 0f), 2.6f);
        Hollin(new Vector3(-3.75f, 3.88f, -4.4f), new Vector3(-90f, 0f, 0f), 2.6f);
        CrearEscombros();
        var sofa = EnCasa("Interior/Sala/Sofa");
        if (sofa != null) VisualesVR.Tenir(sofa.GetComponentsInChildren<Renderer>(), new Color(0.12f, 0.1f, 0.09f), 0.85f);
    }

    void BuscarLuces()
    {
        var interior = EnCasa("Interior");
        if (interior == null) return;
        foreach (var l in interior.GetComponentsInChildren<Light>())
        {
            _lucesCasa.Add(l);
            _intensidades.Add(l.intensity);
        }
    }

    FuegoElectrico CrearFoco(string nombre, Vector3 pos, float ancho, float escala, float radio, float tiempo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        var f = go.AddComponent<FuegoElectrico>();
        f.anchoFoco = ancho;
        f.escala = escala;
        f.radio = radio;
        f.tiempoExtincion = tiempo;
        f.aporteElectrico = false;
        f.extincionPermitida = true;
        f.ignorarColisiones = go.transform;
        f.alExtinguirse += AlExtinguirseFoco;
        _focos.Add(f);
        return f;
    }

    /// <summary>El fuego corta el pasillo: no se puede pasar hasta apagarlo.</summary>
    void CrearBloqueo(Transform foco)
    {
        var go = new GameObject("Bloqueo_Fuego");
        go.transform.SetParent(foco, false);
        go.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        go.AddComponent<BoxCollider>().size = new Vector3(1.88f, 2.1f, 0.6f);
        _bloqueos.Add(go);
    }

    /// <summary>Tablas y restos quemados en el piso (no bloquean: quedan despues de apagar el fuego).</summary>
    void CrearEscombros()
    {
        var foco = new GameObject("Escombros").transform;
        foco.SetParent(transform, false);
        foco.localPosition = focoPasillo;
        var quemado = VisualesVR.MaterialOpaco(new Color(0.07f, 0.06f, 0.055f));
        var azar = new System.Random(7);
        for (int i = 0; i < 7; i++)
        {
            float x = (float)azar.NextDouble() * 1.5f - 0.75f, z = (float)azar.NextDouble() * 0.8f - 0.4f;
            var tabla = VisualesVR.Primitiva(PrimitiveType.Cube, "Escombro", foco, quemado);
            tabla.transform.localPosition = new Vector3(x, 0.02f + i * 0.012f, z);
            tabla.transform.localRotation = Quaternion.Euler(0f, (float)azar.NextDouble() * 180f, (float)azar.NextDouble() * 8f);
            tabla.transform.localScale = new Vector3(0.7f, 0.035f, 0.12f);
        }
    }

    void Hollin(Vector3 pos, Vector3 euler, float tam)
    {
        var q = VisualesVR.Primitiva(PrimitiveType.Quad, "Hollin", transform, _matHollin);
        q.transform.localPosition = pos;
        q.transform.localRotation = Quaternion.Euler(euler);
        q.transform.localScale = new Vector3(tam, tam, 1f);
    }

    ParticleSystem Sistema(string nombre, Vector3 pos)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ZonaFuego.MaterialParticula();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    /// <summary>Humo acumulado bajo el techo. Pocas particulas grandes: el costo es el sobredibujado.</summary>
    ParticleSystem CrearCapaHumo()
    {
        var ps = Sistema("Capa_Humo", new Vector3(1f, 3.35f, -6f));
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(colorHumo.r, colorHumo.g, colorHumo.b, 0.38f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 42;
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 4.5f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(10.5f, 0.5f, 10.5f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ps.Play();
        return ps;
    }

    /// <summary>Humo que sale por arriba de la puerta de entrada: se ve desde la calle.</summary>
    ParticleSystem CrearHumoPuerta()
    {
        var ps = Sistema("Humo_Puerta", new Vector3(0f, 3.1f, 0.1f));
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.1f, 0.095f, 0.09f, 0.45f);
        main.gravityModifier = -0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 22;
        var em = ps.emission;
        em.rateOverTime = 4f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(0.9f, 0.1f, 0.1f); // el dintel de la puerta; emite hacia la calle
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 3f));
        ps.Play();
        return ps;
    }

    /// <summary>Chispas de madera que suben desde el foco del pasillo.</summary>
    ParticleSystem CrearBrasas()
    {
        var ps = Sistema("Brasas", focoPasillo + Vector3.up * 0.3f);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.4f, 0.08f));
        main.gravityModifier = -0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var em = ps.emission;
        em.rateOverTime = 14f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(1.6f, 0.1f, 0.5f);
        forma.rotation = new Vector3(-90f, 0f, 0f);
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.7f;
        ruido.frequency = 1.5f;
        ruido.quality = ParticleSystemNoiseQuality.Low;
        ps.Play();
        return ps;
    }

    void AlExtinguirseFoco()
    {
        Apagados = 0;
        foreach (var f in _focos) if (f.Extinguido) Apagados++;

        // Los focos apagados ya no cortan el paso.
        for (int i = _bloqueos.Count - 1; i >= 0; i--)
        {
            var padre = _bloqueos[i].transform.parent;
            var f = padre != null ? padre.GetComponent<FuegoElectrico>() : null;
            if (f == null || !f.Extinguido) continue;
            Destroy(_bloqueos[i]);
            _bloqueos.RemoveAt(i);
        }

        if (Apagados >= _focos.Count && !Apagado)
        {
            Apagado = true;
            if (_brasas != null) { var em = _brasas.emission; em.rateOverTime = 0f; }
            if (_humoPuerta != null) { var em = _humoPuerta.emission; em.rateOverTime = 0f; }
            if (_capaHumo != null) { var em = _capaHumo.emission; em.rateOverTime = 0f; }
            alApagarTodo?.Invoke();
        }
        else alApagarFoco?.Invoke();
    }

    /// <summary>Foco encendido mas cercano a un punto (para la flecha).</summary>
    public FuegoElectrico FocoActivoMasCercano(Vector3 desde)
    {
        FuegoElectrico mejor = null;
        float mejorD = float.MaxValue;
        foreach (var f in _focos)
        {
            if (f == null || !f.Encendido) continue;
            float d = (f.Centro - desde).sqrMagnitude;
            if (d < mejorD) { mejorD = d; mejor = f; }
        }
        return mejor;
    }

    void Update()
    {
        if (!_encendido || _cabeza == null || _escenario == null) return;

        // El humo se va disipando recien despues de apagar el fuego.
        _kHumo = Apagado ? Mathf.MoveTowards(_kHumo, 0f, Time.deltaTime / 18f) : 1f;
        ActualizarNiebla();
        ActualizarLuces();

        if (Apagado) return;

        if (Time.time >= _proximoPitido)
        {
            _proximoPitido = Time.time + 2.4f;
            SonidosVR.ReproducirEn(SonidosVR.Pitido, transform.TransformPoint(new Vector3(0f, 3.7f, -3f)), 0.7f, 22f);
        }

        // Calor: vibracion al acercarse a un foco y un aviso si te quedas pegado.
        var cerca = FocoActivoMasCercano(_cabeza.position);
        if (cerca == null) return;
        Vector3 d = cerca.Centro - _cabeza.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 2.4f && Time.time >= _proximoCalor)
        {
            _proximoCalor = Time.time + 0.35f;
            EntradaVR.VibrarAmbas(Mathf.Lerp(0.3f, 0.05f, dist / 2.4f), 0.15f, 0.2f);
        }
        if (dist < 1.3f && Time.time >= _proximoAvisoCalor)
        {
            _proximoAvisoCalor = Time.time + 7f;
            JuegoBomberos.Instancia?.Aviso("¡Demasiado calor!\n<size=55%>Retrocede y ataca la base del fuego con el CO2</size>", JuegoBomberos.TipoAviso.Error, 2.5f);
        }
    }

    /// <summary>Niebla oscura solo al estar adentro. Al salir o terminar, se restaura la original.</summary>
    void ActualizarNiebla()
    {
        bool dentro = _kHumo > 0f && _escenario.Dentro(_cabeza.position);
        float objetivo = dentro ? densidadNiebla * _kHumo : 0f;
        _nieblaActual = Mathf.MoveTowards(_nieblaActual, objetivo, Time.deltaTime * 0.15f);

        if (_nieblaActual > 0.0005f)
        {
            if (!_guardoNiebla)
            {
                _guardoNiebla = true;
                _nieblaOriginal = RenderSettings.fog;
                _colorOriginal = RenderSettings.fogColor;
                _modoOriginal = RenderSettings.fogMode;
                _densidadOriginal = RenderSettings.fogDensity;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = colorHumo;
            RenderSettings.fogDensity = _nieblaActual;
        }
        else RestaurarNiebla();
    }

    void RestaurarNiebla()
    {
        if (!_guardoNiebla) return;
        _guardoNiebla = false;
        RenderSettings.fog = _nieblaOriginal;
        RenderSettings.fogColor = _colorOriginal;
        RenderSettings.fogMode = _modoOriginal;
        RenderSettings.fogDensity = _densidadOriginal;
    }

    void ActualizarLuces()
    {
        float objetivo = Apagado ? Mathf.Lerp(1f, luzDuranteFuego, _kHumo) : luzDuranteFuego;
        if (Mathf.Approximately(objetivo, _luzActual)) return;
        _luzActual = objetivo;
        for (int i = 0; i < _lucesCasa.Count; i++)
            if (_lucesCasa[i] != null) _lucesCasa[i].intensity = _intensidades[i] * _luzActual;
    }

    void OnDestroy()
    {
        RestaurarNiebla(); // la ciudad no debe quedar con niebla si la escena se descarga
    }
}
