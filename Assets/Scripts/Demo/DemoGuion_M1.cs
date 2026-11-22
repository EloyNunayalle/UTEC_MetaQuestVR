using System.Collections;
using UnityEngine;

public partial class DemoGuion
{
    // =====================================================================
    // Parte 0: menu (Configuracion -> Volante) y Mision 1 completa
    // =====================================================================
    IEnumerator Parte0_MenuYMision1()
    {
        // Demo desde cero: conduccion en joystick (se cambia en el menu) y el tutorial sin ver.
        PlayerPrefs.DeleteKey("UTEC.TutorialConduccion.v1.Volante");
        PlayerPrefs.SetInt("UTEC.ModoConduccion", (int)ModoConduccion.Joystick);
        ConfiguracionConduccion.Modo = ModoConduccion.Joystick;

        yield return Menu(irAConfiguracion: true, opcion: 1);
        yield return Mision1();
        yield return VolverAlSelector();
    }

    /// <summary>Selector de modo: opcion 0 = Explorar, 1..3 = Misiones (con el stick, aceptando con A).</summary>
    IEnumerator Menu(bool irAConfiguracion, int opcion)
    {
        Log("Menu (opcion " + opcion + ")");
        MirarAlFrente(4f);
        yield return Esperar(2.5f);
        // Manos al frente sosteniendo los controles, mirando el panel.
        D.MoverMano(true, () => PoseEn(true, Cabeza.position + Cabeza.forward * 0.36f + Cabeza.right * -0.14f + Vector3.down * 0.36f), 0.8f);
        D.MoverMano(false, () => PoseEn(false, Cabeza.position + Cabeza.forward * 0.36f + Cabeza.right * 0.14f + Vector3.down * 0.36f), 0.8f);
        MirarAlFrente(12f);
        yield return Esperar(2.2f);

        if (irAConfiguracion)
        {
            // CONFIGURACION: arriba desde EXPLORAR da la vuelta hasta la ultima opcion.
            var idx0 = typeof(JuegoBomberos).GetField("_indiceSel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int k = 0; k < 8 && (int)idx0.GetValue(Juego) != 4; k++)
            {
                yield return StickPulso(false, Vector2.up, 0.3f);
                yield return Esperar(0.6f);
            }
            yield return Esperar(0.8f);
            yield return PulsarA();
            yield return Esperar(1.6f);
            // VOLANTE (verifica que quede marcado antes de aceptar)
            var idx = typeof(JuegoBomberos).GetField("_indiceSel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int k = 0; k < 6 && (int)idx.GetValue(Juego) != 1; k++)
            {
                yield return StickPulso(false, (int)idx.GetValue(Juego) < 1 ? Vector2.down : Vector2.up, 0.3f);
                yield return Esperar(0.6f);
            }
            yield return Esperar(0.8f);
            yield return PulsarA();
            yield return Esperar(2.6f);
            // VOLVER (B tambien vuelve: se usa el boton, mas seguro que contar movimientos)
            Log("Conduccion elegida: " + ConfiguracionConduccion.Modo);
            yield return PulsarB();
            yield return Esperar(1.3f);
        }

        // Baja con el stick hasta la opcion, mirando cual queda marcada (y corrige si un movimiento no se tomo).
        var campoIndice = typeof(JuegoBomberos).GetField("_indiceSel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var campoConfig = typeof(JuegoBomberos).GetField("_enConfiguracion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        for (int intentos = 0; intentos < 12; intentos++)
        {
            int actual = (int)campoIndice.GetValue(Juego);
            bool enConfig = (bool)campoConfig.GetValue(Juego);
            if (enConfig) { yield return PulsarB(); yield return Esperar(1f); continue; }
            if (actual == opcion) break;
            yield return StickPulso(false, actual < opcion ? Vector2.down : Vector2.up, 0.3f);
            yield return Esperar(0.6f);
        }
        yield return Esperar(1.2f);
        Log("Menu: opcion marcada " + campoIndice.GetValue(Juego) + " (pedida " + opcion + ")");
        yield return PulsarA();
        yield return Esperar(0.8f);
        D.ManoAReposo(true, 0.7f);
        D.ManoAReposo(false, 0.7f);
        yield return Esperar(0.5f);
    }

    /// <summary>Despues de un resultado: B vuelve al selector (recarga la escena).</summary>
    IEnumerator VolverAlSelector()
    {
        Log("Volver al selector");
        var viejo = Juego;
        yield return Esperar(1.5f);
        yield return PulsarB();
        yield return EsperarHasta(() => Juego != null && Juego != viejo && D.rig != null, 20f, "recarga de escena");
        yield return Esperar(1.5f);
    }

    // ---------------------------------------------------------------------

    IEnumerator Mision1()
    {
        Log("Mision 1");
        PalancaEnPSinAnimar();
        var j = Juego;
        yield return Esperar(2.5f); // lee el aviso de emergencia

        // 1) Porton principal: boton verde del poste (detras, junto a la salida del patio).
        var principal = Buscar<PortonPrincipal>();
        Vector3 bVerde = principal.PuntoBoton;
        yield return Voltear(bVerde + Vector3.up * 0.3f, 1.4f);
        yield return Caminar(new Vector3(bVerde.x - 0.35f, 0f, bVerde.z - 0.62f));
        yield return Tocar(false, () => principal.PuntoBoton, Vector3.back, principal.radioBoton);
        yield return Voltear(principal.transform.position + Vector3.up * 1.2f, 2.2f); // ve abrirse el porton

        Log("M1 porton principal: " + principal.AbiertoOAbriendo);
        // 2) Porton de la cochera: el boton rojo esta ADENTRO de la estacion, en la pared del frente
        //    (entre los dos portones). Entra por la puerta chica y lo toca desde adentro.
        var bp = Buscar<BotonPorton>();
        Vector3 bRojo = bp.boton.position;
        yield return Caminar(new Vector3(-9.5f, 0f, 7.5f), new Vector3(-14.2f, 0f, 2.2f), new Vector3(-16.45f, 0f, 0.4f),
                             new Vector3(-16.45f, 0f, -4.4f), new Vector3(-11.5f, 0f, -2.6f), new Vector3(bRojo.x - 0.2f, 0f, bRojo.z - 0.66f));
        yield return Tocar(false, () => bp.boton.position, Vector3.back, 0.12f, () => Pokear(bp.transform));
        var pe = Buscar<PortonElevadizo>();
        yield return Voltear(pe.transform.position + Vector3.down * 2.8f, 3f); // ve subir el porton desde adentro

        Log("M1 cochera estado: " + pe.estado);
        // 3) Chaleco: en el estante del fondo, junto a los casilleros.
        var chaleco = j.chaleco;
        var agarrable = chaleco.GetComponent<ObjetoAgarrable>();
        Vector3 c = agarrable.Centro;
        yield return Caminar(new Vector3(-11f, 0f, -3.2f), new Vector3(-17.6f, 0f, -8.2f), new Vector3(c.x + 0.92f, 0f, c.z + 0.05f));
        yield return Voltear(c, 0.9f);
        yield return Alcanzar(false, () => agarrable.Centro, 0.8f);
        yield return Agarrar(false);
        yield return Esperar(0.3f);
        // Lo lleva al pecho y lo suelta.
        MirarLibre(YawHacia(c), 35f);
        D.MoverMano(false, () => PoseEn(false, Cabeza.position + Plano(Cabeza.forward).normalized * 0.22f + Vector3.down * 0.42f), 0.9f);
        yield return Esperar(1.1f);
        yield return Grip(false, false);
        yield return Esperar(0.6f);
        yield return Reposo(false);
        yield return Esperar(1.2f);

        Log("M1 chaleco completado: " + agarrable.Completado);
        // 4) Sube al camion (circulo junto a la puerta del conductor).
        var cam = Camion;
        Vector3 zona = cam.PuntoZonaSubida;
        yield return SubirAlCamion(new Vector3(-15f, 0f, -8.2f), new Vector3(zona.x - 2.2f, 0f, zona.z - 0.2f));
        Log("M1 sentado: " + cam.JugadorSentado);
        yield return LeerTutorial();

        // 5) Maneja hasta el edificio en llamas.
        yield return CambiarMarcha(3);
        yield return TomarVolante();
        yield return PulsarA(); // sirena
        // Estaciona dentro del recuadro amarillo "BOMBEROS", frente al edificio.
        yield return Conducir(j.calleFrenteEdificio + new Vector3(0f, 0f, -0.3f), 1.2f, 7.5f, j.posicionEdificioFuego + Vector3.up * 5f,
                              SalidaEstacion);
        yield return Estacionar();
        yield return Esperar(0.6f);
        yield return BajarDelCamion();

        Log("M1 bajado, camion en " + Camion.transform.position);
        // 6) Apaga el incendio con la manguera.
        yield return ApagarIncendioM1();
        yield return Esperar(4f);
    }

    // ---------------------------------------------------------------------

    /// <summary>Salida del camion: recto por la cochera y el porton principal hasta la calle, antes de girar.</summary>
    static readonly Vector3[] SalidaEstacion = { new Vector3(-5.15f, 0f, 2f), new Vector3(-5.1f, 0f, 14f), new Vector3(-5.1f, 0f, 22.5f) };

    IEnumerator ApagarIncendioM1()
    {
        Log("Incendio M1");
        var j = Juego;
        var cam = Camion;
        var mang = cam.GetComponent<MangueraBomberos>();
        var val = mang.Valvula;

        // Se aleja del circulo de subida (si no, el camion lo vuelve a subir) y va al panel de la bomba,
        // detras de la puerta del conductor: queda parado afuera, de frente al panel.
        Vector3 afuera = val != null ? Plano(val.transform.forward).normalized : Vector3.right;
        if (Vector3.Dot(afuera, Plano(cam.PuntoZonaSubida - cam.transform.position)) < 0f) afuera = -afuera;
        Vector3 panel = val != null ? val.transform.position : cam.PuntoZonaSubida;
        Vector3 pararse = panel + afuera * 0.78f + Plano(cam.Frente).normalized * 0.15f;
        yield return Caminar(1.0f, new Vector3(pararse.x, 0f, pararse.z));
        yield return Voltear(panel, 0.7f);
        yield return EsperarHasta(() => mang.Desplegada && mang.Boquilla != null, 5f, "manguera desplegada");
        if (!mang.Desplegada || mang.Boquilla == null) { Log("AVISO sin manguera"); yield break; }

        // a) Sube la presion de la bomba con la valvula (mano izquierda en circulo).
        if (val != null)
        {
            var rueda = (Transform)Privado(val, "_rueda");
            yield return Voltear(rueda.position, 0.8f);
            yield return Alcanzar(true, () => PoseValvula(val, rueda, 90f), 0.8f);
            yield return Grip(true, true);
            float ang = 90f;
            int vueltas = 0;
            float sentido = -1f;
            float antes = val.Presion;
            while (val.Presion < 0.97f && vueltas < 900)
            {
                ang += sentido * 220f * Time.deltaTime;
                float a = ang;
                D.MoverMano(true, () => PoseValvula(val, rueda, a), 0f);
                vueltas++;
                if (vueltas == 20 && val.Presion < antes - 0.001f) { sentido = -sentido; }
                yield return null;
            }
            yield return Esperar(0.3f);
            yield return Grip(true, false);
            yield return Reposo(true);
            Log("Presion " + val.PresionPorcentaje + "%");
        }

        // b) Toma la boquilla del soporte con la mano derecha.
        var boq = mang.Boquilla.transform;
        yield return Voltear(boq.position, 0.6f);
        yield return Alcanzar(false, () => boq.position, 0.8f);
        yield return Agarrar(false);
        yield return Esperar(0.4f);
        yield return EsperarHasta(() => mang.Agarrada, 1f, "boquilla agarrada");
        Log("Boquilla agarrada: " + mang.Agarrada);

        // c) Camina hacia el edificio (la manguera se desenrolla) y apunta a cada fuego.
        var edificio = FindObjectOfType<EdificioEnLlamas>();
        Vector3 frenteEdif = j.posicionEdificioFuego;
        // Columna por columna: se para frente a cada una (entre el camion y la fachada, dentro del
        // alcance de la manguera) y la apaga de abajo hacia arriba.
        Vector3 conexion = mang.puntoConexion != null ? mang.puntoConexion.position : panel;
        var columnas = new System.Collections.Generic.List<System.Collections.Generic.List<ZonaFuego>>();
        foreach (var zf in edificio.Zonas)
        {
            System.Collections.Generic.List<ZonaFuego> col = null;
            foreach (var cc in columnas)
                if (Plano(cc[0].Centro - zf.Centro).magnitude < 1.5f) { col = cc; break; }
            if (col == null) { col = new System.Collections.Generic.List<ZonaFuego>(); columnas.Add(col); }
            col.Add(zf);
        }
        columnas.Sort((a, b) => Plano(a[0].Centro - conexion).magnitude.CompareTo(Plano(b[0].Centro - conexion).magnitude));
        // Normal de la fachada (hacia la calle): perpendicular a la linea de columnas.
        Vector3 normalFachada = Plano(Cabeza.position - edificio.Zonas[0].Centro).normalized;
        if (columnas.Count >= 2)
        {
            Vector3 linea = Plano(columnas[columnas.Count - 1][0].Centro - columnas[0][0].Centro).normalized;
            Vector3 n = Vector3.Cross(Vector3.up, linea).normalized;
            normalFachada = Vector3.Dot(n, Plano(conexion - columnas[0][0].Centro)) >= 0f ? n : -n;
        }

        // Una sola posicion: al centro de la fachada, a ~6 m (alcance de la manguera, fuera del camion
        // y de su circulo de subida). Desde ahi apunta a cada fuego, uno por uno.
        Vector3 centroFocos = Vector3.zero;
        foreach (var zf in edificio.Zonas) centroFocos += zf.Centro;
        centroFocos /= Mathf.Max(1, edificio.Zonas.Count);
        Vector3 puesto = Plano(centroFocos) + normalFachada * 6.0f;
        if (Plano(puesto - conexion).magnitude > mang.longitud - 1.2f)
            puesto = conexion + Plano(puesto - conexion).normalized * (mang.longitud - 1.2f);
        puesto = FueraDelCamion(cam, puesto, 0.6f);
        yield return Caminar(0.9f, new Vector3(puesto.x, 0f, puesto.z));
        yield return Voltear(centroFocos, 1f);
        yield return Gatillo(false, 1f, 0.35f);

        float limite = Time.time + 220f;
        // Orden: de abajo hacia arriba, columna por columna desde la mas cercana.
        var orden = new System.Collections.Generic.List<ZonaFuego>();
        foreach (var col in columnas) { col.Sort((a, b) => a.Centro.y.CompareTo(b.Centro.y)); orden.AddRange(col); }
        foreach (var z in orden)
        {
            if (z.Apagada || Time.time > limite) continue;
            yield return MojarZona(z, mang, normalFachada);
            Log("Zona " + z.name + " apagada=" + z.Apagada + " (" + edificio.Apagadas + "/" + edificio.Total + ")");
        }
        // Si alguno se reavivo, lo repasa desde el mismo lugar.
        while (edificio != null && !edificio.TodoApagado && Time.time < limite)
        {
            var z = edificio.ZonaActivaMasCercana(Cabeza.position);
            if (z == null) break;
            yield return MojarZona(z, mang, normalFachada);
            Log("Repaso " + z.name + " apagada=" + z.Apagada + " (" + edificio.Apagadas + "/" + edificio.Total + ")");
        }
        yield return Gatillo(false, 0f, 0.3f);
        Mirar(frenteEdif + Vector3.up * 4f);
        yield return Esperar(2.5f);
        yield return Grip(false, false);
        yield return Reposo(false);
    }

    Pose PoseValvula(ValvulaBomba val, Transform rueda, float angGrados)
    {
        Vector3 eje = val.transform.forward;
        Vector3 u = Vector3.ProjectOnPlane(Vector3.up, eje).normalized;
        Vector3 w = Vector3.Cross(eje, u);
        float a = angGrados * Mathf.Deg2Rad;
        Vector3 p = rueda.position + (u * Mathf.Sin(a) + w * Mathf.Cos(a)) * 0.13f;
        // Hacia la rueda, del lado del jugador.
        Vector3 afuera = Vector3.Dot(eje, Cabeza.position - rueda.position) > 0f ? eje : -eje;
        return new Pose(p + afuera * 0.03f, Quaternion.LookRotation(-afuera, u));
    }

    Pose PoseApuntarDir(Vector3 dir)
    {
        var p = PoseApuntar(Cabeza.position + Cabeza.forward);
        return new Pose(p.position, Quaternion.LookRotation(dir.normalized, Vector3.up));
    }

    /// <summary>Control apuntando en una direccion (la boquilla sigue la rotacion de la mano).</summary>
    Pose PoseApuntar(Vector3 punto)
    {
        Vector3 f = Plano(Cabeza.forward).normalized;
        Vector3 der = Vector3.Cross(Vector3.up, f);
        // A la altura de la cadera/cintura y hacia la derecha, como se sostiene una boquilla.
        Vector3 mano = Hombro(false) + f * 0.32f + der * 0.08f + Vector3.down * 0.38f;
        Vector3 d = (punto - mano).normalized;
        return new Pose(mano, Quaternion.LookRotation(d, Vector3.up));
    }

    /// <summary>Apunta el chorro a una zona hasta apagarla, corrigiendo la elevacion segun el efecto (como una persona).</summary>
    IEnumerator MojarZona(ZonaFuego z, MangueraBomberos mang, Vector3 normalFachada)
    {
        var lanza = mang.Lanza;
        var ps = (ParticleSystem)Privado(lanza, "_ps");
        var boq = mang.Boquilla.transform;
        // Rotacion fija del emisor respecto de la boquilla (la boquilla copia la rotacion de la mano).
        Quaternion offEmisor = ps != null ? Quaternion.Inverse(boq.rotation) * ps.transform.rotation : Quaternion.identity;
        var particulas = new ParticleSystem.Particle[ps != null ? ps.main.maxParticles : 1];
        Vector3 n = normalFachada.normalized;

        Vector3 correccion = Vector3.zero;     // ajuste del punto apuntado segun donde cae el agua
        Quaternion rotMano = D.Ancla(false).rotation;
        float fin = Time.time + 40f;
        float vidaPrev = z.Vida, tSinAvance = 0f;
        Mirar(() => z.Centro);
        while (!z.Apagada && Time.time < fin)
        {
            float dt = Time.deltaTime;
            Vector3 objetivo = z.Centro + correccion;
            Vector3 origen = ps != null ? ps.transform.position : boq.position;
            float vel = ps != null ? ps.main.startSpeed.constant : 0f;
            if (vel < 1f) vel = Mathf.Lerp(lanza.velocidadMinima, lanza.velocidadMaxima, lanza.Presion);
            float g = 9.81f * (ps != null ? ps.main.gravityModifier.constant : lanza.gravedad);
            Vector3 dir = Balistica(origen, objetivo, vel, g);
            // La mano gira con calma hacia la direccion necesaria (pulso firme, sin barrer).
            Quaternion deseada = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Inverse(offEmisor);
            rotMano = Quaternion.Slerp(rotMano, deseada, 1f - Mathf.Exp(-dt * 4f));
            var pose = PoseApuntar(Cabeza.position + Cabeza.forward);
            Quaternion rm = rotMano; Vector3 pm = pose.position;
            D.MoverMano(false, () => new Pose(pm, rm), 0f);

            // Donde cruza el chorro el plano de la fachada (particulas reales, aunque pasen de largo).
            if (ps != null)
            {
                int k = ps.GetParticles(particulas);
                Vector3 suma = Vector3.zero; int cuenta = 0;
                for (int i = 0; i < k; i++)
                {
                    Vector3 p = particulas[i].position;
                    float d = Vector3.Dot(p - z.Centro, n);
                    if (Mathf.Abs(d) > 0.45f) continue;
                    if ((p - z.Centro).sqrMagnitude > 6.25f) continue;
                    suma += p; cuenta++;
                }
                if (cuenta >= 4)
                {
                    Vector3 cruce = suma / cuenta;
                    Vector3 err = z.Centro - cruce;
                    err -= n * Vector3.Dot(err, n);
                    correccion += err * Mathf.Clamp01(dt * 1.5f);
                    correccion = Vector3.ClampMagnitude(correccion, 2.5f);
                }
            }
            if (z.Vida < vidaPrev - 0.0005f) tSinAvance = 0f; else tSinAvance += dt;
            vidaPrev = z.Vida;
            if (tSinAvance > 5f) { tSinAvance = 0f; correccion = Vector3.zero; Log("Reapunta " + z.name); }
            yield return null;
        }
        yield return Esperar(0.4f);
    }

    /// <summary>Direccion de lanzamiento (tiro bajo) para llegar a 'obj' con velocidad v y gravedad g.</summary>
    static Vector3 Balistica(Vector3 o, Vector3 obj, float v, float g)
    {
        Vector3 d = obj - o;
        Vector3 h = Plano(d);
        float x = h.magnitude, y = d.y;
        float v2 = v * v;
        float raiz = v2 * v2 - g * (g * x * x + 2f * y * v2);
        float ang = raiz < 0f ? 45f * Mathf.Deg2Rad : Mathf.Atan((v2 - Mathf.Sqrt(raiz)) / (g * x));
        return (h.normalized * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)).normalized;
    }
}
