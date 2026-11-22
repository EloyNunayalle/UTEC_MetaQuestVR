using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guion de la demo: lo que haria una persona con la Quest puesta. Usa solo la persona simulada
/// (DemoVR): mira, camina, mueve las manos, agarra con GRIP, aprieta gatillos y botones.
/// Partes: 0 = menu + Mision 1, 1 = Mision 2, 2 = Mision 3, 3 = Explorar.
/// </summary>
public partial class DemoGuion : MonoBehaviour
{
    public static bool Terminado;
    public int desde;
    public bool grabando;

    DemoVR D => DemoVR.I;
    OVRCameraRig Rig => D.rig;
    Transform Cabeza => D.Cabeza;
    JuegoBomberos Juego => JuegoBomberos.Instancia;

    public static string Paso = "";
    public static string ArchivoLog => System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Temp", "demo_paso.txt");

    IEnumerator Start()
    {
        Terminado = false;
        while (D.rig == null || Juego == null) yield return null;
        yield return null;

        if (desde != 0) ConfiguracionConduccion.Modo = ModoConduccion.Volante;

        if (desde < 0)
        {
            yield return Prueba();
            Log("FIN PRUEBA");
            yield break; // la prueba no cierra Play: se inspecciona a mano
        }

        if (desde <= 0) yield return Parte0_MenuYMision1();
        if (desde <= 1) yield return Parte1_Mision2();
        if (desde <= 2) yield return Parte2_Mision3();
        if (desde <= 3) yield return Parte3_Explorar();

        Log("FIN");
        yield return Esperar(1f);
        Terminado = true;
    }

    // =====================================================================
    // Acciones basicas
    // =====================================================================

    protected IEnumerator Esperar(float s)
    {
        float fin = Time.time + s;
        while (Time.time < fin) yield return null;
    }

    protected IEnumerator EsperarHasta(Func<bool> cond, float maximo, string que)
    {
        float fin = Time.time + maximo;
        while (!cond())
        {
            if (Time.time > fin) { Log("AVISO tiempo agotado esperando: " + que); yield break; }
            yield return null;
        }
    }

    protected void Mirar(Vector3 p) => D.mirarA = () => p;
    protected void Mirar(Func<Vector3> p) => D.mirarA = p;
    protected void Mirar(Transform t) => D.mirarA = () => t != null ? t.position : Cabeza.position + Cabeza.forward;
    protected void MirarLibre(float yawLocal, float pitch = 6f) { D.mirarA = null; D.yawLibre = yawLocal; D.pitchLibre = pitch; }

    /// <summary>Mira al frente segun el rig (por ejemplo, sentado en el camion).</summary>
    protected void MirarAlFrente(float pitch = 6f)
    {
        // En un vehiculo, "al frente" es hacia donde apunta el vehiculo (no el rig).
        var cam = FindObjectOfType<CamionBomberos>();
        var moto = FindObjectOfType<MotoBomberos>();
        Transform veh = cam != null && cam.JugadorSentado ? cam.transform : moto != null && moto.Montado ? moto.transform : null;
        if (veh == null) { MirarLibre(0f, pitch); return; }
        float tan = Mathf.Tan(pitch * Mathf.Deg2Rad);
        D.mirarA = () =>
        {
            Vector3 f = veh == cam?.transform ? cam.Frente : veh.forward;
            f.y = 0f; f.Normalize();
            return Cabeza.position + f * 10f + Vector3.down * (10f * tan);
        };
    }

    /// <summary>Limita un punto de mirada a +-maxGrados del frente del vehiculo (no gira la cabeza de mas al manejar).</summary>
    protected Vector3 LimitarMirada(Vector3 punto, Vector3 frente, float maxGrados)
    {
        Vector3 ojo = Cabeza.position;
        Vector3 d = punto - ojo;
        Vector3 h = new Vector3(d.x, 0f, d.z);
        frente.y = 0f;
        if (h.sqrMagnitude < 1e-4f || frente.sqrMagnitude < 1e-4f) return punto;
        float ang = Vector3.SignedAngle(frente, h, Vector3.up);
        float lim = Mathf.Clamp(ang, -maxGrados, maxGrados);
        if (Mathf.Approximately(ang, lim)) return punto;
        Vector3 nh = Quaternion.AngleAxis(lim, Vector3.up) * frente.normalized * h.magnitude;
        return ojo + nh + Vector3.up * d.y;
    }

    /// <summary>Yaw local (respecto del rig) que apunta a un punto del mundo.</summary>
    protected float YawHacia(Vector3 p)
    {
        Vector3 d = Rig.trackingSpace.InverseTransformDirection(p - Cabeza.position);
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    protected IEnumerator Boton(Action<bool> set, float dur = 0.12f)
    {
        set(true);
        yield return Esperar(dur);
        set(false);
        yield return null;
    }

    protected IEnumerator PulsarA() => Boton(v => D.A = v);
    protected IEnumerator PulsarB(float dur = 0.12f) => Boton(v => D.B = v, dur);
    protected IEnumerator PulsarX() => Boton(v => D.X = v);
    protected IEnumerator ClicStick(bool izq = false) => Boton(v => { if (izq) D.clicL = v; else D.clicR = v; });

    protected IEnumerator StickPulso(bool izquierdo, Vector2 dir, float dur = 0.18f)
    {
        float t = 0f;
        int cuadros = 0;
        while (t < dur || cuadros < 6)
        {
            t += Time.deltaTime;
            cuadros++;
            // Sube rapido, se mantiene a fondo y vuelve: el juego siempre ve el stick pasar el umbral.
            float k = cuadros <= 1 ? 0.5f : Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI) * 1.8f);
            if (cuadros >= 2 && cuadros <= 4) k = 1f;
            Vector2 v = dir * k;
            if (izquierdo) D.stickL = v; else D.stickR = v;
            yield return null;
        }
        if (izquierdo) D.stickL = Vector2.zero; else D.stickR = Vector2.zero;
        yield return Esperar(0.12f);
    }

    /// <summary>Aprieta (o suelta) el GRIP de una mano con un pequeno recorrido, como un dedo real.</summary>
    protected IEnumerator Grip(bool izq, bool apretar, float dur = 0.12f)
    {
        float a = D.Grip(izq), b = apretar ? 1f : 0f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            D.SetGrip(izq, Mathf.Lerp(a, b, t / dur));
            yield return null;
        }
        D.SetGrip(izq, b);
        yield return null;
    }

    protected IEnumerator Gatillo(bool izq, float valor, float dur = 0.2f)
    {
        float a = izq ? D.gatilloL : D.gatilloR;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            D.SetGatillo(izq, Mathf.Lerp(a, valor, t / dur));
            yield return null;
        }
        D.SetGatillo(izq, valor);
    }

    // ---------------- Manos ----------------

    /// <summary>Hombro aproximado (en el mundo) para orientar la mano de forma natural.</summary>
    protected Vector3 Hombro(bool izq)
    {
        Vector3 f = Cabeza.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Rig.transform.forward;
        f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        return Cabeza.position + Vector3.down * 0.22f + r * (izq ? -0.18f : 0.18f) - f * 0.05f;
    }

    /// <summary>Pose de la mano en un punto, con la palma/control apuntando desde el hombro hacia el punto.</summary>
    protected Pose PoseEn(bool izq, Vector3 p, float giro = 0f)
    {
        Vector3 d = p - Hombro(izq);
        if (d.sqrMagnitude < 1e-4f) d = Cabeza.forward;
        var rot = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, (izq ? 1f : -1f) * (20f + giro));
        return new Pose(p, rot);
    }

    protected IEnumerator Alcanzar(bool izq, Func<Vector3> punto, float dur = 0.7f, bool mirar = true)
    {
        if (mirar) Mirar(punto);
        D.MoverMano(izq, () => PoseEn(izq, punto()), dur);
        yield return Esperar(dur + 0.05f);
    }

    protected IEnumerator Alcanzar(bool izq, Func<Pose> pose, float dur = 0.7f)
    {
        D.MoverMano(izq, pose, dur);
        yield return Esperar(dur + 0.05f);
    }

    /// <summary>Deja la mano quieta donde esta (respecto del cuerpo). Se usa justo al agarrar algo,
    /// para que la mano no siga "persiguiendo" el objeto que ahora se mueve con ella.</summary>
    protected void FijarMano(bool izq)
    {
        var esp = Rig.trackingSpace;
        var a = D.Ancla(izq);
        Vector3 pl = esp.InverseTransformPoint(a.position);
        Quaternion rl = Quaternion.Inverse(esp.rotation) * a.rotation;
        D.MoverMano(izq, () => new Pose(esp.TransformPoint(pl), esp.rotation * rl), 0f);
    }

    /// <summary>Agarra con GRIP y deja la mano fija en ese punto.</summary>
    protected IEnumerator Agarrar(bool izq)
    {
        FijarMano(izq);
        yield return Grip(izq, true);
    }

    protected IEnumerator Reposo(bool izq, float dur = 0.6f)
    {
        D.ManoAReposo(izq, dur);
        yield return Esperar(dur);
    }

    /// <summary>Toca un boton con la punta del control: acerca la mano, empuja un poco y la retira.</summary>
    protected IEnumerator Tocar(bool izq, Func<Vector3> boton, Vector3 normalHaciaFuera, float radio, Action alTocar = null)
    {
        Mirar(boton);
        Vector3 n = normalHaciaFuera.normalized;
        D.MoverMano(izq, () => PoseEn(izq, boton() + n * 0.16f), 0.75f);
        yield return Esperar(0.8f);
        D.MoverMano(izq, () => PoseEn(izq, boton() + n * (radio * 0.45f)), 0.28f);
        yield return Esperar(0.3f);
        alTocar?.Invoke();
        yield return Esperar(0.35f);
        D.MoverMano(izq, () => PoseEn(izq, boton() + n * 0.22f), 0.3f);
        yield return Esperar(0.32f);
        yield return Reposo(izq, 0.6f);
    }

    // ---------------- Caminar ----------------

    /// <summary>
    /// Camina (locomocion con el stick, como en la Quest) pasando por los puntos. La cabeza mira
    /// hacia adelante del recorrido; el piso se sigue con un rayo (escalones, veredas, rampas).
    /// </summary>
    protected IEnumerator Caminar(float velocidad, params Vector3[] puntos)
    {
        var rigT = Rig.transform;
        // Velocidad como la locomocion del juego con el stick (FirstPersonLocomotor llega a ~3.7 m/s):
        // los valores del guion son "relativos" (1 = paso normal).
        velocidad = Mathf.Min(3.6f, velocidad * 2.4f);
        puntos = PlanificarCamino(Cabeza.position, puntos);
        _avisoCruce = false;
        D.caminando = true;
        float v = 0f;
        for (int i = 0; i < puntos.Length; i++)
        {
            Vector3 meta = puntos[i];
            bool ultimo = i == puntos.Length - 1;
            while (true)
            {
                float dt = Time.deltaTime;
                Vector3 cab = Cabeza.position;
                Vector3 d = meta - cab; d.y = 0f;
                float dist = d.magnitude;
                if (dist < (ultimo ? 0.04f : 0.35f)) break;

                // Mira un poco mas adelante en el recorrido.
                Vector3 adelante = meta;
                if (!ultimo && dist < 2.5f) adelante = Vector3.Lerp(meta, puntos[i + 1], 1f - dist / 2.5f);
                Vector3 mira = new Vector3(adelante.x, cab.y - 0.15f, adelante.z);
                if ((mira - cab).sqrMagnitude < 0.5f) mira = cab + d.normalized * 3f + Vector3.down * 0.3f;
                D.mirarA = () => mira;

                // Acelera y frena suave; frena al llegar al ultimo punto.
                float objetivo = ultimo ? Mathf.Min(velocidad, dist * 2.6f + 0.2f) : velocidad;
                // No camina "de costado": si hay que girar mucho, va mas lento mientras gira la cabeza.
                float ang = Vector3.Angle(new Vector3(Cabeza.forward.x, 0f, Cabeza.forward.z), d);
                objetivo *= Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(30f, 110f, ang));
                v = Mathf.MoveTowards(v, objetivo, dt * 7f);
                Vector3 paso = d.normalized * Mathf.Min(dist, v * dt);
                rigT.position += paso;
                D.faseCaminar += dt * Mathf.Min(v, 1.6f) * 3.4f;

                // Piso.
                if (D.AlturaPiso(Cabeza.position, rigT.position.y + 1.0f, out float y))
                {
                    var p = rigT.position;
                    p.y = Mathf.MoveTowards(p.y, y, dt * 2.5f);
                    rigT.position = p;
                }
                // Vigia: la cabeza no debe cruzar paredes (rayo entre la posicion anterior y la actual).
                Vector3 cabAhora = Cabeza.position;
                if (_cabPrev.HasValue && (cabAhora - _cabPrev.Value).sqrMagnitude > 1e-6f)
                {
                    foreach (var h in Physics.RaycastAll(_cabPrev.Value, cabAhora - _cabPrev.Value, (cabAhora - _cabPrev.Value).magnitude, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.transform.IsChildOf(Rig.transform)) continue;
                        if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
                        if (h.collider.GetComponentInParent<PuertaAutomatica>() != null || h.collider.GetComponentInParent<PuertaCasa>() != null) continue;
                        Log("AVISO ATRAVIESA " + h.collider.name + " en " + h.point);
                        break;
                    }
                }
                _cabPrev = cabAhora;
                // Vigia: la cabeza nunca debe entrar en el camion.
                if (DentroDelCamion(Cabeza.position) && !_avisoCruce) { _avisoCruce = true; Log("AVISO ATRAVIESA EL CAMION en " + Cabeza.position); }
                // Stick izquierdo empujado (lo ve el juego: diagnostico de bloqueo, etc.).
                D.stickL = new Vector2(0f, Mathf.Clamp01(v / Mathf.Max(0.1f, velocidad)) * 0.9f);
                yield return null;
            }
        }
        D.stickL = Vector2.zero;
        D.caminando = false;
        _cabPrev = null;
    }

    Vector3? _cabPrev;

    protected IEnumerator Caminar(params Vector3[] puntos) => Caminar(1.5f, puntos);

    // ---------------- Evitar el camion y su circulo de subida ----------------

    /// <summary>Si es true, el ultimo punto del camino es el circulo de subida (se quiere subir).</summary>
    protected bool _caminoParaSubir;
    const float RadioEvitarZona = 1.35f;
    const float MargenCamion = 0.4f;

    /// <summary>
    /// Ajusta el camino para que una persona no atraviese el camion ni pise sin querer el circulo
    /// amarillo de subida (que la sube a la cabina): los puntos dentro del circulo se sacan afuera y
    /// los tramos que cruzan el camion o el circulo se desvian por el costado.
    /// </summary>
    Vector3[] PlanificarCamino(Vector3 desde, Vector3[] puntos)
    {
        var cam = FindObjectOfType<CamionBomberos>();
        if (cam == null || cam.JugadorSentado) return puntos;
        Vector3 zona = Plano(cam.PuntoZonaSubida);
        // El circulo esta pegado a la puerta del conductor: se rodea siempre por AFUERA (lejos del camion).
        Vector3 eje = Plano(cam.Frente).normalized;
        Vector3 afuera = Vector3.Cross(Vector3.up, eje).normalized;
        if (Vector3.Dot(afuera, zona - Plano(cam.Limites.center)) < 0f) afuera = -afuera;
        var lista = new List<Vector3>();
        Vector3 prev = Plano(desde);
        void Agregar(Vector3 q, float y)
        {
            foreach (var r in RodeoCamion(cam, prev, q)) lista.Add(r);
            lista.Add(new Vector3(q.x, y, q.z));
            prev = q;
        }
        for (int i = 0; i < puntos.Length; i++)
        {
            Vector3 p = Plano(puntos[i]);
            bool esSubida = _caminoParaSubir && i == puntos.Length - 1;
            // Punto dentro del circulo (sin querer subir): se corre hacia afuera del camion.
            if (!esSubida && (p - zona).magnitude < RadioEvitarZona)
            {
                float a = Vector3.Dot(p - zona, eje);
                float lat = Mathf.Sqrt(Mathf.Max(0f, (RadioEvitarZona + 0.15f) * (RadioEvitarZona + 0.15f) - a * a));
                p = zona + eje * a + afuera * Mathf.Max(lat, 0.3f);
            }
            // Nunca pararse dentro (ni pegado) al camion: se corre al costado mas cercano.
            if (!esSubida) p = FueraDelCamion(cam, p, 0.45f);
            // Pasa por el circulo: lo rodea por afuera (dos puntos, en el sentido de la marcha).
            if (!esSubida)
            {
                Vector3 ab = p - prev;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(zona - prev, ab) / ab.sqrMagnitude) : 0f;
                Vector3 c = prev + ab * t;
                bool desdeDentro = (prev - zona).magnitude < RadioEvitarZona; // saliendo del circulo (al bajar)
                if (!desdeDentro && t > 0.02f && t < 0.98f && (c - zona).magnitude < RadioEvitarZona)
                {
                    float s = Mathf.Sign(Vector3.Dot(ab, eje)); if (s == 0f) s = 1f;
                    Agregar(zona + afuera * (RadioEvitarZona + 0.3f) - eje * s * 0.9f, puntos[i].y);
                    Agregar(zona + afuera * (RadioEvitarZona + 0.3f) + eje * s * 0.9f, puntos[i].y);
                }
            }
            Agregar(p, puntos[i].y);
        }
        return lista.ToArray();
    }

    bool _avisoCruce;

    /// <summary>Si p cae dentro del camion (mas un margen), lo lleva al borde mas cercano, afuera.</summary>
    Vector3 FueraDelCamion(CamionBomberos cam, Vector3 p, float margen)
    {
        var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        float W = (float)typeof(CamionBomberos).GetField("_anchoMedio", bf).GetValue(cam) + margen;
        float L = (float)typeof(CamionBomberos).GetField("_largoMedio", bf).GetValue(cam) + margen;
        Vector3 c = Plano((Vector3)typeof(CamionBomberos).GetProperty("CentroBase", bf).GetValue(cam));
        Vector3 f = Plano(cam.Frente).normalized, r = Vector3.Cross(Vector3.up, f);
        Vector3 d = Plano(p) - c;
        float x = Vector3.Dot(d, r), z = Vector3.Dot(d, f);
        if (Mathf.Abs(x) >= W || Mathf.Abs(z) >= L) return p;
        // Sale por el lado que requiere menos movimiento.
        if (W - Mathf.Abs(x) <= L - Mathf.Abs(z)) x = Mathf.Sign(x == 0f ? 1f : x) * W;
        else z = Mathf.Sign(z == 0f ? 1f : z) * L;
        Vector3 q = c + r * x + f * z;
        return new Vector3(q.x, p.y, q.z);
    }

    bool DentroDelCamion(Vector3 p)
    {
        var cam = FindObjectOfType<CamionBomberos>();
        if (cam == null || cam.JugadorSentado) return false;
        var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        float W0 = (float)typeof(CamionBomberos).GetField("_anchoMedio", bf).GetValue(cam);
        float L0 = (float)typeof(CamionBomberos).GetField("_largoMedio", bf).GetValue(cam);
        Vector3 c = Plano((Vector3)typeof(CamionBomberos).GetProperty("CentroBase", bf).GetValue(cam));
        Vector3 f = Plano(cam.Frente).normalized, r = Vector3.Cross(Vector3.up, f);
        Vector3 d = Plano(p) - c;
        return Mathf.Abs(Vector3.Dot(d, r)) < W0 + 0.05f && Mathf.Abs(Vector3.Dot(d, f)) < L0 + 0.05f;
    }

    /// <summary>Puntos para rodear la caja del camion (agrandada) si el tramo a-b la cruza.</summary>
    List<Vector3> RodeoCamion(CamionBomberos cam, Vector3 a, Vector3 b)
    {
        var res = new List<Vector3>();
        var lim = cam.Limites;
        Vector3 f = Plano(cam.Frente).normalized;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        Vector3 c = Plano(lim.center);
        // Medio largo y medio ancho (la caja del camion esta alineada con sus ejes).
        float L0 = Mathf.Abs(Vector3.Dot(lim.extents, new Vector3(Mathf.Abs(f.x), 0f, Mathf.Abs(f.z))));
        float W0 = Mathf.Abs(Vector3.Dot(lim.extents, new Vector3(Mathf.Abs(r.x), 0f, Mathf.Abs(r.z))));
        // Medidas exactas del camion en sus propios ejes (si estan disponibles).
        var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var fAncho = typeof(CamionBomberos).GetField("_anchoMedio", bf);
        var fLargo = typeof(CamionBomberos).GetField("_largoMedio", bf);
        var pCentro = typeof(CamionBomberos).GetProperty("CentroBase", bf);
        if (fAncho != null && fLargo != null && pCentro != null)
        {
            W0 = (float)fAncho.GetValue(cam);
            L0 = (float)fLargo.GetValue(cam);
            c = Plano((Vector3)pCentro.GetValue(cam));
        }
        float L = L0 + MargenCamion, W = W0 + MargenCamion;
        Vector2 A = new Vector2(Vector3.Dot(a - c, r), Vector3.Dot(a - c, f));
        Vector2 B = new Vector2(Vector3.Dot(b - c, r), Vector3.Dot(b - c, f));
        // El cuerpo del camion (con un margen chico): ningun tramo debe atravesarlo.
        bool Cuerpo(Vector2 p) => Mathf.Abs(p.x) < W0 + 0.15f && Mathf.Abs(p.y) < L0 + 0.15f;
        if (Mathf.Abs(A.x) < W0 && Mathf.Abs(A.y) < L0) return res; // (no deberia pasar: ya esta adentro)
        bool cruza = false;
        for (int k = 1; k < 60 && !cruza; k++) cruza = Cuerpo(Vector2.Lerp(A, B, k / 60f));
        if (!cruza) return res;
        Vector3 Mundo(float x, float z) => c + r * x + f * z;
        float sa = Mathf.Sign(A.x == 0f ? 1f : A.x), sb = Mathf.Sign(B.x == 0f ? 1f : B.x);
        if (sa == sb)
        {
            // Mismo costado: se pega al costado sin entrar.
            res.Add(Mundo(sa * (W + 0.1f), Mathf.Clamp(A.y, -L, L)));
            res.Add(Mundo(sa * (W + 0.1f), Mathf.Clamp(B.y, -L, L)));
            return res;
        }
        // Costados opuestos: por adelante o por atras, el mas corto.
        float Largo(float z) =>
            Vector2.Distance(A, new Vector2(sa * (W + 0.1f), z)) + 2f * W + Vector2.Distance(new Vector2(sb * (W + 0.1f), z), B);
        float zz = Largo(L + 0.1f) <= Largo(-L - 0.1f) ? L + 0.1f : -L - 0.1f;
        res.Add(Mundo(sa * (W + 0.1f), zz));
        res.Add(Mundo(sb * (W + 0.1f), zz));
        return res;
    }

    static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

    /// <summary>Gira el cuerpo (la cabeza) hacia un punto, sin caminar.</summary>
    protected IEnumerator Voltear(Vector3 p, float dur = 0.8f)
    {
        Mirar(p);
        yield return Esperar(dur);
    }

    protected IEnumerator Agacharse(float altura, float dur = 0.8f)
    {
        D.alturaObjetivo = altura;
        yield return Esperar(dur);
    }

    protected void Pararse() => D.alturaObjetivo = D.alturaOjos;

    // =====================================================================
    // Utilidades del mundo
    // =====================================================================

    protected static T Buscar<T>() where T : UnityEngine.Object => FindObjectOfType<T>();

    protected static Transform BuscarNombre(string nombre)
    {
        foreach (var t in FindObjectsOfType<Transform>())
            if (t.name == nombre) return t;
        return null;
    }

    protected static object Privado(object o, string campo)
    {
        var f = o.GetType().GetField(campo, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return f != null ? f.GetValue(o) : null;
    }

    /// <summary>Dispara el evento de un boton del Interaction SDK (poke) al tocarlo con la mano.</summary>
    protected static void Pokear(Transform t)
    {
        var w = t.GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>();
        if (w == null) { Debug.LogWarning("[DemoGuion] Sin InteractableUnityEventWrapper en " + t.name); return; }
        w.WhenSelect.Invoke();
        w.WhenUnselect.Invoke();
    }

    protected void Log(string s)
    {
        Paso = s;
        Debug.Log("[DemoGuion] " + s + "  t=" + Time.time.ToString("F1"));
        try { System.IO.File.AppendAllText(ArchivoLog, System.DateTime.Now.ToString("HH:mm:ss") + " t=" + Time.time.ToString("F1") + " " + s + "\n"); } catch { }
    }
}
