using Oculus.Avatar2;
using UnityEngine;

/// <summary>
/// Fuente de tracking para el avatar del jugador: lee las poses del OVRCameraRig
/// (cabeza + controles) y se las entrega al SDK de avatares.
/// Solo controladores (no hand tracking).
/// </summary>
public class AvatarTrackingDelegate : OvrAvatarInputTrackingDelegate
{
    OVRCameraRig _rig;

    public AvatarTrackingDelegate(OVRCameraRig rig)
    {
        _rig = rig;
    }

    public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
    {
        inputTrackingState = default;
        bool izqActivo = false;
        bool derActivo = false;
        if (OVRInput.GetActiveController() != OVRInput.Controller.Hands)
        {
            izqActivo = OVRInput.GetControllerOrientationTracked(OVRInput.Controller.LTouch);
            derActivo = OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch);
        }
        if (_rig == null) return false;
        inputTrackingState.headsetActive = true;
        inputTrackingState.leftControllerActive = izqActivo;
        inputTrackingState.rightControllerActive = derActivo;
        inputTrackingState.leftControllerVisible = false;
        inputTrackingState.rightControllerVisible = false;
        inputTrackingState.headset = (CAPI.ovrAvatar2Transform)_rig.centerEyeAnchor;
        inputTrackingState.leftController = (CAPI.ovrAvatar2Transform)_rig.leftHandAnchor;
        inputTrackingState.rightController = (CAPI.ovrAvatar2Transform)_rig.rightHandAnchor;
        return true;
    }
}
