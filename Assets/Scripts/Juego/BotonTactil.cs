using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Boton inmersivo: se presiona TOCANDOLO con la mano o con el control (no usa teclas
/// ni botones del control). Se hunde, suena, vibra y ejecuta <see cref="alPresionar"/>.
/// Se crea por codigo con <see cref="Crear"/>. El padre debe tener su +Z apuntando
/// en direccion contraria al jugador (el boton sobresale hacia -Z, hacia el jugador).
/// </summary>
public class BotonTactil : MonoBehaviour
{
    public Action alPresionar;
    public float radio = 0.11f;

    OVRCameraRig _rig;
    Transform _boton;
    Vector3 _reposo, _escalaBoton;
    bool _armado = true;
    float _proximo;

    public static BotonTactil Crear(string nombre, Transform padre, Vector3 posLocal, Color color, string texto,
                                    OVRCameraRig rig, Action accion, float escala = 1f)
    {
        var raiz = new GameObject("Boton_" + nombre).transform;
        raiz.SetParent(padre, false);
        raiz.localPosition = posLocal;
        raiz.localScale = Vector3.one * Mathf.Max(0.1f, escala);

        var placa = VisualesVR.Primitiva(PrimitiveType.Cube, "Placa", raiz, VisualesVR.MaterialOpaco(new Color(0.22f, 0.23f, 0.26f)));
        placa.transform.localScale = new Vector3(0.22f, 0.22f, 0.04f);

        var boton = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Boton", raiz, VisualesVR.MaterialOpaco(color));
        boton.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        boton.transform.localScale = new Vector3(0.16f, 0.025f, 0.16f);
        boton.transform.localPosition = new Vector3(0f, 0f, -0.035f);

        var etiqueta = VisualesVR.Texto("Etiqueta", raiz, 0.045f, new Vector2(0.4f, 0.14f));
        etiqueta.transform.localPosition = new Vector3(0f, 0.2f, -0.03f);
        etiqueta.fontStyle = FontStyles.Bold;
        etiqueta.outlineWidth = 0.2f;
        etiqueta.outlineColor = Color.black;
        etiqueta.text = texto;

        var b = raiz.gameObject.AddComponent<BotonTactil>();
        b._rig = rig;
        b._boton = boton.transform;
        b._reposo = boton.transform.localPosition;
        b._escalaBoton = boton.transform.localScale;
        b.radio = Mathf.Max(0.07f, 0.11f * escala);
        b.alPresionar = accion;
        return b;
    }

    float DistanciaMano(out int mano)
    {
        mano = -1;
        float mejor = float.MaxValue;
        if (_rig == null) return mejor;
        for (int i = 0; i < 2; i++)
        {
            var m = i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor;
            if (m == null || !m.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(m.position, _boton.position);
            if (d < mejor) { mejor = d; mano = i; }
        }
        return mejor;
    }

    void Update()
    {
        if (_boton == null) return;
        float d = DistanciaMano(out int mano);
        bool tocando = d < radio;

        // El boton se hunde al tocarlo y "late" cuando la mano se acerca (invita a presionarlo).
        Vector3 hundido = _reposo + Vector3.forward * 0.018f;
        _boton.localPosition = Vector3.Lerp(_boton.localPosition, tocando ? hundido : _reposo, Time.deltaTime * 15f);
        float cerca = Mathf.Clamp01(1f - (d - radio) / 0.25f);
        _boton.localScale = _escalaBoton * (1f + 0.08f * cerca * (0.5f + 0.5f * Mathf.Sin(Time.time * 10f)));

        if (!tocando)
        {
            if (d > radio * 1.5f) _armado = true;
            return;
        }
        if (!_armado || Time.time < _proximo) return;
        _armado = false;
        _proximo = Time.time + 0.8f;
        SonidosVR.ReproducirEn(SonidosVR.Clic, _boton.position, 1f);
        if (mano >= 0) EntradaVR.Vibrar(mano == 0, 0.6f, 0.08f, 0.8f);
        alPresionar?.Invoke();
    }
}
