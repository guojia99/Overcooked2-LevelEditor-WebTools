using System;
using UnityEngine;
using UnityEngine.Rendering;

public class RendererSceneSettings : Manager
{
	public enum RendererClass
	{
		Invalid = -1,
		Avatar = 0,
		PhysicalAttachment = 1,
		Plate = 2,
		MealCosmetic = 3
	}

	[Serializable]
	public struct Settings
	{
		[SerializeField]
		public LightProbeUsage lightProbeUsage;

		[SerializeField]
		public ReflectionProbeUsage reflectionProbeUsage;

		[SerializeField]
		public Transform probeAnchor;

		[SerializeField]
		public ShadowCastingMode shadowCastingMode;

		[SerializeField]
		public bool receiveShadows;
	}

	[HideInInspector]
	[SerializeField]
	private Settings[] m_settings = new Settings[0];

	[HideInInspector]
	[SerializeField]
	private bool[] m_enabled = new bool[0];

	public bool TryGetSettingsForClass(RendererClass _class, out Settings _settings)
	{
		int num = Mathf.Min(m_enabled.Length, m_settings.Length);
		if (_class >= RendererClass.Avatar && (int)_class < num && m_enabled[(int)_class])
		{
			_settings = m_settings[(int)_class];
			return true;
		}
		_settings = default(Settings);
		return false;
	}
}
