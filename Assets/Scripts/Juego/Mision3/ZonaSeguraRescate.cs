using TMPro;
using UnityEngine;

/// <summary>Zona segura fuera de la vivienda: un circulo verde en la calle con cartel. Se crea por codigo.</summary>
public class ZonaSeguraRescate : MonoBehaviour
{
    public float radio = 2.6f;

    Material _mat;
    TextMeshPro _cartel;
    Transform _cabeza;

    public static ZonaSeguraRescate Crear(Vector3 posicion, float radio, Transform cabeza)
    {
        var go = new GameObject("Zona_Segura");
        go.transform.position = PisoEn(posicion) + Vector3.up * 0.04f;
        var z = go.AddComponent<ZonaSeguraRescate>();
        z.radio = radio;
        z._cabeza = cabeza;
        z._mat = VisualesVR.MaterialSinLuz(new Color(0.25f, 1f, 0.4f, 0.8f));
        VisualesVR.ObjetoMalla("Anillo", go.transform, VisualesVR.Anillo(radio - 0.18f, radio), z._mat);
        z._cartel = VisualesVR.Texto("Cartel", go.transform, 0.16f, new Vector2(3f, 0.4f));
        z._cartel.transform.localPosition = Vector3.up * 1.9f;
        z._cartel.fontStyle = FontStyles.Bold;
        z._cartel.outlineWidth = 0.2f;
        z._cartel.outlineColor = Color.black;
        z._cartel.color = new Color(0.3f, 1f, 0.45f);
        z._cartel.text = "ZONA SEGURA";
        return z;
    }

    /// <summary>Altura del piso (colliders de la ciudad) en un punto.</summary>
    public static Vector3 PisoEn(Vector3 p)
    {
        float mejor = float.MaxValue;
        Vector3 res = p;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
            if (h.distance < mejor) { mejor = h.distance; res = h.point; }
        return res;
    }

    public bool Contiene(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude <= radio;
    }

    void Update()
    {
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        _mat.color = new Color(0.25f, 1f, 0.4f, 0.45f + 0.45f * pulso);
        if (_cabeza == null) return;
        Vector3 mirar = _cartel.transform.position - _cabeza.position;
        mirar.y = 0f;
        if (mirar.sqrMagnitude > 0.001f) _cartel.transform.rotation = Quaternion.LookRotation(mirar);
    }
}
