// Laderas low-poly: Lambert con color por vertice (roca / pasto) y niebla. Sin texturas.
Shader "UTEC/LaderaVertexColor"
{
    Properties
    {
        _Color ("Tinte", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert noforwardadd nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float4 color : COLOR; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb * _Color.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Mobile/VertexLit"
}
