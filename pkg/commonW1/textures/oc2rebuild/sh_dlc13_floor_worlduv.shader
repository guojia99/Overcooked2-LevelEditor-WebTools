Shader "Overcooked2_MoonFestival/Floor_World_RMEA_n" {
	Properties {
		_Albedo ("Albedo", 2D) = "white" {}
		_Detail ("Detail", 2D) = "white" {}
		_DetailRotate ("Detail Rotate", Float) = 0
		_RMEA ("R/M/E/A", 2D) = "white" {}
		_EmissiveColour ("Emissive Colour", Vector) = (0,0,0,1)
		[MaterialToggle] _DiffuseEmissiveColour ("Diffuse Emissive Colour", Float) = 0
		_Normal ("Normal", 2D) = "bump" {}
		_DetailScale ("Detail Scale", Float) = 0
		_ScaleOfMaintext ("Scale Of Main text", Float) = 0
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