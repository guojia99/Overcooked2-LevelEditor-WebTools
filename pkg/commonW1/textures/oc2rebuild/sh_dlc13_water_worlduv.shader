Shader "Overcooked2_MoonFestival/Water_Night" {
	Properties {
		_Diffuse_Map ("Diffuse_Map", 2D) = "white" {}
		_Colour ("Colour", Vector) = (0.5,0.5,0.5,1)
		_NormalMap1 ("Normal Map 1", 2D) = "bump" {}
		_NormalMap2 ("Normal Map 2", 2D) = "bump" {}
		_Diffuse_Speed ("Diffuse_Speed", Float) = 0
		_Diffuse_Distortion ("Diffuse_Distortion", Float) = 0
		_SpecPower ("Spec Power", Float) = 0.3
		_SpecPower_copy ("Spec Power_copy", Float) = 0.3
		_scroll_speed_1 ("scroll_speed_1", Float) = 0
		_scroll_speed_2 ("scroll_speed_2", Float) = 0
		_node_2103 ("node_2103", 2D) = "white" {}
		_node_3424 ("node_3424", 2D) = "white" {}
		_node_3904 ("node_3904", Float) = 0
		_node_42 ("node_42", Float) = 0
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