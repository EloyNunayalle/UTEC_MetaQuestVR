using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using Unity.Profiling;

/// <summary>
/// Escena de diagnóstico en build de Windows: mueve una cámara por puntos
/// representativos del tile y registra draw calls, batches, setPass calls,
/// triángulos y vértices.
/// </summary>
public class DiagnosticoRT : MonoBehaviour
{
    private Camera _cam;
    private ProfilerRecorder _drawCalls;
    private ProfilerRecorder _batches;
    private ProfilerRecorder _setPass;
    private ProfilerRecorder _triangulos;
    private ProfilerRecorder _vertices;

    private Vector3 _centro;
    private Vector3[] _posiciones;
    private Vector3[] _lookTargets;
    private string[] _nombres;
    private string _ruta;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = gameObject.AddComponent<Camera>();
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 600f;
        _cam.fieldOfView = 60f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.09f, 0.12f, 0.18f);
        _cam.cullingMask = ~0;
    }

    private void OnEnable()
    {
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        _triangulos = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
    }

    private void Start()
    {
        StartCoroutine(Registrar());
    }

    private void OnDisable()
    {
        _drawCalls.Dispose();
        _batches.Dispose();
        _setPass.Dispose();
        _triangulos.Dispose();
        _vertices.Dispose();
    }

    private IEnumerator Registrar()
    {
        const string nombreArchivo = "diagnostico_build.csv";
        _ruta = Path.Combine(Application.persistentDataPath, nombreArchivo);

        var min = Vector3.one * float.MaxValue;
        var max = Vector3.one * float.MinValue;
        foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            var bounds = renderer.bounds;
            min = Vector3.Min(min, bounds.min);
            max = Vector3.Max(max, bounds.max);
        }

        _centro = (min + max) * 0.5f;
        float radioCorto = Mathf.Max(8f, Mathf.Min(max.x - min.x, max.z - min.z) * 0.45f);
        float radioLargo = Mathf.Max(12f, Mathf.Max(max.x - min.x, max.z - min.z) * 0.8f);

        _posiciones = new Vector3[6];
        _lookTargets = new Vector3[6];
        _nombres = new string[6];
        _posiciones[0] = _centro; _lookTargets[0] = _centro; _nombres[0] = "centro";
        _posiciones[1] = _centro + new Vector3(radioCorto, 3f, 0f); _lookTargets[1] = _centro + new Vector3(0f, 3f, 0f); _nombres[1] = "este";
        _posiciones[2] = _centro + new Vector3(-radioCorto, 3f, 0f); _lookTargets[2] = _centro + new Vector3(0f, 3f, 0f); _nombres[2] = "oeste";
        _posiciones[3] = _centro + new Vector3(0f, 3f, radioCorto); _lookTargets[3] = _centro + new Vector3(0f, 3f, 0f); _nombres[3] = "norte";
        _posiciones[4] = _centro + new Vector3(0f, 3f, -radioCorto); _lookTargets[4] = _centro + new Vector3(0f, 3f, 0f); _nombres[4] = "sur";
        _posiciones[5] = _centro + new Vector3(-radioLargo, 0f, -radioLargo); _lookTargets[5] = _centro; _nombres[5] = "esquina-los";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(
            "pos,x,y,z,yaw," +
            "drawCalls,batches,setPassCalls,triangulos,vertices," +
            "tiempo");

        for (int i = 0; i < _posiciones.Length; i++)
        {
            var pos = _posiciones[i];
            var look = _lookTargets[i];
            _cam.transform.position = pos;
            _cam.transform.LookAt(look);
            yield return new WaitForSecondsRealtime(0.6f);

            float drawCalls = _drawCalls.LastValue;
            float batches = _batches.LastValue;
            float setPass = _setPass.LastValue;
            float tris = _triangulos.LastValue;
            float verts = _vertices.LastValue;

            float yaw = _cam.transform.eulerAngles.y;
            string linea = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                _nombres[i],
                pos.x.ToString("F2", CultureInfo.InvariantCulture),
                pos.y.ToString("F2", CultureInfo.InvariantCulture),
                pos.z.ToString("F2", CultureInfo.InvariantCulture),
                yaw.ToString("F1", CultureInfo.InvariantCulture),
                drawCalls.ToString("F0", CultureInfo.InvariantCulture),
                batches.ToString("F0", CultureInfo.InvariantCulture),
                setPass.ToString("F0", CultureInfo.InvariantCulture),
                tris.ToString("F0", CultureInfo.InvariantCulture),
                verts.ToString("F0", CultureInfo.InvariantCulture),
                Time.realtimeSinceStartup.ToString("F2", CultureInfo.InvariantCulture));

            sb.AppendLine(linea);
            Debug.Log("[Diagnostico] " + linea);

            yield return null;
        }

        File.WriteAllText(_ruta, sb.ToString());
        Debug.Log("[Diagnostico] archivo=" + _ruta);

        Application.Quit();
    }

    private string ValorF0(ProfilerRecorder rec)
    {
        return rec.LastValue.ToString("F0", CultureInfo.InvariantCulture);
    }

    private void OnGUI()
    {
        if (_posiciones == null || _cam == null) return;
        int indice = Mathf.Clamp(_indiceActual, 0, _posiciones.Length - 1);
        var stats = new[]
        {
            "pos=" + _nombres[indice],
            "drawCalls=" + ValorF0(_drawCalls),
            "batches=" + ValorF0(_batches),
            "setPassCalls=" + ValorF0(_setPass),
            "triangulos=" + ValorF0(_triangulos),
            "vertices=" + ValorF0(_vertices)
        };
        GUI.color = Color.white;
        var fondo = new GUIStyle(GUI.skin.box) { normal = { textColor = Color.white } };
        GUI.Box(new Rect(10, 10, 260, 140), "[Diagnostico]\n" + string.Join("\n", stats), fondo);
    }

    private int _indiceActual;

    private void LateUpdate()
    {
        if (_posiciones != null && _cam != null)
            _indiceActual = Mathf.Clamp(_indiceActual, 0, _posiciones.Length - 1);
    }
}
