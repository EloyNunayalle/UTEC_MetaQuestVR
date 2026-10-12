using TMPro;
using UnityEngine;

/// <summary>
/// Flecha flotante que senala el objetivo actual, con la distancia en metros.
/// </summary>
public class MarcadorObjetivo : MonoBehaviour
{
    public Color color = new Color(1f, 0.8f, 0.1f, 0.95f);

    Transform _cabeza;
    Transform _flecha;
    TextMeshPro _distancia;
    Material _mat;
    Vector3 _punto;
    bool _activo;

    public void Inicializar(Transform cabeza)
    {
        _cabeza = cabeza;
        _mat = VisualesVR.MaterialSinLuz(color);
        _flecha = VisualesVR.ObjetoMalla("Flecha", transform, VisualesVR.FlechaAbajo(0.13f, 0.28f), _mat).transform;
        _distancia = VisualesVR.Texto("Distancia", transform, 0.08f, new Vector2(1f, 0.2f));
        _distancia.fontStyle = FontStyles.Bold;
        _distancia.outlineWidth = 0.25f;
        _distancia.outlineColor = Color.black;
        _distancia.color = color;
        gameObject.SetActive(false);
    }

    /// <summary>Senala un punto (la punta de la flecha queda encima de el).</summary>
    public void Senalar(Vector3 punto)
    {
        _punto = punto;
        if (!_activo) transform.position = punto;
        _activo = true;
        gameObject.SetActive(true);
    }

    public void Ocultar()
    {
        _activo = false;
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!_activo || _cabeza == null) return;
        float bob = 0.08f * Mathf.Sin(Time.time * 3f);
        transform.position = Vector3.Lerp(transform.position, _punto, 1f - Mathf.Exp(-Time.deltaTime * 6f));
        _flecha.localPosition = new Vector3(0f, 0.12f + bob, 0f);
        _flecha.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);

        float d = Vector3.Distance(new Vector3(_cabeza.position.x, 0f, _cabeza.position.z), new Vector3(_punto.x, 0f, _punto.z));
        _distancia.text = d < 1.5f ? "" : Mathf.RoundToInt(d) + " m";
        _distancia.transform.localPosition = new Vector3(0f, 0.55f + bob, 0f);
        Vector3 mirar = _distancia.transform.position - _cabeza.position;
        mirar.y = 0f;
        if (mirar.sqrMagnitude > 0.0001f) _distancia.transform.rotation = Quaternion.LookRotation(mirar);

        // Se agranda con la distancia para que se vea desde lejos.
        float escala = Mathf.Clamp(d / 4f, 1f, 3f);
        transform.localScale = Vector3.one * escala;

        var c = color;
        c.a = 0.7f + 0.3f * Mathf.Sin(Time.time * 6f);
        _mat.color = c;
    }
}
