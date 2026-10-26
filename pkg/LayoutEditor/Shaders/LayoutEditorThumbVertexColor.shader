Shader "LayoutEditor/ThumbVertexColor"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _VertexColorFloor ("Vertex Color Floor", Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 2.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _VertexColorFloor;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float3 vc = lerp(IN.color.rgb, fixed3(1, 1, 1), _VertexColorFloor);
            o.Albedo = c.rgb * vc;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
