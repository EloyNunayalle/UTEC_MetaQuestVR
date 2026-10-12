using UnityEngine;

/// <summary>
/// Al iniciar la partida, orienta el rig para que la cabeza del jugador
/// mire hacia el objetivo (la casa), sin importar la orientacion inicial
/// del visor (simulador o headset).
/// </summary>
public class OrientacionInicial : MonoBehaviour
{
    [Header("Referencias")]
    public Transform cabeza;
    public Transform rig;

    [Header("Orientacion")]
    [Tooltip("Yaw objetivo en grados (180 = mirar hacia -Z, la casa)")]
    public float yawObjetivo = 180f;
    [Tooltip("Tiempo maximo de espera a que el XR entregue una pose valida")]
    public float esperaMaxima = 3f;

    float _t;
    bool _hecho;

    void Awake()
    {
        if (rig == null) rig = transform;
    }

    void Update()
    {
        if (_hecho || cabeza == null || rig == null)
        {
            if (cabeza == null || rig == null) _hecho = true;
            return;
        }

        _t += Time.deltaTime;
        bool poseValida = (cabeza.position.y - rig.position.y) > 0.8f;
        if (!poseValida && _t < esperaMaxima) return;

        float yawActual = cabeza.eulerAngles.y;
        float delta = Mathf.DeltaAngle(yawActual, yawObjetivo);
        rig.rotation = Quaternion.Euler(0f, rig.eulerAngles.y + delta, 0f);
        _hecho = true;
    }
}
