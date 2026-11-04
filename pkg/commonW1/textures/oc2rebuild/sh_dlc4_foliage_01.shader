Shader "Overcooked_2_NewYearsDLC/OC2_Foliage" {
	Properties {
		_Diffuse ("Diffuse", 2D) = "white" {}
		_Metal ("Metal", Range(0, 1)) = 0
		_Gloss ("Gloss", Range(0, 1)) = 0
		_ColourID ("Colour ID", 2D) = "white" {}
		_RedFresnel ("Red Fresnel", Vector) = (0,0,0,1)
		_RedTop ("Red Top", Vector) = (1,1,1,1)
		_RedBottom ("Red Bottom", Vector) = (0,0,0,1)
		_PinkFresnel ("Pink Fresnel", Vector) = (0,0,0,1)
		_PinkBottom ("Pink Bottom", Vector) = (0,0,0,1)
		_PinkTop ("Pink Top", Vector) = (1,1,1,0.366)
		_GreenFesnel ("Green Fesnel", Vector) = (0,0,0,1)
		_GreenTop ("Green Top", Vector) = (1,1,1,1)
		_GreenBottom ("Green Bottom", Vector) = (0,0,0,1)
		_WindDirection ("Wind Direction", Vector) = (1,0.5,0.5,0)
		_Wind ("Wind", Vector) = (0.32,0.34,0.54,0.08)
		_Shadow ("Shadow", Vector) = (0.9,0.42,0.76,0)
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