using System;
using UnityEngine;

/// <summary>
/// Palanca de interruptor (Mision 2): la palanca roja principal del tablero.
/// Se mueve con la mano: agarrandola (GRIP) y llevandola arriba/abajo, o empujandola con el dedo / la punta
/// del control. Arriba = ENCENDIDO, abajo = APAGADO. Al pasar la mitad cambia de estado (clic + vibracion)
/// y al soltarla se asienta sola en el extremo. El estado es logico (lo lee PanelElectrico), no solo visual.
/// El padre debe tener su +Z hacia el jugador: la palanca sobresale hacia +Z y gira sobre el eje X local.
/// Se crea por codigo con <see cref="Crear"/>.
/// </summary>
public class InterruptorPalanca : MonoBehaviour
{
    public float anguloMaximo = 38f;
    [Tooltip("Distancia (m) de la mano a la palanca para tocarla o agarrarla.")]
    public float radioMano = 0.06f;

    public bool Encendido { get; private set; } = true;
    /// <summary>El jugador la toco o la movio al menos una vez.</summary>
    public bool Tocada { get; private set; }
    public bool Agarrada => _mano >= 0;
    /// <summary>Punta (pomo) de la palanca, para la flecha guia.</summary>
    public Vector3 Punta => _pomo != null ? _pomo.position : transform.position;

    public event Action<bool> alCambiar;

    Transform _eje, _pomo;
    Renderer[] _renders;
    float _largo, _angulo, _proximaBusqueda;
    int _mano = -1, _hoverPrev = -1;
    OVRCameraRig _rig;

    public static InterruptorPalanca Crear(string nombre, Transform padre, Vector3 posLocal, float largo, float grosor,
                                           Color colorMango, Color colorBase)
    {
        var raiz = new GameObject(nombre).transform;
        raiz.SetParent(padre, false);
        raiz.localPosition = posLocal;
        raiz.localRotation = Quaternion.identity;

        var placa = VisualesVR.Primitiva(PrimitiveType.Cube, "Base", raiz, VisualesVR.MaterialOpaco(colorBase));
        placa.transform.localScale = new Vector3(grosor * 2.4f, grosor * 3.4f, 0.012f);
        placa.transform.localPosition = new Vector3(0f, 0f, -0.004f);

        var eje = new GameObject("Eje").transform;
        eje.SetParent(raiz, false);

        var palo = VisualesVR.Primitiva(PrimitiveType.Cube, "Palanca", eje, VisualesVR.MaterialOpaco(colorMango));
        palo.transform.localScale = new Vector3(grosor, grosor * 0.8f, largo);
        palo.transform.localPosition = new Vector3(0f, 0f, largo * 0.5f);

        var pomo = VisualesVR.Primitiva(PrimitiveType.Sphere, "Pomo", eje, VisualesVR.MaterialOpaco(Color.Lerp(colorMango, Color.black, 0.2f)));
        pomo.transform.localScale = Vector3.one * grosor * 1.8f;
        pomo.transform.localPosition = new Vector3(0f, 0f, largo);

        var p = raiz.gameObject.AddComponent<InterruptorPalanca>();
        p._eje = eje;
        p._pomo = pomo.transform;
        p._largo = largo;
        p._renders = eje.GetComponentsInChildren<Renderer>();
        p._angulo = p.anguloMaximo;
        p.Aplicar();
        return p;
    }

    /// <summary>Cambia el estado sin sonido ni evento (reinicio de la mision).</summary>
    public void Forzar(bool encendido)
    {
        Encendido = encendido;
        _angulo = encendido ? anguloMaximo : -anguloMaximo;
        _mano = -1;
        Tocada = false;
        Aplicar();
    }

    Transform Mano(int i)
    {
        if (_rig == null && Time.time >= _proximaBusqueda)
        {
            _proximaBusqueda = Time.time + 1f;
            _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : FindObjectOfType<OVRCameraRig>();
        }
        if (_rig == null) return null;
        var m = i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor;
        return m != null && m.gameObject.activeInHierarchy ? m : null;
    }

    /// <summary>Distancia de un punto al segmento eje-pomo.</summary>
    float DistanciaPalanca(Vector3 p)
    {
        Vector3 a = _eje.position, b = _pomo.position;
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector3.Distance(p, a + ab * t);
    }

    /// <summary>Angulo (grados, + arriba) de la mano respecto del eje, en el plano de giro.</summary>
    float AnguloMano(Vector3 p)
    {
        Vector3 v = transform.InverseTransformPoint(p);
        return Mathf.Clamp(Mathf.Atan2(v.y, Mathf.Max(0.01f, v.z)) * Mathf.Rad2Deg, -anguloMaximo, anguloMaximo);
    }

    void Update()
    {
        if (_eje == null) return;
        bool permitido = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;

        int hover = -1;
        float mejor = float.MaxValue;
        if (permitido)
            for (int i = 0; i < 2; i++)
            {
                var m = Mano(i);
                if (m == null) continue;
                float d = DistanciaPalanca(m.position);
                if (d < radioMano && d < mejor) { mejor = d; hover = i; }
            }

        bool moviendo = false;
        if (_mano >= 0)
        {
            var m = Mano(_mano);
            if (m == null || !permitido || !EntradaVR.Agarrando(_mano == 0))
            {
                EntradaVR.Vibrar(_mano == 0, 0.2f, 0.04f);
                _mano = -1;
            }
            else
            {
                _angulo = Mathf.MoveTowards(_angulo, AnguloMano(m.position), Time.deltaTime * 600f);
                moviendo = true;
            }
        }
        else if (hover >= 0)
        {
            var m = Mano(hover);
            Tocada = true;
            if (EntradaVR.AgarreDown(hover == 0))
            {
                _mano = hover;
                EntradaVR.Vibrar(hover == 0, 0.5f, 0.08f, 0.7f);
                SonidosVR.ReproducirEn(SonidosVR.Agarrar, Punta, 0.6f, 8f);
            }
            else
            {
                // Empujon con el dedo: la palanca sigue a la mano mientras la toca.
                _angulo = Mathf.MoveTowards(_angulo, AnguloMano(m.position), Time.deltaTime * 400f);
                moviendo = true;
            }
        }

        Evaluar();

        // Suelta: se asienta en el extremo del estado actual.
        if (!moviendo)
            _angulo = Mathf.MoveTowards(_angulo, Encendido ? anguloMaximo : -anguloMaximo, Time.deltaTime * 320f);

        if (hover != _hoverPrev && hover >= 0 && _mano < 0)
            EntradaVR.Vibrar(hover == 0, 0.25f, 0.04f, 0.8f);
        _hoverPrev = hover;

        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        VisualesVR.Tenir(_renders, new Color(1f, 0.9f, 0.2f), _mano >= 0 ? 0.35f : hover >= 0 ? 0.25f + 0.25f * pulso : 0f);
        Aplicar();
    }

    void Evaluar()
    {
        float umbral = anguloMaximo * 0.35f;
        if (Encendido && _angulo < -umbral) Cambiar(false);
        else if (!Encendido && _angulo > umbral) Cambiar(true);
    }

    void Cambiar(bool encendido)
    {
        Encendido = encendido;
        Tocada = true;
        SonidosVR.ReproducirEn(SonidosVR.Clic, Punta, 1f, 10f);
        SonidosVR.ReproducirEn(SonidosVR.Puerta, Punta, 0.35f, 8f); // golpe seco del mecanismo
        if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.9f, 0.12f, 0.6f);
        else if (_hoverPrev >= 0) EntradaVR.Vibrar(_hoverPrev == 0, 0.9f, 0.12f, 0.6f);
        alCambiar?.Invoke(encendido);
    }

    void Aplicar()
    {
        // Euler X negativo levanta la punta (+Z hacia +Y).
        _eje.localRotation = Quaternion.Euler(-_angulo, 0f, 0f);
    }
}
