using System.Collections;
using UnityEngine;

public partial class DemoGuion
{
    // =====================================================================
    // Parte 2: Mision 3 (rescate de una persona atrapada)
    // =====================================================================
    IEnumerator Parte2_Mision3()
    {
        yield return Menu(irAConfiguracion: false, opcion: 3);
        yield return Mision3();
        yield return VolverAlSelector();
    }

    PuertaMaletera PuertaMaleteraIzq()
    {
        PuertaMaletera mejor = null;
        foreach (var p in FindObjectsOfType<PuertaMaletera>())
            if (p.anguloAbierto > 0f) mejor = p;
        return mejor != null ? mejor : FindObjectOfType<PuertaMaletera>();
    }

    /// <summary>Abre (o cierra) una puerta de la maletera: agarra el tirador y lo lleva en arco alrededor de la bisagra.</summary>
    IEnumerator MoverPuertaMaletera(PuertaMaletera p, bool izq, bool abrir)
    {
        Transform ag = p.agarre != null ? p.agarre : p.transform;
        Vector3 bisagra = p.transform.position;
        yield return Alcanzar(izq, () => ag.position, 0.7f);
        yield return Agarrar(izq);
        Vector3 rel = D.Ancla(izq).position - bisagra;
        var rot0 = D.Ancla(izq).rotation;
        float objetivo = abrir ? p.anguloAbierto * 0.85f : -p.anguloAbierto * 0.85f;
        float dur = 0.9f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float a = objetivo * Mathf.SmoothStep(0f, 1f, t / dur);
            Vector3 pos = bisagra + Quaternion.AngleAxis(a, Vector3.up) * rel;
            var r = Quaternion.AngleAxis(a, Vector3.up) * rot0;
            D.MoverMano(izq, () => new Pose(pos, r), 0f);
            yield return null;
        }
        yield return Esperar(0.15f);
        yield return Grip(izq, false);
        yield return Reposo(izq, 0.5f);
        Log("Maletera " + (abrir ? "abierta: " : "cerrada: ") + p.abierta);
    }

    IEnumerator Mision3()
    {
        Log("Mision 3");
        PalancaEnPSinAnimar();
        var j = Juego;
        var m3 = Mision3Controller.Instancia;
        yield return Esperar(3.5f); // lee el aviso: persona atrapada

        // 1) Porton principal (boton verde, en el patio, junto a donde empieza).
        var principal = Buscar<PortonPrincipal>();
        yield return Voltear(principal.PuntoBoton + Vector3.up * 0.3f, 1.2f);
        yield return Caminar(new Vector3(principal.PuntoBoton.x - 0.35f, 0f, principal.PuntoBoton.z - 0.62f));
        yield return Tocar(false, () => principal.PuntoBoton, Vector3.back, principal.radioBoton);
        yield return Voltear(principal.transform.position + Vector3.up * 1.2f, 1.4f);

        // 2) Entra a la estacion por la puerta chica y abre la cochera desde adentro.
        var bp = Buscar<BotonPorton>();
        Vector3 bRojo = bp.boton.position;
        yield return Caminar(new Vector3(-9.5f, 0f, 7.5f), new Vector3(-14.2f, 0f, 2.2f), new Vector3(-16.45f, 0f, 0.4f),
                             new Vector3(-16.45f, 0f, -4.4f), new Vector3(-11.5f, 0f, -2.6f), new Vector3(bRojo.x - 0.2f, 0f, bRojo.z - 0.66f));
        yield return Tocar(false, () => bp.boton.position, Vector3.back, 0.12f, () => Pokear(bp.transform));
        var pe = Buscar<PortonElevadizo>();
        yield return Voltear(pe.transform.position + Vector3.down * 2.8f, 1.8f);

        // 3) La barra Halligan: en su soporte rojo, al costado del camion.
        var herr = m3.Herramienta;
        var ext = (ExtintorCO2)Privado(m3, "_extintor");
        var cam = Camion;
        Vector3 derCam = Vector3.Cross(Vector3.up, Plano(cam.Frente).normalized);
        Vector3 ladoHerr = Vector3.Dot(herr.Centro - cam.transform.position, derCam) > 0f ? derCam : -derCam;
        Vector3 frenteHerr = herr.Centro + ladoHerr * 0.72f;
        Vector3 frenteCam = cam.transform.position + Plano(cam.Frente).normalized * 5.6f;
        yield return Caminar(new Vector3(frenteCam.x + ladoHerr.x * 2.2f, 0f, frenteCam.z + ladoHerr.z * 2.2f),
                             new Vector3(frenteHerr.x, 0f, frenteHerr.z));
        yield return Voltear(herr.Centro, 0.8f);
        yield return Alcanzar(false, () => herr.Centro, 0.8f);
        yield return Agarrar(false);
        yield return Esperar(0.3f);
        Log("M3 barra agarrada: " + herr.Agarrada);
        // La lleva en la mano, apuntando hacia abajo y adelante.
        D.MoverMano(false, () => { var f = Plano(Cabeza.forward).normalized; return new Pose(Hombro(false) + f * 0.2f + Vector3.down * 0.45f, Quaternion.LookRotation(f * 0.6f + Vector3.down * 0.8f, f)); }, 0.6f);

        // 4) Maletera (atras del camion): la abre con la izquierda y deja la barra adentro.
        var zona = FindObjectOfType<ZonaCargaHerramienta>();
        Vector3 atras = -Plano(cam.Frente).normalized;
        Vector3 frenteMaletera = zona.Punto + atras * 0.85f;
        Vector3 esquina = zona.Punto + atras * 1.6f + ladoHerr * 1.8f;
        yield return Caminar(new Vector3(esquina.x, 0f, esquina.z), new Vector3(frenteMaletera.x, 0f, frenteMaletera.z));
        yield return Voltear(zona.Punto + Vector3.up * 0.3f, 0.6f);
        var puertaM = PuertaMaleteraIzq();
        yield return MoverPuertaMaletera(puertaM, true, true);
        yield return Alcanzar(false, () => zona.Punto + Vector3.up * 0.15f, 0.8f);
        yield return Grip(false, false);
        yield return Esperar(0.5f);
        yield return Reposo(false);
        Log("M3 barra en maletera: " + herr.EnMaletera);

        // 5) El extintor CO2 (en el piso, junto al soporte): se agacha, lo toma y lo carga.
        Vector3 frenteExt = ext.Centro + ladoHerr * 0.62f;
        yield return Caminar(new Vector3(esquina.x, 0f, esquina.z), new Vector3(frenteExt.x, 0f, frenteExt.z));
        yield return Voltear(ext.Centro, 0.6f);
        yield return Agacharse(1.05f, 0.8f);
        yield return Alcanzar(false, () => ext.Centro + Vector3.up * 0.18f, 0.7f);
        yield return Agarrar(false);
        yield return Esperar(0.25f);
        Pararse();
        D.MoverMano(false, () => PoseEn(false, Cabeza.position + Plano(Cabeza.forward).normalized * 0.25f + Vector3.down * 0.8f, -10f), 0.7f);
        yield return Esperar(0.6f);
        yield return Caminar(new Vector3(esquina.x, 0f, esquina.z), new Vector3(frenteMaletera.x, 0f, frenteMaletera.z));
        yield return Voltear(zona.Punto + Vector3.up * 0.3f, 0.5f);
        yield return Alcanzar(false, () => zona.Punto + Vector3.up * 0.2f + derCam * 0.15f, 0.8f);
        yield return Grip(false, false);
        yield return Esperar(0.5f);
        yield return Reposo(false);
        Log("M3 extintor en maletera: " + ext.EnMaletera);
        // Cierra la maletera.
        yield return MoverPuertaMaletera(puertaM, true, false);

        // 6) Aborda el camion.
        Vector3 zonaSub = cam.PuntoZonaSubida;
        Vector3 ladoCond = Plano(zonaSub - cam.transform.position).normalized;
        yield return SubirAlCamion(zona.Punto + atras * 1.6f + ladoCond * 2.0f, zonaSub + ladoCond * 0.6f - Plano(cam.Frente).normalized * 1.2f);
        Log("M3 sentado: " + cam.JugadorSentado);
        yield return LeerTutorial();

        // 7) Conduce a la emergencia (sale y gira a la izquierda), estaciona en el circulo amarillo.
        yield return CambiarMarcha(3);
        yield return TomarVolante();
        yield return PulsarA(); // sirena
        yield return Conducir(m3.puntoEstacionar, 2.2f, 7.5f, m3.puertaCasa + Vector3.up * 1.5f, SalidaEstacion);
        yield return Estacionar();
        yield return Esperar(0.5f);
        yield return BajarDelCamion();
        Log("M3 llegada, camion en " + cam.transform.position);

        // 8) Saca el extintor y la barra de la maletera (uno en cada mano).
        zona = FindObjectOfType<ZonaCargaHerramienta>();
        atras = -Plano(cam.Frente).normalized;
        ladoCond = Plano(cam.PuntoZonaSubida - cam.transform.position).normalized;
        frenteMaletera = zona.Punto + atras * 0.85f;
        Vector3 rodeo = cam.PuntoZonaSubida + ladoCond * 0.9f - Plano(cam.Frente).normalized * 4.2f;
        yield return Caminar(new Vector3(rodeo.x, 0f, rodeo.z), zona.Punto + atras * 1.4f + ladoCond * 1.2f, new Vector3(frenteMaletera.x, 0f, frenteMaletera.z));
        yield return Voltear(zona.Punto + Vector3.up * 0.3f, 0.6f);
        puertaM = PuertaMaleteraIzq();
        yield return MoverPuertaMaletera(puertaM, true, true);
        yield return Alcanzar(true, () => ext.Centro + Vector3.up * 0.15f, 0.8f);
        yield return Agarrar(true);
        yield return Esperar(0.3f);
        D.MoverMano(true, () => PoseEn(true, Cabeza.position + Plano(Cabeza.forward).normalized * 0.22f + Vector3.down * 0.8f, -10f), 0.7f);
        yield return Alcanzar(false, () => herr.Centro, 0.8f);
        yield return Agarrar(false);
        yield return Esperar(0.3f);
        D.MoverMano(false, () => { var f = Plano(Cabeza.forward).normalized; return new Pose(Hombro(false) + f * 0.2f + Vector3.down * 0.45f, Quaternion.LookRotation(f * 0.6f + Vector3.down * 0.8f, f)); }, 0.6f);
        yield return Esperar(0.6f);
        Log("M3 equipo en mano: ext=" + ext.Agarrado + " barra=" + herr.Agarrada);

        // 9) Entra a la casa: se oyen gritos. Fuego en el pasillo y en la sala.
        Vector3 puerta = m3.puertaCasa;
        yield return Caminar(1.2f, zona.Punto + atras * 1.5f, new Vector3(puerta.x, 0f, puerta.z - 2.6f), new Vector3(puerta.x, 0f, puerta.z));
        yield return EsperarHasta(() => FindObjectOfType<EscenarioMision3>() != null, 6f, "interior M3");
        var esc = FindObjectOfType<EscenarioMision3>();
        Vector3 focoPasillo = esc.Incendio.transform.TransformPoint(esc.Incendio.focoPasillo);
        Vector3 focoSala = esc.Incendio.transform.TransformPoint(esc.Incendio.focoSala);
        yield return Caminar(1.0f, new Vector3(puerta.x, 0f, puerta.z + 3.0f), new Vector3(focoPasillo.x, 0f, focoPasillo.z - 2.5f));
        // Retira el pasador con X (la otra mano lleva la barra).
        Mirar(focoPasillo);
        yield return Esperar(0.6f);
        yield return PulsarX();
        yield return Esperar(0.6f);
        Log("M3 pasador: " + ext.PasadorRetirado);
        var inc = esc.Incendio;
        yield return DescargarCO2(ext, () => { var f = inc.FocoActivoMasCercano(Cabeza.position); return f != null ? f.Centro + Vector3.down * 0.2f : focoPasillo; },
            () => { var f = inc.FocoActivoMasCercano(Cabeza.position); return f == null || Plano(f.Centro - focoPasillo).magnitude > 1f; }, 20f);
        Log("M3 fuego pasillo, apagados " + inc.Apagados + "/" + inc.Total);
        // Sala: por el paso lateral del pasillo.
        Vector3 pasoSala = new Vector3(focoPasillo.x, 0f, 33.75f); // abertura entre el pasillo y la sala (plano de la casa)
        Vector3 dentroSala = new Vector3(focoSala.x - 1.8f, 0f, pasoSala.z);
        yield return Caminar(1.0f, pasoSala, dentroSala);
        yield return DescargarCO2(ext, () => focoSala + Vector3.down * 0.25f, () => inc.Apagado, 20f);
        Log("M3 incendio apagado: " + inc.Apagado);
        yield return Esperar(1.5f);

        // 10) Busca a la persona (gritos y golpes) y localiza la puerta bloqueada.
        var pa = esc.puerta;
        Vector3 frentePuerta = pa.PuntoPalanca + Plano(pa.transform.forward).normalized * 0.95f;
        yield return Caminar(1.0f, pasoSala, new Vector3(focoPasillo.x, 0f, focoPasillo.z), new Vector3(frentePuerta.x, 0f, frentePuerta.z));
        Mirar(() => pa.PuntoPalanca);
        yield return Esperar(1.5f);
        // Deja el extintor en el piso para tener una mano libre si hace falta.
        yield return Agacharse(1.15f, 0.6f);
        yield return Grip(true, false);
        Pararse();
        yield return Reposo(true, 0.5f);

        // 11) Fuerza la puerta con la barra: engancha la punta en el anillo y jala hacia si varias veces.
        Vector3 dirPuerta = -Plano(pa.transform.forward).normalized; // de la mano hacia la puerta
        Quaternion rotBarra = Quaternion.LookRotation((dirPuerta + Vector3.down * 0.15f).normalized, Vector3.up);
        Vector3 offPunta = Quaternion.Inverse(D.Ancla(false).rotation) * (herr.Punta - D.Ancla(false).position);
        D.MoverMano(false, () => new Pose(pa.PuntoPalanca - rotBarra * offPunta - dirPuerta * 0.12f, rotBarra), 0.8f);
        yield return Esperar(0.85f);
        offPunta = Quaternion.Inverse(D.Ancla(false).rotation) * (herr.Punta - D.Ancla(false).position);
        D.MoverMano(false, () => new Pose(pa.PuntoPalanca - rotBarra * offPunta, rotBarra), 0.4f);
        yield return EsperarHasta(() => pa.Enganchada, 2f, "barra enganchada");
        Log("M3 enganchada: " + pa.Enganchada);
        Vector3 manoEng = D.Ancla(false).position;
        var rotEng = D.Ancla(false).rotation;
        for (int k = 0; k < 6 && !pa.Forzada; k++)
        {
            // Jala (cuenta) y vuelve (no cuenta): como bombear la barra.
            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                Vector3 p = manoEng - dirPuerta * 0.28f * Mathf.SmoothStep(0f, 1f, t / 0.55f) + Vector3.down * 0.05f * Mathf.Sin(t / 0.55f * Mathf.PI);
                D.MoverMano(false, () => new Pose(p, rotEng), 0f);
                yield return null;
            }
            for (float t = 0f; t < 0.45f && !pa.Forzada; t += Time.deltaTime)
            {
                Vector3 p = manoEng - dirPuerta * 0.28f * (1f - Mathf.SmoothStep(0f, 1f, t / 0.45f));
                D.MoverMano(false, () => new Pose(p, rotEng), 0f);
                yield return null;
            }
        }
        Log("M3 puerta forzada: " + pa.Forzada);
        yield return Esperar(0.6f);
        // Suelta la barra y entra a ver a la persona.
        yield return Grip(false, false);
        yield return Reposo(false);
        var persona = esc.persona;
        Mirar(() => persona.transform.position + Vector3.up * 1.0f);
        yield return Esperar(1.5f);
        // Centro del vano de la puerta: entre la bisagra (pivote) y la cerradura (donde engancho la barra).
        Vector3 centroVano = Plano((pa.transform.position + pa.PuntoPalanca) * 0.5f);
        Vector3 vanoPasillo = centroVano - dirPuerta * 0.7f;
        Vector3 vanoCuarto = centroVano + dirPuerta * 0.8f;
        Vector3 haciaPersona = persona.transform.position;
        Vector3 cercaPersona = haciaPersona - Plano(haciaPersona - vanoCuarto).normalized * 1.2f;
        yield return Caminar(0.8f, vanoPasillo, vanoCuarto, new Vector3(cercaPersona.x, 0f, cercaPersona.z));
        Mirar(() => persona.transform.position + Vector3.up * 1.2f);
        yield return EsperarHasta(() => persona.EstadoActual == PersonaRescatadaNPC.Estado.Siguiendo, 10f, "persona se levanta");
        yield return Esperar(1f);

        // 12) La acompana hasta la zona segura de la calle, mirando atras de vez en cuando.
        Log("M3 acompanando");
        Vector3 zs = m3.zonaSegura;
        var camino = new Vector3[]
        {
            vanoCuarto, vanoPasillo, new Vector3(focoPasillo.x, 0f, focoPasillo.z - 1f),
            new Vector3(puerta.x, 0f, puerta.z + 1.5f), new Vector3(puerta.x, 0f, puerta.z - 1.2f),
            new Vector3(zs.x - 3.5f, 0f, zs.z - 2.2f), new Vector3(zs.x, 0f, zs.z - 0.5f)
        };
        for (int i = 0; i < camino.Length; i++)
        {
            Vector3 p = camino[i]; p.y = 0f;
            yield return Caminar(1.0f, p);
            // Mira atras para ver que la persona lo sigue (y la espera si se quedo atras).
            if (i >= 1)
            {
                Mirar(() => persona.transform.position + Vector3.up * 1.3f);
                yield return Esperar(1.3f);
            }
            float limiteEspera = Time.time + 8f;
            while (Plano(persona.transform.position - Cabeza.position).magnitude > 2.6f && Time.time < limiteEspera)
            {
                Mirar(() => persona.transform.position + Vector3.up * 1.3f);
                yield return null;
            }
        }
        // Sale de la zona para que ella llegue al centro.
        Vector3 fuera = new Vector3(zs.x + 3.2f, 0f, zs.z);
        yield return Caminar(0.9f, fuera);
        Mirar(() => persona.transform.position + Vector3.up * 1.3f);
        yield return EsperarHasta(() => m3.EstadoActual == Mision3Controller.Estado.Completada || j.MisionActiva == 0, 25f, "persona rescatada");
        Log("M3 estado: " + m3.EstadoActual);
        yield return Esperar(5f);
    }

    // =====================================================================
    // Parte 3: Explorar un poco (la estacion, la moto por la ciudad)
    // =====================================================================
    IEnumerator Parte3_Explorar()
    {
        yield return Menu(irAConfiguracion: false, opcion: 0);
        Log("Explorar");
        yield return Esperar(2.5f);
        // Mira la estacion y el patio.
        MirarLibre(YawHacia(new Vector3(-8f, 3f, -2f)) - 25f, 2f);
        yield return Esperar(2f);
        MirarLibre(YawHacia(new Vector3(-8f, 3f, -2f)) + 25f, 0f);
        yield return Esperar(2f);
        // Porton principal y a la moto.
        var principal = Buscar<PortonPrincipal>();
        yield return Voltear(principal.PuntoBoton + Vector3.up * 0.3f, 1.2f);
        yield return Caminar(new Vector3(principal.PuntoBoton.x - 0.35f, 0f, principal.PuntoBoton.z - 0.62f));
        yield return Tocar(false, () => principal.PuntoBoton, Vector3.back, principal.radioBoton);
        yield return Voltear(principal.transform.position + Vector3.up * 1.2f, 1.2f);
        yield return SubirAMoto();
        // Vuelta por toda la ciudad: avenida norte hacia el este, calle del extremo este hacia el sur,
        // avenida sur completa hacia el oeste, calle del extremo oeste hacia el norte y de vuelta a la estacion.
        Log("Explorar: vuelta por la ciudad");
        yield return ConducirMoto(new Vector3(88.5f, 0f, 21.5f), 6f, 9f, false, SalidaPatioMoto);
        yield return ConducirMoto(new Vector3(88.5f, 0f, -77.5f), 6f, 9f, false);
        yield return ConducirMoto(new Vector3(-100.5f, 0f, -77.5f), 6f, 9f, false);
        yield return ConducirMoto(new Vector3(-100.5f, 0f, 21.5f), 6f, 9f, false);
        yield return ConducirMoto(new Vector3(-12f, 0f, 21.5f), 4f, 8f, false);
        // Entra al patio por el porton principal y estaciona frente a la estacion.
        Log("Explorar: regreso a la estacion");
        yield return ConducirMoto(new Vector3(-8.5f, 0f, 8.5f), 1.2f, 3.5f, new Vector3(-5.1f, 0f, 17f), new Vector3(-5.6f, 0f, 12.5f));
        yield return BajarDeMoto();
        // Unos pasos hacia la estacion y la mira.
        yield return Caminar(1.0f, new Vector3(-9.5f, 0f, 5.5f));
        Mirar(new Vector3(-8f, 3.5f, 0f));
        yield return Esperar(3f);
    }
}
