using Oculus.Avatar2;
using Button = OVRInput.Button;
using Touch = OVRInput.Touch;

/// <summary>
/// Mapea los botones de los controles a las poses de los dedos del avatar:
/// trigger = indice, grip = mano cerrada, touches = pulgar/indice, etc.
/// Asi la mano cambia de posicion al presionar botones (solo con controles).
/// </summary>
public class AvatarControlDelegate : OvrAvatarInputControlDelegate
{
    public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
    {
        inputControlState = new OvrAvatarInputControlState();
        inputControlState.type = GetControllerType();
        Actualizar(ref inputControlState.leftControllerState, OVRInput.Controller.LTouch);
        Actualizar(ref inputControlState.rightControllerState, OVRInput.Controller.RTouch);
        return true;
    }

    void Actualizar(ref OvrAvatarControllerState estado, OVRInput.Controller c)
    {
        estado.buttonMask = 0;
        estado.touchMask = 0;

        // Botones presionados
        if (OVRInput.Get(Button.One, c)) estado.buttonMask |= CAPI.ovrAvatar2Button.One;
        if (OVRInput.Get(Button.Two, c)) estado.buttonMask |= CAPI.ovrAvatar2Button.Two;
        if (OVRInput.Get(Button.Three, c)) estado.buttonMask |= CAPI.ovrAvatar2Button.Three;
        if (OVRInput.Get(Button.PrimaryThumbstick, c)) estado.buttonMask |= CAPI.ovrAvatar2Button.Joystick;

        // Toques (dedos apoyados)
        if (OVRInput.Get(Touch.One, c)) estado.touchMask |= CAPI.ovrAvatar2Touch.One;
        if (OVRInput.Get(Touch.Two, c)) estado.touchMask |= CAPI.ovrAvatar2Touch.Two;
        if (OVRInput.Get(Touch.PrimaryThumbstick, c)) estado.touchMask |= CAPI.ovrAvatar2Touch.Joystick;
        if (OVRInput.Get(Touch.PrimaryThumbRest, c)) estado.touchMask |= CAPI.ovrAvatar2Touch.ThumbRest;

        // Trigger (indice)
        estado.indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c);
        if (OVRInput.Get(Touch.PrimaryIndexTrigger, c)) estado.touchMask |= CAPI.ovrAvatar2Touch.Index;
        else if (estado.indexTrigger <= 0f) estado.touchMask |= CAPI.ovrAvatar2Touch.Pointing;

        // Grip (mano)
        estado.handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, c);

        // Pulgar arriba si no hay otro toque de pulgar
        if ((estado.touchMask & (CAPI.ovrAvatar2Touch.One | CAPI.ovrAvatar2Touch.Two |
                                 CAPI.ovrAvatar2Touch.Joystick | CAPI.ovrAvatar2Touch.ThumbRest)) == 0)
        {
            estado.touchMask |= CAPI.ovrAvatar2Touch.ThumbUp;
        }
    }
}
