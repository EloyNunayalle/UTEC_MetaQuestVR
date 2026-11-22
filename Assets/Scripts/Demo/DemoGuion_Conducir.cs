using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Conduccion del camion en modo VOLANTE, como una persona: agarra el volante con las dos manos
/// (GRIP), lo gira moviendo las manos por el aro, acelera con el gatillo derecho, frena con el
/// izquierdo y cambia la marcha llevando la palanca con la mano derecha.
/// </summary>
public partial class DemoGuion
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    CamionBomberos Camion => Buscar<CamionBomberos>();

    // ---------------- Volante ----------------

    VolanteInteractivo _vol;
    float _volCmd;                 // angulo pedido al volante (grados, + = derecha)
    float _volAlAgarrar;           // angulo del volante al agarrarlo
    float _angIzq0, _angDer0;      // angulo de cada mano en el aro al agarrarlo (marco fijo del volante)
    bool _manosEnVolante;

    Vector3 VolCentro() => (Vector3)typeof(VolanteInteractivo).GetMethod("CentroMundo", Priv).Invoke(_vol, null);
    Quaternion VolBase() => (Quaternion)typeof(VolanteInteractivo).GetMethod("BaseMundoRot", Priv).Invoke(_vol, null);
    float VolRadioMundo() => _vol.radio * (float)Privado(_vol, "_escalaMundo");
    float VolAngulo() => (float)Privado(_vol, "_angulo");
    bool VolAgarrado(int i) => ((bool[])Privado(_vol, "_agarrada"))[i];

    /// <summary>Normal del plano del aro que apunta lejos del conductor (hacia el parabrisas).</summary>
    Vector3 VolNormalAfuera()
    {
        Vector3 n = VolBase() * Vector3.forward;
        Vector3 haciaVol = VolCentro() - Cabeza.position;
        return Vector3.Dot(n, haciaVol) >= 0f ? n : -n;
    }

    Vector3 PuntoAro(float angGrados, float extra = 0f)
    {
        float a = angGrados * Mathf.Deg2Rad;
        var b = VolBase();
        float r = VolRadioMundo() + extra;
        return VolCentro() + b * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
    }

    Pose PoseManoVolante(bool izq, float ang)
    {
        var b = VolBase();
        float a = ang * Mathf.Deg2Rad;
        Vector3 radial = b * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        Vector3 tangente = b * new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
        // "Hacia arriba por el aro": la tangente que sube (hacia las 12).
        Vector3 arriba = b * Vector3.up;
        if (Vector3.Dot(tangente, arriba) < 0f) tangente = -tangente;
        Vector3 fwd = (tangente * 0.75f + VolNormalAfuera() * 0.65f).normalized;
        Vector3 up = Vector3.ProjectOnPlane(radial, fwd).normalized;
        // Pomo de la mano sobre el aro (el ancla del control queda apenas por fuera del aro).
        Vector3 p = PuntoAro(ang, 0.012f) - VolNormalAfuera() * 0.015f;
        return new Pose(p, Quaternion.LookRotation(fwd, up));
    }

    /// <summary>Angulo del aro que queda a la izquierda / derecha del conductor ("10 y 2").</summary>
    void CalcularAngulosBase()
    {
        var c = Camion;
        Vector3 derechaCamion = Vector3.Cross(Vector3.up, c.Frente).normalized;
        Vector3 derBase = VolBase() * Vector3.right;
        bool mismo = Vector3.Dot(derBase, derechaCamion) >= 0f;
        // Con +X del aro hacia la derecha: izquierda = 180-25, derecha = 25 (manos un poco arriba).
        _angIzq0 = mismo ? 155f : 25f;
        _angDer0 = mismo ? 25f : 155f;
    }

    /// <summary>Signo que relaciona girar las manos (angulo en el aro) con el angulo del volante.</summary>
    float _signoVolante = 1f;

    IEnumerator TomarVolante()
    {
        _vol = Buscar<VolanteInteractivo>();
        if (_vol == null) { Log("AVISO No hay volante"); yield break; }
        CalcularAngulosBase();
        _volCmd = VolAngulo();
        _volAlAgarrar = _volCmd;
        Mirar(() => VolCentro() + Vector3.up * 0.05f);
        D.MoverMano(true, () => PoseManoVolante(true, _angIzq0), 0.75f);
        yield return Esperar(0.25f);
        D.MoverMano(false, () => PoseManoVolante(false, _angDer0), 0.75f);
        yield return Esperar(0.6f);
        yield return Grip(true, true);
        yield return Esperar(0.15f);
        yield return Grip(false, true);
        yield return Esperar(0.2f);
        if (!VolAgarrado(0) || !VolAgarrado(1))
            Log("AVISO El volante no quedo agarrado: izq=" + VolAgarrado(0) + " der=" + VolAgarrado(1));
        _manosEnVolante = true;
        // Desde ahora las manos siguen el giro pedido.
        D.MoverMano(true, () => PoseManoVolante(true, _angIzq0 + _signoVolante * (_volCmd - _volAlAgarrar)), 0f);
        D.MoverMano(false, () => PoseManoVolante(false, _angDer0 + _signoVolante * (_volCmd - _volAlAgarrar)), 0f);
        // Comprueba el sentido: un pequeno giro de prueba.
        float antes = VolAngulo();
        _volCmd += 6f;
        yield return Esperar(0.25f);
        float despues = VolAngulo();
        if (despues < antes - 1f)
        {
            _signoVolante = -1f;
            Debug.Log("[DemoGuion] Sentido del volante invertido");
        }
        _volCmd -= 6f;
        yield return Esperar(0.2f);
        MirarAlFrente(8f);
    }

    IEnumerator SoltarVolante(bool izq = true, bool der = true)
    {
        _manosEnVolante = false;
        if (izq) yield return Grip(true, false);
        if (der) yield return Grip(false, false);
        if (izq) D.ManoAReposo(true, 0.6f);
        if (der) D.ManoAReposo(false, 0.6f);
        yield return Esperar(0.6f);
    }

    // ---------------- Palanca ----------------

    PalancaCambios Palanca => Buscar<PalancaCambios>();

    int RanuraDeMarcha(PalancaCambios p, int marcha)
    {
        for (int i = 0; i < p.posicionesRanuras.Length; i++)
        {
            int m = p.marchaDeRanura == null || p.marchaDeRanura.Length == 0 ? Mathf.Clamp(i, 0, 3) : p.marchaDeRanura[Mathf.Clamp(i, 0, p.marchaDeRanura.Length - 1)];
            if (m == marcha) return i;
        }
        return -1;
    }

    Vector3 PomoEnRanura(PalancaCambios p, int ranura)
    {
        float desp = (float)Privado(p, "_desplazamiento");
        Vector3 delta = p.transform.parent.TransformVector(p.ejeLocal * (p.posicionesRanuras[ranura] - desp));
        return p.PuntoPomo + delta;
    }

    /// <summary>Deja la palanca en P sin tocarla (camion estacionado al empezar la mision).</summary>
    protected void PalancaEnPSinAnimar()
    {
        var p = Palanca;
        if (p == null) return;
        int r = RanuraDeMarcha(p, 0);
        if (r < 0) return;
        p.marcha = 0;
        typeof(PalancaCambios).GetField("_destinoRanura", Priv).SetValue(p, r);
        typeof(PalancaCambios).GetField("_desplazamiento", Priv).SetValue(p, p.posicionesRanuras[r]);
        typeof(PalancaCambios).GetField("_asentando", Priv).SetValue(p, false);
    }

    /// <summary>Lleva la palanca a una marcha con la mano derecha (0=P, 1=R, 2=N, 3=D).</summary>
    IEnumerator CambiarMarcha(int marcha)
    {
        var p = Palanca;
        if (p == null) { Log("AVISO No hay palanca"); yield break; }
        int ranura = RanuraDeMarcha(p, marcha);
        if (ranura < 0 || p.marcha == marcha) yield break;
        bool estabaEnVolante = _manosEnVolante;
        if (estabaEnVolante)
        {
            // La derecha deja el volante; la izquierda lo sigue sosteniendo.
            yield return Grip(false, false);
        }
        Mirar(() => p.PuntoPomo);
        Vector3 inicio = p.PuntoPomo;
        Vector3 arriba = Vector3.up;
        // Mano sobre el pomo, desde arriba.
        D.MoverMano(false, () => new Pose(p.PuntoPomo + Vector3.up * 0.015f,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(Camion.Frente, Vector3.up).normalized * 0.6f + Vector3.down * 0.8f, Camion.Frente)), 0.7f);
        yield return Esperar(0.75f);
        yield return Grip(false, true);
        yield return Esperar(0.15f);
        // Desliza hasta la ranura (la palanca sigue la proyeccion de la mano sobre la guia).
        Vector3 desde = D.Ancla(false).position;
        Vector3 hasta = desde + (PomoEnRanura(p, ranura) - p.PuntoPomo);
        var rotMano = D.Ancla(false).rotation;
        var camT = Camion.transform;
        Vector3 desdeL = camT.InverseTransformPoint(desde), hastaL = camT.InverseTransformPoint(hasta);
        float dur = 0.55f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, t / dur);
            Vector3 pl = Vector3.Lerp(desdeL, hastaL, e);
            D.MoverMano(false, () => new Pose(camT.TransformPoint(pl), rotMano), 0f);
            yield return null;
        }
        D.MoverMano(false, () => new Pose(camT.TransformPoint(hastaL), rotMano), 0f);
        yield return Esperar(0.25f);
        yield return Grip(false, false);
        yield return Esperar(0.3f);
        Log("Marcha: " + p.NombreMarcha);
        if (estabaEnVolante)
        {
            D.MoverMano(false, () => PoseManoVolante(false, _angDer0 + _signoVolante * (_volCmd - _volAlAgarrar)), 0.6f);
            yield return Esperar(0.6f);
            // Vuelve a agarrar: el volante recuerda el angulo de la mano desde ahora.
            yield return Grip(false, true);
            // Al re-agarrar con la derecha, los angulos de ambas manos deben seguir el mismo comando.
        }
        else D.ManoAReposo(false, 0.6f);
        MirarAlFrente(8f);
    }

    // ---------------- Subir / bajar ----------------

    IEnumerator SubirAlCamion(params Vector3[] camino)
    {
        var c = Camion;
        var lista = new List<Vector3>(camino) { c.PuntoZonaSubida };
        _caminoParaSubir = true;
        _esperoSentado = true;
        yield return Caminar(1.3f, lista.ToArray());
        _caminoParaSubir = false;
        Mirar(() => c.PuntoZonaSubida + c.Frente * 2f + Vector3.up * 1.4f);
        yield return EsperarHasta(() => c.JugadorSentado, 6f, "subir al camion");
        MirarAlFrente(6f);
        yield return Esperar(1f);
    }

    /// <summary>Lee el tutorial de conduccion (si aparece) y lo pasa con el clic del joystick.</summary>
    IEnumerator LeerTutorial()
    {
        yield return Esperar(0.6f);
        int paginas = 0;
        while (TutorialConduccion.Activo && paginas < 8)
        {
            MirarAlFrente(10f);
            yield return Esperar(paginas == 0 ? 3.2f : 2.4f);
            yield return ClicStick(false);
            paginas++;
        }
        yield return Esperar(0.5f);
    }

    IEnumerator BajarDelCamion()
    {
        var c = Camion;
        D.B = true;
        yield return EsperarHasta(() => !c.JugadorSentado, 3f, "bajar");
        _esperoSentado = false;
        D.B = false;
        yield return Esperar(0.8f);
    }

    // ---------------- Conducir ----------------

    /// <summary>
    /// Conduce siguiendo la ruta del GPS hasta 'destino' (pure pursuit), girando el volante con las
    /// manos y dosificando acelerador y freno. Se detiene a 'radio' metros del destino.
    /// </summary>
    IEnumerator Conducir(Vector3 destino, float radio, float vMax = 7f, Vector3? mirarAntesDeLlegar = null, params Vector3[] previos)
    {
        var c = Camion;
        var ruta = new List<Vector3>();
        if (!MapaGPS.Listo) MapaGPS.IntentarConstruir();
        Vector3 desde = previos.Length > 0 ? previos[previos.Length - 1] : c.transform.position;
        if (!MapaGPS.Ruta(desde, destino, ruta))
        {
            Log("AVISO Sin ruta a " + destino);
            ruta.Clear(); ruta.Add(desde); ruta.Add(destino);
        }
        var pre = new List<Vector3> { c.transform.position };
        pre.AddRange(previos);
        if (previos.Length > 0) ruta.RemoveAt(0);
        pre.AddRange(ruta);
        ruta = pre;
        Densificar(ruta, 0.75f);
        Log("Conduciendo a " + destino + " (" + ruta.Count + " puntos)");

        var centroBase = typeof(CamionBomberos).GetProperty("CentroBase", Priv);
        int idx = 0;
        float tAtascado = 0f, enderezarHasta = 0f;
        int atascos = 0;
        float inicio = Time.time;
        while (true)
        {
            float dt = Time.deltaTime;
            Vector3 frente = c.Frente; frente.y = 0f; frente.Normalize();
            Vector3 pos = (Vector3)centroBase.GetValue(c); // el camion gira sobre este punto
            float v = c.Velocidad;

            Vector3 aDestino = destino - pos; aDestino.y = 0f;
            float distFin = aDestino.magnitude;
            if (distFin <= radio && Mathf.Abs(v) < 0.3f) break;
            if (Time.time - inicio > 300f) { Log("AVISO Conduccion demasiado larga"); break; }

            // Punto de la ruta mas cercano hacia adelante.
            for (int k = idx; k < Mathf.Min(ruta.Count, idx + 30); k++)
                if (Plano(ruta[k] - pos).sqrMagnitude < Plano(ruta[idx] - pos).sqrMagnitude) idx = k;

            float L = Mathf.Clamp(3.2f + Mathf.Abs(v) * 0.8f, 3.2f, 11f);
            Vector3 objetivo = PuntoAdelante(ruta, idx, pos, L);
            Vector3 aObj = Plano(objetivo - pos);
            float alfa = Vector3.SignedAngle(frente, aObj, Vector3.up) * Mathf.Deg2Rad;
            float Ld = Mathf.Max(2f, aObj.magnitude);
            // Velocidad angular deseada (grados/s) y eje de giro necesario.
            float vAbs = Mathf.Max(Mathf.Abs(v), 0.5f);
            float yaw = 2f * vAbs * Mathf.Sin(alfa) / Ld * Mathf.Rad2Deg;
            float factor = Mathf.Clamp(v / 3f, 0.15f, 1f);
            float giro = Mathf.Clamp(yaw / (c.giroMaximo * factor), -1f, 1f);
            float angDeseado = Time.time < enderezarHasta ? 0f : giro * _vol.gradosMaximos;
            // Las manos giran el volante a una velocidad humana.
            _volCmd = Mathf.MoveTowards(_volCmd, angDeseado, 170f * dt);

            // Curvatura de lo que viene: frena antes de las curvas.
            float curva = CurvaAdelante(ruta, idx, 14f + Mathf.Abs(v));
            float vObj = Mathf.Lerp(vMax, 2.8f, Mathf.InverseLerp(8f, 60f, curva));
            if (Mathf.Abs(alfa) * Mathf.Rad2Deg > 25f) vObj = Mathf.Min(vObj, 3f);
            if (idx < previos.Length * 8) vObj = Mathf.Min(vObj, 3.2f); // saliendo de la estacion, despacio
            // Al llegar: frena suave para detenerse en el radio.
            float dParar = Mathf.Max(0f, distFin - radio * 0.6f);
            vObj = Mathf.Min(vObj, Mathf.Sqrt(2f * 2.0f * dParar));
            if (distFin <= radio) vObj = 0f;

            float err = vObj - v;
            float acel = 0f, freno = 0f;
            if (err > 0.1f) acel = Mathf.Clamp01(0.3f + err * 0.35f);
            else if (err < -0.6f || vObj < 0.05f) freno = Mathf.Clamp01(-err * 0.35f + (vObj < 0.05f ? 0.4f : 0f));
            D.gatilloR = Mathf.MoveTowards(D.gatilloR, acel, dt * 2.5f);
            D.gatilloL = Mathf.MoveTowards(D.gatilloL, freno, dt * 3f);

            // Mirada: hacia el punto de la ruta (un conductor mira la curva), sin girar de mas.
            Vector3 mira = PuntoAdelante(ruta, idx, pos, 12f + Mathf.Abs(v));
            if (mirarAntesDeLlegar.HasValue && distFin < 22f) mira = mirarAntesDeLlegar.Value;
            Vector3 ojo = Cabeza.position;
            Vector3 mirarP = LimitarMirada(new Vector3(mira.x, ojo.y - 0.6f, mira.z), frente, 38f);
            D.mirarA = () => mirarP;
            if (MirandoGPS(c.transform, curva, alfa * Mathf.Rad2Deg, distFin)) { var g = _pantallaGPS; D.mirarA = () => g.position; }

            // Atascado (choque): primero endereza; si sigue, retrocede un poco con la R.
            if (acel > 0.25f && Mathf.Abs(v) < 0.2f) tAtascado += dt; else tAtascado = 0f;
            if (tAtascado > 0.8f)
            {
                tAtascado = 0f;
                atascos++;
                Log("AVISO Camion atascado en " + pos + " (intento " + atascos + ")");
                if (atascos % 2 == 1) enderezarHasta = Time.time + 2.5f;
                else
                {
                    D.gatilloR = 0f;
                    yield return Retroceder(2.5f);
                    enderezarHasta = 0f;
                }
            }
            yield return null;
        }
        D.gatilloR = 0f;
        yield return Gatillo(true, 0.8f, 0.3f);
        yield return Esperar(0.6f);
        yield return Gatillo(true, 0f, 0.3f);
        Log("Llegada a " + destino + " en " + (Time.time - inicio).ToString("F0") + " s");
    }

    /// <summary>Retrocede en linea recta unos metros: frena, R con la mano, acelera y vuelve a D.</summary>
    IEnumerator Retroceder(float metros)
    {
        var c = Camion;
        yield return Gatillo(true, 1f, 0.2f);
        yield return Esperar(0.3f);
        yield return CambiarMarcha(1);
        yield return Gatillo(true, 0f, 0.2f);
        _volCmd = 0f;
        Vector3 ini = c.transform.position;
        float fin = Time.time + 6f;
        while (Plano(c.transform.position - ini).magnitude < metros && Time.time < fin)
        {
            D.gatilloR = Mathf.MoveTowards(D.gatilloR, Mathf.Abs(c.Velocidad) < 1.2f ? 0.6f : 0.2f, Time.deltaTime * 2f);
            yield return null;
        }
        D.gatilloR = 0f;
        yield return Gatillo(true, 1f, 0.2f);
        yield return Esperar(0.4f);
        yield return CambiarMarcha(3);
        yield return Gatillo(true, 0f, 0.2f);
    }

    // ---------------- Mirar el GPS de vez en cuando ----------------

    Transform _pantallaGPS;
    float _proxGPS, _finGPS;

    /// <summary>
    /// True mientras el conductor echa un vistazo a la pantalla del GPS del vehiculo. Lo hace cada
    /// 7-11 s, solo en tramos rectos y lejos de la llegada (como alguien que va siguiendo la ruta).
    /// </summary>
    bool MirandoGPS(Transform vehiculo, float curva, float alfaGrados, float distFin)
    {
        if (_pantallaGPS == null || !_pantallaGPS.IsChildOf(vehiculo))
        {
            _pantallaGPS = null;
            foreach (var p in FindObjectsOfType<PantallaGPS>())
                if (p.vehiculo == vehiculo || p.transform.IsChildOf(vehiculo)) { _pantallaGPS = p.transform; break; }
            _proxGPS = Time.time + 3f;
            if (_pantallaGPS == null) return false;
        }
        if (Time.time < _finGPS) return true;
        if (Time.time >= _proxGPS && curva < 20f && Mathf.Abs(alfaGrados) < 12f && distFin > 25f)
        {
            _finGPS = Time.time + Random.Range(1.1f, 1.6f);
            _proxGPS = _finGPS + Random.Range(7f, 11f);
            return true;
        }
        return false;
    }


    static void Densificar(List<Vector3> r, float paso)
    {
        var n = new List<Vector3>();
        for (int i = 0; i < r.Count - 1; i++)
        {
            Vector3 a = r[i], b = r[i + 1];
            int k = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / paso));
            for (int j = 0; j < k; j++) n.Add(Vector3.Lerp(a, b, j / (float)k));
        }
        n.Add(r[r.Count - 1]);
        r.Clear(); r.AddRange(n);
    }

    static Vector3 PuntoAdelante(List<Vector3> r, int idx, Vector3 pos, float L)
    {
        for (int k = idx; k < r.Count; k++)
            if (Plano(r[k] - pos).magnitude >= L) return r[k];
        return r[r.Count - 1];
    }

    static float CurvaAdelante(List<Vector3> r, int idx, float largo)
    {
        if (idx >= r.Count - 2) return 0f;
        Vector3 d0 = Plano(r[Mathf.Min(idx + 2, r.Count - 1)] - r[idx]);
        float acum = 0f, maxAng = 0f;
        for (int k = idx + 1; k < r.Count - 1 && acum < largo; k++)
        {
            acum += Vector3.Distance(r[k], r[k - 1]);
            Vector3 d = Plano(r[Mathf.Min(k + 2, r.Count - 1)] - r[k]);
            if (d.sqrMagnitude < 1e-4f) continue;
            maxAng = Mathf.Max(maxAng, Vector3.Angle(d0, d));
        }
        return maxAng;
    }

    /// <summary>Estaciona: frena del todo, pone P con la mano y suelta el volante.</summary>
    IEnumerator Estacionar()
    {
        yield return Gatillo(true, 1f, 0.2f);
        yield return Esperar(0.5f);
        yield return CambiarMarcha(0);
        yield return Gatillo(true, 0f, 0.2f);
        yield return SoltarVolante();
    }

    // ---------------- Capturas de prueba (solo sin grabar) ----------------

    public static string CarpetaCapturas => System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Temp", "capturas");
    float _proxCaptura;
    int _nCaptura;
    RenderTexture _rtCaptura;
    Texture2D _texCaptura;

    bool _esperoSentado, _avisoSentado;

    void LateUpdate()
    {
        // Vigia: la persona no debe quedar sentada en el camion sin que el guion lo pida.
        var camV = D != null && D.rig != null ? FindObjectOfType<CamionBomberos>() : null;
        bool sentado = camV != null && camV.JugadorSentado;
        if (sentado && !_esperoSentado && !_avisoSentado) { _avisoSentado = true; Log("AVISO SUBIDO AL CAMION SIN QUERER en " + Cabeza.position); }
        if (!sentado) _avisoSentado = false;

        if (grabando || D == null || D.rig == null || Time.time < _proxCaptura) return;
        _proxCaptura = Time.time + 1.5f;
        if (_rtCaptura == null)
        {
            System.IO.Directory.CreateDirectory(CarpetaCapturas);
            _rtCaptura = new RenderTexture(640, 360, 24);
            _texCaptura = new Texture2D(640, 360, TextureFormat.RGB24, false);
        }
        var camaras = new List<Camera>();
        foreach (var cm in Camera.allCameras)
            if (cm.targetTexture == null && cm.enabled) camaras.Add(cm);
        camaras.Sort((a, b) => a.depth.CompareTo(b.depth));
        foreach (var cm in camaras)
        {
            var rt = cm.targetTexture;
            cm.targetTexture = _rtCaptura;
            cm.Render();
            cm.targetTexture = rt;
        }
        var prev = RenderTexture.active;
        RenderTexture.active = _rtCaptura;
        _texCaptura.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
        RenderTexture.active = prev;
        string paso = Paso.Length > 40 ? Paso.Substring(0, 40) : Paso;
        foreach (char ch in System.IO.Path.GetInvalidFileNameChars()) paso = paso.Replace(ch, '_');
        string nombre = string.Format("{0:0000}_t{1:000}_{2}.jpg", _nCaptura++, Time.time, paso.Replace(' ', '_'));
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(CarpetaCapturas, nombre), _texCaptura.EncodeToJPG(70));
    }
}
