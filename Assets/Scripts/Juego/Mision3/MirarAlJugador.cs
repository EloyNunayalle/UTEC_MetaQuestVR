using UnityEngine;

/// <summary>Hace que un cartel mire siempre al jugador (solo giro horizontal).</summary>
public class MirarAlJugador : MonoBehaviour
{
    public Transform objetivo;

    void LateUpdate()
    {
        if (objetivo == null) return;
        Vector3 d = transform.position - objetivo.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
    }
}
