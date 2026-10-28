Shader "Custom/WebLiquid" {
    Properties {
        _DiffuseTexture ("Diffuse", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 0.03
    }
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _DiffuseTexture;
            float4 _DiffuseTexture_ST;
            fixed4 _TintColor;
            float _ScrollSpeed;
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _DiffuseTexture);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.uv;
                uv.x += _Time.y * _ScrollSpeed;
                fixed4 c = tex2D(_DiffuseTexture, uv) * _TintColor;
                return c;
            }
            ENDCG
        }
    }
}
