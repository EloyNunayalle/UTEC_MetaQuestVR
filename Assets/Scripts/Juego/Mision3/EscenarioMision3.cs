using UnityEngine;

/// <summary>
/// Vive dentro del prefab de la casa explorable de la Mision 3 (escena Casa_M3_Interior, cargada por
/// proximidad). Al cargarse se presenta a Mision3Controller, que es persistente y guarda el progreso.
/// </summary>
public class EscenarioMision3 : MonoBehaviour
{
    public PuertaAtascada puerta;
    public PersonaRescatadaNPC persona;
    [Tooltip("Interior de la casa, en coordenadas locales de este objeto.")]
    public Vector3 centroCasa = new Vector3(1f, 2.5f, -6f);
    public Vector3 tamanoCasa = new Vector3(11.6f, 3.4f, 11.6f);

    /// <summary>Incendio dentro de la casa (se crea solo).</summary>
    public IncendioCasa Incendio { get; private set; }

    void Awake()
    {
        Incendio = GetComponent<IncendioCasa>();
        if (Incendio == null) Incendio = gameObject.AddComponent<IncendioCasa>();
    }

    public bool Dentro(Vector3 puntoMundo)
    {
        Vector3 p = transform.InverseTransformPoint(puntoMundo) - centroCasa;
        return Mathf.Abs(p.x) <= tamanoCasa.x * 0.5f && Mathf.Abs(p.y) <= tamanoCasa.y * 0.5f && Mathf.Abs(p.z) <= tamanoCasa.z * 0.5f;
    }

    void Start()
    {
        if (Mision3Controller.Instancia != null) Mision3Controller.Instancia.EngancharEscenario(this);
    }
}
