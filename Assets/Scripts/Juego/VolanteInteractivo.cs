using UnityEngine;

/// <summary>
/// Volante del camion agarrable: gira con la mano (GRIP o pellizco cerca del aro) o con
/// el joystick como respaldo. Tiene tope (gradosMaximos) y se queda donde lo dejas.
/// Le entrega el eje de giro (-1..1) al camion por CamionBomberos.ejeGiroManual.
/// </summary>
public class VolanteInteractivo : MonoBehaviour
{
    public CamionBomberos camion;
    [Tooltip("Radio del aro (m): la mano debe acercarse a esta distancia del eje.")]
    public float radio = 0.215f;
    public float gradosMaximos = 120f;
    [Tooltip("Grados por segundo que gira con el joystick a fondo.")]
    public float velocidadStick = 110f;
    public float zonaMuerta = 0.12f;
    public bool invertir;

    float _angulo;
    Quaternion _rotBase;
    bool _init;
    int _mano = -1;
    float _manoIni;
    float _volanteIni;
    float _vibraProx;

    Transform Mano(int i)
    {
        var j = JuegoBomberos.Instancia;
        var rig = j != null ? j.rig : null;
        if (rig == null) return null;
        return i == 0 ? rig.leftHandAnchor : rig.rightHandAnchor;
    }

    float Eje()
    {
        return Mathf.Clamp(_angulo / gradosMaximos, -1f, 1f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!_init)
        {
            _init = true;
            _rotBase = transform.localRotation;
            if (camion != null) camion.ejeGiroManual = Eje;
        }

        if (_mano >= 0)
        {
            var m = Mano(_mano);
            if (m == null || !EntradaVR.Agarrando(_mano == 0))
            {
                Soltar();
            }
            else
            {
                float ang = AnguloMano(m);
                _angulo = Mathf.Clamp(_volanteIni + Mathf.DeltaAngle(_manoIni, ang), -gradosMaximos, gradosMaximos);
            }
        }
        else
        {
            // Respaldo: joystick o teclado empujan el angulo (al soltar queda donde esta).
            Vector2 izq = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            Vector2 der = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
            float h = Mathf.Abs(der.x) > Mathf.Abs(izq.x) ? der.x : izq.x;
            float th = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(th) > Mathf.Abs(h)) h = th;
            if (Mathf.Abs(h) < zonaMuerta) h = 0f;
            if (h != 0f) _angulo = Mathf.Clamp(_angulo + h * velocidadStick * dt, -gradosMaximos, gradosMaximos);

            // Agarre por proximidad al aro.
            for (int i = 0; i < 2; i++)
            {
                var m = Mano(i);
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                Vector3 local = transform.InverseTransformPoint(m.position);
                float distAro = Mathf.Abs(local.magnitude - radio);
                if (distAro > 0.13f || Mathf.Abs(local.z) > 0.13f) continue;
                if (EntradaVR.AgarreDown(i == 0))
                {
                    _mano = i;
                    _manoIni = AnguloMano(m);
                    _volanteIni = _angulo;
                    EntradaVR.Vibrar(i == 0, 0.4f, 0.08f, 0.6f);
                    SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position, 0.6f);
                    break;
                }
            }
        }

        // Gira el pivote en su eje local Z (eje de la columna de direccion).
        float visual = invertir ? -_angulo : _angulo;
        transform.localRotation = _rotBase * Quaternion.AngleAxis(visual, Vector3.forward);

        if (_mano >= 0 && Mathf.Abs(_angulo - _volanteIni) > 4f && Time.time >= _vibraProx)
        {
            _vibraProx = Time.time + 0.25f;
            EntradaVR.Vibrar(_mano == 0, 0.15f, 0.05f, 0.3f);
        }
    }

    float AnguloMano(Transform m)
    {
        Vector3 local = transform.InverseTransformPoint(m.position);
        return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
    }

    void Soltar()
    {
        if (_mano < 0) return;
        _mano = -1;
        SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position, 0.5f);
    }

    void OnDisable()
    {
        if (camion != null) camion.ejeGiroManual = null;
    }
}
