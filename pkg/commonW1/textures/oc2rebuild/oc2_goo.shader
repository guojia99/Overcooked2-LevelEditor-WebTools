Shader "Overcooked2_Medieval/OC2_Goo" {
	Properties {
		_Colour1 ("Colour 1", Vector) = (0.5,0.5,0.5,1)
		_Colour2 ("Colour 2", Vector) = (0.5,0.5,0.5,1)
		_Cubemap ("Cubemap", Cube) = "_Skybox" {}
		_Waves ("Waves", 2D) = "bump" {}
		_WavesScale ("Waves Scale", Float) = 0.25
		_Metal ("Metal", Range(0, 1)) = 0
		_Gloss ("Gloss", Range(0, 1)) = 0
		_WavesSpeedU ("Waves Speed U", Float) = 0.25
		_WavesSpeedV ("Waves Speed V", Float) = -0.25
		_DistortionStrength ("Distortion Strength", Range(1, 0)) = 1
		_DistortionScale ("Distortion Scale", Float) = 1
		_DistortionSpeed ("Distortion Speed", Float) = 0
		_Bubbles ("Bubbles", 2D) = "bump" {}
		_BubbleMask ("Bubble Mask", 2D) = "black" {}
		_BubbleColour ("Bubble Colour", Vector) = (0.5,0.5,0.5,1)
		_BubbleSpeed ("Bubble Speed", Float) = 0.1
		_BubbleScale ("Bubble Scale", Float) = 2.1
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