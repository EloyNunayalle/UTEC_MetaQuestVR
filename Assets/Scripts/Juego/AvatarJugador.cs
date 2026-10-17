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

    OVRCameraRig _rig;
    float _proxOcultar;

    void OcultarControles()
    {
        if (_rig == null) _rig = FindObjectOfType<OVRCameraRig>();
        if (_rig == null) return;
        foreach (var t in _rig.gameObject.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.ToLower().Contains("controlleranchor")) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject != t.gameObject) r.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (_rig == null) _rig = FindObjectOfType<OVRCameraRig>();
        if (_rig == null) return;
        // El avatar se queda en la posicion del jugador (el tracking llega en local).
        transform.position = _rig.transform.position;
        transform.rotation = Quaternion.Euler(0f, _rig.transform.eulerAngles.y, 0f);
        // Los modelos de control visibles se re-crean al iniciar: se re-ocultan.
        if (Time.time >= _proxOcultar)
        {
            _proxOcultar = Time.time + 1f;
            OcultarControles();
        }
    }

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
        // Esperar a que el manager termine de inicializar (incluye la precarga de
        // los zips de presets). Sin esto la carga puede quedar sin texturas.
        float esperaMgr = 0f;
        while (!OvrAvatarManager.initialized && esperaMgr < 10f)
        {
            esperaMgr += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.5f);
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
