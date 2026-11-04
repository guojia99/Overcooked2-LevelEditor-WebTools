Shader "Overcooked_2/OC2_sanddecal" {
	Properties {
		_DiffuseBlend ("Diffuse Blend", Vector) = (0.4980392,0.4980392,0.4980392,1)
		_Metal ("Metal", Float) = 0
		_Gloss ("Gloss", Float) = 0
		_Detail ("Detail", 2D) = "white" {}
		_DetailTile ("Detail Tile", Range(0, 4)) = 0
		_DetailRotation ("Detail Rotation", Range(0, 1)) = 0
		_Noise ("Noise", 2D) = "white" {}
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