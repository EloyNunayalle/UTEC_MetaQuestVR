using System;
using UnityEngine;

/// <summary>
/// Agarre simple por proximidad: acerca la mano (control o mano rastreada) al objeto
/// y presiona GRIP / gatillo o pellizca. El objeto sigue a la mano mientras se mantiene.
/// Al soltarlo, si esta en su destino se dispara <see cref="alSoltarEnDestino"/>;
/// si no, cae al piso (o vuelve a su lugar si no hay piso debajo).
/// Da feedback de resaltado, vibracion y sonido.
/// </summary>
public class ObjetoAgarrable : MonoBehaviour
{
    public float radioAgarre = 0.15f;
    public Color colorResaltado = new Color(1f, 0.85f, 0.1f);
    public Color colorDestino = new Color(0.2f, 1f, 0.3f);

    /// <summary>Devuelve true si la posicion actual del objeto es un destino valido.</summary>
    public Func<Vector3, bool> esDestinoValido;
    public Action alSoltarEnDestino;
    public Action alAgarrar;
    public Action alEntrarDestino;

    public bool Agarrado => _mano >= 0;
    public bool Completado { get; private set; }

    Renderer[] _renders;
    Collider[] _colliders;
    Vector3 _posOrig;
    Quaternion _rotOrig;
    int _mano = -1;
    int _hoverPrev = -1;
    bool _enDestinoPrev;
    Vector3 _offPos;
    Quaternion _offRot;
    Vector3 _regresoDesde;
    float _regresoT = 1f;
    Vector3 _regresoHasta;

    void Awake()
    {
        _renders = GetComponentsInChildren<Renderer>();
        _colliders = GetComponentsInChildren<Collider>();
        _posOrig = transform.position;
        _rotOrig = transform.rotation;
    }

    Transform Mano(int i)
    {
        var rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : null;
        if (rig == null) return null;
        return i == 0 ? rig.leftHandAnchor : rig.rightHandAnchor;
    }

    public Vector3 Centro
    {
        get
        {
            if (VisualesVR.Limites(transform, out var b)) return b.center;
            return transform.position;
        }
    }

    float DistanciaA(Vector3 p)
    {
        if (VisualesVR.Limites(transform, out var b)) return Mathf.Sqrt(b.SqrDistance(p));
        return Vector3.Distance(transform.position, p);
    }

    void Update()
    {
        if (Completado) return;
        bool permitido = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;

        if (_mano < 0)
        {
            // Animacion de regreso a su lugar.
            if (_regresoT < 1f)
            {
                _regresoT = Mathf.Min(1f, _regresoT + Time.deltaTime * 2f);
                float e = _regresoT * _regresoT * (3f - 2f * _regresoT);
                transform.position = Vector3.Lerp(_regresoDesde, _regresoHasta, e);
            }

            int hover = -1;
            if (permitido)
            {
                for (int i = 0; i < 2; i++)
                {
                    var m = Mano(i);
                    if (m == null || !m.gameObject.activeInHierarchy) continue;
                    if (DistanciaA(m.position) > radioAgarre) continue;
                    hover = i;
                    if (EntradaVR.AgarreDown(i == 0))
                    {
                        Agarrar(i, m);
                        return;
                    }
                }
            }

            if (hover != _hoverPrev && hover >= 0)
            {
                EntradaVR.Vibrar(hover == 0, 0.25f, 0.05f, 0.8f);
                SonidosVR.ReproducirEn(SonidosVR.Clic, Centro, 0.5f);
            }
            _hoverPrev = hover;

            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            VisualesVR.Tenir(_renders, colorResaltado, hover >= 0 ? 0.45f + 0.35f * pulso : 0f);
            return;
        }

        // ---- Agarrado ----
        var mano = Mano(_mano);
        if (mano == null) { Soltar(); return; }

        transform.SetPositionAndRotation(mano.TransformPoint(_offPos), mano.rotation * _offRot);

        bool enDestino = esDestinoValido != null && esDestinoValido(Centro);
        if (enDestino && !_enDestinoPrev)
        {
            EntradaVR.Vibrar(_mano == 0, 0.6f, 0.1f, 0.9f);
            SonidosVR.ReproducirEn(SonidosVR.Clic, Centro, 0.8f);
            alEntrarDestino?.Invoke();
        }
        _enDestinoPrev = enDestino;
        VisualesVR.Tenir(_renders, enDestino ? colorDestino : colorResaltado,
            enDestino ? 0.6f + 0.3f * Mathf.Sin(Time.time * 12f) : 0.25f);

        if (!EntradaVR.Agarrando(_mano == 0) || !permitido) Soltar();
    }

    void Agarrar(int i, Transform mano)
    {
        _mano = i;
        _regresoT = 1f;
        _enDestinoPrev = false;
        _offPos = mano.InverseTransformPoint(transform.position);
        _offRot = Quaternion.Inverse(mano.rotation) * transform.rotation;
        foreach (var c in _colliders) if (c != null) c.enabled = false;
        EntradaVR.Vibrar(i == 0, 0.7f, 0.12f, 0.6f);
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, Centro, 0.9f);
        alAgarrar?.Invoke();
    }

    /// <summary>Suelta el objeto (si estaba agarrado) y evalua el destino.</summary>
    public void Soltar()
    {
        if (_mano < 0) return;
        int mano = _mano;
        _mano = -1;
        _hoverPrev = -1;
        VisualesVR.Tenir(_renders, Color.white, 0f);
        EntradaVR.Vibrar(mano == 0, 0.3f, 0.05f);

        if (esDestinoValido != null && esDestinoValido(Centro))
        {
            Completar();
            alSoltarEnDestino?.Invoke();
            return;
        }

        foreach (var c in _colliders) if (c != null) c.enabled = true;
        transform.rotation = _rotOrig;
        _regresoDesde = transform.position;
        _regresoHasta = PosicionEnPiso(out bool hayPiso);
        if (!hayPiso) _regresoHasta = _posOrig;
        _regresoT = 0f;
    }

    /// <summary>Marca el objeto como usado: deja de ser agarrable y se encoge hasta desaparecer.</summary>
    public void Completar()
    {
        Completado = true;
        _mano = -1;
        VisualesVR.Tenir(_renders, Color.white, 0f);
        StartCoroutine(Desaparecer());
    }

    System.Collections.IEnumerator Desaparecer()
    {
        Vector3 escala = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            transform.localScale = escala * (1f - t * t);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    Vector3 PosicionEnPiso(out bool hayPiso)
    {
        hayPiso = false;
        if (!VisualesVR.Limites(transform, out var b)) return transform.position;
        var origen = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        var hits = Physics.RaycastAll(origen, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        Vector3 punto = default;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; punto = h.point; }
        }
        if (mejor == float.MaxValue) return transform.position;
        hayPiso = true;
        // Ubica la base de los limites sobre el piso.
        float baseOffset = transform.position.y - b.min.y;
        return new Vector3(transform.position.x, punto.y + baseOffset + 0.005f, transform.position.z);
    }
}
