Shader "Overcooked2_Christmas2019/OC2_Standard_Snow" {
	Properties {
		_BaseColourAlpha ("Base Colour/Alpha", 2D) = "bump" {}
		_BaseRMEAO ("Base R/M/E/AO", 2D) = "black" {}
		_BaseNormal ("Base Normal", 2D) = "bump" {}
		_EmissiveColour ("Emissive Colour", Vector) = (1,0,0,1)
		[MaterialToggle] _DiffuseEmissiveToggle ("Diffuse Emissive Toggle", Float) = 1
		_SnowNormal ("Snow Normal", 2D) = "bump" {}
		_SnowSparkle ("Snow Sparkle", 2D) = "white" {}
		_SnowColour ("Snow Colour", Vector) = (0.5568628,0.7098039,0.7882354,1)
		_SnowShadowFalloffColour ("Snow Shadow Falloff Colour", Vector) = (0,0.7921569,1,1)
		_SnowMaskInfluence ("Snow Mask Influence", Float) = 2.5
		_SnowMetal ("Snow Metal", Range(0, 1)) = 0.07
		_SnowGloss ("Snow Gloss", Range(0, 1)) = 1
		_SnowSparkleScale ("Snow Sparkle Scale", Range(0, 5)) = 2.8
		_SnowScreenSpaceScale ("Snow Screen Space Scale", Range(0, 5)) = 2.2
		_SnowNormalIntensity ("Snow Normal Intensity", Range(0, 1)) = 0.5
		_SnowNormalScale ("Snow Normal Scale", Range(0, 5)) = 0.3
		_SnowNormalRotate ("Snow Normal Rotate", Range(0, 6)) = 2.45
		_SnowLightwrap ("Snow Lightwrap", Float) = 0.5
		_SnowDiffuseAdd ("Snow Diffuse Add", Float) = 0.2
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