using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mapa y rutas del GPS, a partir de la ciudad que ya esta armada en Ciudad_Tile_00_Estacion:
/// - Mapa: una foto cenital real del tile (camara ortografica que renderiza UNA sola vez a una
///   textura con mipmaps). Se ocultan el jugador, los vehiculos y el HUD durante esa foto.
/// - Calles: grilla de 1.5 m armada con los tramos "Asfalto", el patio de la estacion y el paso del
///   porton. Las celdas con algo solido encima (postes, autos, barreras) quedan bloqueadas.
/// - Ruta: A* sobre esa grilla, prefiriendo el centro de la calzada, y luego simplificada a tramos rectos.
/// No depende de coordenadas fijas: si cambia el layout del tile, el GPS lo sigue.
/// </summary>
public static class MapaGPS
{
    public const float Celda = 1.5f;
    const int ResolucionMapa = 1536;

    public static bool Listo { get; private set; }
    public static RenderTexture Textura { get; private set; }
    /// <summary>Zona del mapa en el mundo: x = X minima, y = Z minima.</summary>
    public static Rect Area { get; private set; }
    public static bool HayCalles => _tipo != null;

    static int _nx, _nz;
    static byte[] _tipo;     // 0 = no transitable, 1 = calle, 2 = patio / porton
    static byte[] _holgura;  // celdas hasta el borde de la calzada (1..4)
    static float _cargaCiudad = -1f;
    static readonly Collider[] _buffer = new Collider[16];

    // A*
    static float[] _g;
    static int[] _padre, _marca;
    static bool[] _cerrado;
    static int _sello;
    static readonly List<int> _heapIdx = new List<int>();
    static readonly List<float> _heapF = new List<float>();
    static readonly List<int> _camino = new List<int>();
    static readonly List<int> _simple = new List<int>();

    /// <summary>Arma el mapa cuando la ciudad ya esta cargada. Devuelve true si esta listo.</summary>
    public static bool IntentarConstruir()
    {
        if (Listo) return true;
        var ciudad = SceneManager.GetSceneByName(GestorEscenas.EscenaMundo);
        if (!ciudad.IsValid() || !ciudad.isLoaded)
        {
            _cargaCiudad = -1f;
            return false;
        }
        if (_cargaCiudad < 0f) _cargaCiudad = Time.unscaledTime;
        if (Time.unscaledTime - _cargaCiudad < 1.5f) return false; // que terminen de iniciarse colliders y LOD

        var calles = new List<Bounds>();
        var extras = new List<Bounds>();
        foreach (var raiz in ciudad.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Asfalto")
                {
                    if (LimitesMalla(t, out Bounds bc)) calles.Add(bc);
                }
                else if (t.name == "Piso_Patio")
                {
                    if (LimitesMalla(t, out Bounds bp)) extras.Add(bp);
                }
                else if (t.name == "Porton_Principal")
                {
                    if (LimitesMalla(t, out Bounds bg)) extras.Add(Conector(bg));
                }
            }

        Area = CalcularArea(calles);
        if (calles.Count > 0) ConstruirGrilla(calles, extras);
        else Debug.LogWarning("[MapaGPS] No hay tramos 'Asfalto' en la ciudad: el GPS muestra el mapa sin rutas.");
        RenderizarMapa(ciudad);
        Listo = true;
        Debug.Log($"[MapaGPS] Mapa {Textura.width}x{Textura.height} de {Area.width:0}x{Area.height:0} m, {calles.Count} tramos de calle.");
        return true;
    }

    // ------------------------------------------------------------------ mapa

    static Rect CalcularArea(List<Bounds> calles)
    {
        if (calles.Count > 0)
        {
            var b = calles[0];
            foreach (var c in calles) b.Encapsulate(c);
            const float margen = 16f;
            return Rect.MinMaxRect(b.min.x - margen, b.min.z - margen, b.max.x + margen, b.max.z + margen);
        }
        var lim = LimiteCiudad.Instancia;
        if (lim != null) return Rect.MinMaxRect(lim.xMin - 10f, lim.zMin - 10f, lim.xMax + 10f, lim.zMax + 10f);
        return Rect.MinMaxRect(-115f, -95f, 105f, 65f);
    }

    static void RenderizarMapa(Scene ciudad)
    {
        int ancho = ResolucionMapa;
        int alto = Mathf.Clamp(Mathf.RoundToInt(ancho * Area.height / Area.width), 256, 2048);
        Textura = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32)
        {
            name = "MapaGPS",
            useMipMap = true,
            autoGenerateMips = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 2
        };
        Textura.Create();

        var go = new GameObject("Camara_MapaGPS");
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.orthographic = true;
        cam.targetTexture = Textura;
        cam.orthographicSize = Area.height * 0.5f;
        cam.aspect = Area.width / Area.height;
        cam.transform.SetPositionAndRotation(new Vector3(Area.center.x, 250f, Area.center.y), Quaternion.Euler(90f, 0f, 0f));
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.33f, 0.45f);
        cam.allowHDR = false;
        cam.allowMSAA = false;

        // Solo la ciudad (y los interiores cargados): fuera el jugador, los vehiculos, el HUD y los efectos.
        var ocultos = new List<Renderer>();
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (!r.enabled) continue;
            bool efecto = r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer;
            if (efecto || r.gameObject.scene.buildIndex <= 0) // 0 = Persistente, -1 = DontDestroyOnLoad
            {
                r.enabled = false;
                ocultos.Add(r);
            }
        }

        // Desde 250 m de altura, sin esto los LOD bajan a cajas y las sombras desaparecen.
        float lodBias = QualitySettings.lodBias, sombras = QualitySettings.shadowDistance;
        bool niebla = RenderSettings.fog;
        QualitySettings.lodBias = 1000f;
        QualitySettings.shadowDistance = 450f;
        RenderSettings.fog = false;
        try
        {
            cam.Render();
        }
        finally
        {
            QualitySettings.lodBias = lodBias;
            QualitySettings.shadowDistance = sombras;
            RenderSettings.fog = niebla;
            foreach (var r in ocultos) if (r != null) r.enabled = true;
            Object.Destroy(go);
        }
    }

    /// <summary>Coordenada de textura (0..1) de un punto del mundo.</summary>
    public static Vector2 UV(Vector3 mundo) =>
        new Vector2((mundo.x - Area.xMin) / Area.width, (mundo.z - Area.yMin) / Area.height);

    // ------------------------------------------------------------------ grilla de calles

    static bool LimitesMalla(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds m = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 w = mf.transform.TransformPoint(c);
                if (!hay) { b = new Bounds(w, Vector3.zero); hay = true; }
                else b.Encapsulate(w);
            }
        }
        return hay;
    }

    /// <summary>Paso por el porton: un rectangulo que atraviesa el muro, del patio a la calle.</summary>
    static Bounds Conector(Bounds porton)
    {
        Vector3 t = porton.size;
        Vector3 tam = t.x >= t.z ? new Vector3(Mathf.Max(1f, t.x - 0.6f), 1f, 14f) : new Vector3(14f, 1f, Mathf.Max(1f, t.z - 0.6f));
        return new Bounds(new Vector3(porton.center.x, porton.min.y, porton.center.z), tam);
    }

    static bool Contiene(Bounds b, float x, float z) => x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z;

    static void ConstruirGrilla(List<Bounds> calles, List<Bounds> extras)
    {
        _nx = Mathf.CeilToInt(Area.width / Celda);
        _nz = Mathf.CeilToInt(Area.height / Celda);
        int n = _nx * _nz;
        _tipo = new byte[n];
        for (int j = 0; j < _nz; j++)
            for (int i = 0; i < _nx; i++)
            {
                float x = Area.xMin + (i + 0.5f) * Celda, z = Area.yMin + (j + 0.5f) * Celda;
                byte tipo = 0;
                float piso = 0f;
                foreach (var b in calles)
                    if (Contiene(b, x, z)) { tipo = 1; piso = b.max.y; break; }
                if (tipo == 0)
                    foreach (var b in extras)
                        if (Contiene(b, x, z)) { tipo = 2; piso = b.min.y; break; }
                if (tipo != 0 && Bloqueada(x, piso, z)) tipo = 0;
                _tipo[i + j * _nx] = tipo;
            }

        // Holgura: distancia (en celdas) hasta lo no transitable, para circular por el centro.
        _holgura = new byte[n];
        var cola = new Queue<int>();
        for (int k = 0; k < n; k++)
        {
            int i = k % _nx, j = k / _nx;
            bool borde = _tipo[k] == 0 || i == 0 || j == 0 || i == _nx - 1 || j == _nz - 1;
            _holgura[k] = (byte)(_tipo[k] == 0 ? 0 : borde ? 1 : 255);
            if (_holgura[k] <= 1) cola.Enqueue(k);
        }
        while (cola.Count > 0)
        {
            int k = cola.Dequeue();
            int i = k % _nx, j = k / _nx;
            int h = _holgura[k] == 0 ? 1 : _holgura[k] + 1;
            if (h > 4) continue;
            for (int d = 0; d < 4; d++)
            {
                int ni = i + (d == 0 ? 1 : d == 1 ? -1 : 0), nj = j + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (ni < 0 || nj < 0 || ni >= _nx || nj >= _nz) continue;
                int nk = ni + nj * _nx;
                if (_holgura[nk] > h) { _holgura[nk] = (byte)h; cola.Enqueue(nk); }
            }
        }
        for (int k = 0; k < n; k++) if (_holgura[k] == 255) _holgura[k] = 4;

        _g = new float[n];
        _padre = new int[n];
        _marca = new int[n];
        _cerrado = new bool[n];
    }

    /// <summary>Hay algo solido a la altura de un vehiculo (no cuentan veredas, vehiculos, jugador ni portones).</summary>
    static bool Bloqueada(float x, float piso, float z)
    {
        int n = Physics.OverlapBoxNonAlloc(new Vector3(x, piso + 1.2f, z), new Vector3(Celda * 0.4f, 0.5f, Celda * 0.4f),
                                           _buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int k = 0; k < n; k++)
        {
            var c = _buffer[k];
            if (c == null) continue;
            if (c.GetComponentInParent<CamionBomberos>() != null || c.GetComponentInParent<MotoBomberos>() != null ||
                c.GetComponentInParent<OVRCameraRig>() != null) continue;
            if (EsPorton(c.transform)) continue; // la ruta pasa por los portones aunque esten cerrados
            return true;
        }
        return false;
    }

    static bool EsPorton(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name.Contains("Porton")) return true;
        return false;
    }

    // ------------------------------------------------------------------ rutas

    /// <summary>Ruta por las calles: desde, puntos de la calzada..., hasta. False si no hay camino.</summary>
    public static bool Ruta(Vector3 desde, Vector3 hasta, List<Vector3> salida)
    {
        salida.Clear();
        if (!Listo || _tipo == null) return false;
        int a = Cercana(desde), b = Cercana(hasta);
        if (a < 0 || b < 0 || !AEstrella(a, b)) return false;
        Simplificar();
        salida.Add(desde);
        for (int i = 0; i < _simple.Count; i++) salida.Add(Centro(_simple[i]));
        salida.Add(hasta);
        return true;
    }

    static Vector3 Centro(int k) => new Vector3(Area.xMin + (k % _nx + 0.5f) * Celda, 0.2f, Area.yMin + (k / _nx + 0.5f) * Celda);

    static int Cercana(Vector3 p)
    {
        int ci = Mathf.FloorToInt((p.x - Area.xMin) / Celda), cj = Mathf.FloorToInt((p.z - Area.yMin) / Celda);
        for (int r = 0; r <= 14; r++)
        {
            int mejor = -1;
            float mejorD = float.MaxValue;
            for (int j = cj - r; j <= cj + r; j++)
                for (int i = ci - r; i <= ci + r; i++)
                {
                    if (Mathf.Max(Mathf.Abs(i - ci), Mathf.Abs(j - cj)) != r) continue; // solo el anillo
                    if (i < 0 || j < 0 || i >= _nx || j >= _nz) continue;
                    int k = i + j * _nx;
                    if (_tipo[k] == 0) continue;
                    float d = (Centro(k) - new Vector3(p.x, 0.2f, p.z)).sqrMagnitude;
                    if (d < mejorD) { mejorD = d; mejor = k; }
                }
            if (mejor >= 0) return mejor;
        }
        return -1;
    }

    static float Penalidad(int k)
    {
        float p = _holgura[k] == 1 ? 2f : _holgura[k] == 2 ? 0.4f : 0f;
        if (_tipo[k] == 2) p += 0.3f;
        return p;
    }

    static bool AEstrella(int inicio, int fin)
    {
        _sello++;
        _heapIdx.Clear();
        _heapF.Clear();
        int fi = fin % _nx, fj = fin / _nx;
        Abrir(inicio, 0f, -1, fi, fj);

        while (_heapIdx.Count > 0)
        {
            int k = Sacar();
            if (_cerrado[k]) continue;
            _cerrado[k] = true;
            if (k == fin)
            {
                _camino.Clear();
                for (int c = fin; c >= 0; c = _padre[c]) _camino.Add(c);
                _camino.Reverse();
                return true;
            }
            int i = k % _nx, j = k / _nx;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= _nx || nj >= _nz) continue;
                    int nk = ni + nj * _nx;
                    if (_tipo[nk] == 0) continue;
                    bool diagonal = di != 0 && dj != 0;
                    if (diagonal && (_tipo[i + nj * _nx] == 0 || _tipo[ni + j * _nx] == 0)) continue; // sin cortar esquinas
                    float g = _g[k] + (diagonal ? 1.4142f : 1f) * Celda * (1f + Penalidad(nk));
                    if (_marca[nk] == _sello && (_cerrado[nk] || g >= _g[nk])) continue;
                    Abrir(nk, g, k, fi, fj);
                }
        }
        return false;
    }

    static void Abrir(int k, float g, int padre, int fi, int fj)
    {
        if (_marca[k] != _sello)
        {
            _marca[k] = _sello;
            _cerrado[k] = false;
        }
        _g[k] = g;
        _padre[k] = padre;
        int dx = Mathf.Abs(k % _nx - fi), dz = Mathf.Abs(k / _nx - fj);
        float h = (Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz)) * Celda;
        // Monticulo binario (con entradas repetidas: la vieja se ignora al estar cerrada).
        _heapIdx.Add(k);
        _heapF.Add(g + h);
        int c = _heapIdx.Count - 1;
        while (c > 0)
        {
            int p = (c - 1) / 2;
            if (_heapF[p] <= _heapF[c]) break;
            Cambiar(c, p);
            c = p;
        }
    }

    static int Sacar()
    {
        int k = _heapIdx[0];
        int ultimo = _heapIdx.Count - 1;
        _heapIdx[0] = _heapIdx[ultimo];
        _heapF[0] = _heapF[ultimo];
        _heapIdx.RemoveAt(ultimo);
        _heapF.RemoveAt(ultimo);
        int c = 0;
        while (true)
        {
            int l = c * 2 + 1, r = l + 1, m = c;
            if (l < _heapIdx.Count && _heapF[l] < _heapF[m]) m = l;
            if (r < _heapIdx.Count && _heapF[r] < _heapF[m]) m = r;
            if (m == c) break;
            Cambiar(c, m);
            c = m;
        }
        return k;
    }

    static void Cambiar(int a, int b)
    {
        int ti = _heapIdx[a]; _heapIdx[a] = _heapIdx[b]; _heapIdx[b] = ti;
        float tf = _heapF[a]; _heapF[a] = _heapF[b]; _heapF[b] = tf;
    }

    /// <summary>Tramos rectos: une celdas mientras la linea siga por la calzada (sin pegarse al borde).</summary>
    static void Simplificar()
    {
        _simple.Clear();
        if (_camino.Count == 0) return;
        _simple.Add(_camino[0]);
        int i = 0;
        while (i < _camino.Count - 1)
        {
            int j = _camino.Count - 1;
            while (j > i + 1 && !Visible(_camino[i], _camino[j])) j--;
            _simple.Add(_camino[j]);
            i = j;
        }
    }

    static bool Visible(int a, int b)
    {
        int minimo = Mathf.Min(2, Mathf.Min(_holgura[a], _holgura[b]));
        Vector3 pa = Centro(a), pb = Centro(b);
        int pasos = Mathf.CeilToInt(Vector3.Distance(pa, pb) / (Celda * 0.5f));
        for (int s = 1; s < pasos; s++)
        {
            Vector3 p = Vector3.Lerp(pa, pb, s / (float)pasos);
            int i = Mathf.FloorToInt((p.x - Area.xMin) / Celda), j = Mathf.FloorToInt((p.z - Area.yMin) / Celda);
            if (i < 0 || j < 0 || i >= _nx || j >= _nz) return false;
            int k = i + j * _nx;
            if (_tipo[k] == 0 || _holgura[k] < minimo) return false;
        }
        return true;
    }
}
