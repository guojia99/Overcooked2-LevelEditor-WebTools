Shader "Custom/WebOpaqueDiffuse" {
    Properties {
        _ColourAlpha ("Colour/Alpha", 2D) = "white" {}
        _Normal ("Normal", 2D) = "bump" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.35
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _ColourAlpha;
        struct Input { float2 uv_ColourAlpha; };
        half _Metallic;
        half _Glossiness;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_ColourAlpha, IN.uv_ColourAlpha);
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
