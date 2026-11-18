using UnityEngine;

/// <summary>
/// Monta el GPS en los vehiculos, sin configurar nada en el Inspector:
/// - Camion: sobre el vidrio del monitor "Pantalla_GPS" que ya esta en el tablero (nodo "Display glass"),
///   del lado que mira al conductor.
/// - Moto: en un soporte nuevo sobre el manubrio, inclinado hacia la cabeza del piloto.
/// Se crea solo al iniciar el juego y vuelve a montar las pantallas si se recarga la escena.
/// </summary>
public class GPSVehiculos : MonoBehaviour
{
    PantallaGPS _gpsCamion;
    MotoBomberos _moto;
    CamionBomberos _camionSinPantalla;
    float _proximo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Instalar()
    {
        if (FindObjectOfType<GPSVehiculos>() != null) return;
        var go = new GameObject("GPS_Vehiculos");
        DontDestroyOnLoad(go);
        go.AddComponent<GPSVehiculos>();
    }

    void Update()
    {
        if (Time.unscaledTime < _proximo) return;
        _proximo = Time.unscaledTime + 1f;

        if (_gpsCamion == null)
        {
            var c = FindObjectOfType<CamionBomberos>();
            if (c != null && c != _camionSinPantalla) _gpsCamion = MontarEnCamion(c);
        }
        if (_moto == null)
        {
            var m = FindObjectOfType<MotoBomberos>();
            if (m != null)
            {
                _moto = m;
                MontarEnMoto(m);
            }
        }
    }

    PantallaGPS MontarEnCamion(CamionBomberos c)
    {
        Transform vidrio = null;
        foreach (var t in c.GetComponentsInChildren<Transform>(true))
            if (t.name == "Display glass") { vidrio = t; break; }
        var mf = vidrio != null ? vidrio.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("[GPS] El camion no tiene el monitor Pantalla_GPS (nodo 'Display glass'): sin GPS en el camion.", c);
            _camionSinPantalla = c;
            return null;
        }

        // El eje mas delgado del vidrio es su normal; de los otros dos, el mas vertical es el alto.
        Bounds b = mf.sharedMesh.bounds;
        Vector3 e = b.extents;
        Vector3[] ejes = { Vector3.right, Vector3.up, Vector3.forward };
        int eN = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
        int eA = (eN + 1) % 3, eB = (eN + 2) % 3;

        Vector3 normal = vidrio.TransformDirection(ejes[eN]).normalized;
        if (Vector3.Dot(normal, c.transform.forward) > 0f) normal = -normal; // la cara que mira al conductor
        Vector3 va = vidrio.TransformVector(ejes[eA] * (e[eA] * 2f));
        Vector3 vb = vidrio.TransformVector(ejes[eB] * (e[eB] * 2f));
        bool aEsAlto = Mathf.Abs(Vector3.Dot(va.normalized, Vector3.up)) >= Mathf.Abs(Vector3.Dot(vb.normalized, Vector3.up));
        Vector3 alto = aEsAlto ? va : vb, ancho = aEsAlto ? vb : va;
        Vector3 arriba = Vector3.ProjectOnPlane(alto, normal).normalized;
        if (Vector3.Dot(arriba, Vector3.up) < 0f) arriba = -arriba;

        float espesor = vidrio.TransformVector(ejes[eN] * e[eN]).magnitude;
        Vector3 centro = vidrio.TransformPoint(b.center) + normal * (espesor + 0.0015f);
        var tam = new Vector2(ancho.magnitude * 0.97f, alto.magnitude * 0.97f);
        var p = PantallaGPS.Crear(vidrio, centro, Quaternion.LookRotation(-normal, arriba), tam, c.transform, () => c.VelocidadKmh);
        Debug.Log($"[GPS] Pantalla del camion: {tam.x:0.000} x {tam.y:0.000} m.", p);
        return p;
    }

    static void MontarEnMoto(MotoBomberos m)
    {
        var t = m.transform;
        // Cabeza del piloto sentado: (0, 1.45, -0.3) en la moto (ver MotoBomberos.Subir).
        Vector3 pos = new Vector3(0f, 1.16f, 0.4f), ojo = new Vector3(0f, 1.45f, -0.3f);
        var negro = VisualesVR.MaterialOpaco(new Color(0.06f, 0.06f, 0.07f));

        var brazo = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Brazo_GPS", t, negro);
        brazo.transform.localPosition = new Vector3(0f, 1.08f, 0.44f);
        brazo.transform.localScale = new Vector3(0.022f, 0.045f, 0.022f);

        var soporte = new GameObject("Soporte_GPS").transform;
        soporte.SetParent(t, false);
        soporte.localPosition = pos;
        soporte.localRotation = Quaternion.LookRotation(pos - ojo, Vector3.up); // la cara (-Z) mira al piloto
        var carcasa = VisualesVR.Primitiva(PrimitiveType.Cube, "Carcasa", soporte, negro);
        carcasa.transform.localPosition = new Vector3(0f, 0f, 0.008f);
        carcasa.transform.localScale = new Vector3(0.185f, 0.116f, 0.014f);

        var p = PantallaGPS.Crear(soporte, soporte.position, soporte.rotation, new Vector2(0.17f, 0.1f), t, () => m.VelocidadKmh);
        p.zoomMin = 55f;
        p.zoomMax = 100f;
    }
}
