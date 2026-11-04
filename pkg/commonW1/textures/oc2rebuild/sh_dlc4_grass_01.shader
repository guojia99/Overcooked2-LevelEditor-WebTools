Shader "Overcooked_2_NewYearsDLC/OC2_Grass" {
	Properties {
		_Diffuse ("Diffuse", 2D) = "white" {}
		_Normal ("Normal", 2D) = "bump" {}
		_Metal ("Metal", Range(0, 1)) = 0
		_Gloss ("Gloss", Range(0, 1)) = 0
		_OverallWindSpeed ("Overall Wind Speed", Float) = 0.5
		_OverallWindAmplitude ("Overall Wind Amplitude", Float) = 1
		_IndividualWindSpeed ("Individual Wind Speed", Float) = 1.5
		_IndividualAmplitude ("Individual Amplitude", Float) = 0.03
		_WindDirection ("Wind Direction", Vector) = (1,0.5,0.5,0)
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