using System.Collections;
using UnityEngine;

public partial class DemoGuion
{
    public static int PruebaMision = 3;

    /// <summary>Banco de pruebas (desde = -1): recarga la escena dos veces (como entre misiones) y deja todo listo para inspeccionar.</summary>
    IEnumerator Prueba()
    {
        Log("Prueba: recargas");
        for (int k = 0; k < 2; k++)
        {
            yield return Esperar(3f);
            var viejo = Juego;
            typeof(JuegoBomberos).GetMethod("Reiniciar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Juego, null);
            yield return EsperarHasta(() => Juego != null && Juego != viejo && D.rig != null, 20f, "recarga");
            Log("Recarga " + (k + 1));
        }
        yield return Esperar(4f);
        MirarAlFrente(20f);
        D.MoverMano(true, () => PoseEn(true, Cabeza.position + Cabeza.forward * 0.4f + Cabeza.right * -0.15f + Vector3.down * 0.3f), 0.8f);
        D.MoverMano(false, () => PoseEn(false, Cabeza.position + Cabeza.forward * 0.4f + Cabeza.right * 0.15f + Vector3.down * 0.3f), 0.8f);
        Log("Prueba lista");
        yield return Esperar(600f);
    }
}
