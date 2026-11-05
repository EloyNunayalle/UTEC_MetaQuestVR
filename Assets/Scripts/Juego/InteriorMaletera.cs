using UnityEngine;

/// <summary>
/// Crea un compartimento oscuro detras de las puertas de la maletera para que,
/// al abrirlas, no se vea el interior hueco del camion ni la cabina.
/// Se genera una sola vez al iniciar y sigue al camion.
/// </summary>
public class InteriorMaletera : MonoBehaviour
{
    [Tooltip("Ancho interior (m).")]
    public float ancho = 0.88f;
    [Tooltip("Alto interior (m).")]
    public float alto = 0.57f;
    [Tooltip("Profundidad del compartimento (m).")]
    public float fondo = 0.55f;
    [Tooltip("Centro del compartimento en coordenadas locales del camion.")]
    public Vector3 centro = new Vector3(0f, 1.045f, -3.96f);

    void Start()
    {
        if (transform.Find("Interior_Maletera") != null) return;

        var raiz = new GameObject("Interior_Maletera").transform;
        raiz.SetParent(transform, false);
        raiz.localPosition = centro;

        var material = VisualesVR.MaterialOpaco(new Color(0.16f, 0.16f, 0.17f));
        const float espesor = 0.02f;

        Crear("Fondo", raiz, material, new Vector3(0f, 0f, fondo * 0.5f),
              new Vector3(ancho, alto, espesor));
        Crear("Piso", raiz, material, new Vector3(0f, -alto * 0.5f, 0f),
              new Vector3(ancho, espesor, fondo));
        Crear("Techo", raiz, material, new Vector3(0f, alto * 0.5f, 0f),
              new Vector3(ancho, espesor, fondo));
        Crear("Pared_L", raiz, material, new Vector3(-ancho * 0.5f, 0f, 0f),
              new Vector3(espesor, alto, fondo));
        Crear("Pared_R", raiz, material, new Vector3(ancho * 0.5f, 0f, 0f),
              new Vector3(espesor, alto, fondo));
    }

    static void Crear(string nombre, Transform padre, Material material, Vector3 posicion, Vector3 escala)
    {
        var go = VisualesVR.Primitiva(PrimitiveType.Cube, nombre, padre, material);
        go.transform.localPosition = posicion;
        go.transform.localScale = escala;
    }
}
