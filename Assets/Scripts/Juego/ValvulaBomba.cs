using TMPro;
using UnityEngine;

/// <summary>
/// Valvula de presion de la bomba, en el panel del camion (junto a la conexion de la manguera),
/// con manometro de aguja.
/// - Se toma con GRIP y se gira: moviendo la mano en circulo alrededor de la rueda (como un
///   volante) o girando la muneca. A la derecha (horario) sube la presion.
/// - Presion 15 % - 100 %: a mayor presion el chorro llega mas lejos y mas alto, sale mas agua,
///   apaga el fuego mas rapido y el retroceso de la boquilla es mayor.
/// - Feedback: clic y vibracion cada 15 grados, tope al llegar al minimo o maximo,
///   aguja del manometro y sonido de la bomba (mas agudo con la presion) mientras sale agua.
/// La crea MangueraBomberos (no requiere configuracion).
/// </summary>
public class ValvulaBomba : MonoBehaviour
{
    [Tooltip("Grados de giro de la valvula entre la presion minima y la maxima.")]
    public float giroTotal = 270f;
    [Tooltip("Multiplicador del giro de la mano (mayor = la valvula gira mas con menos movimiento).")]
    public float sensibilidad = 1.6f;
    [Range(0f, 1f)] public float presionMinima = 0.15f;
    [Tooltip("Presion al iniciar (0-1).")]
    [Range(0f, 1f)] public float presionInicial = 0.5f;
    public float radioAgarre = 0.24f;

    /// <summary>Presion de la bomba 0-1 (la usa LanzaAgua).</summary>
    public float Presion => Mathf.Lerp(presionMinima, 1f, _giro / giroTotal);
    public int PresionPorcentaje => Mathf.RoundToInt(Presion * 100f);

    OVRCameraRig _rig;
    MangueraBomberos _manguera;
    Transform _rueda, _aguja;
    TextMeshPro _texto;
    Renderer[] _rendersRueda;
    AudioSource _bomba;
    float _giro;
    int _mano = -1, _hoverPrev = -1;
    Quaternion _rotManoPrev;
    Vector3 _vecManoPrev;
    float _ultimoClic;

    /// <summary>Crea la valvula y el manometro. Su +Z local apunta hacia afuera del panel.</summary>
    public static ValvulaBomba Crear(Transform padre, Vector3 posicion, Quaternion rotacion, OVRCameraRig rig, MangueraBomberos manguera)
    {
        var go = new GameObject("Valvula_Bomba");
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(posicion, rotacion);
        var v = go.AddComponent<ValvulaBomba>();
        v._rig = rig;
        v._manguera = manguera;
        v.Construir();
        return v;
    }

    void Construir()
    {
        _giro = Mathf.InverseLerp(presionMinima, 1f, presionInicial) * giroTotal;
        var rojo = VisualesVR.MaterialOpaco(new Color(0.8f, 0.08f, 0.05f));
        var metal = VisualesVR.MaterialOpaco(new Color(0.6f, 0.6f, 0.62f));
        var negro = VisualesVR.MaterialOpaco(new Color(0.06f, 0.06f, 0.06f));
        var blanco = VisualesVR.MaterialSinLuz(new Color(0.95f, 0.95f, 0.92f));

        // Eje de la valvula saliendo del panel.
        var eje = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Eje", transform, metal);
        eje.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        eje.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        eje.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

        // Rueda (volante de la valvula): aro + rayos + centro. Gira sobre el +Z local.
        _rueda = new GameObject("Rueda").transform;
        _rueda.SetParent(transform, false);
        _rueda.localPosition = new Vector3(0f, 0f, 0.075f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f;
            var tramo = VisualesVR.Primitiva(PrimitiveType.Cube, "Aro", _rueda, rojo);
            tramo.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            tramo.transform.localPosition = Quaternion.Euler(0f, 0f, a) * new Vector3(0.11f, 0f, 0f);
            tramo.transform.localScale = new Vector3(0.022f, 0.06f, 0.022f);
        }
        for (int i = 0; i < 3; i++)
        {
            var rayo = VisualesVR.Primitiva(PrimitiveType.Cube, "Rayo", _rueda, rojo);
            rayo.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            rayo.transform.localScale = new Vector3(0.22f, 0.016f, 0.014f);
        }
        var centro = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Centro", _rueda, metal);
        centro.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        centro.transform.localScale = new Vector3(0.05f, 0.015f, 0.05f);
        _rendersRueda = _rueda.GetComponentsInChildren<Renderer>();

        // Manometro arriba de la valvula.
        var mano = new GameObject("Manometro").transform;
        mano.SetParent(transform, false);
        mano.localPosition = new Vector3(0f, 0.25f, 0.03f);
        var caja = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Caja", mano, negro);
        caja.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        caja.transform.localScale = new Vector3(0.17f, 0.02f, 0.17f);
        var dial = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Dial", mano, blanco);
        dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        dial.transform.localPosition = new Vector3(0f, 0f, 0.021f);
        dial.transform.localScale = new Vector3(0.15f, 0.002f, 0.15f);
        _aguja = new GameObject("Aguja").transform;
        _aguja.SetParent(mano, false);
        _aguja.localPosition = new Vector3(0f, 0f, 0.025f);
        var punta = VisualesVR.Primitiva(PrimitiveType.Cube, "Punta", _aguja, VisualesVR.MaterialSinLuz(new Color(0.85f, 0.05f, 0.05f)));
        punta.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        punta.transform.localScale = new Vector3(0.006f, 0.06f, 0.003f);

        // Texto: el +Z del objeto apunta hacia afuera del panel, asi que el texto (que se lee
        // desde su -Z) se gira 180 para que lo lea el jugador parado frente al panel.
        _texto = VisualesVR.Texto("Texto", transform, 0.03f, new Vector2(0.4f, 0.12f));
        _texto.transform.localPosition = new Vector3(0f, 0.42f, 0.04f);
        _texto.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        _texto.fontStyle = FontStyles.Bold;
        _texto.outlineWidth = 0.2f;
        _texto.outlineColor = Color.black;

        _bomba = SonidosVR.CrearBucle(transform, SonidosVR.Motor, 0.35f, 20f);
        Actualizar();
    }

    Transform AnclaMano(int i) => _rig == null ? null : (i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor);

    void Update()
    {
        if (_mano < 0)
        {
            int hover = -1;
            for (int i = 0; i < 2; i++)
            {
                var m = AnclaMano(i);
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(m.position, _rueda.position) > radioAgarre) continue;
                // La mano que sostiene la boquilla no agarra la valvula.
                if (_manguera != null && _manguera.Mano == i) continue;
                hover = i;
                if (EntradaVR.AgarreDown(i == 0))
                {
                    _mano = i;
                    _rotManoPrev = m.rotation;
                    _vecManoPrev = VectorEnRueda(m.position);
                    EntradaVR.Vibrar(i == 0, 0.5f, 0.08f, 0.6f);
                    SonidosVR.ReproducirEn(SonidosVR.Agarrar, _rueda.position, 0.7f);
                    break;
                }
            }
            if (hover >= 0 && hover != _hoverPrev) EntradaVR.Vibrar(hover == 0, 0.2f, 0.04f, 0.8f);
            _hoverPrev = hover;
            VisualesVR.Tenir(_rendersRueda, new Color(1f, 0.85f, 0.1f), hover >= 0 && _mano < 0 ? 0.5f : 0f);
        }
        else
        {
            var m = AnclaMano(_mano);
            if (m == null || !EntradaVR.Agarrando(_mano == 0))
            {
                _mano = -1;
            }
            else
            {
                // Visto desde afuera, girar en sentido horario sube la presion.
                Vector3 eje = transform.forward;

                // 1) Mano girando en circulo alrededor del centro de la rueda (como un volante).
                Vector3 vec = VectorEnRueda(m.position);
                float orbita = 0f;
                if (vec.magnitude > 0.035f && _vecManoPrev.magnitude > 0.035f)
                    orbita = Vector3.SignedAngle(_vecManoPrev, vec, eje);
                _vecManoPrev = vec;

                // 2) Giro de la muneca alrededor del eje (como una perilla).
                Quaternion delta = m.rotation * Quaternion.Inverse(_rotManoPrev);
                _rotManoPrev = m.rotation;
                delta.ToAngleAxis(out float angulo, out Vector3 ejeDelta);
                if (angulo > 180f) angulo -= 360f;
                float muneca = float.IsNaN(ejeDelta.x) ? 0f : angulo * Vector3.Dot(ejeDelta, eje);

                // Se usa el movimiento mas grande de los dos (no se suman: al girar el volante la muneca tambien gira).
                float giro = Mathf.Abs(orbita) > Mathf.Abs(muneca) ? orbita : muneca;
                if (Mathf.Abs(giro) < 40f) Girar(giro * sensibilidad); // ignora saltos del tracking
            }
        }
        Actualizar();
    }

    /// <summary>Posicion de la mano proyectada en el plano de la rueda, relativa a su centro.</summary>
    Vector3 VectorEnRueda(Vector3 mano) => Vector3.ProjectOnPlane(mano - _rueda.position, transform.forward);

    void Girar(float grados)
    {
        float antes = _giro;
        _giro = Mathf.Clamp(_giro + grados, 0f, giroTotal);

        // Clic cada 15 grados.
        if (Mathf.FloorToInt(antes / 15f) != Mathf.FloorToInt(_giro / 15f) && Time.time - _ultimoClic > 0.03f)
        {
            _ultimoClic = Time.time;
            SonidosVR.ReproducirEn(SonidosVR.Clic, _rueda.position, 0.5f);
            EntradaVR.Vibrar(_mano == 0, 0.25f, 0.03f, 0.9f);
        }
        // Tope al llegar al extremo.
        bool tope = (_giro <= 0f || _giro >= giroTotal) && Mathf.Abs(antes - _giro) > 0.01f;
        if (tope) EntradaVR.Vibrar(_mano == 0, 0.8f, 0.12f, 0.2f);
    }

    void Actualizar()
    {
        _rueda.localRotation = Quaternion.Euler(0f, 0f, _giro); // la rueda gira con la mano
        // Desde afuera, un angulo positivo sobre +Z local se ve en sentido horario.
        float t = _giro / giroTotal;
        _aguja.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-120f, 120f, t)); // de izquierda a derecha, visto de frente

        int pct = PresionPorcentaje;
        string color = pct >= 80 ? "#FF6B5A" : pct >= 45 ? "#FFD54A" : "#7FD4FF";
        _texto.text = $"PRESIÓN BOMBA <color={color}>{pct}%</color>\n<size=60%>{(_mano >= 0 ? "Gira la mano en círculo (horario = más)" : "Toma la válvula con GRIP y gírala")}</size>";

        // Sonido de la bomba mientras sale agua.
        var lanza = _manguera != null ? _manguera.Lanza : null;
        float caudal = lanza != null ? lanza.Caudal : 0f;
        if (caudal > 0.02f)
        {
            if (!_bomba.isPlaying) _bomba.Play();
            _bomba.volume = 0.2f + 0.3f * caudal;
            _bomba.pitch = 0.8f + 0.8f * Presion;
        }
        else if (_bomba.isPlaying) _bomba.Stop();
    }
}
