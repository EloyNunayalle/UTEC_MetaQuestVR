using UnityEngine;

/// <summary>
/// Palanca de cambios P-R-N-D agarrable: se mueve por la guia, al soltarla se asienta en
/// la ranura mas cercana (con clic). Publica la marcha al camion:
/// 0 = P (parqueado), 1 = R (retroceso), 2 = N (neutro), 3 = D (adelante).
/// </summary>
public class PalancaCambios : MonoBehaviour
{
    public CamionBomberos camion;
    [Tooltip("Angulos de las ranuras P, R, N, D (grados, + = hacia el frente).")]
    public float[] angulosRanuras = new float[] { 24f, 8f, -8f, -24f };
    [Tooltip("Distancia del pomo al pivote (m).")]
    public float radioPomo = 0.24f;
    [Tooltip("Marcha inicial (3 = D).")]
    public int marcha = 3;

    float _angulo;
    Quaternion _rotBase;
    bool _init;
    int _mano = -1;
    float _manoIni;
    float _palancaIni;
    bool _asentando;

    public string NombreMarcha
    {
        get { return marcha == 0 ? "P" : marcha == 1 ? "R" : marcha == 2 ? "N" : "D"; }
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
        if (angulosRanuras == null || angulosRanuras.Length == 0) return 0f;
        return angulosRanuras[Mathf.Clamp(i, 0, angulosRanuras.Length - 1)];
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!_init)
        {
            _init = true;
            _rotBase = transform.localRotation;
            _angulo = Ranura(marcha);
            if (camion != null) camion.marchaManual = Marcha;
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
                float min = Ranura(angulosRanuras.Length - 1) - 4f;
                float max = Ranura(0) + 4f;
                _angulo = Mathf.Clamp(_palancaIni + Mathf.DeltaAngle(_manoIni, ang), min, max);
            }
        }
        else
        {
            if (_asentando)
            {
                float destino = Ranura(marcha);
                _angulo = Mathf.MoveTowards(_angulo, destino, 90f * dt);
                if (Mathf.Abs(_angulo - destino) < 0.2f) { _angulo = destino; _asentando = false; }
            }

            // Agarre por proximidad al pomo.
            for (int i = 0; i < 2; i++)
            {
                var m = Mano(i);
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                Vector3 local = transform.InverseTransformPoint(m.position);
                Vector3 pomo = new Vector3(0f, radioPomo, 0f);
                if (Vector3.Distance(local, pomo) > 0.16f) continue;
                if (EntradaVR.AgarreDown(i == 0))
                {
                    _mano = i;
                    _manoIni = AnguloMano(m);
                    _palancaIni = _angulo;
                    _asentando = false;
                    EntradaVR.Vibrar(i == 0, 0.4f, 0.08f, 0.6f);
                    SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position, 0.6f);
                    break;
                }
            }
        }

        transform.localRotation = _rotBase * Quaternion.AngleAxis(_angulo, Vector3.right);
    }

    float AnguloMano(Transform m)
    {
        Vector3 local = transform.InverseTransformPoint(m.position);
        return Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
    }

    void Soltar()
    {
        if (_mano < 0) return;
        _mano = -1;

        // Se asienta en la ranura mas cercana.
        int mejor = marcha;
        float mejorDist = float.MaxValue;
        for (int i = 0; i < angulosRanuras.Length; i++)
        {
            float d = Mathf.Abs(_angulo - Ranura(i));
            if (d < mejorDist) { mejorDist = d; mejor = i; }
        }
        marcha = mejor;
        _asentando = true;
        EntradaVR.VibrarAmbas(0.25f, 0.06f, 0.5f);
        SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position, 0.8f);
    }

    void OnDisable()
    {
        if (camion != null) camion.marchaManual = null;
    }
}
