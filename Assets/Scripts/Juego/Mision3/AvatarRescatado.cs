using System;
using System.Collections;
using System.Reflection;
using Oculus.Avatar2;
using UnityEngine;

/// <summary>
/// Meta Avatar de la persona rescatada: un OvrAvatarEntity propio, independiente del jugador (no usa
/// el rig, ni los controles, ni la instancia del AvatarJugador). Carga un preset local como el jugador.
/// Las animaciones salen del sistema del SDK, como en sus samples AvatarRetargeting y LegsNetworkLoopback:
/// OvrAvatarAnimationBehavior con el rig RTR00003, y su MecanimLegsAnimationController con los parametros
/// manuales (_useOverrideParameters): agachada, de pie o caminando.
/// Esas clases estan en los samples del SDK (Assets/Samples), que no van al repositorio. Se usan por
/// reflexion para que el proyecto compile igual sin ellos: en ese caso Crear() devuelve null y
/// PersonaRescatadaNPC usa su cuerpo de primitivas.
/// </summary>
public class AvatarRescatado : OvrAvatarEntity
{
    const string TipoComportamiento = "Oculus.Avatar2.Experimental.OvrAvatarAnimationBehavior, Oculus.AvatarSDK2.Samples";
    const string TipoPiernas = "Oculus.Avatar2.MecanimLegsAnimationController, Oculus.AvatarSDK2.Samples";
    const BindingFlags Campos = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const float TiempoMaximoCarga = 45f;

    /// <summary>El avatar ya se ve y sus animaciones responden: se puede ocultar el cuerpo de reemplazo.</summary>
    public bool Listo { get; private set; }
    /// <summary>No se pudo cargar (sin presets, por ejemplo): queda el cuerpo de reemplazo.</summary>
    public bool Fallo { get; private set; }

    int _preset;
    Type _tipoPiernas;
    Component _piernas;
    FieldInfo _fUsar, _fMovimiento, _fVelocidad, _fAgachado;
    object _movCaminar, _movAgachar, _movNinguno;
    object _movActual;

    /// <summary>Crea el avatar como hijo de padre. Devuelve null si falta algo del SDK o sus samples.</summary>
    public static AvatarRescatado Crear(Transform padre, int preset)
    {
        try
        {
            var comportamiento = Type.GetType(TipoComportamiento);
            var piernas = Type.GetType(TipoPiernas);
            if (comportamiento == null || piernas == null || !OvrAvatarManager.hasInstance)
            {
                Debug.Log("[AvatarRescatado] Sin samples del Meta Avatars SDK o sin manager: se usa el cuerpo de reemplazo.");
                return null;
            }

            // Inactivo hasta terminar de configurar: Awake crea la entidad del SDK.
            var go = new GameObject("Avatar_Rescatado");
            go.SetActive(false);
            go.transform.SetParent(padre, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // el rig del SDK mira hacia -Z

            var entrada = go.AddComponent<AvatarEntradaNPC>();
            var avatar = go.AddComponent<AvatarRescatado>();
            avatar.Configurar(preset, piernas, entrada);

            // Rig por defecto (RTR00003) sin mezclar tracking: toda la pose viene de la animacion.
            var c = go.AddComponent(comportamiento);
            PonerCampo(c, "_inputBlended", false);
            PonerCampo(c, "_customAnimationOnly", false);

            go.SetActive(true);
            return avatar;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AvatarRescatado] No se pudo crear el avatar: " + e.Message);
            return null;
        }
    }

    static void PonerCampo(object objeto, string nombre, object valor)
    {
        var f = objeto.GetType().GetField(nombre, Campos);
        if (f != null) f.SetValue(objeto, valor);
        else Debug.LogWarning("[AvatarRescatado] No existe el campo " + nombre);
    }

    void Configurar(int preset, Type piernas, AvatarEntradaNPC entrada)
    {
        _preset = preset;
        _tipoPiernas = piernas;
        // Igual que el sample AvatarRetargeting: cuerpo completo, sin depender del rig del jugador.
        _creationInfo.features = (CAPI.ovrAvatar2EntityFeatures)374;
        _creationInfo.renderFilters.manifestationFlags = CAPI.ovrAvatar2EntityManifestationFlags.Full;
        _creationInfo.renderFilters.viewFlags = CAPI.ovrAvatar2EntityViewFlags.All;
        _creationInfo.renderFilters.loadRigZipFromGlb = false;
        _creationInfo.renderFilters.quality = CAPI.ovrAvatar2EntityQuality.Light; // los zip de presets son "Light"
        _entrada = entrada;
    }

    AvatarEntradaNPC _entrada;
    float _limite;
    bool _oculto;

    IEnumerator Start()
    {
        _limite = Time.time + TiempoMaximoCarga;
        yield return null;
        SetInputManager(_entrada);

        // Mismo proceso que AvatarJugador: esperar al manager y cargar el preset del zip local.
        float espera = 0f;
        while (!OvrAvatarManager.initialized && espera < 10f)
        {
            espera += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (OvrAvatarManager.Instance == null) { Fallo = true; yield break; }

        const bool esZip = true;
        string sufijo = "_"
            + OvrAvatarManager.Instance.GetPlatformGLBPostfix(_creationInfo.renderFilters.quality, esZip)
            + OvrAvatarManager.Instance.GetPlatformGLBVersion(_creationInfo.renderFilters.quality, esZip)
            + OvrAvatarManager.Instance.GetPlatformGLBExtension(esZip);

        for (int intento = 0; intento < 6; intento++)
        {
            LoadAssets(new[] { _preset.ToString() + sufijo }, AssetSource.Zip);
            yield return new WaitForSecondsRealtime(2.5f);
            if (entityStatus.IsSuccess() || HasNonDefaultAvatar) yield break;
        }
        OvrAvatarLog.LogWarning("[AvatarRescatado] No se pudo cargar el preset local; queda el cuerpo de reemplazo.");
        Fallo = true;
    }

    void Update()
    {
        if (!IsCreated) return;

        // Tercera persona con cuerpo completo: lo que ve el jugador es este avatar desde afuera.
        if (GetActiveManifestation() != CAPI.ovrAvatar2EntityManifestationFlags.Full)
            SetActiveManifestation(CAPI.ovrAvatar2EntityManifestationFlags.Full);
        if (GetActiveView() != CAPI.ovrAvatar2EntityViewFlags.ThirdPerson)
            SetActiveView(CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);

        // Oculto hasta estar completo, para que no se vean dos cuerpos a la vez.
        if (!Listo && !_oculto)
        {
            _oculto = true;
            Hidden = true;
        }
        if (Listo || Fallo) return;

        if (HasNonDefaultAvatar && _piernas == null) BuscarPiernas();
        if (_piernas != null)
        {
            Listo = true;
            Hidden = false;
        }
        else if (Time.time > _limite) Fallo = true;
    }

    /// <summary>El rig lo instancia el comportamiento al terminar de cargar el avatar.</summary>
    void BuscarPiernas()
    {
        var c = GetComponentInChildren(_tipoPiernas);
        if (c == null) return;
        var t = _tipoPiernas;
        _fUsar = t.GetField("_useOverrideParameters", Campos);
        _fMovimiento = t.GetField("_movementParameterOverride", Campos);
        _fVelocidad = t.GetField("_moveSpeedOverride", Campos);
        _fAgachado = t.GetField("_crouchStrengthOverride", Campos);
        if (_fUsar == null || _fMovimiento == null || _fVelocidad == null || _fAgachado == null)
        {
            Debug.LogWarning("[AvatarRescatado] El controlador de piernas no tiene los parametros esperados.");
            Fallo = true;
            return;
        }
        var tipoEnum = _fMovimiento.FieldType;
        _movCaminar = Enum.Parse(tipoEnum, "MoveForward");
        _movAgachar = Enum.Parse(tipoEnum, "Crouch");
        _movNinguno = Enum.Parse(tipoEnum, "None");
        _fUsar.SetValue(c, true);
        _piernas = c;
    }

    /// <summary>kParado: 0 agachada - 1 de pie. velocidad: m/s de la marcha (0 = quieta).</summary>
    public void Animar(float kParado, float velocidad)
    {
        if (_piernas == null) return;
        object mov = velocidad > 0.05f ? _movCaminar : kParado < 0.98f ? _movAgachar : _movNinguno;
        if (!Equals(mov, _movActual))
        {
            _movActual = mov;
            _fMovimiento.SetValue(_piernas, mov);
        }
        _fAgachado.SetValue(_piernas, 1f - kParado);
        // Misma escala que usa el controlador del SDK con la velocidad real.
        _fVelocidad.SetValue(_piernas, Mathf.Clamp01((velocidad - 0.1f) / 9.9f));
    }
}
