using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Equipo electrico que se incendia (Mision 2): usa el modelo "Ac units" del proyecto.
/// - El FBX trae 4 equipos en fila: se deja visible solo <see cref="unidadVisible"/> y se centra
///   sobre este objeto (base en el piso). Si el modelo no esta (archivo local que falta) se arma
///   un equipo simple con primitivas.
/// - Crea la caja de conexion (origen del incendio), el cable que la une al tablero y el foco
///   <see cref="FuegoElectrico"/>.
/// - Mientras la instalacion esta ENERGIZADA muestra chispas (Particle System), arcos electricos
///   (LineRenderer) y un zumbido; al cortar la alimentacion desaparecen al instante.
/// - Al extinguirse queda chamuscado (tinte por MaterialPropertyBlock: no modifica el asset).
/// Convencion: origen en el piso, +Z hacia la sala.
/// </summary>
public class MaquinaElectrica : MonoBehaviour
{
    public PanelElectrico panel;
    [Tooltip("Instancia del modelo del equipo (hijo).")]
    public Transform modelo;
    [Tooltip("Nombre del equipo del FBX que se deja visible (el FBX trae Ac1..Ac4).")]
    public string unidadVisible = "Ac3";
    [Tooltip("Altura del techo sobre el piso del cuarto (para tender el cable).")]
    public float alturaTecho = 2.75f;

    public FuegoElectrico Fuego { get; private set; }
    /// <summary>La mision encendio el incidente (el equipo esta en falla).</summary>
    public bool EnFalla { get; set; }
    public bool EfectosElectricos => EnFalla && panel != null && panel.Energizado;
    public Vector3 PuntoConexion => _conexion != null ? _conexion.position : transform.position + Vector3.up;
    public Vector3 Centro => _limitesLocal.center + transform.position;
    public Vector3 PuntoMirar => transform.TransformPoint(new Vector3(0f, _alto * 0.75f, 0f));

    Transform _conexion;
    ParticleSystem _chispas;
    readonly List<LineRenderer> _arcos = new List<LineRenderer>();
    Light _luzArco;
    AudioSource _zumbido;
    Renderer[] _renders;
    Bounds _limitesLocal;
    float _alto = 1f, _largo = 1.8f, _fondo = 0.7f;
    float _proximoArco, _proximaChispa, _chamuscado;
    bool _preparado, _efectosPrev;

    public void Preparar()
    {
        if (_preparado) return;
        _preparado = true;

        AcomodarModelo();
        CrearConexionYCable();
        CrearEfectos();

        var go = new GameObject("Foco_Incendio");
        go.transform.SetParent(transform, false);
        // Sobre la caja de conexion y la parte alta del equipo: ahi se concentra el fuego.
        go.transform.localPosition = new Vector3(_largo * 0.3f, _alto * 0.8f, _fondo * 0.3f);
        Fuego = go.AddComponent<FuegoElectrico>();
        Fuego.anchoFoco = Mathf.Clamp(_largo * 0.45f, 0.6f, 1.1f);
        Fuego.ignorarColisiones = transform;
        Fuego.Apagar();
    }

    void AcomodarModelo()
    {
        if (modelo != null)
        {
            // Deja visible solo una unidad (las demas se desactivan, no se borran).
            Transform elegida = null;
            foreach (Transform hijo in modelo)
                if (hijo.name == unidadVisible) elegida = hijo;
            if (elegida != null)
                foreach (Transform hijo in modelo)
                    if (hijo != elegida) hijo.gameObject.SetActive(false);
        }

        if (modelo == null || !VisualesVR.Limites(modelo, out Bounds b))
        {
            ModeloReemplazo();
            VisualesVR.Limites(modelo, out b);
        }

        // Centra la unidad: base en el piso, centrada en este objeto.
        Vector3 baseCentro = new Vector3(b.center.x, b.min.y, b.center.z);
        modelo.position += transform.position - baseCentro;
        VisualesVR.Limites(modelo, out b);
        _renders = modelo.GetComponentsInChildren<Renderer>();

        // Limites en el espacio de este objeto (rotacion en Y solamente).
        Vector3 c = transform.InverseTransformPoint(b.center);
        Vector3 s = transform.InverseTransformVector(b.size);
        _limitesLocal = new Bounds(transform.rotation * c, b.size);
        _largo = Mathf.Abs(s.x);
        _alto = Mathf.Abs(s.y);
        _fondo = Mathf.Abs(s.z);

        // Collider simple del equipo (bloquea al jugador y frena el CO2 contra la carcasa).
        var col = gameObject.GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.center = c;
        col.size = new Vector3(_largo, _alto, _fondo);
    }

    void ModeloReemplazo()
    {
        var raiz = new GameObject("Equipo_Reemplazo").transform;
        raiz.SetParent(transform, false);
        modelo = raiz;
        var gris = VisualesVR.MaterialOpaco(new Color(0.7f, 0.72f, 0.7f));
        var cuerpo = VisualesVR.Primitiva(PrimitiveType.Cube, "Carcasa", raiz, gris);
        cuerpo.transform.localScale = new Vector3(1.6f, 1f, 0.65f);
        cuerpo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var rejilla = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Ventilador", raiz, VisualesVR.MaterialOpaco(new Color(0.15f, 0.15f, 0.15f)));
        rejilla.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rejilla.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
        rejilla.transform.localPosition = new Vector3(-0.35f, 0.5f, 0.33f);
    }

    void CrearConexionYCable()
    {
        // Caja de conexion en la cara que da a la sala, cerca de un extremo y arriba.
        var caja = VisualesVR.Primitiva(PrimitiveType.Cube, "Caja_Conexion", transform, VisualesVR.MaterialOpaco(new Color(0.32f, 0.33f, 0.35f)));
        caja.transform.localPosition = new Vector3(_largo * 0.3f, _alto * 0.72f, _fondo * 0.5f + 0.07f);
        caja.transform.localScale = new Vector3(0.24f, 0.28f, 0.12f);
        var franja = VisualesVR.Primitiva(PrimitiveType.Cube, "Franja_Peligro", caja.transform, VisualesVR.MaterialOpaco(new Color(1f, 0.75f, 0.05f)));
        franja.transform.localPosition = new Vector3(0f, 0.35f, 0.51f);
        franja.transform.localScale = new Vector3(1f, 0.15f, 0.02f);
        _conexion = new GameObject("Punto_Conexion").transform;
        _conexion.SetParent(transform, false);
        _conexion.localPosition = caja.transform.localPosition + new Vector3(0f, 0f, 0.07f);

        if (panel == null) return;
        // Cable: sale del tablero, sube al techo, corre por el techo y baja a la caja de conexion.
        float yTecho = transform.position.y + alturaTecho;
        Vector3 a = panel.SalidaCable;
        Vector3 fin = caja.transform.position + Vector3.up * 0.14f;
        var puntos = new[]
        {
            a,
            new Vector3(a.x, yTecho, a.z),
            new Vector3(a.x, yTecho, fin.z),
            new Vector3(fin.x, yTecho, fin.z),
            fin
        };
        var cable = new GameObject("Cable_Alimentacion").AddComponent<LineRenderer>();
        cable.transform.SetParent(transform, true);
        cable.useWorldSpace = true;
        cable.positionCount = puntos.Length;
        cable.SetPositions(puntos);
        cable.widthMultiplier = 0.03f;
        cable.numCornerVertices = 3;
        cable.sharedMaterial = VisualesVR.MaterialOpaco(new Color(0.08f, 0.08f, 0.08f));
        cable.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cable.receiveShadows = false;
    }

    void CrearEfectos()
    {
        // ---- Chispas: emision moderada a rafagas, desde la caja de conexion ----
        var go = new GameObject("Chispas");
        go.transform.SetParent(_conexion, false);
        _chispas = go.AddComponent<ParticleSystem>();
        _chispas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _chispas.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.7f, 0.2f));
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var em = _chispas.emission;
        em.rateOverTime = 0f;
        var forma = _chispas.shape;
        forma.shapeType = ParticleSystemShapeType.Hemisphere;
        forma.radius = 0.04f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1.5f;
        r.sharedMaterial = ZonaFuego.MaterialParticula();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        _chispas.Play();

        // ---- Arcos electricos: dos descargas entre bornes de la caja y la carcasa ----
        var matArco = VisualesVR.MaterialSinLuz(new Color(0.75f, 0.88f, 1f, 1f));
        for (int i = 0; i < 2; i++)
        {
            var lr = new GameObject("Arco_" + i).AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 8;
            lr.widthMultiplier = 0.012f;
            lr.sharedMaterial = matArco;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            _arcos.Add(lr);
        }

        // Una sola luz para los arcos (sin sombras).
        _luzArco = new GameObject("Luz_Arco").AddComponent<Light>();
        _luzArco.transform.SetParent(_conexion, false);
        _luzArco.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        _luzArco.type = LightType.Point;
        _luzArco.color = new Color(0.65f, 0.8f, 1f);
        _luzArco.range = 3f;
        _luzArco.shadows = LightShadows.None;
        _luzArco.enabled = false;

        _zumbido = SonidosVR.CrearBucle(_conexion, SonidosVR.Zumbido, 0.55f, 10f);
        _zumbido.pitch = 1.08f;
    }

    // ------------------------------------------------------------------

    /// <summary>Quita el chamuscado y los efectos (reinicio).</summary>
    public void Restablecer()
    {
        Preparar();
        EnFalla = false;
        _chamuscado = 0f;
        VisualesVR.Tenir(_renders, Color.white, 0f);
        Fuego.Apagar();
        MostrarEfectos(false);
    }

    void Update()
    {
        if (!_preparado) return;
        bool efectos = EfectosElectricos;
        if (efectos != _efectosPrev) MostrarEfectos(efectos); // al cortar: desaparecen inmediatamente
        if (efectos) AnimarEfectos();

        // El fuego recibe energia solo mientras la instalacion esta energizada.
        if (Fuego != null) Fuego.aporteElectrico = panel == null || panel.Energizado;

        // Chamuscado progresivo mientras arde (y queda asi al extinguirse).
        if (Fuego != null && Fuego.Encendido) _chamuscado = Mathf.MoveTowards(_chamuscado, 1f, Time.deltaTime / 40f);
        if (_chamuscado > 0f) VisualesVR.Tenir(_renders, new Color(0.18f, 0.16f, 0.14f), 0.25f + 0.55f * _chamuscado);
    }

    void MostrarEfectos(bool visibles)
    {
        _efectosPrev = visibles;
        foreach (var a in _arcos) a.enabled = false;
        _luzArco.enabled = false;
        if (!visibles)
        {
            _chispas.Clear(true);
            if (_zumbido.isPlaying) _zumbido.Stop();
        }
        else if (!_zumbido.isPlaying) _zumbido.Play();
    }

    void AnimarEfectos()
    {
        // Rafagas de chispas a intervalos irregulares.
        if (Time.time >= _proximaChispa)
        {
            _proximaChispa = Time.time + Random.Range(0.12f, 0.7f);
            _chispas.Emit(Random.Range(4, 12));
            if (Random.value < 0.45f) SonidosVR.ReproducirEn(SonidosVR.Chispa, _conexion.position, Random.Range(0.4f, 0.8f), 12f);
        }

        // Arcos: se redibujan ~20 veces por segundo y aparecen de forma intermitente.
        if (Time.time < _proximoArco) return;
        _proximoArco = Time.time + 0.05f;
        float ancho = _largo * 0.5f;
        for (int i = 0; i < _arcos.Count; i++)
        {
            var lr = _arcos[i];
            bool on = Random.value < 0.6f;
            lr.enabled = on;
            if (!on) continue;
            Vector3 a = _conexion.position + transform.right * (i == 0 ? -0.08f : 0.08f) + transform.up * 0.1f;
            Vector3 b = transform.TransformPoint(new Vector3(Mathf.Clamp(_conexion.localPosition.x + (i == 0 ? -0.35f : 0.25f), -ancho, ancho),
                                                             _alto * Random.Range(0.85f, 1f), _fondo * 0.5f));
            for (int k = 0; k < lr.positionCount; k++)
            {
                float t = k / (float)(lr.positionCount - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (k > 0 && k < lr.positionCount - 1) p += Random.insideUnitSphere * 0.045f;
                lr.SetPosition(k, p);
            }
            lr.widthMultiplier = Random.Range(0.008f, 0.02f);
        }
        _luzArco.enabled = _arcos[0].enabled || _arcos[1].enabled;
        _luzArco.intensity = Random.Range(0.8f, 2.2f);
    }
}
