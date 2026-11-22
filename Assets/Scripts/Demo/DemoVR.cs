using System;
using System.Collections.Generic;
using System.Reflection;
using Oculus.Avatar2;
using UnityEngine;

/// <summary>
/// "Persona simulada" para grabar la demo sin visor: inyecta el estado de los controles Touch en
/// OVRInput (sticks, gatillos, GRIP y botones) y mueve la cabeza, las manos y el cuerpo del rig
/// con movimientos suaves y naturales. El juego no se modifica: lee los mismos anclajes
/// (leftHandAnchor / rightHandAnchor / centerEyeAnchor) y la misma entrada que con la Quest.
/// Solo se activa desde el editor (menu Demo); en el build no hace nada porque nadie lo crea.
/// </summary>
[DefaultExecutionOrder(-5000)]
public class DemoVR : MonoBehaviour
{
    public static DemoVR I { get; private set; }

    // ---------------- Entrada simulada ----------------
    [NonSerialized] public Vector2 stickL, stickR;
    [NonSerialized] public float gatilloL, gatilloR, gripL, gripR;
    [NonSerialized] public bool A, B, X, Y, clicL, clicR;

    // ---------------- Cuerpo ----------------
    public OVRCameraRig rig;
    public float alturaOjos = 1.64f;
    [NonSerialized] public float alturaObjetivo = 1.64f;
    float _altura = 1.64f;

    /// <summary>Punto del mundo que mira la cabeza (null = mira hacia donde camina / al frente).</summary>
    public Func<Vector3> mirarA;
    /// <summary>Si no hay punto: yaw local (grados, respecto del rig) y pitch.</summary>
    [NonSerialized] public float yawLibre, pitchLibre = 6f;
    [NonSerialized] public float rapidezCabeza = 4.5f;

    // ---------------- Locomocion ----------------
    [NonSerialized] public bool caminando;
    [NonSerialized] public float faseCaminar;
    [NonSerialized] public bool controlarPiso = true;

    // ---------------- Manos ----------------
    public class Mano
    {
        public bool izquierda;
        public Func<Pose> objetivo;          // pose objetivo en el MUNDO (null = reposo)
        public float tweenDur, tweenT = 1f;   // interpolacion min-jerk desde la pose de inicio
        public Pose inicioLocal;              // en el espacio del TrackingSpace
        public Pose actualLocal;
        public bool init;
    }
    public readonly Mano[] manos = { new Mano { izquierda = true }, new Mano { izquierda = false } };

    Quaternion _cabezaLocal = Quaternion.identity;
    bool _cabezaInit;
    float _ruidoSemilla;
    Vector3 _inclinacion;
    /// <summary>Largo comodo del brazo del avatar (hombro a control), en metros.</summary>
    public const float LargoBrazo = 0.56f;

    static FieldInfo _campoTracking;
    OvrAvatarInputTrackingDelegatedProvider _proveedor;
    AvatarEntradaJugador _entradaParcheada;
    bool _registrado;

    public static DemoVR Crear()
    {
        if (I != null) return I;
        var go = new GameObject("[DemoVR]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<DemoVR>();
        go.AddComponent<DemoPoses>();
        return I;
    }

    void Awake()
    {
        I = this;
        _ruidoSemilla = 13.7f;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    // =====================================================================
    // Entrada: se escribe directamente en el estado de los controles de OVRInput.
    // Sin visor OVRManager no llama a OVRInput.Update(), asi que nadie lo pisa.
    // =====================================================================
    void Update()
    {
        if (rig == null) rig = FindObjectOfType<OVRCameraRig>();
        InyectarEntrada();
        ApagarLocomotor();
        ParchearAvatar();
    }

    void InyectarEntrada()
    {
        var s = new OVRPlugin.ControllerState6();
        s.ConnectedControllers = (uint)OVRInput.Controller.Touch;
        uint b = 0;
        if (A) b |= (uint)OVRInput.RawButton.A;
        if (B) b |= (uint)OVRInput.RawButton.B;
        if (X) b |= (uint)OVRInput.RawButton.X;
        if (Y) b |= (uint)OVRInput.RawButton.Y;
        if (clicL) b |= (uint)OVRInput.RawButton.LThumbstick;
        if (clicR) b |= (uint)OVRInput.RawButton.RThumbstick;
        s.LIndexTrigger = gatilloL; s.RIndexTrigger = gatilloR;
        s.LHandTrigger = gripL; s.RHandTrigger = gripR;
        s.LThumbstick = new OVRPlugin.Vector2f { x = stickL.x, y = stickL.y };
        s.RThumbstick = new OVRPlugin.Vector2f { x = stickR.x, y = stickR.y };
        const float u = 0.5f;
        if (gatilloL >= u) b |= (uint)OVRInput.RawButton.LIndexTrigger;
        if (gatilloR >= u) b |= (uint)OVRInput.RawButton.RIndexTrigger;
        if (gripL >= u) b |= (uint)OVRInput.RawButton.LHandTrigger;
        if (gripR >= u) b |= (uint)OVRInput.RawButton.RHandTrigger;
        if (stickL.y >= u) b |= (uint)OVRInput.RawButton.LThumbstickUp;
        if (stickL.y <= -u) b |= (uint)OVRInput.RawButton.LThumbstickDown;
        if (stickL.x <= -u) b |= (uint)OVRInput.RawButton.LThumbstickLeft;
        if (stickL.x >= u) b |= (uint)OVRInput.RawButton.LThumbstickRight;
        if (stickR.y >= u) b |= (uint)OVRInput.RawButton.RThumbstickUp;
        if (stickR.y <= -u) b |= (uint)OVRInput.RawButton.RThumbstickDown;
        if (stickR.x <= -u) b |= (uint)OVRInput.RawButton.RThumbstickLeft;
        if (stickR.x >= u) b |= (uint)OVRInput.RawButton.RThumbstickRight;
        s.Buttons = b;
        // Pulgares apoyados (el avatar los usa para la pose de los dedos).
        s.Touches = (uint)(OVRInput.RawTouch.LThumbRest | OVRInput.RawTouch.RThumbRest);
        if (gatilloL > 0.05f) s.Touches |= (uint)OVRInput.RawTouch.LIndexTrigger;
        if (gatilloR > 0.05f) s.Touches |= (uint)OVRInput.RawTouch.RIndexTrigger;

        foreach (var c in OVRInput.controllers)
        {
            c.previousState = c.currentState;
            var t = c.controllerType;
            if (t == OVRInput.Controller.Touch || t == OVRInput.Controller.LTouch || t == OVRInput.Controller.RTouch)
                c.currentState = s;
            else
                c.currentState = new OVRPlugin.ControllerState6();
        }
        OVRInput.connectedControllerTypes = OVRInput.Controller.Touch;
        OVRInput.activeControllerType = OVRInput.Controller.Touch;
    }

    /// <summary>El locomotor del Interaction SDK pelearia con el rig que movemos a mano: se apaga.</summary>
    void ApagarLocomotor()
    {
        if (rig == null) return;
        var raiz = rig.transform;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == "Locomotor" && t.gameObject.activeSelf) t.gameObject.SetActive(false);
    }

    /// <summary>El avatar del jugador, sin visor, usa poses fijas: se le dan las de la cabeza y las manos simuladas.</summary>
    void ParchearAvatar()
    {
        // La entrada que usa DE VERDAD el avatar actual (tras recargar la escena puede quedar otra vieja).
        var avatarActual = FindObjectOfType<AvatarJugador>();
        var entrada = avatarActual != null ? avatarActual.InputManager as AvatarEntradaJugador : null;
        if (entrada == null) entrada = FindObjectOfType<AvatarEntradaJugador>();
        if (entrada == null || rig == null) return;
        if (_campoTracking == null)
            _campoTracking = typeof(OvrAvatarInputManager).GetField("_inputTrackingProvider", BindingFlags.Instance | BindingFlags.NonPublic);
        if (_campoTracking == null) return;
        if (_entradaParcheada != entrada || _proveedor == null)
        {
            _proveedor = new OvrAvatarInputTrackingDelegatedProvider(new DelegadoTracking(this));
            _entradaParcheada = entrada;
        }
        if (!ReferenceEquals(_campoTracking.GetValue(entrada), _proveedor))
        {
            // Que inicialice lo suyo primero (si no, lo pisaria al inicializar).
            var _ = entrada.InputTrackingProvider;
            _campoTracking.SetValue(entrada, _proveedor);
            _registrado = false;
        }
        // La entidad registra el proveedor en el SDK nativo solo al crearse: se le pide que lo vuelva a leer.
        if (!_registrado)
        {
            var avatar = FindObjectOfType<AvatarJugador>();
            if (avatar != null && avatar.IsCreated)
            {
                var m = typeof(OvrAvatarEntity).GetMethod("UpdateInputProviders", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null) { m.Invoke(avatar, new object[] { entrada }); _registrado = true; }
            }
        }
    }

    class DelegadoTracking : OvrAvatarInputTrackingDelegate
    {
        readonly DemoVR _d;
        public DelegadoTracking(DemoVR d) { _d = d; }
        public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState s)
        {
            s = default;
            var rig = _d.rig;
            if (rig == null) return false;
            s.headsetActive = true;
            s.leftControllerActive = true;
            s.rightControllerActive = true;
            s.leftControllerVisible = false;
            s.rightControllerVisible = false;
            s.headset = (CAPI.ovrAvatar2Transform)rig.centerEyeAnchor;
            s.leftController = (CAPI.ovrAvatar2Transform)rig.leftHandAnchor;
            s.rightController = (CAPI.ovrAvatar2Transform)rig.rightHandAnchor;
            return true;
        }
    }

    // =====================================================================
    // Poses (las aplica DemoPoses, despues de que el camion/la moto muevan el rig)
    // =====================================================================

    public Transform Espacio => rig != null ? rig.trackingSpace : null;
    public Transform Cabeza => rig != null ? rig.centerEyeAnchor : null;

    static float MinJerk(float t) { t = Mathf.Clamp01(t); return t * t * t * (10f - 15f * t + 6f * t * t); }

    public void AplicarPoses()
    {
        if (rig == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var esp = rig.trackingSpace;

        // ---- Altura y balanceo al caminar ----
        _altura = Mathf.MoveTowards(_altura, alturaObjetivo, dt * 0.9f);
        float bob = 0f, bobLado = 0f;
        if (caminando)
        {
            bob = Mathf.Sin(faseCaminar * 2f) * 0.012f;
            bobLado = Mathf.Sin(faseCaminar) * 0.008f;
        }

        // ---- Cabeza ----
        var cab = rig.centerEyeAnchor;
        Quaternion deseada;
        if (mirarA != null)
        {
            Vector3 ojo = esp.TransformPoint(new Vector3(0f, _altura, 0f));
            Vector3 dir = mirarA() - ojo;
            if (dir.sqrMagnitude < 1e-4f) dir = esp.forward;
            Vector3 dirLocal = esp.InverseTransformDirection(dir);
            // Sin mirar en vertical hacia abajo (se veria el interior del propio avatar): maximo ~48 grados.
            float h = new Vector2(dirLocal.x, dirLocal.z).magnitude;
            float pitch = Mathf.Atan2(-dirLocal.y, Mathf.Max(1e-4f, h)) * Mathf.Rad2Deg;
            if (pitch > 40f)
            {
                Vector3 hd = h > 1e-4f ? new Vector3(dirLocal.x, 0f, dirLocal.z) / h : Vector3.forward;
                dirLocal = hd * Mathf.Cos(40f * Mathf.Deg2Rad) - Vector3.up * Mathf.Sin(40f * Mathf.Deg2Rad);
            }
            deseada = Quaternion.LookRotation(dirLocal, Vector3.up);
        }
        else deseada = Quaternion.Euler(pitchLibre, yawLibre, 0f);

        // Temblor humano leve + balanceo al caminar.
        float t = Time.time;
        float ny = (Mathf.PerlinNoise(_ruidoSemilla, t * 0.35f) - 0.5f) * 2.2f;
        float np = (Mathf.PerlinNoise(t * 0.3f, _ruidoSemilla) - 0.5f) * 1.6f;
        float nr = (Mathf.PerlinNoise(t * 0.25f, _ruidoSemilla * 2f) - 0.5f) * 1.2f;
        if (caminando) { np += Mathf.Sin(faseCaminar * 2f) * 0.6f; nr += Mathf.Sin(faseCaminar) * 0.9f; }
        deseada = deseada * Quaternion.Euler(np, ny, nr);

        if (!_cabezaInit) { _cabezaLocal = deseada; _cabezaInit = true; }
        _cabezaLocal = Quaternion.Slerp(_cabezaLocal, deseada, 1f - Mathf.Exp(-dt * rapidezCabeza));
        // Sin inclinacion lateral exagerada.
        cab.localRotation = _cabezaLocal;
        Vector3 yawFwd = _cabezaLocal * Vector3.forward; yawFwd.y = 0f;
        if (yawFwd.sqrMagnitude < 1e-4f) yawFwd = Vector3.forward;
        Quaternion yawLocal = Quaternion.LookRotation(yawFwd.normalized, Vector3.up);
        Vector3 cabezaBase = new Vector3(0f, _altura + bob, 0f) + yawLocal * new Vector3(bobLado, 0f, 0f);

        // Inclinacion del cuerpo: si una mano va mas lejos que el largo natural del brazo, la cabeza
        // (y con ella el torso del avatar) se acerca hacia ella; si va baja, se agacha un poco.
        Vector3 inclinar = Vector3.zero;
        for (int i = 0; i < 2; i++)
        {
            var m = manos[i];
            if (m.objetivo == null || !m.init) continue;
            Vector3 hombro = cabezaBase + yawLocal * new Vector3(m.izquierda ? -0.18f : 0.18f, -0.27f, -0.04f);
            Vector3 v = m.actualLocal.position - hombro;
            float exceso = v.magnitude - LargoBrazo;
            if (exceso > 0f) inclinar += v.normalized * exceso;
        }
        inclinar.y = Mathf.Min(0f, inclinar.y) * 0.7f;
        inclinar = Vector3.ClampMagnitude(inclinar, 0.42f);
        _inclinacion = Vector3.Lerp(_inclinacion, inclinar, 1f - Mathf.Exp(-dt * 7f));
        cab.localPosition = cabezaBase + _inclinacion;
        foreach (var ojo in new[] { rig.leftEyeAnchor, rig.rightEyeAnchor })
            if (ojo != null) ojo.SetLocalPositionAndRotation(cab.localPosition, cab.localRotation);

        // ---- Manos ----
        for (int i = 0; i < 2; i++) AplicarMano(manos[i], esp, yawLocal, dt);
    }

    /// <summary>Pose de reposo (local al TrackingSpace): manos relajadas a los costados, un poco al frente.</summary>
    public Pose ReposoLocal(bool izquierda, Quaternion yawLocal)
    {
        float s = izquierda ? -1f : 1f;
        float t = Time.time;
        float respira = Mathf.Sin(t * 1.3f + (izquierda ? 0.6f : 0f)) * 0.006f;
        float vaiven = caminando ? Mathf.Sin(faseCaminar + (izquierda ? 0f : Mathf.PI)) * 0.05f : 0f;
        Vector3 p = new Vector3(0.2f * s, _altura - 0.62f + respira, 0.2f + vaiven);
        Quaternion r = Quaternion.Euler(28f, -8f * s, 12f * s);
        return new Pose(yawLocal * p, yawLocal * r);
    }

    void AplicarMano(Mano m, Transform esp, Quaternion yawLocal, float dt)
    {
        Pose destino;
        if (m.objetivo != null)
        {
            Pose w = m.objetivo();
            destino = new Pose(esp.InverseTransformPoint(w.position), Quaternion.Inverse(esp.rotation) * w.rotation);
        }
        else destino = ReposoLocal(m.izquierda, yawLocal);

        if (!m.init) { m.actualLocal = destino; m.inicioLocal = destino; m.init = true; }

        if (m.tweenT < 1f)
        {
            m.tweenT = Mathf.Min(1f, m.tweenT + dt / Mathf.Max(0.01f, m.tweenDur));
            float e = MinJerk(m.tweenT);
            m.actualLocal = new Pose(Vector3.Lerp(m.inicioLocal.position, destino.position, e),
                                     Quaternion.Slerp(m.inicioLocal.rotation, destino.rotation, e));
        }
        else if (m.objetivo == null)
        {
            // En reposo sigue a la cabeza con un poco de retraso (no rigido).
            float k = 1f - Mathf.Exp(-dt * 6f);
            m.actualLocal = new Pose(Vector3.Lerp(m.actualLocal.position, destino.position, k),
                                     Quaternion.Slerp(m.actualLocal.rotation, destino.rotation, k));
        }
        else m.actualLocal = destino;

        var ancla = m.izquierda ? rig.leftHandAnchor : rig.rightHandAnchor;
        ancla.SetLocalPositionAndRotation(m.actualLocal.position, m.actualLocal.rotation);
    }

    /// <summary>Lleva la mano a una pose objetivo (que puede moverse) en 'dur' segundos con movimiento humano.</summary>
    public void MoverMano(bool izquierda, Func<Pose> objetivo, float dur)
    {
        var m = manos[izquierda ? 0 : 1];
        m.inicioLocal = m.actualLocal;
        m.objetivo = objetivo;
        m.tweenDur = Mathf.Max(0.01f, dur);
        m.tweenT = dur <= 0f ? 1f : 0f;
    }

    public void ManoAReposo(bool izquierda, float dur = 0.6f) => MoverMano(izquierda, null, dur);

    public float Grip(bool izquierda) => izquierda ? gripL : gripR;
    public void SetGrip(bool izquierda, float v) { if (izquierda) gripL = v; else gripR = v; }
    public void SetGatillo(bool izquierda, float v) { if (izquierda) gatilloL = v; else gatilloR = v; }
    public Transform Ancla(bool izquierda) => izquierda ? rig.leftHandAnchor : rig.rightHandAnchor;

    // ---- Piso bajo el jugador ----
    public bool AlturaPiso(Vector3 p, float desde, out float y)
    {
        y = 0f;
        float mejor = float.MaxValue;
        int mascara = ~(1 << MangueraBomberos.CapaManguera);
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, desde, p.z), Vector3.down, 6f, mascara, QueryTriggerInteraction.Ignore))
        {
            if (rig != null && h.collider.transform.IsChildOf(rig.transform)) continue;
            if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
            if (h.distance < mejor) { mejor = h.distance; y = h.point.y; }
        }
        return mejor < float.MaxValue;
    }
}

/// <summary>Aplica las poses de la persona simulada despues de que el camion y la moto (orden -900) muevan el rig.</summary>
[DefaultExecutionOrder(-850)]
public class DemoPoses : MonoBehaviour
{
    void Update()
    {
        if (DemoVR.I != null) DemoVR.I.AplicarPoses();
    }
}
