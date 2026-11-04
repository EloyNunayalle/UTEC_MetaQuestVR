using UnityEngine;

public enum ModoConduccion
{
    Joystick = 0,
    Volante = 1
}

/// <summary>
/// Modo de conducción elegido en el menú inicial. Se guarda en PlayerPrefs
/// para conservarlo entre sesiones.
/// </summary>
public static class ConfiguracionConduccion
{
    const string Clave = "UTEC.ModoConduccion";
    static bool _cargado;
    static ModoConduccion _modo;

    public static ModoConduccion Modo
    {
        get
        {
            if (!_cargado)
            {
                _modo = (ModoConduccion)PlayerPrefs.GetInt(Clave, (int)ModoConduccion.Joystick);
                _cargado = true;
            }
            return _modo;
        }
        set
        {
            _modo = value;
            _cargado = true;
            PlayerPrefs.SetInt(Clave, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static string Nombre =>
        Modo == ModoConduccion.Joystick ? "Joystick" : "Volante";
}
