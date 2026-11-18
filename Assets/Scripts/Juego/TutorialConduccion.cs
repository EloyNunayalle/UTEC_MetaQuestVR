using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tutorial de conducción que aparece la primera vez que el jugador sube al camión.
/// Se muestra en una capa overlay con una cámara propia para que el interior del
/// camión no lo tape ni lo recorte. Se avanza con el clic del joystick.
/// </summary>
public class TutorialConduccion : MonoBehaviour
{
    struct Pagina
    {
        public string titulo;
        public string cuerpo;
    }

    public static TutorialConduccion Instancia { get; private set; }
    public static bool Activo => Instancia != null;

    const int CapaTutorial = 31;
    const string ClaveBase = "UTEC.TutorialConduccion.v1.";

    CamionBomberos _camion;
    Camera _camaraOverlay;
    Camera _camaraPrincipal;
    int _mascaraPrincipal;
    bool _mascaraGuardada;
    Transform _panel;
    TextMeshPro _titulo, _cuerpo, _pie, _contador;
    readonly List<Pagina> _paginas = new List<Pagina>();
    int _indice;
    float _inicio;

    static string Clave => ClaveBase + ConfiguracionConduccion.Modo;

    public static bool DebeMostrar()
    {
        return PlayerPrefs.GetInt(Clave, 0) == 0;
    }

    /// <summary>Muestra el tutorial si aun no se completo para el modo actual.</summary>
    public static void Mostrar(Transform cabeza, CamionBomberos camion)
    {
        if (Instancia != null || camion == null || cabeza == null || !DebeMostrar()) return;
        var go = new GameObject("Tutorial_Conduccion");
        go.AddComponent<TutorialConduccion>().Iniciar(cabeza, camion);
    }

    /// <summary>Cierra el tutorial sin marcarlo como visto (por ejemplo, si el jugador se baja).</summary>
    public static void Cerrar(bool completado)
    {
        if (Instancia == null) return;
        Instancia.CerrarInterno(completado);
    }

    void Iniciar(Transform cabeza, CamionBomberos camion)
    {
        Instancia = this;
        _camion = camion;
        _camion.tutorialActivo = true;
        _inicio = Time.unscaledTime;

        if (JuegoBomberos.Instancia != null) JuegoBomberos.Instancia.OcultarHudTutorial();

        CrearPaginas();
        CrearOverlay(cabeza);
        CrearPanel(cabeza);
        MostrarPagina();
    }

    void CrearPaginas()
    {
        bool volante = ConfiguracionConduccion.Modo == ModoConduccion.Volante;

        _paginas.Add(new Pagina
        {
            titulo = "CONDUCCIÓN DEL CAMIÓN",
            cuerpo = "<b>Modo: " + ConfiguracionConduccion.Nombre + "</b>\n\n" +
                     "Antes de moverte, poné una marcha con la palanca.\n\n" +
                     "Lee cada página y tocá el <b>clic del joystick</b> para avanzar."
        });

        _paginas.Add(new Pagina
        {
            titulo = "AVANZAR Y RETROCEDER",
            cuerpo = volante
                ? "<b>Gatillo derecho:</b> acelerar (mantener)\n" +
                  "<b>Gatillo izquierdo:</b> frenar\n\n" +
                  "La marcha debe estar en <b>D</b> para avanzar o <b>R</b> para retroceder."
                : "<b>Stick izquierdo:</b>\n" +
                  "• Arriba: avanzar\n" +
                  "• Abajo: frenar / retroceder\n\n" +
                  "La marcha debe estar en <b>D</b> para avanzar o <b>R</b> para retroceder."
        });

        _paginas.Add(new Pagina
        {
            titulo = "GIRAR",
            cuerpo = volante
                ? "Agarrá el <b>aro del volante</b> con GRIP o pellizco.\n\n" +
                  "Girá la mano para girar el camión. En este modo el joystick ya no gira."
                : "<b>Stick izquierdo o derecho:</b>\n" +
                  "• Izquierda / derecha: girar\n\n" +
                  "En este modo el volante es solo visual: no hace falta agarrarlo."
        });

        _paginas.Add(new Pagina
        {
            titulo = "PALANCA DE CAMBIOS",
            cuerpo = "Agarrá el pomo con <b>GRIP</b> o pellizco y llevalo a:\n\n" +
                     "<b>P</b> aparcado     <b>R</b> reversa     <b>N</b> neutral\n" +
                     "<b>D</b> avanzar       <b>L</b> baja\n" +
                     (volante ? "\nAtajo sin soltar el volante: <b>stick derecho arriba = D</b>, <b>abajo = R</b>." : "")
        });

        _paginas.Add(new Pagina
        {
            titulo = "OTROS CONTROLES",
            cuerpo = "<b>A:</b> sirena     <b>X:</b> luces\n" +
                     "<b>B / Y (mantener):</b> bajar del camión\n" +
                     "<b>GPS:</b> pantalla del tablero\n\n" +
                     "La palanca y el volante se manejan con las manos."
        });
    }

    void CrearOverlay(Transform cabeza)
    {
        _camaraPrincipal = cabeza.GetComponent<Camera>();
        if (_camaraPrincipal != null)
        {
            _mascaraPrincipal = _camaraPrincipal.cullingMask;
            _mascaraGuardada = true;
            _camaraPrincipal.cullingMask &= ~(1 << CapaTutorial);
        }

        var go = new GameObject("Tutorial_OverlayCam");
        go.transform.SetParent(cabeza, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        _camaraOverlay = go.AddComponent<Camera>();
        _camaraOverlay.clearFlags = CameraClearFlags.Depth;
        _camaraOverlay.cullingMask = 1 << CapaTutorial;
        _camaraOverlay.depth = (_camaraPrincipal != null ? _camaraPrincipal.depth : 0f) + 10f;
        _camaraOverlay.fieldOfView = _camaraPrincipal != null ? _camaraPrincipal.fieldOfView : 60f;
        _camaraOverlay.nearClipPlane = 0.01f;
        _camaraOverlay.farClipPlane = 5f;
        _camaraOverlay.allowHDR = false;
        _camaraOverlay.allowMSAA = false;
        _camaraOverlay.useOcclusionCulling = false;
    }

    void CrearPanel(Transform cabeza)
    {
        _panel = new GameObject("Panel_Tutorial").transform;
        _panel.SetParent(_camaraOverlay != null ? _camaraOverlay.transform : cabeza, false);
        _panel.localPosition = new Vector3(0f, -0.02f, 0.72f);
        _panel.localRotation = Quaternion.identity;

        var matFondo = VisualesVR.MaterialSinLuz(new Color(0.025f, 0.03f, 0.055f, 1f));
        matFondo.renderQueue = 3000;
        var fondo = VisualesVR.Primitiva(PrimitiveType.Quad, "Fondo", _panel, matFondo);
        fondo.transform.localScale = new Vector3(0.66f, 0.46f, 1f);

        var matFranja = VisualesVR.MaterialSinLuz(new Color(0.85f, 0.12f, 0.07f, 1f));
        matFranja.renderQueue = 3001;
        var franja = VisualesVR.Primitiva(PrimitiveType.Quad, "Franja", _panel, matFranja);
        franja.transform.localPosition = new Vector3(0f, 0.205f, -0.001f);
        franja.transform.localScale = new Vector3(0.66f, 0.026f, 1f);

        _titulo = VisualesVR.Texto("Titulo", _panel, 0.030f, new Vector2(0.50f, 0.045f), TextAlignmentOptions.Left);
        _titulo.transform.localPosition = new Vector3(-0.06f, 0.18f, -0.01f);
        _titulo.fontStyle = FontStyles.Bold;
        _titulo.color = new Color(1f, 0.94f, 0.85f);
        _titulo.fontMaterial.renderQueue = 3005;

        _contador = VisualesVR.Texto("Contador", _panel, 0.018f, new Vector2(0.08f, 0.03f), TextAlignmentOptions.Right);
        _contador.transform.localPosition = new Vector3(0.25f, 0.18f, -0.01f);
        _contador.color = new Color(0.7f, 0.75f, 0.82f);
        _contador.fontMaterial.renderQueue = 3005;

        _cuerpo = VisualesVR.Texto("Cuerpo", _panel, 0.0215f, new Vector2(0.58f, 0.26f), TextAlignmentOptions.TopLeft);
        _cuerpo.transform.localPosition = new Vector3(0f, 0.01f, -0.01f);
        _cuerpo.color = Color.white;
        _cuerpo.fontMaterial.renderQueue = 3005;

        _pie = VisualesVR.Texto("Pie", _panel, 0.017f, new Vector2(0.52f, 0.035f), TextAlignmentOptions.Left);
        _pie.transform.localPosition = new Vector3(-0.06f, -0.19f, -0.01f);
        _pie.color = new Color(1f, 0.82f, 0.25f);
        _pie.fontStyle = FontStyles.Bold;
        _pie.fontMaterial.renderQueue = 3005;

        PonerCapa(_panel);
    }

    void PonerCapa(Transform raiz)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = CapaTutorial;
    }

    void MostrarPagina()
    {
        if (_indice < 0 || _indice >= _paginas.Count) return;
        var p = _paginas[_indice];
        _titulo.text = p.titulo;
        _cuerpo.text = p.cuerpo;
        _contador.text = (_indice + 1) + " / " + _paginas.Count;
        bool ultima = _indice >= _paginas.Count - 1;
        _pie.text = ultima ? "Clic del joystick: empezar" : "Clic del joystick: siguiente";
    }

    void Update()
    {
        if (_camion == null || !_camion.JugadorSentado)
        {
            CerrarInterno(false);
            return;
        }

        if (Time.unscaledTime < _inicio + 0.35f) return;

        bool avanzar = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch) ||
                       OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch) ||
                       Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.T);
        if (!avanzar) return;

        if (_indice < _paginas.Count - 1)
        {
            _indice++;
            MostrarPagina();
            SonidosVR.Reproducir2D(SonidosVR.Clic, 0.7f);
            EntradaVR.VibrarAmbas(0.25f, 0.04f, 0.7f);
        }
        else
        {
            CerrarInterno(true);
        }
    }

    void CerrarInterno(bool completado)
    {
        if (completado)
        {
            PlayerPrefs.SetInt(Clave, 1);
            PlayerPrefs.Save();
        }

        if (_camion != null) _camion.tutorialActivo = false;
        if (_camaraPrincipal != null && _mascaraGuardada)
            _camaraPrincipal.cullingMask = _mascaraPrincipal;
        _mascaraGuardada = false;

        if (_camaraOverlay != null) Destroy(_camaraOverlay.gameObject);
        _camaraOverlay = null;
        if (Instancia == this) Instancia = null;

        if (completado && JuegoBomberos.Instancia != null)
            JuegoBomberos.Instancia.MostrarHudTrasTutorial();

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_camion != null) _camion.tutorialActivo = false;
        if (_camaraPrincipal != null && _mascaraGuardada)
            _camaraPrincipal.cullingMask = _mascaraPrincipal;
        if (Instancia == this) Instancia = null;
    }
}
