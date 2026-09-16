using System;

namespace UnityEngine.PostProcessing
{
	[Serializable]
	public class TiltShiftModel : PostProcessingModel
	{
		[Serializable]
		public struct Settings
		{
			[Range(0f, 1f)]
			[Tooltip("Range of the blur. *Keep low for performance. ~0.4 is good.*")]
			public float BlurArea;

			public bool Downsample;

			[Range(1f, 4f)]
			[Tooltip("Number of iterations. *Performance Warning*")]
			public int Iterations;

			public static Settings defaultSettings
			{
				get
				{
					return new Settings
					{
						BlurArea = 0.4f,
						Downsample = true,
						Iterations = 1
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
