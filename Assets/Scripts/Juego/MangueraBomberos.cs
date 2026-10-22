using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Manguera de bomberos fisica conectada al costado del camion.
///
/// - Cuerpo: cadena de Rigidbodies unidos con ConfigurableJoint (posicion bloqueada,
///   giro limitado y un "resorte" de flexion), asi la manguera cae, se arrastra y se dobla.
/// - Visual: un tubo suave (malla generada cada frame sobre una curva Catmull-Rom).
/// - Boquilla: se toma con GRIP (o pellizco). Sigue a la mano con un pequeno retraso
///   (seguimiento por velocidad), por eso se siente su peso y su inercia.
/// - Al subir al camion la manguera se guarda; al bajar se vuelve a desplegar.
///
/// Se crea sola desde JuegoBomberos (no requiere prefab). Si quieres ajustar valores
/// en el Inspector antes de Play, agrega este componente al objeto del camion.
/// </summary>
[DefaultExecutionOrder(9500)]
public class MangueraBomberos : MonoBehaviour
{
    [Header("Conexion al camion")]
    [Tooltip("Punto donde se conecta la manguera. Vacio = costado derecho trasero, a 1 m de altura.")]
    public Transform puntoConexion;
    [Tooltip("Ajuste del punto automatico (ejes del camion: X derecha, Y arriba, Z frente).")]
    public Vector3 ajusteConexion = Vector3.zero;

    [Header("Manguera")]
    public float longitud = 7.5f;
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
    /// <summary>Mano que sostiene la boquilla: 0 izquierda, 1 derecha, -1 ninguna.</summary>
    public int Mano => _mano;
    public Rigidbody Boquilla => _boquilla;
    public LanzaAgua Lanza => _lanza;

    OVRCameraRig _rig;
    CamionBomberos _camion;
    Transform _ancla;
    GameObject _raiz;
    readonly List<Rigidbody> _eslabones = new List<Rigidbody>();
    readonly List<ConfigurableJoint> _juntas = new List<ConfigurableJoint>();
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
    float _tiempoEstirada, _proximaRigidez, _proximaVibracion, _rigidezAplicada = -1f;
    bool _sentadoPrev;
    bool _listo;

    public void Inicializar(OVRCameraRig rig, CamionBomberos camion)
    {
        _rig = rig;
        _camion = camion;
        _ancla = puntoConexion != null ? puntoConexion : CrearAnclaAutomatica();
        _listo = true;
        Desplegar();
    }

    Transform CrearAnclaAutomatica()
    {
        Bounds b = _camion.Limites;
        Vector3 fwd = _camion.Frente;
        Vector3 der = Vector3.Cross(Vector3.up, fwd);
        float largo = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fwd.x), 0f, Mathf.Abs(fwd.z))));
        float ancho = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(der.x), 0f, Mathf.Abs(der.z))));
        Vector3 basePiso = new Vector3(b.center.x, b.min.y, b.center.z);

        var t = new GameObject("Manguera_Conexion").transform;
        t.SetParent(_camion.transform, true);
        t.position = basePiso
                     + der * (ancho + 0.05f + ajusteConexion.x)
                     + Vector3.up * (1.0f + ajusteConexion.y)
                     + fwd * (-largo * 0.45f + ajusteConexion.z);
        t.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        // Toma visible en el camion (racor de bronce).
        var racor = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Racor", t, VisualesVR.MaterialOpaco(new Color(0.75f, 0.55f, 0.2f)));
        racor.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        racor.transform.localScale = new Vector3(0.11f, 0.05f, 0.11f);
        return t;
    }

    // ------------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------------

    /// <summary>Crea la manguera colgando del camion y tendida en el piso junto a el.</summary>
    public void Desplegar()
    {
        if (!_listo || _ancla == null) return;
        Guardar();

        _raiz = new GameObject("Manguera_Bomberos");
        int n = Mathf.Max(4, Mathf.RoundToInt(longitud / largoEslabon));
        List<Vector3> camino = CalcularCamino(n);

        // Ancla fisica (cinematica) que sigue al camion.
        var anclaRb = _ancla.GetComponent<Rigidbody>();
        if (anclaRb == null) anclaRb = _ancla.gameObject.AddComponent<Rigidbody>();
        anclaRb.isKinematic = true;

        Rigidbody previo = anclaRb;
        Vector3 anclaPrevio = Vector3.zero;
        _eslabones.Clear();
        _juntas.Clear();

        for (int i = 0; i < n; i++)
        {
            Vector3 a = camino[i], b = camino[i + 1];
            var go = new GameObject("Eslabon_" + i) { layer = CapaManguera };
            go.transform.SetParent(_raiz.transform, false);
            go.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(b - a, Vector3.up));

            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = radio;
            col.height = largoEslabon + radio * 2f;

            var rb = go.AddComponent<Rigidbody>();
            ConfigurarCuerpo(rb, masaEslabon);

            _juntas.Add(Unir(go, previo, new Vector3(0f, 0f, -largoEslabon * 0.5f), anclaPrevio));
            _eslabones.Add(rb);
            previo = rb;
            anclaPrevio = new Vector3(0f, 0f, largoEslabon * 0.5f);
        }

        CrearBoquilla(camino[n], camino[n] - camino[n - 1], previo, anclaPrevio);
        IgnorarColisiones();
        CrearTubo();
        _rigidezAplicada = -1f;
        AplicarRigidez(0f);
    }

    /// <summary>Retira la manguera (al subir al camion o antes de re-desplegarla).</summary>
    public void Guardar()
    {
        SoltarBoquilla();
        if (_raiz != null) Destroy(_raiz);
        _raiz = null;
        _boquilla = null;
        _lanza = null;
        _eslabones.Clear();
        _juntas.Clear();
    }

    /// <summary>
    /// Camino inicial: baja desde la conexion hasta el piso con una curva suave
    /// y sigue tendida hacia el frente del camion, pegada a su costado.
    /// </summary>
    List<Vector3> CalcularCamino(int n)
    {
        var puntos = new List<Vector3>(n + 1);
        Vector3 fwd = _camion.Frente;
        Vector3 p = _ancla.position;
        float piso = BuscarPiso(p);
        puntos.Add(p);

        Vector3 dir = Vector3.down;
        for (int i = 0; i < n; i++)
        {
            float alturaLibre = p.y - (piso + radio + 0.01f);
            // Cerca del piso, la direccion gira gradualmente hacia el frente (sin superar el doblez maximo).
            Vector3 deseada = alturaLibre > largoEslabon * 2.5f ? Vector3.down : fwd;
            dir = Vector3.RotateTowards(dir, deseada, Mathf.Deg2Rad * dobladoMaximo * 0.8f, 0f).normalized;
            Vector3 siguiente = p + dir * largoEslabon;
            if (siguiente.y < piso + radio + 0.01f)
            {
                siguiente.y = piso + radio + 0.01f;
                siguiente = p + (siguiente - p).normalized * largoEslabon;
            }
            puntos.Add(siguiente);
            p = siguiente;
        }
        return puntos;
    }

    float BuscarPiso(Vector3 desde)
    {
        float mejor = desde.y - 1f;
        foreach (var h in Physics.RaycastAll(desde, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(_camion.transform)) continue;
            if (h.normal.y < 0.5f) continue;
            if (h.point.y > mejor) mejor = h.point.y;
        }
        return mejor;
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

        // La distancia entre eslabones no cambia: la manguera no se estira.
        j.xMotion = ConfigurableJointMotion.Locked;
        j.yMotion = ConfigurableJointMotion.Locked;
        j.zMotion = ConfigurableJointMotion.Locked;

        // Puede doblarse hasta cierto angulo; el giro sobre su propio eje es menor (torsion).
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
        return j;
    }

    void CrearBoquilla(Vector3 pos, Vector3 dir, Rigidbody previo, Vector3 anclaPrevio)
    {
        var go = new GameObject("Boquilla") { layer = CapaManguera };
        go.transform.SetParent(_raiz.transform, false);
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        go.transform.SetPositionAndRotation(pos + dir.normalized * (largoBoquilla * 0.5f), rot);

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.radius = 0.05f;
        col.height = largoBoquilla;

        _boquilla = go.AddComponent<Rigidbody>();
        ConfigurarCuerpo(_boquilla, masaBoquilla);
        _juntas.Add(Unir(go, previo, new Vector3(0f, 0f, -largoBoquilla * 0.5f), anclaPrevio));

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

    void IgnorarColisiones()
    {
        var propios = new List<Collider>();
        foreach (var rb in _eslabones) propios.Add(rb.GetComponent<Collider>());
        propios.Add(_boquilla.GetComponent<Collider>());

        // Eslabones vecinos no chocan entre si (evita temblores en las uniones).
        for (int i = 0; i + 1 < propios.Count; i++)
            Physics.IgnoreCollision(propios[i], propios[i + 1]);

        // La manguera no se engancha con la carroceria del camion.
        foreach (var c in _camion.GetComponentsInChildren<Collider>())
            foreach (var p in propios)
                Physics.IgnoreCollision(c, p);
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

        int tramos = (_eslabones.Count + 1) * Subdivisiones + 1; // = puntos de la curva
        _vertices = new Vector3[tramos * (Lados + 1)];
        _normales = new Vector3[_vertices.Length];
        var tris = new int[(tramos - 1) * Lados * 6];
        int k = 0;
        for (int r = 0; r < tramos - 1; r++)
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
            int idx = Mathf.Min(r, _curva.Count - 1);
            Vector3 c = _curva[idx];
            Vector3 tangente = (_curva[Mathf.Min(idx + 1, _curva.Count - 1)] - _curva[Mathf.Max(idx - 1, 0)]);
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

        // Se guarda al subir al camion y se despliega al bajar (con el camion detenido).
        bool sentado = _camion != null && _camion.JugadorSentado;
        if (sentado && !_sentadoPrev) Guardar();
        if (!sentado && !Desplegada && Mathf.Abs(_camion.Velocidad) < 0.1f) Desplegar();
        _sentadoPrev = sentado;
        if (!Desplegada) return;

        // Si la fisica se descontrolo (atravesó el piso, salio disparada) se vuelve a desplegar.
        if (!EsValida()) { Desplegar(); return; }

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
        if (!Desplegada || _boquilla == null) return;
        float empuje = _lanza != null ? _lanza.Empuje : 0f;

        if (_mano >= 0)
        {
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
        // La boquilla conserva su velocidad: si la sueltas en movimiento, sale despedida y cae.
    }

    void AplicarRigidez(float empuje)
    {
        float k = rigidezFlexion * Mathf.Lerp(1f, multiplicadorRigidezConAgua, empuje);
        if (Mathf.Abs(k - _rigidezAplicada) < 0.05f) return;
        _rigidezAplicada = k;
        var drive = new JointDrive { positionSpring = k, positionDamper = k * 0.08f, maximumForce = float.MaxValue };
        foreach (var j in _juntas) if (j != null) j.slerpDrive = drive;
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
            texto = $"PRESIÓN {_lanza.NombrePresion}  -  {(_lanza.Niebla ? "NIEBLA" : "CHORRO")}\n" +
                    $"<size=80%>Caudal <color=#7FD4FF>{new string('|', barras)}</color><color=#555555>{new string('|', 10 - barras)}</color></size>\n" +
                    "<size=60%>Gatillo: agua   A/X: presión   B/Y: chorro/niebla</size>";
        }
        else if (dist < 3f)
        {
            texto = "MANGUERA\n<size=65%>Acerca la mano y presiona GRIP</size>";
        }
        _etiqueta.text = texto;
        if (texto.Length == 0) return;

        Vector3 pos = _boquilla.position + Vector3.up * (_mano >= 0 ? 0.28f : 0.4f);
        _etiqueta.transform.position = pos;
        Vector3 mirar = pos - cabeza.position;
        if (mirar.sqrMagnitude > 0.0001f) _etiqueta.transform.rotation = Quaternion.LookRotation(mirar, Vector3.up);
    }

    void OnDestroy()
    {
        if (_raiz != null) Destroy(_raiz);
    }
}
