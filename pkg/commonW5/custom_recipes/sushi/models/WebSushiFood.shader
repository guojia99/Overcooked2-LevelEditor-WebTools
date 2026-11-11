Shader "Custom/WebSushiFood" {
    Properties {
        _MainTex ("Main Texture (preview)", 2D) = "white" {}
        _DiffuseMap ("Diffuse Map", 2D) = "white" {}
        _MaskColor ("Mask Color", Color) = (0.5, 0.5, 0.5, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.04
        _Roughness ("Roughness", Range(0, 1)) = 0.5
        _Brightness ("Brightness", Range(0, 1)) = 0
        _highlight_colour ("Highlight Colour", Color) = (0.91, 0.87, 0.78, 1)
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _DiffuseMap;
        struct Input { float2 uv_DiffuseMap; };
        half _Metallic;
        half _Roughness;
        half _Brightness;
        fixed4 _highlight_colour;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_DiffuseMap, IN.uv_DiffuseMap);
            fixed3 lit = lerp(c.rgb, c.rgb * _highlight_colour.rgb * 2.0, _Brightness);
            o.Albedo = lit;
            o.Metallic = _Metallic;
            o.Smoothness = 1.0 - _Roughness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
