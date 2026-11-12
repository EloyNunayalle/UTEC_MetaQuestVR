// Agua del mar para Quest: opaca, sin texturas. Color por fresnel (azul profundo -> horizonte),
// brillo del sol con una normal ondulada analitica (3 senos) y niebla de Unity. Un solo pase.
Shader "UTEC/AguaSimple"
{
    Properties
    {
        _ColorProfundo ("Color profundo", Color) = (0.05, 0.22, 0.34, 1)
        _ColorSuperficie ("Color superficie", Color) = (0.16, 0.45, 0.55, 1)
        _ColorHorizonte ("Color horizonte", Color) = (0.62, 0.70, 0.74, 1)
        _Brillo ("Intensidad brillo sol", Range(0, 4)) = 1.6
        _Rugosidad ("Rugosidad olas", Range(0, 0.3)) = 0.09
        _Escala ("Escala olas", Float) = 0.22
        _Velocidad ("Velocidad olas", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _ColorProfundo, _ColorSuperficie, _ColorHorizonte, _LightColor0;
            half _Brillo, _Rugosidad, _Escala, _Velocidad;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 mundo : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.mundo = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Velocidad;
                float2 p = i.mundo.xz * _Escala;
                float3 n = normalize(float3(
                    sin(p.x * 1.3 + t) * 0.6 + sin((p.x + p.y) * 0.7 + t * 1.4) * 0.4,
                    1.0 / max(_Rugosidad, 0.001),
                    cos(p.y * 1.1 - t * 0.8) * 0.6 + sin((p.y - p.x) * 2.3 + t * 2.1) * 0.25));

                float3 v = normalize(_WorldSpaceCameraPos - i.mundo);
                float fres = pow(1.0 - saturate(v.y), 4.0);
                fixed3 col = lerp(_ColorSuperficie.rgb, _ColorProfundo.rgb, saturate(v.y * 1.5));
                col = lerp(col, _ColorHorizonte.rgb, fres);

                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), 180.0) * _Brillo;
                col += _LightColor0.rgb * spec;

                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
