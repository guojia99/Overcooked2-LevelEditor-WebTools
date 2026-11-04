Shader "Overcooked_2/OC2_gloss_d_hl" {
	Properties {
		_DiffuseMap ("Diffuse Map", 2D) = "white" {}
		_MaskColor ("Mask Color", Vector) = (0.5019608,0.5019608,0.5019608,1)
		_Metallic ("Metallic", Float) = 0.04
		_Roughness ("Roughness", Float) = 0.5
		_Brightness ("Brightness", Range(0, 1)) = 0
		_highlight_colour ("highlight_colour", Vector) = (0.9117647,0.8719588,0.7843858,1)
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