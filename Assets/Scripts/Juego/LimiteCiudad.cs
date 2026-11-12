using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Limite logico de la ciudad de tile unico (complementa el limite fisico: mar con baranda al norte,
/// laderas al este/oeste/sur y control cerrado en el acceso sur).
///
/// Cada frame (LateUpdate, despues de la locomocion y de la conduccion):
/// - Jugador: si la cabeza queda fuera del rectangulo (caminando, teletransportado o por cualquier
///   otro motivo) se devuelve al borde interior, moviendo el rig y el CharacterController juntos.
/// - Vehiculos (camion y moto): se toma su caja en mundo y se empuja hacia adentro.
/// Muestra un aviso breve ("La ciudad esta cerrada por emergencia") con enfriamiento.
/// Vive en la escena persistente: funciona aunque el tile se recargue.
/// </summary>
[DefaultExecutionOrder(1000)]
public class LimiteCiudad : MonoBehaviour
{
    public static LimiteCiudad Instancia { get; private set; }

    [Header("Area jugable (mundo, X/Z)")]
    [Tooltip("Borde interior de la ladera oeste / este, la baranda del malecon (norte) y la barrera del control sur.")]
    public float xMin = -108.4f;
    public float xMax = 95.7f;
    public float zMin = -85.9f;
    public float zMax = 51.6f;
    [Tooltip("Distancia minima de la cabeza al borde (m).")]
    public float margenJugador = 0.35f;
    [Tooltip("Por debajo de esta altura se considera que el jugador cayo del mapa y se lo devuelve al HUB.")]
    public float alturaMinima = -6f;
    public Vector3 puntoRescate = new Vector3(-11.6f, 0.1f, 8f);

    [Header("Aviso")]
    public string mensaje = "La ciudad está cerrada por emergencia";
    public float enfriamientoAviso = 4f;

    OVRCameraRig _rig;
    // Capsulas del locomotor del Interaction SDK (no es el CharacterController de Unity).
    Oculus.Interaction.Locomotion.CharacterController[] _capsulas = new Oculus.Interaction.Locomotion.CharacterController[0];
    readonly List<Vehiculo> _vehiculos = new List<Vehiculo>();
    float _proximoAviso, _proximaBusqueda;

    struct Vehiculo
    {
        public Transform t;
        public Vector3[] esquinas; // caja local
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public bool Dentro(Vector3 p, float margen = 0f) =>
        p.x >= xMin + margen && p.x <= xMax - margen && p.z >= zMin + margen && p.z <= zMax - margen;

    /// <summary>Punto dentro del area mas cercano a p (misma altura).</summary>
    public Vector3 Recortar(Vector3 p, float margen = 0f) =>
        new Vector3(Mathf.Clamp(p.x, xMin + margen, xMax - margen), p.y, Mathf.Clamp(p.z, zMin + margen, zMax - margen));

    void LateUpdate()
    {
        if (Time.unscaledTime >= _proximaBusqueda) Buscar();
        LimitarJugador();
        foreach (var v in _vehiculos) if (v.t != null) LimitarVehiculo(v);
    }

    void Buscar()
    {
        _proximaBusqueda = Time.unscaledTime + 2f;
        if (_rig == null) _rig = FindObjectOfType<OVRCameraRig>();
        _capsulas = FindObjectsOfType<Oculus.Interaction.Locomotion.CharacterController>();

        _vehiculos.RemoveAll(v => v.t == null);
        foreach (var c in FindObjectsOfType<CamionBomberos>()) Registrar(c.transform);
        foreach (var m in FindObjectsOfType<MotoBomberos>()) Registrar(m.transform);
    }

    void Registrar(Transform t)
    {
        foreach (var v in _vehiculos) if (v.t == t) return;
        // Caja en espacio local del vehiculo a partir de las mallas (se calcula una vez).
        bool hay = false;
        Bounds b = default;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var esq = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                var local = t.InverseTransformPoint(mf.transform.TransformPoint(esq));
                if (!hay) { b = new Bounds(local, Vector3.zero); hay = true; } else b.Encapsulate(local);
            }
        }
        if (!hay) return;
        Vector3 ext = b.extents;
        var c = b.center;
        _vehiculos.Add(new Vehiculo
        {
            t = t,
            esquinas = new[]
            {
                c + new Vector3(-ext.x, 0f, -ext.z), c + new Vector3(ext.x, 0f, -ext.z),
                c + new Vector3(-ext.x, 0f, ext.z), c + new Vector3(ext.x, 0f, ext.z)
            }
        });
    }

    void LimitarJugador()
    {
        if (_rig == null) return;
        Transform cabeza = _rig.centerEyeAnchor != null ? _rig.centerEyeAnchor : _rig.transform;
        Vector3 p = cabeza.position;

        // Red de seguridad: si cae por debajo del mundo, vuelve al HUB.
        if (_rig.transform.position.y < alturaMinima)
        {
            Desplazar(puntoRescate - new Vector3(p.x, _rig.transform.position.y, p.z));
            Avisar();
            return;
        }

        // Sentado en un vehiculo: lo limita el vehiculo.
        if (SentadoEnVehiculo()) return;

        if (Dentro(p, margenJugador)) return;
        Vector3 objetivo = Recortar(p, margenJugador);
        Desplazar(new Vector3(objetivo.x - p.x, 0f, objetivo.z - p.z));
        Avisar();
    }

    bool SentadoEnVehiculo()
    {
        foreach (var v in _vehiculos)
        {
            if (v.t == null) continue;
            var c = v.t.GetComponent<CamionBomberos>();
            if (c != null && c.JugadorSentado) return true;
            var m = v.t.GetComponent<MotoBomberos>();
            if (m != null && m.Montado) return true;
        }
        return false;
    }

    /// <summary>Mueve el rig y la capsula del locomotor juntos, para que la capsula no "tire" de vuelta.</summary>
    void Desplazar(Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-8f) return;
        foreach (var c in _capsulas)
            if (c != null && c.isActiveAndEnabled && !c.transform.IsChildOf(_rig.transform))
                c.SetPosition(c.Pose.position + delta);
        _rig.transform.position += delta;
        Physics.SyncTransforms();
    }

    void LimitarVehiculo(Vehiculo v)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var e in v.esquinas)
        {
            Vector3 w = v.t.TransformPoint(e);
            minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
            minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
        }
        Vector3 d = Vector3.zero;
        if (minX < xMin) d.x = xMin - minX; else if (maxX > xMax) d.x = xMax - maxX;
        if (minZ < zMin) d.z = zMin - minZ; else if (maxZ > zMax) d.z = zMax - maxZ;
        if (d == Vector3.zero) return;

        v.t.position += d;
        // Si el jugador va sentado, el rig acompana al vehiculo (lo mueve el propio vehiculo en su Update,
        // pero aqui corremos despues): se corrige en el mismo frame para evitar un tiron.
        var c = v.t.GetComponent<CamionBomberos>();
        var m = v.t.GetComponent<MotoBomberos>();
        if (_rig != null && ((c != null && c.JugadorSentado) || (m != null && m.Montado)))
            Desplazar(d);
        Avisar();
    }

    void Avisar()
    {
        if (Time.unscaledTime < _proximoAviso) return;
        _proximoAviso = Time.unscaledTime + enfriamientoAviso;
        if (JuegoBomberos.Instancia != null)
            JuegoBomberos.Instancia.Aviso(mensaje, JuegoBomberos.TipoAviso.Error, 2.5f);
        else
            Debug.Log("[LimiteCiudad] " + mensaje);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
        Vector3 a = new Vector3(xMin, 0.2f, zMin), b = new Vector3(xMax, 0.2f, zMin),
                c = new Vector3(xMax, 0.2f, zMax), d = new Vector3(xMin, 0.2f, zMax);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
    }
#endif
}
