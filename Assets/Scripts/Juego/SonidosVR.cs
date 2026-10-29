using System;
using UnityEngine;

/// <summary>
/// Sonidos del juego generados por codigo (no requieren archivos de audio):
/// alarma, exito, error, clic, agarrar, sirena, motor, puerta y victoria.
/// </summary>
public static class SonidosVR
{
    const int Hz = 22050;

    static AudioClip _alarma, _exito, _error, _clic, _agarrar, _sirena, _motor, _puerta, _victoria, _campana, _agua, _fuego;
    static AudioClip _zumbido, _chispa, _descarga, _pitido;

    public static AudioClip Alarma => Obtener(ref _alarma, "Alarma", 2.4f, Alarma_);
    public static AudioClip Campana => Obtener(ref _campana, "Campana", 1.2f, Campana_);
    public static AudioClip Exito => Obtener(ref _exito, "Exito", 0.55f, Exito_);
    public static AudioClip Error => Obtener(ref _error, "Error", 0.35f, Error_);
    public static AudioClip Clic => Obtener(ref _clic, "Clic", 0.06f, Clic_);
    public static AudioClip Agarrar => Obtener(ref _agarrar, "Agarrar", 0.12f, Agarrar_);
    public static AudioClip Sirena => Obtener(ref _sirena, "Sirena", 3f, Sirena_);
    public static AudioClip Motor => Obtener(ref _motor, "Motor", 2f, Motor_);
    public static AudioClip Puerta => Obtener(ref _puerta, "Puerta", 0.7f, Puerta_);
    public static AudioClip Victoria => Obtener(ref _victoria, "Victoria", 1.8f, Victoria_);
    public static AudioClip Agua => Obtener(ref _agua, "Agua", 2f, Agua_);
    public static AudioClip Fuego => Obtener(ref _fuego, "Fuego", 3f, Fuego_);
    // Mision 2 (incendio electrico)
    public static AudioClip Zumbido => Obtener(ref _zumbido, "Zumbido", 2f, Zumbido_);
    public static AudioClip Chispa => Obtener(ref _chispa, "Chispa", 0.25f, Chispa_);
    public static AudioClip Descarga => Obtener(ref _descarga, "Descarga", 2f, Descarga_);
    public static AudioClip Pitido => Obtener(ref _pitido, "Pitido", 0.14f, Pitido_);

    static AudioSource _fuente2D;

    /// <summary>Sonido de interfaz (no espacial).</summary>
    public static void Reproducir2D(AudioClip clip, float volumen = 0.8f)
    {
        if (clip == null) return;
        if (_fuente2D == null)
        {
            var go = new GameObject("[Sonido2D]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _fuente2D = go.AddComponent<AudioSource>();
            _fuente2D.spatialBlend = 0f;
            _fuente2D.playOnAwake = false;
        }
        _fuente2D.PlayOneShot(clip, volumen);
    }

    /// <summary>Sonido 3D de un solo uso en una posicion del mundo.</summary>
    public static void ReproducirEn(AudioClip clip, Vector3 pos, float volumen = 1f, float distanciaMax = 25f)
    {
        if (clip == null) return;
        var go = new GameObject("[Sonido] " + clip.name);
        go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        ConfigurarEspacial(s, distanciaMax);
        s.clip = clip;
        s.volume = volumen;
        s.Play();
        UnityEngine.Object.Destroy(go, clip.length + 0.1f);
    }

    /// <summary>Crea una fuente 3D en bucle (apagada) colgada de un transform.</summary>
    public static AudioSource CrearBucle(Transform padre, AudioClip clip, float volumen, float distanciaMax = 40f)
    {
        var go = new GameObject("[Bucle] " + clip.name);
        go.transform.SetParent(padre, false);
        var s = go.AddComponent<AudioSource>();
        ConfigurarEspacial(s, distanciaMax);
        s.clip = clip;
        s.loop = true;
        s.volume = volumen;
        return s;
    }

    static void ConfigurarEspacial(AudioSource s, float distanciaMax)
    {
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = distanciaMax;
        s.dopplerLevel = 0f;
    }

    // ------------------------------------------------------------------

    static AudioClip Obtener(ref AudioClip cache, string nombre, float segundos, Func<float, float, float> f)
    {
        if (cache == null) cache = Crear(nombre, segundos, f);
        return cache;
    }

    static AudioClip Crear(string nombre, float segundos, Func<float, float, float> f)
    {
        int n = Mathf.CeilToInt(segundos * Hz);
        var datos = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Hz;
            datos[i] = Mathf.Clamp(f(t, segundos), -1f, 1f);
        }
        var clip = AudioClip.Create(nombre, n, 1, Hz, false);
        clip.SetData(datos, 0);
        return clip;
    }

    static float Seno(float frec, float t) => Mathf.Sin(2f * Mathf.PI * frec * t);
    static float Cuadrada(float frec, float t) => Seno(frec, t) >= 0f ? 1f : -1f;
    static float Envolvente(float t, float dur, float ataque = 0.01f, float caida = 0.05f)
        => Mathf.Clamp01(t / ataque) * Mathf.Clamp01((dur - t) / caida);

    static float Alarma_(float t, float d)
    {
        // Dos tonos alternados tipo alarma de estacion.
        float frec = ((int)(t / 0.25f)) % 2 == 0 ? 880f : 660f;
        float v = 0.6f * Seno(frec, t) + 0.25f * Cuadrada(frec, t);
        return 0.35f * v * Envolvente(t, d, 0.02f, 0.2f);
    }

    static float Campana_(float t, float d)
    {
        // Campana de estacion: golpes con decaimiento.
        float local = t % 0.3f;
        float env = Mathf.Exp(-local * 9f);
        float v = Seno(1250f, t) + 0.5f * Seno(1870f, t) + 0.3f * Seno(2650f, t);
        return 0.22f * v * env * Envolvente(t, d, 0.005f, 0.1f);
    }

    static readonly float[] NotasExito = { 523.25f, 659.25f, 783.99f };
    static readonly float[] NotasVictoria = { 392f, 523.25f, 659.25f, 783.99f };
    static readonly float[] InicioVictoria = { 0f, 0.18f, 0.36f, 0.54f };

    static float Exito_(float t, float d)
    {
        // Arpegio ascendente Do-Mi-Sol.
        var notas = NotasExito;
        int k = Mathf.Min(2, (int)(t / 0.13f));
        float local = t - k * 0.13f;
        float env = Mathf.Exp(-local * 6f);
        return 0.4f * (Seno(notas[k], t) + 0.3f * Seno(notas[k] * 2f, t)) * env * Envolvente(t, d, 0.005f, 0.1f);
    }

    static float Error_(float t, float d)
    {
        return 0.25f * (Cuadrada(140f, t) * 0.6f + Seno(70f, t) * 0.4f) * Envolvente(t, d, 0.01f, 0.08f);
    }

    static float Clic_(float t, float d)
    {
        return 0.5f * Seno(1800f, t) * Mathf.Exp(-t * 80f);
    }

    static float Agarrar_(float t, float d)
    {
        float frec = Mathf.Lerp(300f, 700f, t / d);
        return 0.45f * Seno(frec, t) * Envolvente(t, d, 0.005f, 0.06f);
    }

    static float Sirena_(float t, float d)
    {
        // Sirena "wail": la frecuencia sube y baja (2 ciclos en el clip para que haga loop).
        // Fase integrada de f(t) = 900 + 350*sin(2*pi*t/1.5).
        float w = 2f * Mathf.PI / 1.5f;
        float fase = 2f * Mathf.PI * (900f * t - 350f / w * (Mathf.Cos(w * t) - 1f));
        float v = Mathf.Sin(fase) + 0.35f * Mathf.Sin(2f * fase) + 0.15f * Mathf.Sin(3f * fase);
        return 0.3f * v;
    }

    static float Motor_(float t, float d)
    {
        // Rumor grave de motor diesel (frecuencias enteras -> loop sin cortes).
        float v = 0.6f * Seno(45f, t) + 0.3f * Seno(90f, t) + 0.15f * Cuadrada(30f, t);
        v *= 0.8f + 0.2f * Seno(6f, t);
        return 0.35f * v;
    }

    static float Hash01(float x)
    {
        float h = Mathf.Sin(x * 127.1f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    static float Fuego_(float t, float d)
    {
        // Fuego: rumor grave + siseo suave + chasquidos aleatorios (crepitar de madera).
        float rumor = (Mathf.PerlinNoise(t * 90f, 1.3f) - 0.5f) * 2f;
        float siseo = (Mathf.PerlinNoise(t * 1800f, 5.7f) - 0.5f) * 2f;
        float k = Mathf.Floor(t * 35f), local = t * 35f - k;
        float chasquido = Hash01(k) > 0.8f ? (Hash01(t * Hz) * 2f - 1f) * Mathf.Exp(-local * 25f) : 0f;
        return 0.35f * rumor + 0.12f * siseo + 0.55f * chasquido;
    }

    static float Agua_(float t, float d)
    {
        // Chorro de agua: siseo (ruido blanco suave) + rumor (ruido de baja frecuencia). Es ruido, asi que el loop no se nota.
        float blanco = Mathf.Sin((t * Hz) * 12.9898f) * 43758.5453f;
        blanco = (blanco - Mathf.Floor(blanco)) * 2f - 1f;
        float rumor = (Mathf.PerlinNoise(t * 600f, 0.7f) - 0.5f) * 2f;
        float silbido = (Mathf.PerlinNoise(t * 4000f, 3.1f) - 0.5f) * 2f;
        return 0.18f * blanco + 0.3f * rumor + 0.25f * silbido;
    }

    static float Zumbido_(float t, float d)
    {
        // Zumbido de red electrica: 100 Hz + armonicos y un "buzz" de 50 Hz (frecuencias enteras -> loop limpio).
        float v = 0.55f * Seno(100f, t) + 0.25f * Seno(150f, t) + 0.12f * Seno(300f, t) + 0.1f * Cuadrada(50f, t);
        v *= 0.85f + 0.15f * Seno(4f, t);
        return 0.3f * v;
    }

    static float Chispa_(float t, float d)
    {
        // Chasquido electrico: ruido seco con caida rapida y un pico agudo.
        float ruido = Hash01(t * Hz * 1.7f) * 2f - 1f;
        float chas = ruido * Mathf.Exp(-t * 30f);
        float pico = Seno(2400f, t) * Mathf.Exp(-t * 60f) * 0.4f;
        float k = Mathf.Floor(t * 90f);
        float rafaga = Hash01(k + 3.1f) > 0.55f ? ruido * 0.5f * Mathf.Exp(-t * 12f) : 0f;
        return 0.6f * (chas + pico + rafaga) * Envolvente(t, d, 0.001f, 0.03f);
    }

    static float Descarga_(float t, float d)
    {
        // Descarga de CO2: siseo fuerte (ruido blanco) con un rumor grave de fondo.
        float blanco = Mathf.Sin((t * Hz) * 78.233f) * 43758.5453f;
        blanco = (blanco - Mathf.Floor(blanco)) * 2f - 1f;
        float rumor = (Mathf.PerlinNoise(t * 300f, 2.3f) - 0.5f) * 2f;
        float silbido = (Mathf.PerlinNoise(t * 5200f, 7.9f) - 0.5f) * 2f;
        return 0.3f * blanco + 0.18f * rumor + 0.22f * silbido;
    }

    static float Pitido_(float t, float d)
    {
        return 0.4f * Seno(2200f, t) * Envolvente(t, d, 0.005f, 0.03f);
    }

    static float Puerta_(float t, float d)
    {
        // Silbido neumatico + golpe suave.
        float ruido = (Mathf.PerlinNoise(t * 900f, 0.3f) - 0.5f) * 2f;
        float silbido = ruido * Mathf.Exp(-t * 5f) * 0.4f;
        float golpe = Seno(90f, t) * Mathf.Exp(-t * 18f) * 0.4f;
        return (silbido + golpe) * Envolvente(t, d, 0.005f, 0.1f);
    }

    static float Victoria_(float t, float d)
    {
        // Fanfarria corta: Sol-Do-Mi-Sol(agudo) sostenido.
        var notas = NotasVictoria;
        var inicio = InicioVictoria;
        float v = 0f;
        for (int k = 0; k < notas.Length; k++)
        {
            if (t < inicio[k]) continue;
            float local = t - inicio[k];
            float env = k == notas.Length - 1 ? Mathf.Exp(-local * 1.6f) : Mathf.Exp(-local * 7f);
            v += (Seno(notas[k], t) + 0.4f * Seno(notas[k] * 2f, t) + 0.2f * Cuadrada(notas[k], t)) * env;
        }
        return 0.22f * v * Envolvente(t, d, 0.005f, 0.2f);
    }
}
