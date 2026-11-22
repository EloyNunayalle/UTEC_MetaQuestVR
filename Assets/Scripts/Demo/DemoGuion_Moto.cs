using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Moto: se sube parandose en el circulo, maneja con el stick (manos en los punos) y baja con B.</summary>
public partial class DemoGuion
{
    MotoBomberos Moto => Buscar<MotoBomberos>();

    Pose PoseManoMoto(MotoBomberos m, bool izq)
    {
        var t = m.transform;
        Vector3 p = t.TransformPoint(new Vector3(izq ? -0.31f : 0.31f, 1.07f, 0.45f));
        Quaternion r = t.rotation * Quaternion.Euler(18f, izq ? 8f : -8f, izq ? 75f : -75f);
        return new Pose(p, r);
    }

    IEnumerator SubirAMoto()
    {
        var m = Moto;
        Vector3 z = m.PuntoSubida;
        yield return Caminar(1.3f, new Vector3(z.x, 0f, z.z));
        Mirar(() => m.transform.position + m.transform.forward * 1.5f + Vector3.up * 0.9f);
        yield return EsperarHasta(() => m.Montado, 5f, "subir a la moto");
        MirarAlFrente(8f);
        D.MoverMano(true, () => PoseManoMoto(m, true), 0.7f);
        D.MoverMano(false, () => PoseManoMoto(m, false), 0.7f);
        yield return Esperar(0.8f);
        yield return Grip(true, true);
        yield return Grip(false, true);
    }

    IEnumerator BajarDeMoto()
    {
        var m = Moto;
        yield return Grip(true, false);
        yield return Grip(false, false);
        yield return PulsarB();
        yield return EsperarHasta(() => !m.Montado, 2f, "bajar de la moto");
        D.ManoAReposo(true, 0.6f);
        D.ManoAReposo(false, 0.6f);
        yield return Esperar(0.6f);
    }

    /// <summary>Maneja la moto con el stick izquierdo siguiendo la ruta del GPS (pure pursuit).</summary>
    IEnumerator ConducirMoto(Vector3 destino, float radio, float vMax, params Vector3[] previos) => ConducirMoto(destino, radio, vMax, true, previos);

    IEnumerator ConducirMoto(Vector3 destino, float radio, float vMax, bool parar, params Vector3[] previos)
    {
        var m = Moto;
        var ruta = new List<Vector3>();
        Vector3 desde = previos.Length > 0 ? previos[previos.Length - 1] : m.transform.position;
        if (!MapaGPS.Ruta(desde, destino, ruta)) { Log("AVISO moto sin ruta"); ruta.Clear(); ruta.Add(desde); ruta.Add(destino); }
        var pre = new List<Vector3> { m.transform.position };
        pre.AddRange(previos);
        if (previos.Length > 0) ruta.RemoveAt(0);
        pre.AddRange(ruta);
        ruta = pre;
        Densificar(ruta, 0.75f);
        Log("Moto hacia " + destino + " (" + ruta.Count + " puntos)");

        int idx = 0;
        float inicio = Time.time, tAtascado = 0f;
        Vector3 posPrev = m.transform.position;
        while (true)
        {
            float dt = Time.deltaTime;
            var t = m.transform;
            Vector3 pos = t.position;
            Vector3 f = Plano(t.forward).normalized;
            float v = dt > 0f ? Vector3.Dot(pos - posPrev, f) / dt : 0f;
            posPrev = pos;
            float distFin = Plano(destino - pos).magnitude;
            if (distFin <= radio && (!parar || Mathf.Abs(v) < 0.3f)) break;
            if (Time.time - inicio > 200f) { Log("AVISO moto demasiado larga"); break; }

            for (int k = idx; k < Mathf.Min(ruta.Count, idx + 30); k++)
                if (Plano(ruta[k] - pos).sqrMagnitude < Plano(ruta[idx] - pos).sqrMagnitude) idx = k;
            float L = Mathf.Clamp(2.5f + Mathf.Abs(v) * 0.7f, 2.5f, 9f);
            Vector3 obj = PuntoAdelante(ruta, idx, pos, L);
            float alfa = Vector3.SignedAngle(f, Plano(obj - pos), Vector3.up);
            float giro = Mathf.Clamp(alfa / 35f, -1f, 1f);

            float curva = CurvaAdelante(ruta, idx, 12f + Mathf.Abs(v));
            float vObj = Mathf.Lerp(vMax, 3f, Mathf.InverseLerp(8f, 60f, curva));
            if (Mathf.Abs(alfa) > 30f) vObj = Mathf.Min(vObj, 2.5f);
            if (parar)
            {
                vObj = Mathf.Min(vObj, Mathf.Sqrt(2f * 3f * Mathf.Max(0f, distFin - radio * 0.5f)));
                if (distFin <= radio) vObj = 0f;
            }
            float acel = Mathf.Clamp(vObj / Mathf.Max(1f, m.velocidadMaxima) + (vObj - v) * 0.15f, 0f, 1f);
            if (vObj < 0.05f) acel = 0f;

            Vector2 s = new Vector2(giro, acel);
            D.stickL = Vector2.MoveTowards(D.stickL, s, dt * 4f);

            if (acel > 0.3f && Mathf.Abs(v) < 0.15f) tAtascado += dt; else tAtascado = 0f;
            if (tAtascado > 1.5f)
            {
                tAtascado = 0f;
                Log("AVISO moto atascada en " + pos);
                // Reversa corta y vuelve a intentar.
                for (float r = 0f; r < 1.2f; r += Time.deltaTime) { D.stickL = new Vector2(0f, -0.8f); yield return null; }
            }

            Vector3 mira = PuntoAdelante(ruta, idx, pos, 10f + Mathf.Abs(v));
            Vector3 mp = LimitarMirada(new Vector3(mira.x, Cabeza.position.y - 0.5f, mira.z), f, 38f);
            D.mirarA = () => mp;
            if (MirandoGPS(m.transform, curva, alfa, distFin)) { var g = _pantallaGPS; D.mirarA = () => g.position; }
            yield return null;
        }
        if (parar) D.stickL = Vector2.zero;
        Log("Moto llego a " + destino);
    }
}
