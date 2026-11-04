Shader "Overcooked_2/OC2_water_foam_02" {
	Properties {
		_MaskColor ("Mask Color", Vector) = (0.3529412,0.7254902,0.7960785,1)
		_DiffuseMap ("Diffuse Map", 2D) = "white" {}
		_WorldUVScale ("World UV Scale", Float) = 0
		_Speed ("Speed", Float) = 0
		_Alpha ("Alpha", 2D) = "white" {}
		_Alpha_Threshold_1 ("Alpha_Threshold_1", Range(0, 1)) = 0.3
		_DiffuseMap_2 ("Diffuse Map_2", 2D) = "white" {}
		_Speed_2 ("Speed_2", Float) = 0
		_WorldUVScale2 ("World UV Scale 2", Float) = 0
		_node_3389 ("node_3389", Float) = 3
		_MaskColor_copy ("Mask Color_copy", Vector) = (0.7843138,1,0.9215687,1)
		_node_9344_copy ("node_9344_copy", Range(0, 1)) = 0
		_Alpha_copy ("Alpha_copy", 2D) = "white" {}
		_node_3389_copy ("node_3389_copy", Float) = 3
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