using System;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;

[Serializable]
public class SerializedSceneData : ScriptableObject
{
	[Serializable]
	public class CameraData
	{
		[Serializable]
		public class FogData
		{
			[SerializeField]
			public FogConfig.Kind m_kind;

			[SerializeField]
			public float m_fogOffset;

			[SerializeField]
			public float m_fogNear;

			[SerializeField]
			public float m_fogFar = 100f;

			[SerializeField]
			public Color m_fogColour = Color.grey;
		}

		[SerializeField]
		public PostProcessingProfile PostProcessing;

		[SerializeField]
		public FogData Fog = new FogData();
	}

	[Serializable]
	public class RenderData
	{
		[Serializable]
		public class GradientAmbientData
		{
			[SerializeField]
			[ColorUsage(false)]
			public Color SkyColour = Color.white;

			[SerializeField]
			[ColorUsage(false)]
			public Color EquatorColour = Color.white;

			[SerializeField]
			[ColorUsage(false)]
			public Color GroundColour = Color.white;
		}

		[Serializable]
		public class SkyboxAmbientData
		{
			[SerializeField]
			[ColorUsage(false)]
			public Color SkyboxColour = Color.white;
		}

		[Serializable]
		public class ColorAmbientData
		{
			[SerializeField]
			[ColorUsage(false)]
			public Color Colour = Color.white;
		}

		[Serializable]
		public class SkyboxEnvironmentData
		{
			[SerializeField]
			public int Resolution = 32;

			[SerializeField]
			public float IntensityMultiplier = 1f;

			[SerializeField]
			public int Bounces = 5;
		}

		[Serializable]
		public class CustomEnvironmentData
		{
			[SerializeField]
			public Cubemap Cubemap;

			[SerializeField]
			public float IntensityMultiplier = 1f;

			[SerializeField]
			public int Bounces = 5;
		}

		[SerializeField]
		public Material SkyboxMaterial;

		[SerializeField]
		public AmbientMode AmbientSource = AmbientMode.Trilight;

		[HideInInspectorTest("AmbientSource", AmbientMode.Skybox)]
		[SerializeField]
		public SkyboxAmbientData SkyboxData = new SkyboxAmbientData();

		[HideInInspectorTest("AmbientSource", AmbientMode.Trilight)]
		[SerializeField]
		public GradientAmbientData GradientData = new GradientAmbientData();

		[HideInInspectorTest("AmbientSource", AmbientMode.Flat)]
		[SerializeField]
		public ColorAmbientData ColorData = new ColorAmbientData();

		[SerializeField]
		public DefaultReflectionMode ReflectionSource = DefaultReflectionMode.Custom;

		[HideInInspectorTest("ReflectionSource", DefaultReflectionMode.Skybox)]
		[SerializeField]
		public SkyboxEnvironmentData SkyboxReflectionData = new SkyboxEnvironmentData();

		[HideInInspectorTest("ReflectionSource", DefaultReflectionMode.Custom)]
		[SerializeField]
		public CustomEnvironmentData CustomReflectionData = new CustomEnvironmentData();
	}

	[Serializable]
	public class AudioData
	{
		[SerializeField]
		public AudioDirectoryData[] Directories = new AudioDirectoryData[0];

		[SerializeField]
		public AudioClip Music;

		[SerializeField]
		public AudioClip Ambience;
	}

	[Serializable]
	public class SkyColours
	{
		[SerializeField]
		[ColorUsage(false)]
		public Color m_waterColour;

		[SerializeField]
		[ColorUsage(false)]
		public Color m_cloudColour;

		[SerializeField]
		[ColorUsage(false)]
		public Color m_cloudShadowColour;
	}

	[Header("Scene Data")]
	[SerializeField]
	public CameraData CameraSettings = new CameraData();

	[SerializeField]
	public RenderData SceneSettings = new RenderData();

	[Space]
	[SerializeField]
	public AudioData AudioSettings = new AudioData();
}
