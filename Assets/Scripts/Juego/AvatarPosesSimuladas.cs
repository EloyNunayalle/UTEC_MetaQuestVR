using Oculus.Avatar2;
using UnityEngine;

/// <summary>
/// Poses simuladas para probar el avatar en el editor sin headset (igual que el
/// sample oficial): cabeza a 1.5 m y controladores inactivos.
/// Solo se usa en el editor cuando no hay un dispositivo XR activo; con el
/// simulador XR o en la Quest se usan las poses reales del rig.
/// </summary>
public class AvatarPosesSimuladas : OvrAvatarInputTrackingDelegate
{
    public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
    {
        inputTrackingState = default;
        inputTrackingState.headsetActive = true;
        inputTrackingState.leftControllerActive = false;
        inputTrackingState.rightControllerActive = false;
        inputTrackingState.leftControllerVisible = false;
        inputTrackingState.rightControllerVisible = false;
        inputTrackingState.headset.position = new Vector3(0f, 1.5f, 0f);
        inputTrackingState.headset.orientation = Quaternion.Euler(0f, 180f, 0f);
        inputTrackingState.headset.scale = Vector3.one;
        return true;
    }
}
