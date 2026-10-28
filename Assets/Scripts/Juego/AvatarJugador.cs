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
    [Tooltip("Cargar el avatar de la cuenta Meta (CDN). Requiere registrar el App ID en Meta > Platform. Si falla, usa el preset local.")]
    public bool cargarDeCuenta = false;

    [Tooltip("Segundos maximos de espera para descargar el avatar de la cuenta.")]
    public float esperaMaxima = 20f;

    [Header("Visual")]
    [Tooltip("Activa las piernas. Desactivado: solo cuerpo superior y manos.")]
    public bool mostrarPiernas = false;

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

    void Update()
    {
        if (_rig == null) _rig = FindObjectOfType<OVRCameraRig>();
        if (_rig == null) return;
        // El avatar se emparenta al TrackingSpace del rig: asi sigue al jugador (y al
        // camion) automaticamente, sin copiar posicion por frame. El OvrAvatarManager
        // muestrea la transform del entity en su propio Update (orden -1) y con el
        // parentesco siempre ve la posicion ya actualizada del mismo frame.
        if (transform.parent != _rig.trackingSpace)
        {
            transform.SetParent(_rig.trackingSpace, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        // Los modelos de control visibles se re-crean al iniciar: se re-ocultan.
        if (Time.time >= _proxOcultar)
        {
            _proxOcultar = Time.time + 1f;
            OcultarControles();
        }

        // Toggle de piernas: Half = torso/manos, Full = cuerpo completo.
        if (IsCreated)
        {
            var deseadas = mostrarPiernas
                ? CAPI.ovrAvatar2EntityManifestationFlags.Full
                : CAPI.ovrAvatar2EntityManifestationFlags.Half;
            if (GetActiveManifestation() != deseadas)
                SetActiveManifestation(deseadas);
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
        try
        {
            Oculus.Platform.Users.GetLoggedInUser().OnComplete(msg =>
            {
                if (!msg.IsError) id = msg.Data.ID;
                listo = true;
            });
        }
        catch (System.Exception e)
        {
            OvrAvatarLog.LogWarning("[AvatarJugador] Usuario no disponible (" + e.Message + "); uso preset local.");
        }
        // Con timeout: si el callback de la plataforma nunca llega, se usa el preset local.
        float tUsuario = 0f;
        while (!listo && tUsuario < 5f)
        {
            tUsuario += Time.deltaTime;
            yield return null;
        }

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
