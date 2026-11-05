using Oculus.Avatar2;

/// <summary>
/// Entrada del avatar de la persona rescatada: una pose fija, sin leer el visor ni los controles del
/// jugador. El controlador de piernas del SDK necesita una "cabeza" valida; el movimiento real lo
/// hacen las animaciones y el transform del personaje.
/// </summary>
public class AvatarEntradaNPC : OvrAvatarInputManager
{
    protected override void OnTrackingInitialized()
    {
        _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new AvatarPosesSimuladas());
    }
}
