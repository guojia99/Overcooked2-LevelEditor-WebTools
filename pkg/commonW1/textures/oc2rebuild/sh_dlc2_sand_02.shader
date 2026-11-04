Shader "Overcooked_2/OC2_sand_02" {
	Properties {
		_DiffuseBlend ("Diffuse Blend", Vector) = (0.4980392,0.4980392,0.4980392,1)
		_NormalMap ("Normal Map", 2D) = "bump" {}
		_Detail ("Detail", 2D) = "white" {}
		_DetailTile ("Detail Tile", Range(0, 1)) = 0
		_DetailRotation ("Detail Rotation", Range(0, 1)) = 0
		_Noise ("Noise", 2D) = "white" {}
		_SandDuneIntensity ("Sand Dune Intensity", Range(0, 1)) = 0
		_SandDuneRotator ("Sand Dune Rotator", Range(0, 6.3)) = 6.3
		_BlendPosition ("Blend Position", Float) = 0
		_BlendWidth ("Blend Width", Float) = 0
		_BlendRotation ("Blend Rotation", Range(0, 6.3)) = 0
		_EmissiveBlend ("Emissive Blend", 2D) = "white" {}
		_EmissiveMultiplier ("Emissive Multiplier", Float) = 0
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