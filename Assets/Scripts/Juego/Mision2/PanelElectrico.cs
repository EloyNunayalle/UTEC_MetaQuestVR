using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Tablero electrico principal de la Mision 2 (incendio electrico).
/// Tiene un solo interruptor que funciona: la palanca ROJA principal. <see cref="Energizado"/> depende solo de ella.
/// <see cref="Verificado"/> se activa cuando el comprobador de tension toca el tablero con la alimentacion cortada.
///
/// El modelo del tablero ("VR-Ready Fuse Box") es una sola malla: no se mueve. Encima se construyen por
/// codigo la palanca roja, el rotulo, la zona donde se apoya el comprobador (aparece solo mientras lo
/// tienes en la mano), el resultado grande de la medicion, el zumbido y la luz de emergencia.
/// Convencion: el origen de este objeto esta en la pared y su +Z apunta hacia la sala (frente del tablero).
/// </summary>
public class PanelElectrico : MonoBehaviour
{
    [Header("Ubicacion sobre el modelo (local, +Z = frente)")]
    [Tooltip("Sobre el interruptor rojo del modelo.")]
    public Vector3 posInterruptorPrincipal = new Vector3(0.17f, 0f, 0.212f);
    [Tooltip("Centro de la zona donde se apoya el comprobador (frente del tablero).")]
    public Vector3 posZonaPrueba = new Vector3(-0.06f, 0f, 0.2f);
    [Tooltip("Medio tamano de la zona de prueba (m): basta con tocar el tablero de frente.")]
    public Vector3 medioZonaPrueba = new Vector3(0.3f, 0.28f, 0.2f);

    [Header("Iluminacion")]
    [Tooltip("Luz del techo del cuarto: se apaga al cortar la alimentacion.")]
    public Light luzTecho;
    [Tooltip("Luz de emergencia (se crea sola): posicion local respecto del tablero.")]
    public Vector3 posLuzEmergencia = new Vector3(-0.75f, 0.85f, 0.06f);

    public bool Energizado { get; private set; } = true;
    public bool Verificado { get; private set; }
    /// <summary>La mision esta en curso (zumbido y luces activos).</summary>
    public bool Activo { get; set; }
    public bool Tocado => _principal != null && _principal.Tocada;
    public Vector3 PuntoPrueba => transform.TransformPoint(posZonaPrueba);
    public Vector3 PuntoInterruptor => _principal != null ? _principal.Punta : transform.TransformPoint(posInterruptorPrincipal);
    public Vector3 Centro => transform.TransformPoint(new Vector3(0f, 0f, 0.15f));
    /// <summary>Salida del cable de alimentacion (arriba del tablero).</summary>
    public Vector3 SalidaCable => transform.TransformPoint(new Vector3(0.05f, 0.25f, 0.05f));

    /// <summary>true = se conecto, false = se corto la alimentacion general.</summary>
    public event Action<bool> alCambiarAlimentacion;

    InterruptorPalanca _principal;
    AudioSource _zumbido;
    Light _luzEmergencia;
    Material _matEmergencia, _matObjetivo;
    Transform _objetivo, _anillo, _resultadoRaiz;
    TextMeshPro _resultado;
    float _intensidadTecho = 1f, _finResultado;
    bool _preparado, _mostrarObjetivo;

    /// <summary>Construye las piezas interactivas (una sola vez).</summary>
    public void Preparar()
    {
        if (_preparado) return;
        _preparado = true;

        AsegurarModelo();

        _principal = InterruptorPalanca.Crear("Interruptor_Principal", transform, posInterruptorPrincipal, 0.1f, 0.026f,
            new Color(0.85f, 0.08f, 0.05f), new Color(0.12f, 0.12f, 0.13f));
        _principal.alCambiar += AlCambiarPrincipal;

        Rotulo("TABLERO GENERAL\n<size=60%>Baja la palanca roja para cortar la luz</size>", new Vector3(0f, 0.31f, 0.03f), 0.03f,
            new Vector2(0.56f, 0.1f), Color.black, new Color(1f, 0.82f, 0.1f));

        CrearObjetivoPrueba();
        CrearResultado();
        _zumbido = SonidosVR.CrearBucle(transform, SonidosVR.Zumbido, 0.22f, 7f);
        CrearLuzEmergencia();
        if (luzTecho != null) _intensidadTecho = luzTecho.intensity;
        ActualizarLuces();
    }

    /// <summary>
    /// Si el modelo del tablero no esta en el proyecto (archivo local que falta), arma una caja simple
    /// para que la mision funcione igual.
    /// </summary>
    void AsegurarModelo()
    {
        if (VisualesVR.Limites(transform, out _)) return;
        var caja = VisualesVR.Primitiva(PrimitiveType.Cube, "Tablero_Reemplazo", transform, VisualesVR.MaterialOpaco(new Color(0.55f, 0.57f, 0.6f)));
        caja.transform.localScale = new Vector3(0.48f, 0.44f, 0.17f);
        caja.transform.localPosition = new Vector3(0f, 0f, 0.09f);
    }

    /// <summary>Anillo verde que late en el frente del tablero: "apoya aqui el comprobador".</summary>
    void CrearObjetivoPrueba()
    {
        _objetivo = new GameObject("Zona_Prueba").transform;
        _objetivo.SetParent(transform, false);
        _objetivo.localPosition = posZonaPrueba + new Vector3(0f, 0f, 0.03f);
        _matObjetivo = VisualesVR.MaterialSinLuz(new Color(0.3f, 1f, 0.4f, 0.8f));
        _anillo = VisualesVR.ObjetoMalla("Anillo", _objetivo, VisualesVR.Anillo(0.07f, 0.09f), _matObjetivo).transform;
        _anillo.localRotation = Quaternion.Euler(90f, 0f, 0f); // el anillo (plano XZ) queda de frente a la sala

        var t = VisualesVR.Texto("Indicacion", _objetivo, 0.022f, new Vector2(0.4f, 0.05f));
        t.transform.localPosition = new Vector3(0f, -0.13f, 0f);
        t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        t.text = "TOCA AQUÍ CON EL COMPROBADOR";
        t.color = new Color(0.3f, 1f, 0.4f);
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.2f;
        t.outlineColor = Color.black;
        t.enableWordWrapping = false;
        _objetivo.gameObject.SetActive(false);
    }

    /// <summary>Resultado grande de la medicion, flotando sobre el tablero.</summary>
    void CrearResultado()
    {
        _resultadoRaiz = new GameObject("Resultado_Medicion").transform;
        _resultadoRaiz.SetParent(transform, false);
        _resultadoRaiz.localPosition = new Vector3(0f, 0.5f, 0.15f);
        _resultado = VisualesVR.Texto("Texto", _resultadoRaiz, 0.07f, new Vector2(0.9f, 0.2f));
        _resultado.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        _resultado.fontStyle = FontStyles.Bold;
        _resultado.outlineWidth = 0.25f;
        _resultado.outlineColor = Color.black;
        _resultado.enableWordWrapping = false;
        _resultadoRaiz.gameObject.SetActive(false);
    }

    void CrearLuzEmergencia()
    {
        var caja = VisualesVR.Primitiva(PrimitiveType.Cube, "Luz_Emergencia", transform, VisualesVR.MaterialOpaco(new Color(0.92f, 0.92f, 0.9f)));
        caja.transform.localPosition = posLuzEmergencia;
        caja.transform.localScale = new Vector3(0.32f, 0.08f, 0.06f);
        _matEmergencia = VisualesVR.MaterialSinLuz(new Color(0.35f, 0.35f, 0.32f));
        for (int i = 0; i < 2; i++)
        {
            var foco = VisualesVR.Primitiva(PrimitiveType.Cube, "Foco", caja.transform, _matEmergencia);
            foco.transform.localPosition = new Vector3(i == 0 ? -0.3f : 0.3f, -0.1f, 0.55f);
            foco.transform.localScale = new Vector3(0.3f, 0.6f, 0.1f);
        }
        Rotulo("EMERGENCIA", posLuzEmergencia + new Vector3(0f, 0.07f, 0.035f), 0.018f, new Vector2(0.3f, 0.03f), new Color(0.1f, 0.5f, 0.1f), null);

        _luzEmergencia = new GameObject("Luz").AddComponent<Light>();
        _luzEmergencia.transform.SetParent(transform, false);
        _luzEmergencia.transform.localPosition = posLuzEmergencia + new Vector3(0f, -0.1f, 0.25f);
        _luzEmergencia.type = LightType.Point;
        _luzEmergencia.color = new Color(1f, 0.97f, 0.9f);
        _luzEmergencia.range = 7f;
        _luzEmergencia.intensity = 1.3f;
        _luzEmergencia.shadows = LightShadows.None;
        _luzEmergencia.enabled = false;
    }

    /// <summary>Rotulo de texto (con placa de fondo opcional) pegado al tablero.</summary>
    void Rotulo(string texto, Vector3 pos, float alturaLetra, Vector2 caja, Color colorTexto, Color? fondo)
    {
        if (fondo.HasValue)
        {
            var placa = VisualesVR.Primitiva(PrimitiveType.Cube, "Placa_" + texto.Split('\n')[0], transform, VisualesVR.MaterialOpaco(fondo.Value));
            placa.transform.localPosition = pos + new Vector3(0f, 0f, -0.006f);
            placa.transform.localScale = new Vector3(caja.x + 0.02f, caja.y + 0.02f, 0.008f);
        }
        var t = VisualesVR.Texto("Rotulo", transform, alturaLetra, caja);
        t.transform.localPosition = pos;
        // El texto de TMP se lee desde su -Z: se gira para que mire hacia la sala (+Z).
        t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        t.text = texto;
        t.color = colorTexto;
        t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false;
    }

    // ------------------------------------------------------------------

    void AlCambiarPrincipal(bool encendido)
    {
        Energizado = encendido;
        if (encendido) Verificado = false; // si se vuelve a conectar, la verificacion ya no vale
        ActualizarLuces();
        if (Activo)
        {
            if (!encendido) SonidosVR.ReproducirEn(SonidosVR.Chispa, PuntoInterruptor, 0.7f, 10f);
            EntradaVR.VibrarAmbas(0.4f, 0.1f, 0.4f);
        }
        alCambiarAlimentacion?.Invoke(encendido);
    }

    /// <summary>El punto esta tocando el frente del tablero (zona de prueba).</summary>
    public bool EnZonaPrueba(Vector3 puntoMundo)
    {
        Vector3 p = transform.InverseTransformPoint(puntoMundo) - posZonaPrueba;
        return Mathf.Abs(p.x) <= medioZonaPrueba.x && Mathf.Abs(p.y) <= medioZonaPrueba.y && Mathf.Abs(p.z) <= medioZonaPrueba.z;
    }

    /// <summary>
    /// Medicion del comprobador: devuelve false si la punta no esta tocando el tablero.
    /// El resultado depende SOLO del estado logico del interruptor principal.
    /// </summary>
    public bool Comprobar(Vector3 punta, out bool sinTension)
    {
        sinTension = !Energizado;
        if (!EnZonaPrueba(punta)) return false;
        if (sinTension) Verificado = true;
        _resultado.text = sinTension ? "SIN TENSIÓN\n<size=50%>Es seguro usar el extintor</size>" : "¡HAY TENSIÓN!\n<size=50%>Baja la palanca roja</size>";
        _resultado.color = sinTension ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.25f, 0.2f);
        _resultadoRaiz.gameObject.SetActive(true);
        _finResultado = Time.time + 4f;
        return true;
    }

    /// <summary>Muestra/oculta el anillo "toca aqui" (mientras el jugador tiene el comprobador).</summary>
    public void MostrarObjetivo(bool mostrar) => _mostrarObjetivo = mostrar;

    /// <summary>Vuelve todo a ENCENDIDO (reinicio de la mision o de la practica).</summary>
    public void Restablecer()
    {
        Preparar();
        _principal.Forzar(true);
        Energizado = true;
        Verificado = false;
        _resultadoRaiz.gameObject.SetActive(false);
        ActualizarLuces();
    }

    void ActualizarLuces()
    {
        // Sin alimentacion: se apaga la luz del techo y se enciende la de emergencia.
        bool corte = Activo && !Energizado;
        if (luzTecho != null) luzTecho.intensity = corte ? 0f : _intensidadTecho;
        if (_luzEmergencia != null) _luzEmergencia.enabled = corte;
        if (_matEmergencia != null) _matEmergencia.color = corte ? new Color(1f, 1f, 0.92f) : new Color(0.35f, 0.35f, 0.32f);
    }

    void Update()
    {
        if (_zumbido == null) return;
        bool sonar = Activo && Energizado;
        if (sonar && !_zumbido.isPlaying) _zumbido.Play();
        else if (!sonar && _zumbido.isPlaying) _zumbido.Stop();

        bool objetivo = _mostrarObjetivo && Activo;
        if (_objetivo.gameObject.activeSelf != objetivo) _objetivo.gameObject.SetActive(objetivo);
        if (objetivo)
        {
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            _anillo.localScale = Vector3.one * (1f + 0.15f * pulso);
            _matObjetivo.color = new Color(0.3f, 1f, 0.4f, 0.5f + 0.4f * pulso);
        }

        if (_resultadoRaiz.gameObject.activeSelf && Time.time >= _finResultado) _resultadoRaiz.gameObject.SetActive(false);
    }

    /// <summary>Llamar al cambiar Activo para refrescar luces.</summary>
    public void Refrescar() => ActualizarLuces();
}
