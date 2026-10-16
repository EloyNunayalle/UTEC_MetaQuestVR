using UnityEngine;

/// <summary>
/// Palanca de cambios agarrable (modelo importado o procedural). La palanca se DESLIZA
/// por la guia (no rota): la mano la empuja y al soltarla se asienta en la ranura mas
/// cercana (con clic). Publica la marcha al camion:
/// 0 = P (parqueado), 1 = R (retroceso), 2 = N (neutro), 3 = D (adelante), 4 = L (baja).
/// Las ranuras extra se mapean con marchaDeRanura.
/// - La mano se sigue por proyeccion sobre la guia (marco fijo, sin realimentacion).
/// - El agarre es por proximidad al pomo (tope de la malla) en el mundo.
/// - Las letras (letras[]) se resaltan segun la ranura activa.
/// </summary>
public class PalancaCambios : MonoBehaviour
{
    public CamionBomberos camion;
    [Tooltip("Posiciones de las ranuras a lo largo de la guia (unidades locales).")]
    public float[] posicionesRanuras = new float[] { 0f, -0.4f, -0.8f, -1.2f };
    [Tooltip("Marcha que entrega cada ranura (0=P, 1=R, 2=N, 3=D, 4=L).")]
    public int[] marchaDeRanura;
    [Tooltip("Eje local de deslizamiento (direccion de la guia).")]
    public Vector3 ejeLocal = Vector3.up;
    [Tooltip("Letras que se resaltan, en el orden de las ranuras.")]
    public Renderer[] letras;
    [Tooltip("Marcha inicial (3 = D).")]
    public int marcha = 3;
    [Tooltip("(Compatibilidad con la palanca procedural) Distancia del pomo al pivote (m).")]
    public float radioPomo = 0.24f;
    [Tooltip("Margen extra mas alla de las ranuras extremas (unidades locales).")]
    public float margenExtra = 0.12f;

    float _desplazamiento;
    Vector3 _baseLocalPos;
    Vector3 _knobLocalPadre;
    Transform _padre;
    Vector3 _localKnob;
    bool _init;
    int _mano = -1;
    float _manoIni;
    float _palancaIni;
    bool _asentando;
    int _destinoRanura;
    bool _enganchado;
    float _proxBusca;
    Color[] _colBase;
    int _ranuraVisible = -1;

    public string NombreMarcha
    {
        get { return marcha == 0 ? "P" : marcha == 1 ? "R" : marcha == 2 ? "N" : marcha == 4 ? "L" : "D"; }
    }

    int Marcha()
    {
        return marcha;
    }

    Transform Mano(int i)
    {
        var j = JuegoBomberos.Instancia;
        var rig = j != null ? j.rig : null;
        if (rig == null) return null;
        return i == 0 ? rig.leftHandAnchor : rig.rightHandAnchor;
    }

    float Ranura(int i)
    {
        if (posicionesRanuras == null || posicionesRanuras.Length == 0) return 0f;
        return posicionesRanuras[Mathf.Clamp(i, 0, posicionesRanuras.Length - 1)];
    }

    int RanuraCercana(float d)
    {
        int mejor = 0;
        float mejorDist = float.MaxValue;
        for (int i = 0; i < posicionesRanuras.Length; i++)
        {
            float dd = Mathf.Abs(d - Ranura(i));
            if (dd < mejorDist) { mejorDist = dd; mejor = i; }
        }
        return mejor;
    }

    int MarchaDeRanura(int i)
    {
        if (marchaDeRanura == null || marchaDeRanura.Length == 0) return Mathf.Clamp(i, 0, 3);
        return marchaDeRanura[Mathf.Clamp(i, 0, marchaDeRanura.Length - 1)];
    }

    // Proyeccion de la mano sobre la guia, en unidades locales del padre.
    float ProyeccionMano(Transform m)
    {
        if (_padre == null) return 0f;
        Vector3 local = _padre.InverseTransformPoint(m.position) - _knobLocalPadre;
        return Vector3.Dot(local, ejeLocal);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!_init)
        {
            _init = true;
            _baseLocalPos = transform.localPosition;
            _padre = transform.parent;
            _destinoRanura = Mathf.Clamp(marcha, 0, posicionesRanuras != null && posicionesRanuras.Length > 0 ? posicionesRanuras.Length - 1 : 0);
            _desplazamiento = Ranura(_destinoRanura);

            // Punto del pomo (tope de la malla) en local, para el agarre.
            Renderer rr = GetComponentInChildren<Renderer>();
            if (rr != null)
            {
                Bounds b = rr.bounds;
                Vector3 tope = new Vector3(b.center.x, b.max.y, b.center.z);
                _localKnob = transform.InverseTransformPoint(tope);
            }
            else _localKnob = new Vector3(0f, 0.24f, 0f);

            _knobLocalPadre = _baseLocalPos + _localKnob;

            if (letras != null && letras.Length > 0)
            {
                _colBase = new Color[letras.Length];
                for (int i = 0; i < letras.Length; i++)
                {
                    if (letras[i] == null) continue;
                    _colBase[i] = letras[i].material.color;
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
                camion.marchaManual = Marcha;
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
                float proy = ProyeccionMano(m);
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < posicionesRanuras.Length; i++)
                {
                    float r = Ranura(i);
                    if (r < min) min = r;
                    if (r > max) max = r;
                }
                min -= margenExtra;
                max += margenExtra;
                _desplazamiento = Mathf.Clamp(_palancaIni + (proy - _manoIni), min, max);
            }
        }
        else
        {
            if (_asentando)
            {
                float destino = Ranura(_destinoRanura);
                _desplazamiento = Mathf.MoveTowards(_desplazamiento, destino, 1.2f * dt);
                if (Mathf.Abs(_desplazamiento - destino) < 0.002f) { _desplazamiento = destino; _asentando = false; }
            }

            // Agarre por proximidad al pomo (en el mundo, sigue la pose actual).
            for (int i = 0; i < 2; i++)
            {
                var m = Mano(i);
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                Vector3 pomo = transform.TransformPoint(_localKnob);
                if (Vector3.Distance(m.position, pomo) > 0.16f) continue;
                if (EntradaVR.AgarreDown(i == 0))
                {
                    _mano = i;
                    _manoIni = ProyeccionMano(m);
                    _palancaIni = _desplazamiento;
                    _asentando = false;
                    EntradaVR.Vibrar(i == 0, 0.4f, 0.08f, 0.6f);
                    SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position, 0.6f);
                    break;
                }
            }
        }

        // Se desliza por la guia.
        transform.localPosition = _baseLocalPos + ejeLocal * _desplazamiento;

        // Letras: se resalta la ranura activa.
        int visible = RanuraCercana(_desplazamiento);
        if (visible != _ranuraVisible)
        {
            _ranuraVisible = visible;
            ActualizarLetras(visible);
        }
    }

    void ActualizarLetras(int activa)
    {
        if (letras == null || _colBase == null) return;
        for (int i = 0; i < letras.Length; i++)
        {
            if (letras[i] == null) continue;
            Material mat = letras[i].material;
            if (i == activa)
            {
                mat.color = _colBase[i] * 1.35f;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", _colBase[i] * 1.6f);
                }
            }
            else
            {
                mat.color = _colBase[i];
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    void Soltar()
    {
        if (_mano < 0) return;
        _mano = -1;
        _destinoRanura = RanuraCercana(_desplazamiento);
        marcha = MarchaDeRanura(_destinoRanura);
        _asentando = true;
        EntradaVR.VibrarAmbas(0.25f, 0.06f, 0.5f);
        SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position, 0.8f);
    }

    void OnDisable()
    {
        if (camion != null) camion.marchaManual = null;
        _enganchado = false;
    }
}
