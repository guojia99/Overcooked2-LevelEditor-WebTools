Shader "Overcooked_2_WinterDLC/OC2_Snow" {
	Properties {
		_Colour ("Colour", Vector) = (0.5,0.5,0.5,1)
		_Metal ("Metal", Range(0, 1)) = 0
		_Gloss ("Gloss", Range(0, 1)) = 0
		_Normal ("Normal", 2D) = "bump" {}
		_NormalIntensity ("Normal Intensity", Range(0, 1)) = 0
		_NormalScale ("Normal Scale", Range(0, 5)) = 0
		_NormalRotate ("Normal Rotate", Range(0, 6)) = 0
		_Sparkle ("Sparkle", 2D) = "white" {}
		_SparkleMask ("Sparkle Mask", 2D) = "white" {}
		_SparkleScale ("Sparkle Scale", Range(0, 5)) = 0
		_ScreenSpaceScale ("Screen Space Scale", Range(0, 5)) = 0
		_LightWrapping ("Light Wrapping", Vector) = (0.5,0.5,0.5,1)
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