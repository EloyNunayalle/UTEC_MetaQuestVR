using System.Collections;
using Oculus.Avatar2;
using UnityEngine;

/// <summary>
/// Avatar del jugador: intenta cargar el avatar de su cuenta Meta (CDN) y, si no
/// hay o falla, usa un preset local (con reintentos, porque el zip tarda en cargar).
/// Los brazos y manos siguen los controladores (poses de dedos incluidas).
/// </summary>
public class AvatarJugador : OvrAvatarEntity
{
    [Tooltip("Cargar el avatar de la cuenta Meta (CDN). Si falla, usa el preset local.")]
    public bool cargarDeCuenta = true;

    [Tooltip("Segundos maximos de espera para descargar el avatar de la cuenta.")]
    public float esperaMaxima = 20f;

    protected IEnumerator Start()
    {
        yield return null;
        if (cargarDeCuenta) yield return CargarCuenta();
        else yield return CargarPreset();
    }

    IEnumerator CargarCuenta()
    {
        if (OvrPlatformInit.status == OvrPlatformInitStatus.NotStarted)
        {
            OvrPlatformInit.InitializeOvrPlatform();
        }
        float t = 0f;
        while (OvrPlatformInit.status != OvrPlatformInitStatus.Succeeded && t < 10f)
        {
            if (OvrPlatformInit.status == OvrPlatformInitStatus.Failed)
            {
                OvrAvatarLog.LogWarning("[AvatarJugador] Plataforma no disponible; uso preset local.");
                yield return CargarPreset();
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }
        if (OvrPlatformInit.status != OvrPlatformInitStatus.Succeeded)
        {
            yield return CargarPreset();
            yield break;
        }

        ulong id = 0;
        bool listo = false;
        Oculus.Platform.Users.GetLoggedInUser().OnComplete(msg =>
        {
            if (!msg.IsError) id = msg.Data.ID;
            listo = true;
        });
        while (!listo) yield return null;

        if (id == 0)
        {
            OvrAvatarLog.LogWarning("[AvatarJugador] Sin usuario logueado; uso preset local.");
            yield return CargarPreset();
            yield break;
        }

        _userId = id;
        LoadUser();
        float espera = 0f;
        while (entityStatus == CAPI.ovrAvatar2Result.Pending && espera < esperaMaxima)
        {
            espera += Time.deltaTime;
            yield return null;
        }
        if (!entityStatus.IsSuccess() && !HasNonDefaultAvatar)
        {
            OvrAvatarLog.LogWarning("[AvatarJugador] No se pudo cargar el avatar de la cuenta; uso preset local.");
            yield return CargarPreset();
        }
    }

    IEnumerator CargarPreset()
    {
        if (OvrAvatarManager.Instance == null)
        {
            OvrAvatarLog.LogError("[AvatarJugador] OvrAvatarManager no disponible.");
            yield break;
        }
        const bool esZip = true;
        string postfix = "_"
            + OvrAvatarManager.Instance.GetPlatformGLBPostfix(_creationInfo.renderFilters.quality, esZip)
            + OvrAvatarManager.Instance.GetPlatformGLBVersion(_creationInfo.renderFilters.quality, esZip)
            + OvrAvatarManager.Instance.GetPlatformGLBExtension(esZip);

        int intentos = 0;
        while (intentos < 6)
        {
            intentos++;
            LoadAssets(new[] { "0" + postfix }, AssetSource.Zip);
            yield return new WaitForSecondsRealtime(2.5f);
            if (entityStatus.IsSuccess() || HasNonDefaultAvatar)
            {
                OvrAvatarLog.LogInfo("[AvatarJugador] Preset cargado (intento " + intentos + ").");
                yield break;
            }
        }
        OvrAvatarLog.LogWarning("[AvatarJugador] No se pudo cargar el preset local tras varios intentos.");
    }
}
