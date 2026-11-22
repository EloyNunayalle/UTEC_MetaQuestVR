using System.Collections;
using UnityEngine;

public partial class DemoGuion
{
    // =====================================================================
    // Parte 1: Mision 2 (incendio electrico en la casa de ladrillo de la calle este)
    // =====================================================================
    IEnumerator Parte1_Mision2()
    {
        yield return Menu(irAConfiguracion: false, opcion: 2);
        yield return Mision2();
        yield return VolverAlSelector();
    }

    /// <summary>Salida del patio (a pie o en moto): por el porton principal hasta la calle.</summary>
    static readonly Vector3[] SalidaPatioMoto = { new Vector3(-6.4f, 0f, 12.2f), new Vector3(-5.0f, 0f, 17.5f), new Vector3(-2.5f, 0f, 21f) };

    IEnumerator Mision2()
    {
        Log("Mision 2");
        var j = Juego;
        yield return Esperar(3.5f); // lee el aviso de emergencia electrica

        // 1) Abre el porton principal (boton verde, del lado del patio).
        var principal = Buscar<PortonPrincipal>();
        Vector3 bVerde = principal.PuntoBoton;
        yield return Voltear(bVerde + Vector3.up * 0.3f, 1.2f);
        yield return Caminar(new Vector3(bVerde.x - 0.35f, 0f, bVerde.z - 0.62f));
        yield return Tocar(false, () => principal.PuntoBoton, Vector3.back, principal.radioBoton);
        yield return Voltear(principal.transform.position + Vector3.up * 1.2f, 1.6f);

        // 2) Sube a la moto y va a la casa siguiendo el GPS.
        yield return SubirAMoto();
        Log("M2 en moto: " + Moto.Montado);
        yield return Esperar(0.8f);
        yield return ConducirMoto(new Vector3(55.2f, 0f, -13f), 1.4f, 8.5f, SalidaPatioMoto);
        yield return BajarDeMoto();

        // 3) Entra a la casa (la escena del interior se carga al acercarse).
        yield return EsperarHasta(() => FindObjectOfType<Mision2Electrica>() != null, 8f, "interior de la casa M2");
        var m = FindObjectOfType<Mision2Electrica>();
        if (m == null) { Log("AVISO sin Mision2Electrica"); yield break; }
        Vector3 entrada = m.puerta.PuntoEntrada;
        yield return Voltear(entrada, 1f);
        yield return Caminar(1.2f, new Vector3(entrada.x - 2.6f, 0f, entrada.z), new Vector3(entrada.x - 0.4f, 0f, entrada.z),
                             new Vector3(entrada.x + 1.2f, 0f, entrada.z));
        // Desde la sala se ve el equipo que echa chispas por el paso al cuarto de servicio.
        Vector3 paso = m.transform.TransformPoint(m.pasoCuarto);
        yield return Caminar(1.0f, new Vector3(paso.x - 1.3f, 0f, paso.z));
        Mirar(() => m.maquina.PuntoMirar);
        yield return Esperar(2.2f);
        Log("M2 riesgo identificado: " + m.Tareas[0].hecha);

        // 4) Tablero: pared izquierda del cuarto de servicio, al entrar.
        var panel = m.panel;
        Vector3 haciaAfuera = -panel.transform.forward; // el tablero mira hacia el cuarto
        if (Vector3.Dot(haciaAfuera, Plano(m.transform.TransformPoint(m.centroCuarto) - panel.Centro)) < 0f) haciaAfuera = -haciaAfuera;
        Vector3 frenteTablero = panel.Centro + Plano(haciaAfuera).normalized * 0.95f;
        yield return Caminar(1.0f, new Vector3(paso.x + 0.6f, 0f, paso.z), new Vector3(frenteTablero.x, 0f, frenteTablero.z));
        yield return Voltear(panel.Centro, 1.2f);
        Log("M2 tablero localizado: " + m.Tareas[1].hecha);

        // 5) Baja la palanca roja con la mano derecha.
        var palanca = (InterruptorPalanca)Privado(panel, "_principal");
        if (palanca != null)
        {
            var eje = (Transform)Privado(palanca, "_eje");
            yield return Alcanzar(false, () => palanca.Punta, 0.8f);
            yield return Grip(false, true);
            float largo = Vector3.Distance(eje.position, palanca.Punta);
            Vector3 ini = D.Ancla(false).position;
            var rot = D.Ancla(false).rotation;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float ang = Mathf.Lerp(30f, -55f, Mathf.SmoothStep(0f, 1f, t / 0.7f)) * Mathf.Deg2Rad;
                Vector3 p = eje.position + palanca.transform.TransformDirection(new Vector3(0f, Mathf.Sin(ang), Mathf.Cos(ang))) * largo;
                D.MoverMano(false, () => new Pose(p, rot), 0f);
                yield return null;
            }
            yield return Esperar(0.4f);
            yield return Grip(false, false);
            yield return Reposo(false, 0.6f);
        }
        yield return Esperar(1.5f);
        Log("M2 energizado: " + panel.Energizado);

        // 6) Comprobador: lo toma de la mesa y toca el tablero con la punta.
        var comp = m.comprobador;
        yield return Voltear(comp.Centro, 0.8f);
        Vector3 frenteMesa = comp.Centro + Plano(Cabeza.position - comp.Centro).normalized * 0.65f;
        yield return Caminar(0.9f, new Vector3(frenteMesa.x, 0f, frenteMesa.z));
        yield return Alcanzar(false, () => comp.Centro, 0.8f);
        yield return Agarrar(false);
        yield return Esperar(0.3f);
        yield return Caminar(0.9f, new Vector3(frenteTablero.x, 0f, frenteTablero.z));
        yield return Voltear(panel.PuntoPrueba, 0.6f);
        // Apunta el comprobador al tablero y apoya la punta en el anillo verde.
        Quaternion rotComp = Quaternion.LookRotation((panel.PuntoPrueba - Hombro(false)).normalized, Vector3.up);
        D.MoverMano(false, () => new Pose(panel.PuntoPrueba - rotComp * Vector3.forward * 0.3f, rotComp), 0.7f);
        yield return Esperar(0.8f);
        Vector3 offPunta = Quaternion.Inverse(D.Ancla(false).rotation) * (comp.Punta - D.Ancla(false).position);
        D.MoverMano(false, () => new Pose(panel.PuntoPrueba - rotComp * offPunta + rotComp * Vector3.forward * 0.01f, rotComp), 0.6f);
        yield return EsperarHasta(() => panel.Verificado, 3f, "medir sin tension");
        yield return Esperar(1.6f); // lee SIN TENSION
        Log("M2 verificado: " + panel.Verificado);
        // Deja el comprobador en la mesa.
        yield return Caminar(0.9f, new Vector3(frenteMesa.x, 0f, frenteMesa.z));
        Vector3 mesa = FindMesa(comp);
        yield return Voltear(mesa, 0.5f);
        D.MoverMano(false, () => PoseEn(false, mesa + Vector3.up * 0.04f), 0.7f);
        yield return Esperar(0.75f);
        yield return Grip(false, false);
        yield return Reposo(false);

        // 7) Extintor CO2 junto al paso: lo toma, retira el pasador con la otra mano y apaga el fuego.
        var ext = m.extintor;
        yield return Voltear(ext.Centro, 0.8f);
        // Cuelga en el tabique del lado de la SALA: vuelve por el paso y se para frente a el.
        Vector3 haciaSala = Plano(paso - m.transform.TransformPoint(m.centroCuarto)).normalized;
        haciaSala = Mathf.Abs(haciaSala.x) > Mathf.Abs(haciaSala.z) ? new Vector3(Mathf.Sign(haciaSala.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(haciaSala.z));
        Vector3 frenteExt = ext.Centro + haciaSala * 0.72f;
        yield return Caminar(1.0f, new Vector3(paso.x - haciaSala.x * 0.6f, 0f, paso.z - haciaSala.z * 0.6f),
                             new Vector3(paso.x + haciaSala.x * 0.8f, 0f, paso.z + haciaSala.z * 0.8f),
                             new Vector3(frenteExt.x, 0f, frenteExt.z));
        yield return Voltear(ext.Centro, 0.5f);
        yield return Alcanzar(false, () => ext.Centro + Vector3.up * 0.12f, 0.8f);
        yield return Agarrar(false);
        yield return Esperar(0.3f);
        yield return EsperarHasta(() => ext.Agarrado, 1f, "extintor agarrado");
        // Lo trae al frente y retira el pasador con la izquierda.
        D.MoverMano(false, () => PoseEn(false, Cabeza.position + Plano(Cabeza.forward).normalized * 0.5f + Vector3.down * 0.38f, -10f), 0.7f);
        yield return Esperar(0.8f);
        yield return QuitarPasador(ext);
        Log("M2 pasador: " + ext.PasadorRetirado);

        // Entra al cuarto y apunta a la base del fuego.
        var fuego = m.maquina.Fuego;
        Vector3 haciaFuego = Plano(fuego.Centro - paso).normalized;
        Vector3 puestoFuego = fuego.Centro - haciaFuego * 2.4f;
        yield return Caminar(1.0f, new Vector3(paso.x + haciaSala.x * 0.8f, 0f, paso.z + haciaSala.z * 0.8f),
                             new Vector3(paso.x - haciaSala.x * 0.6f, 0f, paso.z - haciaSala.z * 0.6f),
                             new Vector3(puestoFuego.x, 0f, puestoFuego.z));
        yield return DescargarCO2(ext, () => fuego.Centro + Vector3.down * 0.25f, () => fuego.Extinguido, 20f);
        Log("M2 extinguido: " + fuego.Extinguido);

        // 8) Verifica que el area es segura: se acerca y mira el equipo.
        yield return Esperar(1f);
        Vector3 puestoMirar = m.maquina.PuntoMirar - Plano(m.maquina.PuntoMirar - Cabeza.position).normalized * 1.6f;
        D.MoverMano(false, () => PoseEn(false, Cabeza.position + Plano(Cabeza.forward).normalized * 0.25f + Vector3.down * 0.75f, -10f), 0.6f);
        yield return Caminar(0.8f, new Vector3(puestoMirar.x, 0f, puestoMirar.z));
        Mirar(() => m.maquina.PuntoMirar);
        yield return EsperarHasta(() => m.EstadoActual == Mision2Electrica.Estado.Asegurado, 8f, "area asegurada");
        Log("M2 estado: " + m.EstadoActual);
        yield return Esperar(2f);
        // Deja el extintor en el piso y lee el resultado.
        yield return Agacharse(1.2f, 0.7f);
        yield return Grip(false, false);
        yield return Esperar(0.4f);
        Pararse();
        yield return Reposo(false);
        MirarLibre(YawHacia(m.maquina.PuntoMirar), 8f);
        yield return Esperar(5f);
    }

    /// <summary>Punto sobre la mesa donde estaba el comprobador (para dejarlo).</summary>
    Vector3 FindMesa(ComprobadorTension comp)
    {
        var p = (Vector3)Privado(comp, "_posInicial");
        return p == Vector3.zero ? comp.transform.position : p + Vector3.up * 0.05f;
    }

    /// <summary>La mano libre toma el pasador del extintor (GRIP) y lo tira.</summary>
    IEnumerator QuitarPasador(ExtintorCO2 ext)
    {
        var pasador = (Transform)Privado(ext, "_pasador");
        bool izq = ext.Mano != 0; // la otra mano
        if (pasador != null)
        {
            yield return Alcanzar(izq, () => VisualesVR.Limites(pasador, out var b) ? b.center : pasador.position, 0.7f);
            yield return Agarrar(izq);
            yield return Esperar(0.2f);
            Vector3 p0 = D.Ancla(izq).position;
            var r0 = D.Ancla(izq).rotation;
            Vector3 tirar = Plano(Cabeza.position - p0).normalized * 0.12f + (izq ? -1f : 1f) * Vector3.Cross(Vector3.up, Plano(Cabeza.forward).normalized) * 0.1f;
            D.MoverMano(izq, () => new Pose(p0 + tirar, r0), 0.35f);
            yield return Esperar(0.4f);
            yield return Grip(izq, false);
        }
        if (!ext.PasadorRetirado)
        {
            // Respaldo: boton A/X de la mano que lo sostiene.
            if (ext.Mano == 0) yield return PulsarX(); else yield return PulsarA();
        }
        yield return Reposo(izq, 0.6f);
    }

    /// <summary>Apunta la bocina (sigue la mano) a la base del fuego, barriendo, y aprieta el gatillo hasta apagarlo.</summary>
    IEnumerator DescargarCO2(ExtintorCO2 ext, System.Func<Vector3> objetivo, System.Func<bool> listo, float maximo)
    {
        bool izq = ext.Mano == 0;
        Mirar(objetivo);
        float fin = Time.time + maximo, t0 = Time.time;
        bool apretado = false;
        while (!listo() && Time.time < fin)
        {
            float t = Time.time - t0;
            Vector3 f = Plano(Cabeza.forward).normalized;
            Vector3 der = Vector3.Cross(Vector3.up, f);
            Vector3 mano = Hombro(izq) + f * 0.38f + der * (izq ? -0.05f : 0.05f) + Vector3.down * 0.28f;
            Vector3 obj = objetivo() + der * Mathf.Sin(t * 2.4f) * 0.25f; // barrido de lado a lado
            var rot = Quaternion.LookRotation((obj - mano).normalized, Vector3.up);
            D.MoverMano(izq, () => new Pose(mano, rot), apretado ? 0f : 0.5f);
            if (!apretado && t > 0.5f) { apretado = true; yield return Gatillo(izq, 1f, 0.25f); }
            yield return null;
        }
        yield return Gatillo(izq, 0f, 0.25f);
        yield return Esperar(0.5f);
    }

}
