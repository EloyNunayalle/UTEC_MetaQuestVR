// Cielo de atardecer costero para Quest: gradiente cenit -> horizonte calido, resplandor hacia el sol,
// disco solar alineado con la luz direccional (_WorldSpaceLightPos0) y bruma marina bajo el horizonte.
// Sin texturas ni ruido: unas pocas operaciones por pixel. Compatible con stereo instanced/multiview.
Shader "UTEC/CieloAtardecer"
{
    Properties
    {
        _Cenit ("Cenit", Color) = (0.20, 0.42, 0.76, 1)
        _Medio ("Celeste intermedio", Color) = (0.62, 0.78, 0.93, 1)
        _Horizonte ("Horizonte (= color de niebla)", Color) = (0.93, 0.80, 0.66, 1)
        _AltoHorizonte ("Alto de la franja calida", Range(0.02, 0.5)) = 0.14
        _Resplandor ("Resplandor del sol", Color) = (1.00, 0.62, 0.34, 1)
        _BajoHorizonte ("Bruma bajo el horizonte", Color) = (0.62, 0.66, 0.66, 1)
        _ColorSol ("Color del sol", Color) = (1.00, 0.93, 0.78, 1)
        _Exponente ("Caida del gradiente", Range(0.2, 2)) = 0.55
        _IntensidadResplandor ("Intensidad resplandor", Range(0, 1.5)) = 0.85
        _TamSol ("Radio del sol (rad)", Range(0.005, 0.1)) = 0.028
        _DirSolRespaldo ("Dir. sol si no hay luz", Vector) = (-0.79, 0.41, 0.46, 0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Cenit, _Medio, _Horizonte, _Resplandor, _BajoHorizonte, _ColorSol;
            half _Exponente, _IntensidadResplandor, _TamSol, _AltoHorizonte;
            float4 _DirSolRespaldo;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 s = _WorldSpaceLightPos0.xyz;
                s = dot(s, s) > 0.01 ? normalize(s) : normalize(_DirSolRespaldo.xyz);

                float up = d.y;
                // Horizonte calido -> celeste (franja baja) -> azul del cenit; asi no pasa por violeta.
                fixed3 bajo = lerp(_Horizonte.rgb, _Medio.rgb, smoothstep(0.0, _AltoHorizonte, up));
                fixed3 col = up >= 0
                    ? lerp(bajo, _Cenit.rgb, pow(saturate((up - _AltoHorizonte) / (1.0 - _AltoHorizonte)), _Exponente))
                    : lerp(_Horizonte.rgb, _BajoHorizonte.rgb, saturate(-up * 5.0));

                float sd = dot(d, s);
                // Resplandor amplio y calido cerca del horizonte, del lado del sol.
                float glow = pow(saturate(sd * 0.5 + 0.5), 7.0) * (1.0 - saturate(abs(up) * 1.6)) * _IntensidadResplandor;
                col = lerp(col, _Resplandor.rgb, saturate(glow));
                // Halo + disco.
                float halo = pow(saturate(sd), 350.0) * 0.55;
                float cosR = cos(_TamSol);
                float disco = smoothstep(cosR - 0.00035, cosR, sd) * saturate(up * 40.0 + 1.0);
                col += _ColorSol.rgb * halo;
                col = lerp(col, _ColorSol.rgb * 1.25, disco);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
