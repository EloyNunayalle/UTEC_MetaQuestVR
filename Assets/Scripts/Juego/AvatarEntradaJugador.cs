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
#if UNITY_EDITOR
        // Sin dispositivo XR activo (editor puro) se usan poses simuladas, igual
        // que el sample oficial. Con el simulador XR o la Quest se usan las
        // poses reales del rig.
        if (!OvrAvatarUtility.IsHeadsetActive())
        {
            _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new AvatarPosesSimuladas());
        }
        else
#endif
        {
            _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new AvatarTrackingDelegate(rig));
        }
        _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(new AvatarControlDelegate());
    }
}
