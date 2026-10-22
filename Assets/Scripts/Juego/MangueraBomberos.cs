using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Manguera de bomberos fisica conectada al PANEL DE BOMBA del camion
/// (costado del conductor, justo detras de la puerta de la cabina).
///
/// - La boquilla queda colgada en un soporte junto al panel, a la altura de la cintura
///   (no hay que agacharse). Se toma con GRIP (o pellizco).
/// - Carrete: la manguera empieza corta y SE DESENROLLA mientras caminas jalando la
///   boquilla (se agregan eslabones en la conexion), hasta la longitud maxima.
/// - Si sueltas la boquilla cerca del soporte se vuelve a enganchar; si queda tirada,
///   a los pocos segundos se enrolla sola y vuelve al soporte.
/// - Cuerpo: cadena de Rigidbodies con ConfigurableJoint (no se estira, giro limitado,
///   resorte de flexion). Visual: tubo suave generado cada frame.
/// - La boquilla sigue a la mano con retraso (peso e inercia) y tiene retroceso al echar agua.
///
/// Se crea sola desde JuegoBomberos: no requiere configuracion en el Inspector.
/// </summary>
[DefaultExecutionOrder(9500)]
public class MangueraBomberos : MonoBehaviour
{
    [Header("Conexion (panel de bomba)")]
    [Tooltip("Punto de conexion. Vacio = panel de bomba detras de la puerta del conductor.")]
    public Transform puntoConexion;
    [Tooltip("Metros desde el frente del camion hasta la conexion del panel.")]
    public float distanciaDesdeFrente = 2.95f;
    public float alturaConexion = 0.8f;
    [Tooltip("Ajuste fino (ejes del camion: X hacia afuera del costado, Y arriba, Z frente).")]
    public Vector3 ajusteConexion = Vector3.zero;

    [Header("Soporte de la boquilla")]
    [Tooltip("Posicion del soporte respecto a la conexion (X afuera, Y arriba, Z frente).")]
    public Vector3 offsetSoporte = new Vector3(0.12f, 0.2f, 0.55f);
    [Tooltip("Si sueltas la boquilla a menos de esta distancia del soporte, se engancha.")]
    public float radioEnganche = 0.45f;
    [Tooltip("Segundos que la boquilla puede quedar tirada antes de enrollarse sola.")]
    public float segundosParaEnrollar = 6f;

    [Header("Manguera")]
    [Tooltip("Longitud maxima desenrollada (m).")]
    public float longitud = 8f;
    [Tooltip("Eslabones con la boquilla en el soporte.")]
    public int eslabonesIniciales = 4;
    [Tooltip("Largo de cada eslabon fisico. Menor = mas flexible y suave, pero mas costoso.")]
    public float largoEslabon = 0.25f;
    public float radio = 0.035f;
    public float masaEslabon = 0.25f;
    [Tooltip("Grados que puede doblarse cada union.")]
    public float dobladoMaximo = 35f;
    [Tooltip("Rigidez de flexion sin agua. Con agua sube (la manguera llena es mas dura).")]
    public float rigidezFlexion = 6f;
    public float multiplicadorRigidezConAgua = 4f;
    public Color color = new Color(0.62f, 0.07f, 0.05f);
    public Material materialManguera;

    [Header("Boquilla")]
    public float masaBoquilla = 1.6f;
    public float largoBoquilla = 0.34f;
    [Tooltip("Modelo opcional para la boquilla (su eje +Z debe apuntar hacia donde sale el agua).")]
    public GameObject modeloBoquilla;
    public float radioAgarre = 0.18f;

    [Header("Sensacion de peso")]
    [Tooltip("0-1: que tan rapido la boquilla alcanza la mano. Menor = se siente mas pesada.")]
    [Range(0.05f, 1f)] public float respuestaPosicion = 0.45f;
    [Range(0.05f, 1f)] public float respuestaGiro = 0.35f;
    [Tooltip("Velocidad maxima con la que la boquilla sigue a la mano (m/s).")]
    public float velocidadMaxima = 6f;
    [Tooltip("Si la mano se aleja mas que esto de la boquilla (manguera al limite), se suelta.")]
    public float distanciaSoltar = 0.55f;

    [Header("Retroceso al expulsar agua")]
    [Tooltip("Cuanto empuja el chorro la boquilla hacia atras (metros a presion maxima).")]
    public float retroceso = 0.05f;
    public float temblor = 0.006f;
    public float fuerzaRetroceso = 25f;

    public const int CapaManguera = 2; // "Ignore Raycast": no la ven el teletransporte ni los raycasts por defecto

    public bool Desplegada => _raiz != null;
    public bool Agarrada => _mano >= 0;
    public bool EnSoporte => _enSoporte;
    /// <summary>Mano que sostiene la boquilla: 0 izquierda, 1 derecha, -1 ninguna.</summary>
    public int Mano => _mano;
    public Rigidbody Boquilla => _boquilla;
    public LanzaAgua Lanza => _lanza;
    public float LongitudDesenrollada => _eslabones.Count * largoEslabon;

    OVRCameraRig _rig;
    CamionBomberos _camion;
    Transform _ancla, _soporte;
    Rigidbody _anclaRb;
    GameObject _raiz;
    readonly List<Rigidbody> _eslabones = new List<Rigidbody>();
    readonly List<ConfigurableJoint> _juntas = new List<ConfigurableJoint>(); // _juntas[i] une _eslabones[i] con el anterior; la ultima es la de la boquilla
    readonly List<Collider> _colliders = new List<Collider>();
    Collider[] _collidersCamion;
    Rigidbody _boquilla;
    Transform _agarreBoquilla;
    LanzaAgua _lanza;
    Renderer[] _rendersBoquilla;
    TextMeshPro _etiqueta;

    Mesh _tubo;
    Vector3[] _vertices, _normales;
    readonly List<Vector3> _puntos = new List<Vector3>();
    readonly List<Vector3> _curva = new List<Vector3>();
    const int Lados = 8;
    const int Subdivisiones = 3;

    int _mano = -1;
    int _hoverPrev = -1;
    bool _enSoporte;
    float _tiempoEstirada, _proximaRigidez, _proximaVibracion, _rigidezAplicada = -1f, _sueltaDesde = -1f;
    bool _sentadoPrev;
    bool _listo;

    int MaxEslabones => Mathf.Max(eslabonesIniciales + 1, Mathf.RoundToInt(longitud / largoEslabon));

    public void Inicializar(OVRCameraRig rig, CamionBomberos camion)
    {
        _rig = rig;
        _camion = camion;
        _collidersCamion = _camion.GetComponentsInChildren<Collider>();
        _ancla = puntoConexion != null ? puntoConexion : CrearAnclaAutomatica();
        _anclaRb = _ancla.GetComponent<Rigidbody>();
        if (_anclaRb == null) _anclaRb = _ancla.gameObject.AddComponent<Rigidbody>();
        _anclaRb.isKinematic = true;
        CrearSoporte();
        _listo = true;
        Desplegar();
    }

    // ------------------------------------------------------------------
    // Puntos en el camion
    // ------------------------------------------------------------------

    Vector3 Frente => _camion.Frente;
    /// <summary>Hacia afuera del costado del conductor (donde esta la puerta y el panel).</summary>
    Vector3 Afuera => Vector3.Cross(Vector3.up, Frente) * (_camion.conductorIzquierda ? -1f : 1f);

    /// <summary>Caja de referencia del camion: su BoxCollider (ajustado a la carroceria) o sus limites visuales.</summary>
    Bounds CajaCamion()
    {
        var box = _camion.GetComponent<BoxCollider>();
        return box != null && box.enabled ? box.bounds : _camion.Limites;
    }

    Transform CrearAnclaAutomatica()
    {
        Bounds b = CajaCamion();
        Vector3 fwd = Frente, fuera = Afuera;
        float largo = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fwd.x), 0f, Mathf.Abs(fwd.z))));
        float ancho = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fuera.x), 0f, Mathf.Abs(fuera.z))));
        Vector3 basePiso = new Vector3(b.center.x, b.min.y, b.center.z);

        var t = new GameObject("Manguera_Conexion_PanelBomba").transform;
        t.SetParent(_camion.transform, true);
        t.position = basePiso
                     + fuera * (ancho - 0.04f + ajusteConexion.x)
                     + Vector3.up * (alturaConexion + ajusteConexion.y)
                     + fwd * (largo - distanciaDesdeFrente + ajusteConexion.z);
        t.rotation = Quaternion.LookRotation(fuera, Vector3.up); // +Z: hacia afuera del panel

        // Racor de bronce saliendo del panel (tapa cualquier separacion con la carroceria).
        var racor = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Racor", t, VisualesVR.MaterialOpaco(new Color(0.78f, 0.58f, 0.22f)));
        racor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        racor.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        racor.transform.localScale = new Vector3(0.1f, 0.13f, 0.1f);
        return t;
    }

    void CrearSoporte()
    {
        _soporte = new GameObject("Manguera_Soporte").transform;
        _soporte.SetParent(_camion.transform, true);
        _soporte.position = _ancla.position + Afuera * offsetSoporte.x + Vector3.up * offsetSoporte.y + Frente * offsetSoporte.z;
        _soporte.rotation = Quaternion.LookRotation(Frente, Vector3.up); // la boquilla descansa apuntando al frente

        var gris = VisualesVR.MaterialOpaco(new Color(0.35f, 0.36f, 0.38f));
        var base_ = VisualesVR.Primitiva(PrimitiveType.Cube, "Bandeja", _soporte, gris);
        base_.transform.localPosition = new Vector3(0f, -0.07f, 0f);
        base_.transform.localScale = new Vector3(0.12f, 0.025f, largoBoquilla + 0.04f);
        var brazo = VisualesVR.Primitiva(PrimitiveType.Cube, "Brazo", _soporte, gris);
        brazo.transform.localPosition = new Vector3(-0.07f, -0.07f, 0f);
        brazo.transform.localScale = new Vector3(0.12f, 0.025f, 0.06f);
    }

    // ------------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------------

    /// <summary>Crea la manguera corta con la boquilla enganchada en el soporte (manguera "enrollada").</summary>
    public void Desplegar()
    {
        if (!_listo || _ancla == null) return;
        Guardar();

        _raiz = new GameObject("Manguera_Bomberos");
        _eslabones.Clear();
        _juntas.Clear();
        _colliders.Clear();

        // Boquilla en el soporte; la manguera cuelga en "V" entre la conexion y la boquilla.
        Vector3 inicio = _ancla.position;
        Vector3 fin = _soporte.position - _soporte.forward * (largoBoquilla * 0.5f);
        int n = Mathf.Max(2, eslabonesIniciales);
        if (n % 2 == 1) n++;
        var camino = CaminoEnV(inicio, fin, n);

        Rigidbody previo = _anclaRb;
        Vector3 anclaPrevio = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            var rb = CrearEslabon(camino[i], camino[i + 1]);
            var j = Unir(rb.gameObject, previo, new Vector3(0f, 0f, -largoEslabon * 0.5f), anclaPrevio);
            if (i == 0) ConfigurarCarrete(j);
            _eslabones.Add(rb);
            _juntas.Add(j);
            previo = rb;
            anclaPrevio = new Vector3(0f, 0f, largoEslabon * 0.5f);
        }

        CrearBoquilla(previo, anclaPrevio);
        for (int i = 0; i + 1 < _colliders.Count; i++) Physics.IgnoreCollision(_colliders[i], _colliders[i + 1]);
        CrearTubo();
        _rigidezAplicada = -1f;
        AplicarRigidez(0f);
        _sueltaDesde = -1f;
    }

    /// <summary>Retira la manguera (al subir al camion o antes de re-desplegarla).</summary>
    public void Guardar()
    {
        _mano = -1;
        _hoverPrev = -1;
        if (_raiz != null) Destroy(_raiz);
        _raiz = null;
        _boquilla = null;
        _lanza = null;
        _eslabones.Clear();
        _juntas.Clear();
        _colliders.Clear();
    }

    /// <summary>Puntos de una "V" colgante de n tramos exactos entre a y b.</summary>
    List<Vector3> CaminoEnV(Vector3 a, Vector3 b, int n)
    {
        float mitad = n * 0.5f * largoEslabon;
        float d = Vector3.Distance(a, b);
        Vector3 medio = (a + b) * 0.5f;
        float caida = Mathf.Sqrt(Mathf.Max(0.0001f, mitad * mitad - d * d * 0.25f));
        Vector3 vertice = medio + Vector3.down * caida;

        var puntos = new List<Vector3>(n + 1);
        for (int i = 0; i <= n / 2; i++) puntos.Add(Vector3.Lerp(a, vertice, i / (n * 0.5f)));
        for (int i = 1; i <= n / 2; i++) puntos.Add(Vector3.Lerp(vertice, b, i / (n * 0.5f)));
        return puntos;
    }

    Rigidbody CrearEslabon(Vector3 a, Vector3 b)
    {
        var go = new GameObject("Eslabon") { layer = CapaManguera };
        go.transform.SetParent(_raiz.transform, false);
        Vector3 dir = b - a;
        go.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Frente, Vector3.up));

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.radius = radio;
        col.height = largoEslabon + radio * 2f;
        IgnorarCamion(col);

        var rb = go.AddComponent<Rigidbody>();
        ConfigurarCuerpo(rb, masaEslabon);
        return rb;
    }

    void IgnorarCamion(Collider c)
    {
        _colliders.Insert(0, c);
        if (_collidersCamion == null) return;
        foreach (var t in _collidersCamion) if (t != null) Physics.IgnoreCollision(t, c);
    }

    void ConfigurarCuerpo(Rigidbody rb, float masa)
    {
        rb.mass = masa;
        rb.drag = 0.3f;
        rb.angularDrag = 1.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.solverIterations = 12;
        rb.solverVelocityIterations = 4;
        rb.maxAngularVelocity = 30f;
    }

    ConfigurableJoint Unir(GameObject go, Rigidbody conectado, Vector3 anclaLocal, Vector3 anclaConectada)
    {
        var j = go.AddComponent<ConfigurableJoint>();
        j.autoConfigureConnectedAnchor = false;
        j.connectedBody = conectado;
        j.anchor = anclaLocal;
        j.connectedAnchor = anclaConectada;
        ConfigurarUnion(j);
        return j;
    }

    /// <summary>Union normal: no se estira; dobla hasta cierto angulo, con poca torsion.</summary>
    void ConfigurarUnion(ConfigurableJoint j)
    {
        j.xMotion = ConfigurableJointMotion.Locked;
        j.yMotion = ConfigurableJointMotion.Locked;
        j.zMotion = ConfigurableJointMotion.Locked;
        j.angularXMotion = ConfigurableJointMotion.Limited;
        j.angularYMotion = ConfigurableJointMotion.Limited;
        j.angularZMotion = ConfigurableJointMotion.Limited;
        j.lowAngularXLimit = new SoftJointLimit { limit = -dobladoMaximo };
        j.highAngularXLimit = new SoftJointLimit { limit = dobladoMaximo };
        j.angularYLimit = new SoftJointLimit { limit = dobladoMaximo };
        j.angularZLimit = new SoftJointLimit { limit = 10f };
        j.rotationDriveMode = RotationDriveMode.Slerp;
        j.projectionMode = JointProjectionMode.PositionAndRotation;
        j.projectionDistance = 0.02f;
        j.projectionAngle = 5f;
        j.enablePreprocessing = false;
    }

    /// <summary>Union del carrete: el primer eslabon puede alejarse hasta un tramo de la conexion (asi "sale" manguera).</summary>
    void ConfigurarCarrete(ConfigurableJoint j)
    {
        j.xMotion = ConfigurableJointMotion.Limited;
        j.yMotion = ConfigurableJointMotion.Limited;
        j.zMotion = ConfigurableJointMotion.Limited;
        j.linearLimit = new SoftJointLimit { limit = largoEslabon };
        j.angularXMotion = ConfigurableJointMotion.Free;
        j.angularYMotion = ConfigurableJointMotion.Free;
        j.angularZMotion = ConfigurableJointMotion.Free;
        j.projectionMode = JointProjectionMode.None;
        j.enablePreprocessing = false;
    }

    void CrearBoquilla(Rigidbody previo, Vector3 anclaPrevio)
    {
        var go = new GameObject("Boquilla") { layer = CapaManguera };
        go.transform.SetParent(_raiz.transform, false);
        go.transform.SetPositionAndRotation(_soporte.position, _soporte.rotation);

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.radius = 0.05f;
        col.height = largoBoquilla;
        IgnorarCamion(col);

        _boquilla = go.AddComponent<Rigidbody>();
        ConfigurarCuerpo(_boquilla, masaBoquilla);
        _juntas.Add(Unir(go, previo, new Vector3(0f, 0f, -largoBoquilla * 0.5f), anclaPrevio));
        Enganchar();

        if (modeloBoquilla != null)
        {
            var m = Instantiate(modeloBoquilla, go.transform, false);
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else ConstruirBoquillaSimple(go.transform);

        // Punto de agarre (la mano queda en la empunadura, un poco atras del centro).
        _agarreBoquilla = new GameObject("Agarre").transform;
        _agarreBoquilla.SetParent(go.transform, false);
        _agarreBoquilla.localPosition = new Vector3(0f, -0.03f, -0.04f);

        // Salida del agua en la punta.
        var salida = new GameObject("Salida_Agua").transform;
        salida.SetParent(go.transform, false);
        salida.localPosition = new Vector3(0f, 0f, largoBoquilla * 0.5f + 0.03f);
        _lanza = salida.gameObject.AddComponent<LanzaAgua>();
        _lanza.Inicializar(this);

        _rendersBoquilla = go.GetComponentsInChildren<Renderer>();

        _etiqueta = VisualesVR.Texto("Etiqueta_Manguera", _raiz.transform, 0.05f, new Vector2(0.9f, 0.3f));
        _etiqueta.fontStyle = FontStyles.Bold;
        _etiqueta.outlineWidth = 0.2f;
        _etiqueta.outlineColor = Color.black;
        _etiqueta.text = "";
    }

    static void ConstruirBoquillaSimple(Transform padre)
    {
        var negro = VisualesVR.MaterialOpaco(new Color(0.08f, 0.08f, 0.08f));
        var bronce = VisualesVR.MaterialOpaco(new Color(0.78f, 0.58f, 0.22f));
        var rojo = VisualesVR.MaterialOpaco(new Color(0.8f, 0.1f, 0.05f));

        var cuerpo = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Cuerpo", padre, negro);
        cuerpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cuerpo.transform.localScale = new Vector3(0.075f, 0.13f, 0.075f);
        cuerpo.transform.localPosition = new Vector3(0f, 0f, -0.03f);

        var punta = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Punta", padre, bronce);
        punta.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        punta.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        punta.transform.localPosition = new Vector3(0f, 0f, 0.14f);

        var racor = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Racor", padre, bronce);
        racor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        racor.transform.localScale = new Vector3(0.085f, 0.025f, 0.085f);
        racor.transform.localPosition = new Vector3(0f, 0f, -0.16f);

        var empunadura = VisualesVR.Primitiva(PrimitiveType.Cube, "Empunadura", padre, negro);
        empunadura.transform.localPosition = new Vector3(0f, -0.07f, -0.05f);
        empunadura.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        empunadura.transform.localScale = new Vector3(0.035f, 0.1f, 0.04f);

        var palanca = VisualesVR.Primitiva(PrimitiveType.Cube, "Palanca", padre, rojo);
        palanca.transform.localPosition = new Vector3(0f, 0.05f, 0.02f);
        palanca.transform.localScale = new Vector3(0.09f, 0.015f, 0.12f);
    }

    /// <summary>Deja la boquilla quieta en el soporte (cinematica).</summary>
    void Enganchar()
    {
        _enSoporte = true;
        _boquilla.isKinematic = true;
        _boquilla.transform.SetPositionAndRotation(_soporte.position, _soporte.rotation);
    }

    // ------------------------------------------------------------------
    // Carrete: agrega manguera en la conexion mientras se jala la boquilla
    // ------------------------------------------------------------------

    void Desenrollar()
    {
        if (_eslabones.Count == 0 || _eslabones.Count >= MaxEslabones) return;
        var primero = _eslabones[0];
        Vector3 a = _ancla.position;
        Vector3 b = primero.transform.TransformPoint(0f, 0f, -largoEslabon * 0.5f);
        if (Vector3.Distance(a, b) < largoEslabon * 0.9f) return;

        // Nuevo eslabon entre la conexion y el que era el primero.
        var nuevo = CrearEslabon(a, a + (b - a).normalized * largoEslabon);
        nuevo.velocity = primero.velocity * 0.5f;
        var jNuevo = Unir(nuevo.gameObject, _anclaRb, new Vector3(0f, 0f, -largoEslabon * 0.5f), Vector3.zero);
        ConfigurarCarrete(jNuevo);

        // El antiguo primero queda unido al nuevo con una union normal.
        var jViejo = _juntas[0];
        jViejo.connectedBody = nuevo;
        jViejo.connectedAnchor = new Vector3(0f, 0f, largoEslabon * 0.5f);
        ConfigurarUnion(jViejo);
        Physics.IgnoreCollision(nuevo.GetComponent<Collider>(), primero.GetComponent<Collider>());

        _eslabones.Insert(0, nuevo);
        _juntas.Insert(0, jNuevo);
        _rigidezAplicada = -1f;

        // Feedback del carrete: clic suave y pulso en la mano.
        SonidosVR.ReproducirEn(SonidosVR.Clic, a, 0.35f);
        if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.15f, 0.03f, 0.9f);
    }

    // ------------------------------------------------------------------
    // Tubo visual
    // ------------------------------------------------------------------

    void CrearTubo()
    {
        var go = new GameObject("Manguera_Tubo");
        go.transform.SetParent(_raiz.transform, false);
        _tubo = new Mesh { name = "Manguera" };
        _tubo.MarkDynamic();

        // Se reserva para la longitud maxima: los anillos sobrantes se colapsan al final.
        int anillos = (MaxEslabones + 1) * Subdivisiones + 1;
        _vertices = new Vector3[anillos * (Lados + 1)];
        _normales = new Vector3[_vertices.Length];
        var tris = new int[(anillos - 1) * Lados * 6];
        int k = 0;
        for (int r = 0; r < anillos - 1; r++)
            for (int s = 0; s < Lados; s++)
            {
                int a = r * (Lados + 1) + s, b = a + Lados + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
            }
        _tubo.vertices = _vertices;
        _tubo.triangles = tris;

        go.AddComponent<MeshFilter>().sharedMesh = _tubo;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materialManguera != null ? materialManguera : CrearMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ActualizarTubo();
    }

    Material CrearMaterial()
    {
        var m = VisualesVR.MaterialOpaco(color);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
        return m;
    }

    void ActualizarTubo()
    {
        if (_tubo == null || _boquilla == null) return;

        // Puntos de control: conexion, extremos de cada eslabon y la parte trasera de la boquilla.
        _puntos.Clear();
        _puntos.Add(_ancla.position);
        foreach (var rb in _eslabones) _puntos.Add(rb.transform.TransformPoint(0f, 0f, largoEslabon * 0.5f));
        _puntos.Add(_boquilla.transform.TransformPoint(0f, 0f, -largoBoquilla * 0.45f));

        // Curva suave Catmull-Rom.
        _curva.Clear();
        for (int i = 0; i < _puntos.Count - 1; i++)
        {
            Vector3 p0 = _puntos[Mathf.Max(0, i - 1)], p1 = _puntos[i], p2 = _puntos[i + 1], p3 = _puntos[Mathf.Min(_puntos.Count - 1, i + 2)];
            for (int s = 0; s < Subdivisiones; s++)
            {
                float t = s / (float)Subdivisiones, t2 = t * t, t3 = t2 * t;
                _curva.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        _curva.Add(_puntos[_puntos.Count - 1]);

        int anillos = _vertices.Length / (Lados + 1);
        Vector3 normal = Vector3.zero;
        for (int r = 0; r < anillos; r++)
        {
            if (r >= _curva.Count)
            {
                // Anillos no usados (manguera aun enrollada): colapsados en el ultimo punto.
                Vector3 fin = _curva[_curva.Count - 1];
                for (int s = 0; s <= Lados; s++) { _vertices[r * (Lados + 1) + s] = fin; _normales[r * (Lados + 1) + s] = Vector3.up; }
                continue;
            }
            Vector3 c = _curva[r];
            Vector3 tangente = _curva[Mathf.Min(r + 1, _curva.Count - 1)] - _curva[Mathf.Max(r - 1, 0)];
            if (tangente.sqrMagnitude < 1e-8f) tangente = Vector3.forward;
            tangente.Normalize();

            // Marco por "transporte paralelo": el tubo no se retuerce.
            if (r == 0) normal = Vector3.Cross(tangente, Mathf.Abs(tangente.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            else normal = Vector3.ProjectOnPlane(normal, tangente).normalized;
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(tangente, Vector3.right).normalized;
            Vector3 binormal = Vector3.Cross(tangente, normal);

            for (int s = 0; s <= Lados; s++)
            {
                float a = s / (float)Lados * Mathf.PI * 2f;
                Vector3 n = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                _vertices[r * (Lados + 1) + s] = c + n * radio;
                _normales[r * (Lados + 1) + s] = n;
            }
        }
        _tubo.vertices = _vertices;
        _tubo.normals = _normales;
        _tubo.RecalculateBounds();
    }

    // ------------------------------------------------------------------
    // Logica
    // ------------------------------------------------------------------

    Transform AnclaMano(int i) => _rig == null ? null : (i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor);

    void Update()
    {
        if (!_listo) return;
        if (_ancla == null) { Guardar(); enabled = false; return; } // la escena del camion se descargo

        // Se guarda al subir al camion y se vuelve a colgar en el soporte al bajar.
        bool sentado = _camion != null && _camion.JugadorSentado;
        if (sentado && !_sentadoPrev) Guardar();
        if (!sentado && !Desplegada && Mathf.Abs(_camion.Velocidad) < 0.1f) Desplegar();
        _sentadoPrev = sentado;
        if (!Desplegada) return;

        // Si la fisica se descontrolo (atraveso el piso, salio disparada) vuelve al soporte.
        if (!EsValida()) { Desplegar(); return; }

        // Tirada en el piso mucho tiempo: se enrolla sola y vuelve al soporte (no hay que agacharse).
        if (_mano < 0 && !_enSoporte && _sueltaDesde > 0f && Time.time - _sueltaDesde > segundosParaEnrollar)
        {
            Desplegar();
            SonidosVR.ReproducirEn(SonidosVR.Agarrar, _soporte.position, 0.8f);
            JuegoBomberos.Instancia?.Aviso("La manguera se enrolló sola", JuegoBomberos.TipoAviso.Info, 1.5f);
            return;
        }

        bool permitido = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;

        if (_mano < 0)
        {
            int hover = -1;
            if (permitido)
            {
                for (int i = 0; i < 2; i++)
                {
                    var m = AnclaMano(i);
                    if (m == null || !m.gameObject.activeInHierarchy) continue;
                    if (Vector3.Distance(m.position, _boquilla.position) > radioAgarre) continue;
                    hover = i;
                    if (EntradaVR.AgarreDown(i == 0)) { Agarrar(i); break; }
                }
            }
            if (hover >= 0 && hover != _hoverPrev)
            {
                EntradaVR.Vibrar(hover == 0, 0.25f, 0.05f, 0.8f);
                SonidosVR.ReproducirEn(SonidosVR.Clic, _boquilla.position, 0.5f);
            }
            _hoverPrev = hover;
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            if (_mano < 0) VisualesVR.Tenir(_rendersBoquilla, new Color(1f, 0.85f, 0.1f), hover >= 0 ? 0.4f + 0.3f * pulso : 0f);
        }
        else
        {
            if (!EntradaVR.Agarrando(_mano == 0) || !permitido) SoltarBoquilla();
        }

        // La manguera llena de agua se pone mas rigida.
        if (Time.time >= _proximaRigidez)
        {
            _proximaRigidez = Time.time + 0.1f;
            AplicarRigidez(_lanza != null ? _lanza.Empuje : 0f);
        }

        // Vibracion continua mientras sale agua (proporcional a la presion y el caudal).
        if (_mano >= 0 && _lanza != null && _lanza.Empuje > 0.02f && Time.time >= _proximaVibracion)
        {
            _proximaVibracion = Time.time + 0.1f;
            EntradaVR.Vibrar(_mano == 0, 0.12f + 0.5f * _lanza.Empuje, 0.12f, 0.3f);
        }

        ActualizarEtiqueta();
    }

    void FixedUpdate()
    {
        if (!Desplegada || _boquilla == null || _enSoporte) return;
        float empuje = _lanza != null ? _lanza.Empuje : 0f;

        if (_mano >= 0)
        {
            // Mientras se jala, el carrete suelta manguera.
            Desenrollar();

            var mano = AnclaMano(_mano);
            if (mano == null) { SoltarBoquilla(); return; }

            // Pose objetivo: la empunadura en la mano, apuntando hacia donde apunta el control.
            Quaternion rotObjetivo = mano.rotation;
            Vector3 posObjetivo = mano.position - rotObjetivo * _agarreBoquilla.localPosition;

            // Retroceso: el chorro empuja la boquilla hacia atras y la hace temblar un poco.
            if (empuje > 0f)
            {
                Vector3 atras = -(rotObjetivo * Vector3.forward);
                float tiempo = Time.time * 18f;
                Vector3 vibra = new Vector3(Mathf.PerlinNoise(tiempo, 0f) - 0.5f, Mathf.PerlinNoise(0f, tiempo) - 0.5f, 0f);
                posObjetivo += atras * (retroceso * empuje) + rotObjetivo * vibra * (temblor * 2f * empuje);
            }

            // Seguimiento por velocidad: la boquilla "alcanza" a la mano con retraso (peso e inercia).
            float dt = Time.fixedDeltaTime;
            Vector3 velDeseada = Vector3.ClampMagnitude((posObjetivo - _boquilla.position) / dt, velocidadMaxima);
            _boquilla.velocity = Vector3.Lerp(_boquilla.velocity, velDeseada, respuestaPosicion);

            Quaternion delta = rotObjetivo * Quaternion.Inverse(_boquilla.rotation);
            delta.ToAngleAxis(out float angulo, out Vector3 eje);
            if (angulo > 180f) angulo -= 360f;
            if (!float.IsNaN(eje.x) && Mathf.Abs(angulo) > 0.01f)
            {
                Vector3 velAngDeseada = eje * (angulo * Mathf.Deg2Rad / dt);
                _boquilla.angularVelocity = Vector3.Lerp(_boquilla.angularVelocity, velAngDeseada, respuestaGiro);
            }

            // Manguera al limite: si la mano se aleja demasiado, se suelta sola.
            if (Vector3.Distance(posObjetivo, _boquilla.position) > distanciaSoltar)
            {
                _tiempoEstirada += dt;
                if (_tiempoEstirada > 0.3f)
                {
                    SoltarBoquilla();
                    EntradaVR.VibrarAmbas(0.8f, 0.2f, 0.2f);
                    SonidosVR.ReproducirEn(SonidosVR.Error, _boquilla.position, 0.7f);
                    JuegoBomberos.Instancia?.Aviso("¡La manguera no da más!", JuegoBomberos.TipoAviso.Error, 1.5f);
                }
            }
            else _tiempoEstirada = 0f;
        }

        // Fuerza de reaccion del chorro (tambien se nota en la manguera).
        if (empuje > 0f)
            _boquilla.AddForce(-_boquilla.transform.forward * (fuerzaRetroceso * empuje), ForceMode.Force);
    }

    void LateUpdate()
    {
        if (Desplegada) ActualizarTubo();
    }

    void Agarrar(int mano)
    {
        _mano = mano;
        _tiempoEstirada = 0f;
        _enSoporte = false;
        _boquilla.isKinematic = false;
        VisualesVR.Tenir(_rendersBoquilla, Color.white, 0f);
        EntradaVR.Vibrar(mano == 0, 0.7f, 0.15f, 0.5f);
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, _boquilla.position, 0.9f);
    }

    public void SoltarBoquilla()
    {
        if (_mano < 0) return;
        EntradaVR.Vibrar(_mano == 0, 0.3f, 0.05f);
        _mano = -1;
        _hoverPrev = -1;

        // Cerca del soporte: se vuelve a colgar y la manguera se enrolla.
        if (_boquilla != null && Vector3.Distance(_boquilla.position, _soporte.position) < radioEnganche)
        {
            Desplegar();
            SonidosVR.ReproducirEn(SonidosVR.Puerta, _soporte.position, 0.6f);
            JuegoBomberos.Instancia?.Aviso("Manguera guardada", JuegoBomberos.TipoAviso.Exito, 1.2f);
            return;
        }
        // Si no, cae con su velocidad (se puede lanzar) y empieza la cuenta para enrollarse sola.
        _sueltaDesde = Time.time;
    }

    void AplicarRigidez(float empuje)
    {
        float k = rigidezFlexion * Mathf.Lerp(1f, multiplicadorRigidezConAgua, empuje);
        if (Mathf.Abs(k - _rigidezAplicada) < 0.05f) return;
        _rigidezAplicada = k;
        var drive = new JointDrive { positionSpring = k, positionDamper = k * 0.08f, maximumForce = float.MaxValue };
        for (int i = 1; i < _juntas.Count; i++) if (_juntas[i] != null) _juntas[i].slerpDrive = drive; // la 0 es el carrete (libre)
    }

    bool EsValida()
    {
        if (_boquilla == null) return false;
        Vector3 p = _boquilla.position;
        if (float.IsNaN(p.x) || float.IsInfinity(p.x)) return false;
        if (Vector3.Distance(p, _ancla.position) > longitud + 2f) return false;
        if (p.y < _ancla.position.y - 10f) return false;
        return true;
    }

    void ActualizarEtiqueta()
    {
        if (_etiqueta == null || _rig == null) return;
        var cabeza = _rig.centerEyeAnchor;
        float dist = Vector3.Distance(cabeza.position, _boquilla.position);

        string texto = "";
        if (_mano >= 0 && _lanza != null)
        {
            int barras = Mathf.RoundToInt(_lanza.Caudal * 10f);
            texto = $"PRESIÓN {_lanza.NombrePresion}  -  {(_lanza.Niebla ? "NIEBLA" : "CHORRO")}   <size=80%>{LongitudDesenrollada:0.0}/{longitud:0} m</size>\n" +
                    $"<size=80%>Caudal <color=#7FD4FF>{new string('|', barras)}</color><color=#555555>{new string('|', 10 - barras)}</color></size>\n" +
                    "<size=60%>Gatillo: agua   A/X: presión   B/Y: chorro/niebla</size>";
        }
        else if (dist < 3f)
        {
            texto = _enSoporte ? "MANGUERA\n<size=65%>Acerca la mano y presiona GRIP</size>"
                               : "MANGUERA\n<size=65%>GRIP para tomar - se enrolla sola si la dejas</size>";
        }
        _etiqueta.text = texto;
        if (texto.Length == 0) return;

        Vector3 pos = _boquilla.position + Vector3.up * (_mano >= 0 ? 0.28f : 0.35f);
        _etiqueta.transform.position = pos;
        Vector3 mirar = pos - cabeza.position;
        if (mirar.sqrMagnitude > 0.0001f) _etiqueta.transform.rotation = Quaternion.LookRotation(mirar, Vector3.up);
    }

    void OnDestroy()
    {
        if (_raiz != null) Destroy(_raiz);
    }
}
