Shader "Custom/WebKettleEmissive" {
    Properties {
        _ColourAlpha ("Colour/Alpha", 2D) = "white" {}
        _RMEAO ("R/M/E/AO", 2D) = "black" {}
        _Normal ("Normal", 2D) = "bump" {}
        _EmissiveColor ("Emissive Color", Color) = (0.5, 0.5, 0.5, 1)
        _EmissiveStrength ("Emissive Strength", Range(0, 1)) = 0
        _Metallic ("Metallic", Range(0, 1)) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _ColourAlpha;
        sampler2D _RMEAO;
        struct Input { float2 uv_ColourAlpha; };
        half4 _EmissiveColor;
        half _EmissiveStrength;
        half _Metallic;
        half _Glossiness;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_ColourAlpha, IN.uv_ColourAlpha);
            fixed4 m = tex2D(_RMEAO, IN.uv_ColourAlpha);
            o.Albedo = c.rgb;
            o.Metallic = _Metallic * m.g;
            o.Smoothness = _Glossiness * (1.0 - m.r * 0.5);
            o.Occlusion = m.a;
            o.Emission = _EmissiveColor.rgb * _EmissiveStrength * (0.35 + m.b * 0.65);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
