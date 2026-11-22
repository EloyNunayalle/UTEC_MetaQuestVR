using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Arranca la demo (persona simulada + guion) al entrar en Play y, si se pidio, la graba con
/// Unity Recorder (MP4 H.264 1920x1080 a 30 fps con audio, a tiempo fijo: no depende de los fps del editor).
/// </summary>
[InitializeOnLoad]
public static class DemoEditor
{
    const string KeyActivo = "DemoVR_Activo";
    const string KeyGrabar = "DemoVR_Grabar";
    const string KeyDesde = "DemoVR_Desde";
    const string KeyEscala = "DemoVR_Escala";
    public const string CarpetaSalida = "Recordings";

    static RecorderController _controller;

    static DemoEditor()
    {
        EditorApplication.playModeStateChanged += AlCambiarPlay;
        EditorApplication.update += Vigilar;
    }

    [MenuItem("Demo/Probar (sin grabar)")]
    public static void Probar() => Iniciar(false, EditorPrefs.GetInt(KeyDesde, 0), 1f);

    [MenuItem("Demo/Grabar video completo")]
    public static void Grabar() => Iniciar(true, 0, 1f);

    public static void Iniciar(bool grabar, int desde, float escala)
    {
        EditorPrefs.SetBool(KeyActivo, true);
        EditorPrefs.SetBool(KeyGrabar, grabar);
        EditorPrefs.SetInt(KeyDesde, desde);
        EditorPrefs.SetFloat(KeyEscala, escala);
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    static void AlCambiarPlay(PlayModeStateChange e)
    {
        if (e == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(KeyActivo, false))
        {
            EditorPrefs.SetBool(KeyActivo, false); // una sola vez por pedido
            var d = DemoVR.Crear();
            var g = d.gameObject.AddComponent<DemoGuion>();
            g.desde = EditorPrefs.GetInt(KeyDesde, 0);
            g.grabando = EditorPrefs.GetBool(KeyGrabar, false);
            if (!g.grabando) Time.timeScale = EditorPrefs.GetFloat(KeyEscala, 1f);
            if (g.grabando) EmpezarGrabacion();
        }
        else if (e == PlayModeStateChange.ExitingPlayMode)
        {
            TerminarGrabacion();
        }
    }

    static void Vigilar()
    {
        if (!EditorApplication.isPlaying) return;
        if (DemoGuion.Terminado)
        {
            DemoGuion.Terminado = false;
            TerminarGrabacion();
            EditorApplication.isPlaying = false;
        }
    }

    static void EmpezarGrabacion()
    {
        var ajustes = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var video = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        video.name = "Demo";
        video.Enabled = true;
        video.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
        };
        video.CaptureAudio = true;
        video.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        string carpeta = Path.Combine(Directory.GetCurrentDirectory(), CarpetaSalida);
        Directory.CreateDirectory(carpeta);
        video.OutputFile = Path.Combine(carpeta, "Demo_VR_Bomberos");
        ajustes.AddRecorderSettings(video);
        ajustes.SetRecordModeToManual();
        ajustes.FrameRatePlayback = FrameRatePlayback.Constant;
        ajustes.FrameRate = 30f;
        ajustes.CapFrameRate = true;

        _controller = new RecorderController(ajustes);
        _controller.PrepareRecording();
        bool ok = _controller.StartRecording();
        Debug.Log("[DemoEditor] Grabacion iniciada: " + ok + " -> " + video.OutputFile + ".mp4");
    }

    static void TerminarGrabacion()
    {
        if (_controller == null) return;
        if (_controller.IsRecording()) _controller.StopRecording();
        _controller = null;
        Debug.Log("[DemoEditor] Grabacion terminada");
    }
}
