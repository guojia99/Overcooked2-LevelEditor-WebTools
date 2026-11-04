Shader "Overcooked2_Circus/OC2_Ground" {
	Properties {
		_ForegroundScale ("Foreground Scale", Float) = 1
		_BackgroundScale ("Background Scale", Float) = 1
		_Mask ("Mask", 2D) = "black" {}
		_ForegroundColour ("Foreground Colour", 2D) = "white" {}
		_ForegroundNormals ("Foreground Normals", 2D) = "bump" {}
		_ForegroundRMEAO ("Foreground R/M/E/AO", 2D) = "black" {}
		_BackgroundColour ("Background Colour", 2D) = "white" {}
		_BackgroundNormals ("Background Normals", 2D) = "bump" {}
		_BackgroundRMEAO ("Background R/M/E/AO", 2D) = "black" {}
		_node_8555 ("node_8555", Float) = 0
		_node_7662 ("node_7662", Float) = 1
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