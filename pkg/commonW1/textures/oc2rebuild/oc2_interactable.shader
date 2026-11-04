Shader "Overcooked2_Wilderness/OC2_Interactable" {
	Properties {
		_ColourAlpha ("Colour/Alpha", 2D) = "white" {}
		_RMEAO ("R/M/E/AO", 2D) = "black" {}
		_Normal ("Normal", 2D) = "bump" {}
		_EmissiveColour ("Emissive Colour", Vector) = (0.5,0.5,0.5,1)
		[MaterialToggle] _DiffuseEmissiveToggle ("Diffuse Emissive Toggle", Float) = 0.5
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