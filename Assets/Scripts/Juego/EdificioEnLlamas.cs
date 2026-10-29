using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edificio en llamas: reparte varios focos de fuego independientes (ZonaFuego) sobre
/// la fachada que mira hacia la estacion, en una grilla de columnas x pisos.
/// Lleva la cuenta de cuantos quedan y avisa cuando se apaga cada uno y todo el edificio.
///
/// Lo agrega solo JuegoBomberos al edificio mas cercano al porton principal (o al que
/// indiques por nombre). Tambien se puede agregar a mano a cualquier edificio.
/// </summary>
public class EdificioEnLlamas : MonoBehaviour
{
    [Header("Focos de fuego")]
    public int columnas = 3;
    [Tooltip("Maximo de pisos con fuego (se limita por la altura del edificio).")]
    public int pisosMaximos = 3;
    public float alturaPiso = 3f;
    [Tooltip("Distancia de las llamas delante de la fachada (m).")]
    public float separacionFachada = 0.35f;
    [Tooltip("Los pisos altos necesitan mas agua: multiplicador por piso.")]
    public float resistenciaExtraPorPiso = 0.25f;

    public int Total => _zonas.Count;
    public int Apagadas { get; private set; }
    public bool TodoApagado => _zonas.Count > 0 && Apagadas >= _zonas.Count;
    public IReadOnlyList<ZonaFuego> Zonas => _zonas;

    public event Action<ZonaFuego> alApagarZona;
    public event Action alApagarTodo;

    readonly List<ZonaFuego> _zonas = new List<ZonaFuego>();
    Transform _raiz;

    /// <summary>Crea los focos en la fachada que mira hacia "mirandoA" (p. ej. la estacion).</summary>
    public void Inicializar(Vector3 mirandoA)
    {
        if (_raiz != null) return;
        if (!VisualesVR.Limites(transform, out Bounds b))
        {
            Debug.LogWarning("[EdificioEnLlamas] El edificio no tiene renderers: no se crean focos.", this);
            return;
        }

        // Fachada: la cara de los limites que mira hacia el punto indicado.
        Vector3 d = mirandoA - b.center; d.y = 0f;
        Vector3 normal = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
        Vector3 lado = Vector3.Cross(Vector3.up, normal);
        float semiAncho = Mathf.Abs(Vector3.Dot(b.extents, lado));
        float semiFondo = Mathf.Abs(Vector3.Dot(b.extents, normal));
        Vector3 caraCentro = new Vector3(b.center.x, b.min.y, b.center.z) + normal * semiFondo;

        _raiz = new GameObject("Fuegos").transform;
        _raiz.SetParent(transform, true);

        int pisos = Mathf.Clamp(Mathf.FloorToInt((b.size.y - 1f) / alturaPiso), 1, pisosMaximos);
        int cols = Mathf.Max(1, columnas);
        float anchoCol = semiAncho * 2f / cols;
        float anchoFoco = Mathf.Clamp(anchoCol * 0.55f, 0.8f, 1.8f);

        for (int p = 0; p < pisos; p++)
            for (int c = 0; c < cols; c++)
            {
                float x = -semiAncho + anchoCol * (c + 0.5f);
                // Un poco de variacion para que no se vea como una grilla perfecta.
                x += UnityEngine.Random.Range(-0.15f, 0.15f) * anchoCol;
                float y = 0.9f + p * alturaPiso;
                Vector3 pos = caraCentro + lado * x + Vector3.up * y + normal * separacionFachada;
                float escala = UnityEngine.Random.Range(0.85f, 1.25f);

                var z = ZonaFuego.Crear($"Fuego_P{p + 1}_C{c + 1}", _raiz, pos, normal, anchoFoco, escala);
                z.aguaNecesaria *= 1f + resistenciaExtraPorPiso * p;
                z.alApagarse += AlApagarse;
                _zonas.Add(z);
            }
    }

    void AlApagarse(ZonaFuego z)
    {
        Apagadas = 0;
        foreach (var zona in _zonas) if (zona.Apagada) Apagadas++;
        alApagarZona?.Invoke(z);
        if (TodoApagado) alApagarTodo?.Invoke();
    }

    /// <summary>Vuelve a encender todos los focos.</summary>
    public void Reavivar()
    {
        foreach (var z in _zonas) z.Reavivar();
        Apagadas = 0;
    }

    /// <summary>Oculta (o vuelve a mostrar) todos los focos sin cambiar su estado (lo usa la Mision 2).</summary>
    public void Pausar(bool pausar)
    {
        if (_raiz != null) _raiz.gameObject.SetActive(!pausar);
    }

    /// <summary>Foco encendido mas cercano a un punto (para guiar al jugador).</summary>
    public ZonaFuego ZonaActivaMasCercana(Vector3 desde)
    {
        ZonaFuego mejor = null;
        float mejorD = float.MaxValue;
        foreach (var z in _zonas)
        {
            if (z == null || z.Apagada) continue;
            float dd = (z.Centro - desde).sqrMagnitude;
            if (dd < mejorD) { mejorD = dd; mejor = z; }
        }
        return mejor;
    }
}
