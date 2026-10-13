using TMPro;
using UnityEngine;

/// <summary>
/// HUD del juego en el mundo (VR): un panel que sigue suavemente la mirada
/// (con objetivo, lista de tareas, tiempo y puntaje) y avisos grandes temporales.
/// </summary>
public class HudBomberos : MonoBehaviour
{
    public float distancia = 1.3f;
    public float alturaRelativa = -0.32f;
    public float anguloParaSeguir = 28f;

    Transform _cabeza;
    Transform _panel;
    TextMeshPro _titulo, _cuerpo, _pie;
    Renderer _fondo;
    Material _matFondo;
    bool _siguiendo = true;
    Vector3 _dirPanel;

    Transform _avisoRaiz;
    TextMeshPro _aviso;
    float _avisoInicio, _avisoFin;
    Color _avisoColor;

    public void Inicializar(Transform cabeza)
    {
        _cabeza = cabeza;

        _panel = new GameObject("Panel").transform;
        _panel.SetParent(transform, false);

        _matFondo = VisualesVR.MaterialSinLuz(new Color(0.05f, 0.05f, 0.08f, 0.78f));
        var fondo = VisualesVR.Primitiva(PrimitiveType.Quad, "Fondo", _panel, _matFondo);
        fondo.transform.localScale = new Vector3(0.78f, 0.48f, 1f);
        _fondo = fondo.GetComponent<Renderer>();

        var franja = VisualesVR.Primitiva(PrimitiveType.Quad, "Franja", _panel, VisualesVR.MaterialSinLuz(new Color(0.85f, 0.1f, 0.05f, 0.95f)));
        franja.transform.localPosition = new Vector3(0f, 0.215f, -0.001f);
        franja.transform.localScale = new Vector3(0.78f, 0.05f, 1f);

        _titulo = VisualesVR.Texto("Titulo", _panel, 0.03f, new Vector2(0.74f, 0.05f));
        _titulo.transform.localPosition = new Vector3(0f, 0.215f, -0.004f);
        _titulo.fontStyle = FontStyles.Bold;

        _cuerpo = VisualesVR.Texto("Cuerpo", _panel, 0.026f, new Vector2(0.72f, 0.33f), TextAlignmentOptions.TopLeft);
        _cuerpo.transform.localPosition = new Vector3(0f, 0.01f, -0.004f);

        _pie = VisualesVR.Texto("Pie", _panel, 0.022f, new Vector2(0.72f, 0.05f), TextAlignmentOptions.Left);
        _pie.transform.localPosition = new Vector3(0f, -0.205f, -0.004f);
        _pie.color = new Color(1f, 0.85f, 0.3f);

        _avisoRaiz = new GameObject("Aviso").transform;
        _avisoRaiz.SetParent(transform, false);
        _aviso = VisualesVR.Texto("Texto", _avisoRaiz, 0.07f, new Vector2(1.4f, 0.4f));
        _aviso.fontStyle = FontStyles.Bold;
        _aviso.outlineWidth = 0.25f;
        _aviso.outlineColor = Color.black;
        _aviso.text = "";

        ColocarPanel(1f);
        _avisoRaiz.gameObject.SetActive(false);
    }

    public void Mostrar(string titulo, string cuerpo, string pie)
    {
        if (_titulo == null) return;
        _titulo.text = titulo;
        _cuerpo.text = cuerpo;
        _pie.text = pie;
    }

    public void Pie(string pie)
    {
        if (_pie != null) _pie.text = pie;
    }

    /// <summary>Aviso grande frente a la vista durante unos segundos.</summary>
    public void Aviso(string texto, Color color, float duracion = 2.5f)
    {
        if (_aviso == null) return;
        _aviso.text = texto;
        _avisoColor = color;
        _avisoInicio = Time.time;
        _avisoFin = Time.time + duracion;
        _avisoRaiz.gameObject.SetActive(true);
        ColocarAviso(1f);
    }

    void LateUpdate()
    {
        if (_cabeza == null) return;
        ColocarPanel(Time.deltaTime);

        if (_avisoRaiz.gameObject.activeSelf)
        {
            ColocarAviso(Time.deltaTime * 10f);
            float t = Time.time;
            float aparecer = Mathf.Clamp01((t - _avisoInicio) / 0.15f);
            float desvanecer = Mathf.Clamp01((_avisoFin - t) / 0.4f);
            var c = _avisoColor;
            c.a = Mathf.Min(aparecer, desvanecer);
            _aviso.color = c;
            _avisoRaiz.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, aparecer));
            if (t >= _avisoFin) _avisoRaiz.gameObject.SetActive(false);
        }
    }

    Vector3 FrenteHorizontal()
    {
        Vector3 f = _cabeza.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = _cabeza.up;
        f.y = 0f;
        return f.normalized;
    }

    void ColocarPanel(float paso)
    {
        Vector3 f = FrenteHorizontal();

        // Solo la DIRECCION del panel es perezosa (se reacomoda al girar la cabeza);
        // la distancia a la cabeza es siempre fija, asi el panel avanza contigo al caminar
        // y nunca se te viene encima como una pared.
        if (paso >= 1f || _dirPanel == Vector3.zero) { _dirPanel = f; _siguiendo = false; }
        else
        {
            if (Vector3.Angle(_dirPanel, f) > anguloParaSeguir) _siguiendo = true;
            if (_siguiendo)
            {
                _dirPanel = Vector3.Slerp(_dirPanel, f, 1f - Mathf.Exp(-paso * 4f)).normalized;
                if (Vector3.Angle(_dirPanel, f) < 2f) _siguiendo = false;
            }
        }

        _panel.position = _cabeza.position + _dirPanel * distancia + Vector3.up * alturaRelativa;
        Vector3 mirar = _panel.position - _cabeza.position;
        if (mirar.sqrMagnitude > 0.0001f) _panel.rotation = Quaternion.LookRotation(mirar, Vector3.up);
    }

    void ColocarAviso(float paso)
    {
        Vector3 objetivo = _cabeza.position + _cabeza.forward * 1.2f + Vector3.up * 0.08f;
        _avisoRaiz.position = Vector3.Lerp(_avisoRaiz.position, objetivo, Mathf.Clamp01(paso));
        Vector3 mirar = _avisoRaiz.position - _cabeza.position;
        if (mirar.sqrMagnitude > 0.0001f) _avisoRaiz.rotation = Quaternion.LookRotation(mirar, Vector3.up);
    }
}
