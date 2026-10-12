using UnityEngine;

/// <summary>
/// Caminar libre: reemplaza la locomocion del Interaction SDK (que choca con todo)
/// por un movimiento con joystick que atraviesa paredes y objetos, pero sigue
/// la altura del piso (escaleras, desniveles).
/// - Stick izquierdo: caminar hacia donde miras.
/// - Stick derecho a los lados: girar de a 30 grados.
/// - Teclado (editor): flechas para caminar.
/// Se pausa sola mientras el jugador esta sentado en el camion.
/// </summary>
[DefaultExecutionOrder(9000)]
public class LocomocionLibre : MonoBehaviour
{
    public static bool Pausada;

    public float velocidad = 2.2f;
    public float velocidadRapida = 4f;
    public float anguloGiro = 30f;
    [Tooltip("Altura maxima de escalon que se sube automaticamente.")]
    public float alturaEscalon = 0.5f;
    public float zonaMuerta = 0.15f;
    [Tooltip("Segundos de espera al iniciar (para que el rig quede ubicado).")]
    public float esperaInicial = 0.5f;

    OVRCameraRig _rig;
    float _rigSobrePiso;
    bool _activa;
    bool _giroListo = true;
    float _velY;

    public void Inicializar(OVRCameraRig rig)
    {
        _rig = rig;
        Pausada = false;
    }

    void Update()
    {
        if (_rig == null) return;

        if (!_activa)
        {
            if (Time.timeSinceLevelLoad < esperaInicial) return;
            Activar();
        }

        if (Pausada) return;

        var rigT = _rig.transform;
        var cabeza = _rig.centerEyeAnchor;

        // ---- Movimiento ----
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (Input.GetKey(KeyCode.UpArrow)) stick.y = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) stick.y = -1f;
        if (Input.GetKey(KeyCode.LeftArrow)) stick.x = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) stick.x = 1f;
        if (stick.magnitude < zonaMuerta) stick = Vector2.zero;

        if (stick != Vector2.zero)
        {
            Vector3 f = cabeza.forward; f.y = 0f; f.Normalize();
            Vector3 d = cabeza.right; d.y = 0f; d.Normalize();
            // Gatillo izquierdo presionado = correr.
            bool rapido = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.5f || Input.GetKey(KeyCode.LeftShift);
            float v = rapido ? velocidadRapida : velocidad;
            rigT.position += (f * stick.y + d * stick.x) * (v * Time.deltaTime);
        }

        // ---- Giro por pasos (comodo en VR) ----
        float gx = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;
        if (Input.GetKeyDown(KeyCode.Comma)) gx = -1f;
        if (Input.GetKeyDown(KeyCode.Period)) gx = 1f;
        if (Mathf.Abs(gx) < 0.3f) _giroListo = true;
        else if (_giroListo && Mathf.Abs(gx) > 0.7f)
        {
            _giroListo = false;
            rigT.RotateAround(cabeza.position, Vector3.up, Mathf.Sign(gx) * anguloGiro);
        }

        // ---- Seguir el piso ----
        if (BuscarPiso(cabeza.position, rigT.position.y - _rigSobrePiso, out float piso))
        {
            float objetivo = piso + _rigSobrePiso;
            float y = rigT.position.y;
            if (objetivo >= y)
            {
                // Subir escalones: suave pero rapido.
                y = Mathf.Lerp(y, objetivo, 1f - Mathf.Exp(-Time.deltaTime * 15f));
                _velY = 0f;
            }
            else
            {
                // Bajar: cae con gravedad hasta el piso.
                _velY -= 9.8f * Time.deltaTime;
                y = Mathf.Max(objetivo, y + _velY * Time.deltaTime);
                if (y <= objetivo) _velY = 0f;
            }
            rigT.position = new Vector3(rigT.position.x, y, rigT.position.z);
        }
    }

    void Activar()
    {
        _activa = true;

        // Apaga la locomocion del SDK (la que bloquea al jugador).
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Locomot")) continue;
            if (_rig.centerEyeAnchor.IsChildOf(t)) continue;
            t.gameObject.SetActive(false);
        }

        // Guarda la altura del rig sobre el piso tal como la dejo el SDK (altura fija del jugador).
        var rigT = _rig.transform;
        _rigSobrePiso = 0f;
        if (BuscarPiso(_rig.centerEyeAnchor.position, rigT.position.y, out float piso))
            _rigSobrePiso = rigT.position.y - piso;
    }

    /// <summary>Piso mas alto bajo la cabeza que no supere un escalon sobre los pies actuales.</summary>
    bool BuscarPiso(Vector3 cabeza, float pies, out float piso)
    {
        piso = 0f;
        Vector3 origen = new Vector3(cabeza.x, pies + alturaEscalon + 0.05f, cabeza.z);
        var hits = Physics.RaycastAll(origen, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        bool hay = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(_rig.transform)) continue;
            if (h.normal.y < 0.5f) continue; // solo superficies horizontales
            if (!hay || h.point.y > piso) { piso = h.point.y; hay = true; }
        }
        return hay;
    }
}
