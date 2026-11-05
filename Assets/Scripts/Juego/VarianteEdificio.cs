using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cambia la variante de un edificio. La ciudad tiene el edificio EXTERIOR (sellado, sin interior);
/// la escena del interior de una mision trae su propia version explorable en el mismo lugar.
/// Mientras esa escena esta cargada, el exterior se oculta; al descargarla, vuelve a aparecer.
/// </summary>
public class VarianteEdificio : MonoBehaviour
{
    [Tooltip("Posicion (mundo) del edificio exterior de la ciudad que esta variante reemplaza.")]
    public Vector3 posicionExterior;
    [Tooltip("Distancia maxima para identificar la raiz del edificio exterior.")]
    public float radio = 1.5f;

    GameObject _exterior;

    void Awake()
    {
        var ciudad = SceneManager.GetSceneByName(GestorEscenas.EscenaMundo);
        if (!ciudad.isLoaded) return;
        foreach (var raiz in ciudad.GetRootGameObjects())
        {
            if (raiz.GetComponent<LODGroup>() == null) continue;
            Vector3 d = raiz.transform.position - posicionExterior;
            d.y = 0f;
            if (d.magnitude > radio) continue;
            _exterior = raiz;
            _exterior.SetActive(false);
            return;
        }
        Debug.LogWarning($"[VarianteEdificio] No se encontro el edificio exterior en {posicionExterior}.", this);
    }

    void OnDestroy()
    {
        if (_exterior != null) _exterior.SetActive(true);
    }
}
