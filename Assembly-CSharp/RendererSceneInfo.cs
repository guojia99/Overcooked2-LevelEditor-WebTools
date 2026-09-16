using UnityEngine;

[ExecuteInEditMode]
public class RendererSceneInfo : MonoBehaviour
{
	[SerializeField]
	public RendererSceneSettings.RendererClass m_rendererClass = RendererSceneSettings.RendererClass.PhysicalAttachment;

	private RendererSceneSettings.Settings? m_settings;

	private Renderer[] m_renderers = new Renderer[0];

	protected void Start()
	{
		UpdateRendererSettings();
		ApplySettings(true);
	}

	protected void UpdateRendererSettings()
	{
		RendererSceneSettings rendererSceneSettings = GameUtils.RequestManager<RendererSceneSettings>();
		RendererSceneSettings.Settings _settings;
		if (rendererSceneSettings != null && rendererSceneSettings.TryGetSettingsForClass(m_rendererClass, out _settings))
		{
			m_settings = _settings;
		}
		else
		{
			m_settings = null;
		}
	}

	public void ApplySettings(bool _updateHirearchy)
	{
		if (!m_settings.HasValue)
		{
			return;
		}
		if (_updateHirearchy)
		{
			if (m_rendererClass == RendererSceneSettings.RendererClass.Avatar)
			{
				m_renderers = base.gameObject.RequestComponentsRecursive<SkinnedMeshRenderer>();
			}
			else
			{
				m_renderers = base.gameObject.RequestComponentsRecursive<Renderer>();
			}
		}
		RendererSceneSettings.Settings _settings = m_settings.Value;
		for (int i = 0; i < m_renderers.Length; i++)
		{
			ApplySettingsToRenderer(m_renderers[i], ref _settings);
		}
	}

	protected void ApplySettingsToRenderer(Renderer _renderer, ref RendererSceneSettings.Settings _settings)
	{
		_renderer.lightProbeUsage = _settings.lightProbeUsage;
		_renderer.reflectionProbeUsage = _settings.reflectionProbeUsage;
		_renderer.probeAnchor = _settings.probeAnchor;
		_renderer.shadowCastingMode = _settings.shadowCastingMode;
		_renderer.receiveShadows = _settings.receiveShadows;
	}
}
