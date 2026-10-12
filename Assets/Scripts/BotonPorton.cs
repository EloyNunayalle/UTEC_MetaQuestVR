using UnityEngine;

public class BotonPorton : MonoBehaviour
{
    public PortonElevadizo porton;
    public Transform boton;
    public float recorrido = 0.02f;
    public float velocidad = 10f;

    Vector3 _posInicial;
    Vector3 _posFinal;
    bool _presionado;
    float _ultimoDisparo = -10f;

    void Awake()
    {
        if (boton != null)
        {
            _posInicial = boton.localPosition;
            _posFinal = _posInicial + new Vector3(0f, 0f, recorrido);
        }
    }

    public void Presionar()
    {
        _presionado = true;
        if (porton != null && Time.time - _ultimoDisparo > 0.6f)
        {
            _ultimoDisparo = Time.time;
            porton.Alternar();
        }
    }

    public void Soltar()
    {
        _presionado = false;
    }

    void Update()
    {
        if (boton == null) return;
        boton.localPosition = Vector3.Lerp(boton.localPosition, _presionado ? _posFinal : _posInicial, Time.deltaTime * velocidad);
    }
}