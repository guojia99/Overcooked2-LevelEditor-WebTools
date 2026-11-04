Shader "Overcooked2_Wilderness/OC2_Ground" {
	Properties {
		_Mask ("Mask", 2D) = "black" {}
		_RedColourHeight ("Red Colour/Height", 2D) = "white" {}
		_RedRMAO ("Red R/M/AO", 2D) = "white" {}
		_RedNormal ("Red Normal", 2D) = "bump" {}
		_RedScale ("Red Scale", Float) = 1
		_GreenColourHeight ("Green Colour/Height", 2D) = "white" {}
		_GreenRGAO ("Green R/G/AO", 2D) = "bump" {}
		_GreenNormal ("Green Normal", 2D) = "bump" {}
		_GreenScale ("Green Scale", Float) = 1
		_BlueColourHeight ("Blue Colour/Height", 2D) = "white" {}
		_BlueRMAO ("Blue R/M/AO", 2D) = "white" {}
		_BlueNormal ("Blue Normal", 2D) = "bump" {}
		_BlueScale ("Blue Scale", Float) = 1
		_Detail ("Detail", 2D) = "gray" {}
		_DetailScale ("Detail Scale", Float) = 1
		_DetailRotate ("Detail Rotate", Float) = 0
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