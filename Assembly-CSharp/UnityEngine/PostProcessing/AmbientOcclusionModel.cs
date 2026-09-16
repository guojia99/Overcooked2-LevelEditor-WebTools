using System;

namespace UnityEngine.PostProcessing
{
	[Serializable]
	public class AmbientOcclusionModel : PostProcessingModel
	{
		public enum SampleCount
		{
			Lowest = 3,
			Low = 6,
			Medium = 10,
			High = 16
		}

		[Serializable]
		public struct Settings
		{
			[Range(0f, 4f)]
			[Tooltip("Degree of darkness produced by the effect.")]
			public float intensity;

			[Range(0.005f, 0.05f)]
			[Tooltip("Radius of sample points, which affects extent of darkened areas.")]
			public float radius;

			[Tooltip("Number of sample points, which affects quality and performance.")]
			public SampleCount sampleCount;

			[Tooltip("Halves the resolution of the effect to increase performance at the cost of visual quality.")]
			public bool downsampling;

			[Tooltip("Rotation Texture.")]
			public Texture2D RotationTexture;

			public float blurSize;

			public float distanceFalloff;

			public static Settings defaultSettings
			{
				get
				{
					return new Settings
					{
						intensity = 1f,
						radius = 0.3f,
						sampleCount = SampleCount.Medium,
						downsampling = true,
						distanceFalloff = 4f
					};
				}
			}
		}

		[SerializeField]
		private Settings m_Settings = Settings.defaultSettings;

		public Settings settings
		{
			get
			{
				return m_Settings;
			}
			set
			{
				m_Settings = value;
			}
		}

		public override void Reset()
		{
			m_Settings = Settings.defaultSettings;
		}
	}
}
