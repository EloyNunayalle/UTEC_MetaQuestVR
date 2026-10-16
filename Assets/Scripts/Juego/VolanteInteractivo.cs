using UnityEngine;

/// <summary>
/// Volante del camion agarrable: gira con la mano (GRIP o pellizco cerca del aro) o con
/// el joystick como respaldo. Tiene tope (gradosMaximos) y se queda donde lo dejas.
/// Le entrega el eje de giro (-1..1) al camion por CamionBomberos.ejeGiroManual.
/// - Gira sobre el CENTRO visual del volante (no sobre el pivote del nodo).
/// - Todo se calcula en LOCAL / con el marco base actual: sigue al camion cuando se mueve
///   (nada se queda atras y el agarre funciona siempre).
/// - El angulo de la mano se mide en un marco FIJO (sin realimentacion, no parpadea).
/// - anguloInicial endereza el modelo; las piezas en giranConElVolante giran con el.
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
    [Tooltip("Giro inicial del volante (grados): compensa la orientacion del modelo para que arranque recto.")]
    public float anguloInicial = -11f;
    [Tooltip("Piezas que giran junto con el volante (p. ej. las que dependen de el).")]
    public Transform[] giranConElVolante;

    float _angulo;
    Quaternion _rotBase;
    bool _init;
    int _mano = -1;
    float _manoIni;
    float _volanteIni;
    float _vibraProx;
    bool _enganchado;
    float _proxBusca;
    Vector3 _posBase;
    Vector3 _centroLocal;
    Vector3 _centroPadre;
    float _escalaMundo;
    Transform _padre;
    Vector3[] _piezasLocalPos;
    Quaternion[] _piezasLocalRot;

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

    // Marco base del volante EN EL MUNDO, calculado al vuelo (sigue al camion).
    Quaternion BaseMundoRot()
    {
        return _padre != null ? _padre.rotation * _rotBase : transform.rotation;
    }

    Vector3 CentroMundo()
    {
        return _padre != null ? _padre.TransformPoint(_centroPadre) : transform.TransformPoint(_centroLocal);
    }

    // Angulo de la mano medido en el marco FIJO del volante (marco base, no el que gira).
    float AnguloMano(Transform m)
    {
        Vector3 rel = m.position - CentroMundo();
        Quaternion b = BaseMundoRot();
        return Mathf.Atan2(Vector3.Dot(rel, b * Vector3.up), Vector3.Dot(rel, b * Vector3.right)) * Mathf.Rad2Deg;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!_init)
        {
            _init = true;
            _padre = transform.parent;
            _rotBase = transform.localRotation;
            _posBase = transform.localPosition;
            _escalaMundo = Mathf.Max(0.0001f, transform.lossyScale.x);

            // Centro visual del volante (en local y en el marco del padre; constantes).
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                _centroLocal = transform.InverseTransformPoint(b.center);
            }
            Vector3 sc = transform.localScale;
            _centroPadre = _posBase + _rotBase * Vector3.Scale(sc, _centroLocal);

            if (giranConElVolante != null && giranConElVolante.Length > 0)
            {
                _piezasLocalPos = new Vector3[giranConElVolante.Length];
                _piezasLocalRot = new Quaternion[giranConElVolante.Length];
                for (int i = 0; i < giranConElVolante.Length; i++)
                {
                    var p = giranConElVolante[i];
                    if (p == null) continue;
                    _piezasLocalPos[i] = p.localPosition;
                    _piezasLocalRot[i] = p.localRotation;
                }
            }
        }

        // El camion puede no existir al inicio (se agrega en runtime): se busca con reintentos.
        if (!_enganchado && Time.time >= _proxBusca)
        {
            _proxBusca = Time.time + 0.5f;
            if (camion == null) camion = FindObjectOfType<CamionBomberos>();
            if (camion != null)
            {
                camion.ejeGiroManual = Eje;
                _enganchado = true;
            }
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

            // Agarre por proximidad al aro (desde el centro actual: funciona aunque el camion se mueva).
            for (int i = 0; i < 2; i++)
            {
                var m = Mano(i);
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                Vector3 rel = (m.position - CentroMundo()) / _escalaMundo;
                float distAro = Mathf.Abs(rel.magnitude - radio);
                if (distAro > 0.13f || Mathf.Abs(Vector3.Dot(rel, BaseMundoRot() * Vector3.forward)) > 0.13f) continue;
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

        // Gira sobre el centro visual del volante (eje local Z = eje de la columna),
        // compensando que el pivote del nodo puede no estar en el centro.
        // anguloInicial endereza el modelo (las piezas usan el mismo valor).
        float visual = (invertir ? -_angulo : _angulo) + anguloInicial;
        Quaternion q = _rotBase * Quaternion.AngleAxis(visual, Vector3.forward);
        Vector3 scv = transform.localScale;
        transform.localPosition = _posBase + _rotBase * Vector3.Scale(scv, _centroLocal) - q * Vector3.Scale(scv, _centroLocal);
        transform.localRotation = q;

        // Las piezas giran con el volante (en local: siguen al camion, no se quedan atras).
        if (_piezasLocalPos != null)
        {
            Quaternion qP = Quaternion.AngleAxis(visual, _rotBase * Vector3.forward);
            for (int i = 0; i < _piezasLocalPos.Length; i++)
            {
                var p = giranConElVolante[i];
                if (p == null) continue;
                p.localRotation = qP * _piezasLocalRot[i];
                p.localPosition = _centroPadre + qP * (_piezasLocalPos[i] - _centroPadre);
            }
        }

        if (_mano >= 0 && Mathf.Abs(_angulo - _volanteIni) > 4f && Time.time >= _vibraProx)
        {
            _vibraProx = Time.time + 0.25f;
            EntradaVR.Vibrar(_mano == 0, 0.15f, 0.05f, 0.3f);
        }
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
        _enganchado = false;
    }
}
