Shader "Overcooked_2/OC2_vortex_distortion" {
	Properties {
		_Refraction ("Refraction", Float) = 1
		_Diffuse_Distortion ("Diffuse_Distortion", Float) = 1
		_Diffuse_Map ("Diffuse_Map", 2D) = "white" {}
		_opacity ("opacity", Float) = 1
		_Rotation_speed ("Rotation_speed", Float) = 1
		_time ("time", Float) = 1
		_Diffuse_Alpha ("Diffuse_Alpha", 2D) = "white" {}
		_Normal ("Normal", 2D) = "bump" {}
		_NormalMap1 ("Normal Map 1", 2D) = "bump" {}
		_scroll_speed_1 ("scroll_speed_1", Float) = 0
		_NormalMap2 ("Normal Map 2", 2D) = "bump" {}
		_scroll_speed_2 ("scroll_speed_2", Float) = 0
		_warp_whril ("warp_whril", Float) = 0.3
		_alpha_flatten ("alpha_flatten", Float) = 0
		[HideInInspector] _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Diffuse"
	//CustomEditor "ShaderForgeMaterialInspector"
}