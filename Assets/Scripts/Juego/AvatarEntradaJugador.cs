using Oculus.Avatar2;
using UnityEngine;

/// <summary>
/// Manager de entrada del avatar: conecta el OVRCameraRig y los controles con el
/// SDK de avatares. Las manos/brazos del avatar siguen los controladores y los
/// dedos cambian de pose al presionar botones (sin hand tracking).
/// </summary>
public class AvatarEntradaJugador : OvrAvatarInputManager
{
    [Tooltip("Rig del jugador (poses de cabeza y manos). Si esta vacio, se busca solo.")]
    public OVRCameraRig rig;

    protected override void OnTrackingInitialized()
    {
        if (rig == null) rig = FindObjectOfType<OVRCameraRig>();
        _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new AvatarTrackingDelegate(rig));
        _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(new AvatarControlDelegate());
    }
}
