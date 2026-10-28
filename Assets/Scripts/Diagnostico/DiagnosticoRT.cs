using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

/// <summary>
/// Escena de diagnóstico en build de Windows: mueve una cámara por puntos
/// representativos del tile y registra draw calls, batches, setPass calls,
/// triángulos y vértices.
/// Con --juego mide Persistente + tile + interior y escribe diagnostico_juego.csv.
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
    private string _nombreArchivo = "diagnostico_build.csv";
    private bool _juegoCompleto;

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
        _cam.tag = "MainCamera";
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
        var args = System.Environment.GetCommandLineArgs();
        bool perfilQuest = System.Array.IndexOf(args, "--quest") >= 0;
        bool sinSombras = System.Array.IndexOf(args, "--sin-sombras") >= 0;
        bool juegoCompleto = System.Array.IndexOf(args, "--juego") >= 0;
        _juegoCompleto = juegoCompleto;
        if (juegoCompleto) _nombreArchivo = "diagnostico_juego.csv";

        if (perfilQuest)
        {
            QualitySettings.SetQualityLevel(2, true);
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadows = ShadowQuality.HardOnly;
            Debug.Log("[Diagnostico] perfil=Quest (Medium, 1 cascada)");
        }

        if (sinSombras)
        {
            QualitySettings.shadows = ShadowQuality.Disable;
            foreach (var luz in Object.FindObjectsOfType<Light>(true))
                luz.shadows = LightShadows.None;
            Debug.Log("[Diagnostico] sombras=OFF");
        }
        else
        {
            Debug.Log("[Diagnostico] sombras=ON");
        }

        if (juegoCompleto)
        {
            foreach (var camara in Object.FindObjectsOfType<Camera>(true))
                if (camara != _cam) camara.enabled = false;

            if (Application.CanStreamedLevelBeLoaded("Estacion_Interior") &&
                !SceneManager.GetSceneByName("Estacion_Interior").isLoaded)
            {
                SceneManager.LoadScene("Estacion_Interior", LoadSceneMode.Additive);
                Debug.Log("[Diagnostico] interior cargado para medicion");
            }
        }

        LogBatching();
        StartCoroutine(Registrar());
    }

    private void LogBatching()
    {
        int total = 0;
        int batched = 0;
        var porRoot = new System.Collections.Generic.Dictionary<string, int[]>();
        var porMaterial = new System.Collections.Generic.Dictionary<string, int[]>();

        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.enabled) continue;
            total++;
            bool estaBatcheado = r.isPartOfStaticBatch;
            if (estaBatcheado) batched++;

            var t = r.transform;
            while (t.parent != null) t = t.parent;
            string root = t.name;
            if (!porRoot.TryGetValue(root, out var datosRoot))
                porRoot[root] = datosRoot = new int[2];
            datosRoot[0]++;
            if (estaBatcheado) datosRoot[1]++;

            string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "(sin material)";
            if (!porMaterial.TryGetValue(mat, out var datosMat))
                porMaterial[mat] = datosMat = new int[2];
            datosMat[0]++;
            if (estaBatcheado) datosMat[1]++;
        }

        Debug.Log("[Diagnostico] staticBatch=" + batched + "/" + total);
        foreach (var par in porRoot)
            Debug.Log("[Diagnostico] batch root " + par.Key + "=" + par.Value[1] + "/" + par.Value[0]);
        foreach (var par in porMaterial)
            Debug.Log("[Diagnostico] batch mat " + par.Key + "=" + par.Value[1] + "/" + par.Value[0]);
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
        if (_juegoCompleto)
            yield return new WaitForSecondsRealtime(2.5f);

        _ruta = Path.Combine(Application.persistentDataPath, _nombreArchivo);

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
            if (i == 1) LogActivos("este");

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

    private void LogActivos(string etiqueta)
    {
        int total = 0;
        int batched = 0;
        int submeshes = 0;
        var porRoot = new System.Collections.Generic.Dictionary<string, int[]>();
        var porMaterial = new System.Collections.Generic.Dictionary<string, int[]>();

        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.enabled) continue;
            total++;
            if (r.isPartOfStaticBatch) batched++;
            var mf = r.GetComponent<MeshFilter>();
            int sub = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.subMeshCount : 0;
            submeshes += sub;

            var t = r.transform;
            while (t.parent != null) t = t.parent;
            string root = t.name;
            if (!porRoot.TryGetValue(root, out var datosRoot))
                porRoot[root] = datosRoot = new int[3];
            datosRoot[0]++;
            if (r.isPartOfStaticBatch) datosRoot[1]++;
            datosRoot[2] += sub;

            string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "(sin material)";
            if (!porMaterial.TryGetValue(mat, out var datosMat))
                porMaterial[mat] = datosMat = new int[3];
            datosMat[0]++;
            if (r.isPartOfStaticBatch) datosMat[1]++;
            datosMat[2] += sub;
        }

        Debug.Log("[Diagnostico] activos " + etiqueta + " renderers=" + total +
                  " submeshes=" + submeshes + " batched=" + batched);
        foreach (var par in porRoot)
            Debug.Log("[Diagnostico] activo root " + par.Key + " rend=" + par.Value[0] +
                      " sub=" + par.Value[2] + " batch=" + par.Value[1]);
        foreach (var par in porMaterial)
            Debug.Log("[Diagnostico] activo mat " + par.Key + " rend=" + par.Value[0] +
                      " sub=" + par.Value[2] + " batch=" + par.Value[1]);
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
