using UnityEngine;

/// <summary>
/// Entrada unificada para el juego: controles Touch (OVRInput), manos
/// (pellizco con OVRHand) y teclado (para probar en el editor).
/// Tambien maneja la vibracion (haptica) de los controles.
/// </summary>
public static class EntradaVR
{
    static Runner _runner;

    static Runner R
    {
        get
        {
            if (_runner == null)
            {
                var go = new GameObject("[EntradaVR]");
                go.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            return _runner;
        }
    }

    // ---------- Agarre (grip / gatillo / pellizco) ----------

    public static bool Agarrando(bool izquierda) => R.agarre[izquierda ? 0 : 1];
    public static bool AgarreDown(bool izquierda) => R.agarreDown[izquierda ? 0 : 1];

    // ---------- Confirmar (A / X / pellizco / Espacio) ----------

    public static bool ConfirmarDown() => R.confirmarDown;

    // ---------- Cancelar / bajar (B / Y / Q) ----------

    public static bool CancelarDown() => R.cancelarDown;

    /// <summary>Hay alguna mano rastreada pellizcando (para gestos mantenidos).</summary>
    public static bool AlgunPellizco() => R.pellizco[0] || R.pellizco[1];

    // ---------- Haptica ----------

    public static void Vibrar(bool izquierda, float amplitud, float duracion, float frecuencia = 0.5f)
    {
        R.Vibrar(izquierda ? 0 : 1, amplitud, duracion, frecuencia);
    }

    public static void VibrarAmbas(float amplitud, float duracion, float frecuencia = 0.5f)
    {
        Vibrar(true, amplitud, duracion, frecuencia);
        Vibrar(false, amplitud, duracion, frecuencia);
    }

    [DefaultExecutionOrder(-1000)]
    class Runner : MonoBehaviour
    {
        public readonly bool[] agarre = new bool[2];
        public readonly bool[] agarreDown = new bool[2];
        public readonly bool[] pellizco = new bool[2];
        public bool confirmarDown;
        public bool cancelarDown;

        readonly bool[] _pellizcoPrev = new bool[2];
        readonly float[] _finVibracion = { -1f, -1f };
        OVRHand[] _manos = new OVRHand[2];
        float _proximaBusqueda;

        static readonly OVRInput.Controller[] Ctrl = { OVRInput.Controller.LTouch, OVRInput.Controller.RTouch };

        void BuscarManos()
        {
            if (Time.unscaledTime < _proximaBusqueda) return;
            if (_manos[0] != null && _manos[1] != null) return;
            _proximaBusqueda = Time.unscaledTime + 2f;
            foreach (var h in FindObjectsOfType<OVRHand>())
            {
                int lado = LadoDe(h.transform);
                if (lado >= 0 && _manos[lado] == null) _manos[lado] = h;
            }
        }

        static int LadoDe(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name.ToLowerInvariant();
                if (n.Contains("left") || n.Contains("izq")) return 0;
                if (n.Contains("right") || n.Contains("der")) return 1;
            }
            return -1;
        }

        void Update()
        {
            BuscarManos();

            bool pellizcoDown = false;
            for (int i = 0; i < 2; i++)
            {
                var c = Ctrl[i];
                bool control = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, c) ||
                               OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, c);

                var h = _manos[i];
                bool pinch = h != null && h.isActiveAndEnabled && h.IsTracked &&
                             h.GetFingerIsPinching(OVRHand.HandFinger.Index);
                pellizco[i] = pinch;
                if (pinch && !_pellizcoPrev[i]) pellizcoDown = true;
                _pellizcoPrev[i] = pinch;

                bool nuevo = control || pinch;
                agarreDown[i] = nuevo && !agarre[i];
                agarre[i] = nuevo;

                if (_finVibracion[i] > 0f && Time.unscaledTime >= _finVibracion[i])
                {
                    _finVibracion[i] = -1f;
                    OVRInput.SetControllerVibration(0f, 0f, c);
                }
            }

            confirmarDown = OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Three) ||
                            pellizcoDown ||
                            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E);

            cancelarDown = OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four) ||
                           Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Backspace);
        }

        public void Vibrar(int i, float amp, float dur, float freq)
        {
            OVRInput.SetControllerVibration(Mathf.Clamp01(freq), Mathf.Clamp01(amp), Ctrl[i]);
            _finVibracion[i] = Mathf.Max(_finVibracion[i], Time.unscaledTime + dur);
        }

        void OnDisable()
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
