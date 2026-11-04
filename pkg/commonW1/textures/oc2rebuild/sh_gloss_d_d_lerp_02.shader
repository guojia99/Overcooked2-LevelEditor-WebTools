Shader "Overcooked_2/OC2_sand_mask" {
	Properties {
		_Diffuse ("Diffuse", 2D) = "white" {}
		_Colour ("Colour", Vector) = (0.5,0.5,0.5,1)
		_Normals ("Normals", 2D) = "bump" {}
		_Metal ("Metal", Range(0, 1)) = 0
		_Gloss ("Gloss", Range(0, 1)) = 0
		_DetaillMap ("Detaill Map", 2D) = "white" {}
		_DetailScale ("Detail Scale", Range(0, 10)) = 0
		_DetailRotate ("Detail Rotate", Range(0, 6.4)) = 0
		_SandColour ("Sand Colour", Vector) = (0.5,0.5,0.5,1)
		_SandNoise ("Sand Noise", 2D) = "white" {}
		_SandDetailMap ("Sand Detail Map", 2D) = "white" {}
		_SandDetailScale ("Sand Detail Scale", Range(0, 10)) = 0
		_SandDetailRotate ("Sand Detail Rotate", Range(0, 6.4)) = 0
		_Mask ("Mask", 2D) = "white" {}
		_SandDetalSpecPower ("SandDetalSpecPower", Float) = 0
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