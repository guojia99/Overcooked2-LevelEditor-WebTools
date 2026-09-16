using System;
using UnityEngine;

public class QualitySettingsOverride : MonoBehaviour
{
	[Serializable]
	public class ShadowSettings
	{
		public ShadowQuality m_quality;

		public ShadowResolution m_resolution;

		public ShadowProjection m_projection;

		public float m_distance;
	}

	public ShadowSettings m_shadowOverrides;

	private ShadowSettings m_shadowCachedSettings;

	private void CacheSettings()
	{
		m_shadowCachedSettings = new ShadowSettings();
		m_shadowCachedSettings.m_quality = QualitySettings.shadows;
		m_shadowCachedSettings.m_resolution = QualitySettings.shadowResolution;
		m_shadowCachedSettings.m_projection = QualitySettings.shadowProjection;
		m_shadowCachedSettings.m_distance = QualitySettings.shadowDistance;
	}

	private void ApplySettings(ShadowSettings settings)
	{
		QualitySettings.shadows = settings.m_quality;
		QualitySettings.shadowResolution = settings.m_resolution;
		QualitySettings.shadowProjection = settings.m_projection;
		QualitySettings.shadowDistance = settings.m_distance;
	}

	private void Awake()
	{
		CacheSettings();
		ApplySettings(m_shadowOverrides);
	}

	private void OnDestroy()
	{
		ApplySettings(m_shadowCachedSettings);
	}
}
