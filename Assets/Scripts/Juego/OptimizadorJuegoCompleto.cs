using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Optimización de runtime para el juego completo (Persistente + tile + interior).
/// No toca gameplay: conserva manos, controladores y objetos interactivos.
/// - Apaga sombras del Camera Rig, camión, HUD y ayudas visuales.
/// - Desactiva renderers de efectos visuales de Oculus Interaction
///   (rayos, retículas, glows, debug, teleport, flechas de giro).
/// Se ejecuta en builds; en la escena de diagnóstico sin estos objetos no hace nada.
/// </summary>
public static class OptimizadorJuegoCompleto
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        var go = new GameObject("OptimizadorJuegoCompleto");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<OptimizadorJuegoCompletoBehaviour>();
    }
}

public class OptimizadorJuegoCompletoBehaviour : MonoBehaviour
{
    private static readonly string[] Visuales =
    {
        "rayvisual", "cursorvisual", "pokeglow", "grabglow", "pokelimiter",
        "reticle", "teleport", "snapturn", "turnarrow", "pinchglow",
        "distantinteraction", "tubevisual", "handpointer", "grabstatevisual",
        "handgrabuse", "interactordebug", "linevisual", "overshoot"
    };

    private IEnumerator Start()
    {
        for (int i = 0; i < 8; i++)
        {
            Aplicar();
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Aplicar();
    }

    private void Aplicar()
    {
        var avatar = GameObject.Find("AvatarJugador");
        var rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig != null)
        {
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                // El avatar de Meta Avatars se emparenta al TrackingSpace en runtime;
                // no debe desactivarse: sus renderers no son "manos/controladores".
                if (avatar != null && r.transform.IsChildOf(avatar.transform)) continue;

                // Esferas y puntos de depuración de Oculus Interaction.
                if (r.name == "sphere" || r.name == "PokeLocation")
                {
                    r.enabled = false;
                    continue;
                }

                r.shadowCastingMode = ShadowCastingMode.Off;
                if (EsVisual(r.transform, rig.transform) ||
                    !EsManoControlador(r.transform, rig.transform))
                {
                    r.enabled = false;
                }
            }
        }

        var camion = GameObject.Find("Camion_Bomberos");
        if (camion != null)
        {
            foreach (var r in camion.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (var nombre in new[]
                 {
                     "HUD_Bomberos", "Selector_Modo", "Manguera_Bomberos",
                     "Marcador_Objetivo"
                 })
        {
            var go = GameObject.Find(nombre);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
        }

        var interior = SceneManager.GetSceneByName("Estacion_Interior");
        if (interior.isLoaded)
        {
            foreach (var raiz in interior.GetRootGameObjects())
                foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private static bool EsVisual(Transform t, Transform raiz)
    {
        var actual = t;
        while (actual != null && actual != raiz)
        {
            string nombre = actual.name.ToLowerInvariant();
            foreach (var token in Visuales)
                if (nombre.Contains(token)) return true;
            actual = actual.parent;
        }
        return false;
    }

    private static bool EsManoControlador(Transform t, Transform raiz)
    {
        var actual = t;
        while (actual != null && actual != raiz)
        {
            string nombre = actual.name.ToLowerInvariant();
            if (nombre.Contains("hand") || nombre.Contains("controller")) return true;
            actual = actual.parent;
        }
        return false;
    }
}
