using UnityEngine;

/// <summary>
/// Volante del camion. En modo Volante se agarra con la mano (GRIP o pellizco cerca
/// del aro) y entrega el eje a CamionBomberos.ejeGiroManual. En modo Joystick es solo
/// visual: refleja la posicion del stick (vuelve al centro al soltar el stick, igual que
/// el giro del camion), pero no se puede agarrar.
/// - Gira sobre el CENTRO visual del volante (no sobre el pivote del nodo).
/// - Todo se calcula en LOCAL / con el marco base actual: sigue al camion cuando se mueve
///   (nada se queda atras y el agarre funciona siempre).
/// - El angulo de la mano se mide en un marco FIJO (sin realimentacion, no parpadea) y se
///   acumula cuadro a cuadro: no hay saltos al pasar los 180 grados, al llegar al tope ni
///   cuando la mano cruza el centro del aro.
/// - Al soltarlo en modo Volante, vuelve SUAVE al centro solo si el camion avanza (como el
///   autocentrado de un vehiculo real); detenido se queda donde lo dejaste.
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
    [Header("Modo Volante: autocentrado al soltar")]
    [Tooltip("Al soltar el volante, vuelve solo al centro mientras el camion avanza.")]
    public bool autocentrarAlSoltar = true;
    [Tooltip("Grados por segundo de retorno a la velocidad de referencia (o mas).")]
    public float velocidadAutocentrado = 90f;
    [Tooltip("Velocidad del camion (km/h) a la que el retorno alcanza su maximo. Detenido no retorna.")]
    public float kmhAutocentradoMax = 15f;
    [Tooltip("Distancia minima (m) de la mano al eje para medir su angulo (cerca del centro el angulo no es confiable).")]
    public float radioMinimoMano = 0.06f;
    [Tooltip("Cambio maximo de angulo de la mano por cuadro (grados); mas que esto es un salto de medicion y se ignora.")]
    public float saltoMaximoPorCuadro = 45f;

    float _angulo;
    Quaternion _rotBase;
    bool _init;
    int _mano = -1;
    float _manoPrev;
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
    float AnguloMano(Transform m) => AnguloMano(m, out _);

    float AnguloMano(Transform m, out float radioEnPlano)
    {
        Vector3 rel = m.position - CentroMundo();
        Quaternion b = BaseMundoRot();
        float x = Vector3.Dot(rel, b * Vector3.right), y = Vector3.Dot(rel, b * Vector3.up);
        radioEnPlano = Mathf.Sqrt(x * x + y * y);
        return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool modoVolante = ConfiguracionConduccion.Modo == ModoConduccion.Volante;

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

        // En modo Joystick el volante es solo visual: no se puede agarrar.
        if (_mano >= 0 && !modoVolante) Soltar();

        if (_mano >= 0)
        {
            var m = Mano(_mano);
            if (m == null || !EntradaVR.Agarrando(_mano == 0))
            {
                Soltar();
            }
            else
            {
                // Incremental: se suma solo el giro de la mano desde el cuadro anterior. Asi el volante
                // no salta al pasar los 180 grados (DeltaAngle desde el agarre se "daba vuelta"), al
                // llegar al tope (lo que sobra se descarta y al volver responde de inmediato) ni cuando
                // la mano pasa cerca del eje, donde el angulo medido no es confiable.
                float ang = AnguloMano(m, out float r);
                float d = Mathf.DeltaAngle(_manoPrev, ang);
                _manoPrev = ang;
                if (r >= radioMinimoMano * _escalaMundo && Mathf.Abs(d) <= saltoMaximoPorCuadro)
                    _angulo = Mathf.Clamp(_angulo + d, -gradosMaximos, gradosMaximos);
            }
        }
        else
        {
            // Modo Joystick: el volante sigue al stick (solo visual; el camion gira
            // directamente con ese mismo stick). En modo Volante esta entrada se ignora.
            if (!modoVolante)
            {
                Vector2 izq = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
                Vector2 der = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
                float h = Mathf.Abs(der.x) > Mathf.Abs(izq.x) ? der.x : izq.x;
                float th = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(th) > Mathf.Abs(h)) h = th;
                if (Mathf.Abs(h) < zonaMuerta) h = 0f;
                // En modo Joystick el volante refleja la posicion del stick y
                // vuelve al centro al soltarlo (no se queda girado).
                _angulo = Mathf.MoveTowards(_angulo, h * gradosMaximos, velocidadStick * dt);
            }
            else
            {
                // Suelto: autocentrado suave proporcional a la velocidad (detenido no se mueve).
                if (autocentrarAlSoltar && camion != null && Mathf.Abs(_angulo) > 0.01f)
                {
                    float f = Mathf.Clamp01(camion.VelocidadKmh / Mathf.Max(0.1f, kmhAutocentradoMax));
                    _angulo = Mathf.MoveTowards(_angulo, 0f, velocidadAutocentrado * f * dt);
                }

                // Agarre por proximidad al aro, solo en modo Volante.
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
                        _manoPrev = AnguloMano(m);
                        _volanteIni = _angulo;
                        EntradaVR.Vibrar(i == 0, 0.4f, 0.08f, 0.6f);
                        SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position, 0.6f);
                        break;
                    }
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
