using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor persistente de escenas (PLAN_ESCENAS_v1.md).
///
/// - Garantiza que "Persistente" (jugador + managers + camión) siempre esté cargada.
/// - Si el juego inicia desde la Persistente, carga encima la escena del mundo.
/// - Carga/descarga "Estacion_Interior" (decoración, chaleco, extintor, marcas de
///   piso) según la distancia del jugador a la estación, con histéresis para evitar
///   cargas/descargas continuas.
/// - Carga/descarga los interiores de las misiones ("Casa_M2_Interior", "Casa_M3_Interior")
///   por proximidad y SOLO durante su mision: en Explorar y en las demas misiones la ciudad
///   queda con los edificios exteriores (sellados).
/// - Sobrevive a las recargas de escena (DontDestroyOnLoad): si "Reiniciar" recarga
///   el mundo en modo Single y se lleva la Persistente, este gestor la repone.
///
/// El objeto se crea solo al iniciar Play/build; no hay que ponerlo en ninguna escena.
/// </summary>
public class GestorEscenas : MonoBehaviour
{
    public const string EscenaPersistente = "Persistente";
    public const string EscenaMundo = "Ciudad_Tile_00_Estacion";
    public const string EscenaEstacionInterior = "Estacion_Interior";
    public const string EscenaInteriorM2 = "Casa_M2_Interior";
    public const string EscenaInteriorM3 = "Casa_M3_Interior";

    /// <summary>Interior de una mision: su escena, el centro del edificio y la mision que lo usa.</summary>
    struct InteriorMision
    {
        public string escena;
        public Vector3 centro;
        public int mision;
    }

    static readonly InteriorMision[] InterioresMision =
    {
        new InteriorMision { escena = EscenaInteriorM2, centro = new Vector3(17.65f, 0f, -14f), mision = 2 },
        new InteriorMision { escena = EscenaInteriorM3, centro = new Vector3(-40.11f, 0f, 33.96f), mision = 3 }
    };

    [Header("Estación interior (carga por proximidad)")]
    [Tooltip("Centro del edificio de la estación (coordenadas del mundo).")]
    public Vector3 centroEstacion = new Vector3(-11.6f, 0f, -2.84f);
    [Tooltip("Distancia a la que se carga el interior.")]
    public float radioCarga = 45f;
    [Tooltip("Distancia a la que se descarga el interior (debe ser mayor que radioCarga).")]
    public float radioDescarga = 60f;

    [Header("Interiores de mision (carga por proximidad)")]
    [Tooltip("Distancia al edificio a la que se carga su interior durante la mision.")]
    public float radioCargaMision = 30f;
    [Tooltip("Distancia al edificio a la que se descarga su interior (debe ser mayor que la de carga).")]
    public float radioDescargaMision = 42f;

    static GestorEscenas _instancia;

    bool _cargando;
    float _proximaComprobacion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        if (_instancia != null) return;

        var go = new GameObject("Gestor_Escenas");
        DontDestroyOnLoad(go);
        _instancia = go.AddComponent<GestorEscenas>();

        // Cargas iniciales SINCRONAS: ocurren antes de los Start del primer frame,
        // así JuegoBomberos encuentra chaleco/extintor/marcas del interior.
        _instancia.CargaInicial();
    }

    void CargaInicial()
    {
        if (!SceneManager.GetSceneByName(EscenaPersistente).isLoaded)
            SceneManager.LoadScene(EscenaPersistente, LoadSceneMode.Additive);

        if (SceneManager.GetActiveScene().name == EscenaPersistente &&
            !SceneManager.GetSceneByName(EscenaMundo).isLoaded)
            SceneManager.LoadScene(EscenaMundo, LoadSceneMode.Additive);

        CargarInteriorSiCerca();
    }

    void Update()
    {
        if (_cargando) return;

        // --- Autoreparación: si una recarga Single se llevó la Persistente ---
        if (!SceneManager.GetSceneByName(EscenaPersistente).isLoaded)
        {
            SceneManager.LoadScene(EscenaPersistente, LoadSceneMode.Additive);
            CargarInteriorSiCerca(); // síncrono, antes de los Start del próximo frame
            return;
        }

        if (SceneManager.GetActiveScene().name == EscenaPersistente &&
            !SceneManager.GetSceneByName(EscenaMundo).isLoaded)
        {
            SceneManager.LoadScene(EscenaMundo, LoadSceneMode.Additive);
            return;
        }

        // --- Interior por proximidad (comprobación 2 veces por segundo) ---
        if (Time.unscaledTime < _proximaComprobacion) return;
        _proximaComprobacion = Time.unscaledTime + 0.5f;

        var jugador = BuscarJugador();
        if (jugador == null) return;

        float distancia = DistanciaHorizontal(jugador.position, centroEstacion);
        bool cargado = SceneManager.GetSceneByName(EscenaEstacionInterior).isLoaded;

        if (!cargado && distancia <= radioCarga)
        {
            StartCoroutine(CargarAditivo(EscenaEstacionInterior));
        }
        else if (cargado && distancia > radioDescarga && PuedeDescargarInterior())
        {
            StartCoroutine(Descargar(EscenaEstacionInterior));
        }

        if (!_cargando) ActualizarInterioresMision(jugador);
    }

    /// <summary>
    /// Mismo sistema que el interior de la estacion, con una condicion mas: solo se carga durante su
    /// mision. Al terminar la mision o alejarse, se descarga y el edificio exterior vuelve (VarianteEdificio).
    /// </summary>
    void ActualizarInterioresMision(Transform jugador)
    {
        var juego = JuegoBomberos.Instancia;
        int mision = juego != null ? juego.MisionActiva : 0;
        foreach (var interior in InterioresMision)
        {
            bool cargado = SceneManager.GetSceneByName(interior.escena).isLoaded;
            bool corresponde = mision == interior.mision;
            float distancia = DistanciaHorizontal(jugador.position, interior.centro);

            if (!cargado && corresponde && distancia <= radioCargaMision)
            {
                StartCoroutine(CargarAditivo(interior.escena));
                return;
            }
            if (cargado && distancia > radioDescargaMision &&
                (!corresponde || juego == null || juego.PermiteDescargarInterior(interior.mision)))
            {
                StartCoroutine(Descargar(interior.escena));
                return;
            }
        }
    }

    /// <summary>
    /// El interior contiene el chaleco y el extintor: no se descarga con la misión
    /// en curso y el equipamiento pendiente (el manager aún evalúa esas tareas).
    /// </summary>
    static bool PuedeDescargarInterior()
    {
        var juego = JuegoBomberos.Instancia;
        return juego == null || juego.PermiteDescargarEstacionInterior;
    }

    void CargarInteriorSiCerca()
    {
        if (SceneManager.GetSceneByName(EscenaEstacionInterior).isLoaded) return;

        var jugador = BuscarJugador();
        if (jugador == null) return;

        if (DistanciaHorizontal(jugador.position, centroEstacion) <= radioCarga)
            SceneManager.LoadScene(EscenaEstacionInterior, LoadSceneMode.Additive);
    }

    IEnumerator CargarAditivo(string nombre)
    {
        if (SceneManager.GetSceneByName(nombre).isLoaded) yield break;
        _cargando = true;
        yield return SceneManager.LoadSceneAsync(nombre, LoadSceneMode.Additive);
        _cargando = false;

        if (nombre == EscenaEstacionInterior)
        {
            var juego = JuegoBomberos.Instancia;
            if (juego != null) juego.RevisarEquipamiento();
        }
    }

    IEnumerator Descargar(string nombre)
    {
        _cargando = true;
        yield return SceneManager.UnloadSceneAsync(nombre);
        _cargando = false;
        Resources.UnloadUnusedAssets();
    }

    static Transform BuscarJugador()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) return rig.centerEyeAnchor != null ? rig.centerEyeAnchor : rig.transform;

        var avatar = GameObject.Find("AvatarJugador");
        return avatar != null ? avatar.transform : null;
    }

    static float DistanciaHorizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
