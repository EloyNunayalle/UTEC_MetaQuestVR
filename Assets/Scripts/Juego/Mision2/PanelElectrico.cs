using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tablero electrico principal de la Mision 2 (incendio electrico).
/// Estado LOGICO de la instalacion: <see cref="Energizado"/> depende SOLO del interruptor principal;
/// los breakers secundarios cortan circuitos parciales y no la alimentacion general.
/// <see cref="Verificado"/> se activa solo cuando el comprobador de tension mide sin tension en los bornes.
///
/// El modelo del tablero ("VR-Ready Fuse Box") es una sola malla: no se mueve. Encima se construyen por
/// codigo la palanca principal, tres breakers secundarios, los indicadores, los bornes de prueba, los
/// rotulos, el zumbido y la luz de emergencia.
/// Convencion: el origen de este objeto esta en la pared y su +Z apunta hacia la sala (frente del tablero).
/// </summary>
public class PanelElectrico : MonoBehaviour
{
    [Header("Ubicacion de las piezas sobre el modelo (local, +Z = frente)")]
    public Vector3 posInterruptorPrincipal = new Vector3(0.17f, 0f, 0.212f);
    public Vector3[] posBreakers =
    {
        new Vector3(-0.15f, 0f, 0.212f), new Vector3(-0.09f, 0f, 0.212f), new Vector3(-0.03f, 0f, 0.212f)
    };
    public Vector3 posBornes = new Vector3(-0.04f, -0.13f, 0.18f);
    public Vector3 posIndicadores = new Vector3(0.15f, -0.13f, 0.18f);
    [Tooltip("Distancia (m) de la punta del comprobador a los bornes para que la medicion cuente.")]
    public float radioPrueba = 0.13f;

    [Header("Iluminacion")]
    [Tooltip("Luz del techo del cuarto: se apaga al cortar la alimentacion.")]
    public Light luzTecho;
    [Tooltip("Luz de emergencia (se crea sola): posicion local respecto del tablero.")]
    public Vector3 posLuzEmergencia = new Vector3(-0.75f, 0.85f, 0.06f);

    public bool Energizado { get; private set; } = true;
    public bool Verificado { get; private set; }
    /// <summary>La mision esta en curso (zumbido y luces activos).</summary>
    public bool Activo { get; set; }
    public bool Tocado
    {
        get
        {
            if (_principal != null && _principal.Tocada) return true;
            foreach (var b in _secundarios) if (b != null && b.Tocada) return true;
            return false;
        }
    }
    public Vector3 PuntoPrueba => _bornes != null ? _bornes.position : transform.TransformPoint(posBornes);
    public Vector3 PuntoInterruptor => _principal != null ? _principal.Punta : transform.TransformPoint(posInterruptorPrincipal);
    public Vector3 Centro => transform.TransformPoint(new Vector3(0f, 0f, 0.15f));
    /// <summary>Salida del cable de alimentacion (arriba del tablero).</summary>
    public Vector3 SalidaCable => transform.TransformPoint(new Vector3(0.05f, 0.25f, 0.05f));

    /// <summary>true = se conecto, false = se corto la alimentacion general.</summary>
    public event Action<bool> alCambiarAlimentacion;
    /// <summary>(numero de circuito 1..n, encendido).</summary>
    public event Action<int, bool> alAccionarSecundario;

    InterruptorPalanca _principal;
    readonly List<InterruptorPalanca> _secundarios = new List<InterruptorPalanca>();
    Material _matLedTension, _matLedSinTension;
    AudioSource _zumbido;
    Light _luzEmergencia;
    Material _matEmergencia;
    Transform _bornes;
    float _intensidadTecho = 1f;
    bool _preparado;

    static readonly Color LedRojo = new Color(1f, 0.12f, 0.08f);
    static readonly Color LedVerde = new Color(0.2f, 1f, 0.35f);
    static readonly Color LedApagado = new Color(0.15f, 0.15f, 0.15f);

    /// <summary>Construye las piezas interactivas (una sola vez).</summary>
    public void Preparar()
    {
        if (_preparado) return;
        _preparado = true;

        AsegurarModelo();

        _principal = InterruptorPalanca.Crear("Interruptor_Principal", transform, posInterruptorPrincipal, 0.09f, 0.024f,
            new Color(0.85f, 0.08f, 0.05f), new Color(0.12f, 0.12f, 0.13f));
        _principal.alCambiar += AlCambiarPrincipal;
        Rotulo("PRINCIPAL", posInterruptorPrincipal + new Vector3(0f, 0.085f, 0.005f), 0.022f, new Vector2(0.14f, 0.04f), Color.white, null);

        for (int i = 0; i < posBreakers.Length; i++)
        {
            int circuito = i + 1;
            var b = InterruptorPalanca.Crear("Breaker_C" + circuito, transform, posBreakers[i], 0.045f, 0.013f,
                new Color(0.1f, 0.1f, 0.1f), new Color(0.85f, 0.85f, 0.82f));
            b.radioMano = 0.04f;
            b.anguloMaximo = 32f;
            b.Forzar(true);
            b.alCambiar += v => alAccionarSecundario?.Invoke(circuito, v);
            _secundarios.Add(b);
            Rotulo("C" + circuito, posBreakers[i] + new Vector3(0f, 0.06f, 0.005f), 0.016f, new Vector2(0.05f, 0.03f), Color.white, null);
        }

        CrearIndicadores();
        CrearBornes();
        Rotulo("TABLERO GENERAL\n<size=60%>¡Corte aquí la alimentación!</size>", new Vector3(0f, 0.31f, 0.03f), 0.03f,
            new Vector2(0.52f, 0.1f), Color.black, new Color(1f, 0.82f, 0.1f));

        _zumbido = SonidosVR.CrearBucle(transform, SonidosVR.Zumbido, 0.22f, 7f);
        CrearLuzEmergencia();
        if (luzTecho != null) _intensidadTecho = luzTecho.intensity;
        ActualizarIndicadores();
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
        var riel = VisualesVR.Primitiva(PrimitiveType.Cube, "Riel", transform, VisualesVR.MaterialOpaco(new Color(0.85f, 0.85f, 0.82f)));
        riel.transform.localScale = new Vector3(0.42f, 0.09f, 0.02f);
        riel.transform.localPosition = new Vector3(0f, 0f, 0.18f);
    }

    void CrearIndicadores()
    {
        var placa = VisualesVR.Primitiva(PrimitiveType.Cube, "Indicadores", transform, VisualesVR.MaterialOpaco(new Color(0.1f, 0.1f, 0.11f)));
        placa.transform.localPosition = posIndicadores + new Vector3(0f, 0f, -0.006f);
        placa.transform.localScale = new Vector3(0.12f, 0.06f, 0.01f);

        _matLedTension = VisualesVR.MaterialSinLuz(LedRojo);
        _matLedSinTension = VisualesVR.MaterialSinLuz(LedApagado);
        var rojo = VisualesVR.Primitiva(PrimitiveType.Sphere, "LED_Tension", transform, _matLedTension);
        rojo.transform.localPosition = posIndicadores + new Vector3(-0.03f, 0.005f, 0f);
        rojo.transform.localScale = Vector3.one * 0.018f;
        var verde = VisualesVR.Primitiva(PrimitiveType.Sphere, "LED_SinTension", transform, _matLedSinTension);
        verde.transform.localPosition = posIndicadores + new Vector3(0.03f, 0.005f, 0f);
        verde.transform.localScale = Vector3.one * 0.018f;
        Rotulo("TENSIÓN   SIN T.", posIndicadores + new Vector3(0f, -0.022f, 0.002f), 0.011f, new Vector2(0.12f, 0.02f), Color.white, null);
    }

    void CrearBornes()
    {
        _bornes = new GameObject("Bornes_Prueba").transform;
        _bornes.SetParent(transform, false);
        _bornes.localPosition = posBornes;
        var laton = VisualesVR.MaterialOpaco(new Color(0.85f, 0.65f, 0.25f));
        for (int i = 0; i < 2; i++)
        {
            var b = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Borne_" + (i == 0 ? "L" : "N"), _bornes, laton);
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.transform.localPosition = new Vector3(i == 0 ? -0.022f : 0.022f, 0f, 0.008f);
            b.transform.localScale = new Vector3(0.016f, 0.012f, 0.016f);
        }
        Rotulo("PUNTO DE PRUEBA", new Vector3(0f, -0.025f, 0.004f), 0.011f, new Vector2(0.1f, 0.02f), Color.white, null, _bornes);
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
    void Rotulo(string texto, Vector3 pos, float alturaLetra, Vector2 caja, Color colorTexto, Color? fondo,
                Transform padre = null)
    {
        if (padre == null) padre = transform;
        if (fondo.HasValue)
        {
            var placa = VisualesVR.Primitiva(PrimitiveType.Cube, "Placa_" + texto.Split('\n')[0], padre, VisualesVR.MaterialOpaco(fondo.Value));
            placa.transform.localPosition = pos + new Vector3(0f, 0f, -0.006f);
            placa.transform.localScale = new Vector3(caja.x + 0.02f, caja.y + 0.02f, 0.008f);
        }
        var t = VisualesVR.Texto("Rotulo", padre, alturaLetra, caja);
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
        ActualizarIndicadores();
        if (Activo)
        {
            if (!encendido) SonidosVR.ReproducirEn(SonidosVR.Chispa, PuntoInterruptor, 0.7f, 10f);
            EntradaVR.VibrarAmbas(0.4f, 0.1f, 0.4f);
        }
        alCambiarAlimentacion?.Invoke(encendido);
    }

    /// <summary>
    /// Medicion del comprobador de tension: devuelve false si la punta no esta en los bornes.
    /// El resultado depende SOLO del estado logico del interruptor principal.
    /// </summary>
    public bool Comprobar(Vector3 punta, out bool sinTension)
    {
        sinTension = !Energizado;
        if (Vector3.Distance(punta, PuntoPrueba) > radioPrueba) return false;
        if (sinTension) Verificado = true;
        ActualizarIndicadores();
        return true;
    }

    /// <summary>Vuelve todo a ENCENDIDO (reinicio de la mision o de la practica).</summary>
    public void Restablecer()
    {
        Preparar();
        _principal.Forzar(true);
        foreach (var b in _secundarios) b.Forzar(true);
        Energizado = true;
        Verificado = false;
        ActualizarIndicadores();
    }

    void ActualizarIndicadores()
    {
        if (_matLedTension != null) _matLedTension.color = Energizado ? LedRojo : LedApagado;
        if (_matLedSinTension != null) _matLedSinTension.color = Verificado ? LedVerde : LedApagado;

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

        // El LED rojo parpadea mientras hay tension con la mision en curso (llama la atencion).
        if (_matLedTension != null && Energizado)
            _matLedTension.color = Activo && Mathf.Repeat(Time.time, 0.8f) > 0.4f ? LedApagado : LedRojo;
    }

    /// <summary>Llamar al cambiar Activo para refrescar luces.</summary>
    public void Refrescar() => ActualizarIndicadores();
}
