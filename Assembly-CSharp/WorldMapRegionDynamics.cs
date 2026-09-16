using System;
using System.Collections;
using UnityEngine;
using UnityEngine.PostProcessing;

public class WorldMapRegionDynamics : MonoBehaviour
{
	[Serializable]
	public class TransitionData
	{
		[SerializeField]
		public float m_duration = 1f;

		[SerializeField]
		public AnimationCurve m_curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		[SerializeField]
		[Mask(typeof(PostProcessingBehaviourUtils.LerpFilter))]
		public int m_postProcLerpFilter = 2147482623;
	}

	[SerializeField]
	[AssignComponent(Editorbility.Editable)]
	private WorldMapRegionTransitioner m_transitioner;

	[SerializeField]
	public TransitionData m_transitionData;

	private IEnumerator m_transitionRoutine;

	[SerializeField]
	public Camera m_camera;

	private PostProcessingBehaviour m_postProcessing;

	[SerializeField]
	private PersistentMusic m_MusicSource;

	[SerializeField]
	private AudioSource m_AmbienceSource;

	[SerializeField]
	private Renderer m_skyRenderer;

	private Material[] m_skyMaterials;

	private readonly int m_skyWaterID = Shader.PropertyToID("_Water_col");

	private readonly int m_skyCloudID = Shader.PropertyToID("_Cloud_col");

	private readonly int m_skyCloudShadowID = Shader.PropertyToID("_Cloud_Shadow_col");

	private void Awake()
	{
		if (m_transitioner != null)
		{
			m_transitioner.RegisterRegionChangeCallback(OnRegionChanged);
		}
		if (m_camera != null && m_camera.gameObject != null)
		{
			PostProcessingBehaviour postProcessingBehaviour = m_camera.gameObject.RequestComponent<PostProcessingBehaviour>();
			if (postProcessingBehaviour != null)
			{
				m_postProcessing = postProcessingBehaviour;
			}
		}
		if (m_skyRenderer != null)
		{
			m_skyMaterials = m_skyRenderer.materials.AllRemoved_Predicate((Material material) => material == null || !material.HasProperty(m_skyWaterID) || !material.HasProperty(m_skyCloudID) || !material.HasProperty(m_skyCloudShadowID));
		}
	}

	private void OnRegionChanged(WorldMapRegion _oldRegion, WorldMapRegion _newRegion)
	{
		IEnumerator enumerator = TransitionRoutine(_oldRegion, _newRegion);
		enumerator.MoveNext();
		m_transitionRoutine = enumerator;
	}

	private IEnumerator TransitionRoutine(WorldMapRegion _oldRegion, WorldMapRegion _newRegion)
	{
		int layer = base.gameObject.layer;
		float time = 0f;
		float progress = 0f;
		bool passedHalfway = false;
		SerializedSceneData.RenderData existingRenderData = StartScreenBackgroundDataUtils.CopyCurrentRenderData();
		PostProcessingProfile existingPostProcProfile = CopyCurrentPostProcessingProfile(_oldRegion);
		do
		{
			time = Mathf.Min(time + TimeManager.GetDeltaTime(layer), m_transitionData.m_duration);
			float num;
			if (m_transitionData.m_duration > 0f)
			{
				num = Mathf.Clamp01(m_transitionData.m_curve.Evaluate(time / m_transitionData.m_duration));
			}
			else
			{
				float num2 = 1f;
				num = num2;
			}
			progress = num;
			if (progress >= 0.5f && !passedHalfway)
			{
				StartScreenBackgroundDataUtils.SwapBackgroundAudio(_newRegion.m_data.AudioSettings, ref m_MusicSource, ref m_AmbienceSource, false);
				passedHalfway = true;
			}
			StartScreenBackgroundDataUtils.LerpRenderData(existingRenderData, _newRegion.m_data.SceneSettings, progress);
			if (m_skyMaterials != null)
			{
				SerializedSceneData.SkyColours skyColourSettings = _newRegion.m_data.SkyColourSettings;
				SerializedSceneData.SkyColours start = ((!(_oldRegion != null)) ? skyColourSettings : _oldRegion.m_data.SkyColourSettings);
				for (int i = 0; i < m_skyMaterials.Length; i++)
				{
					Material _material = m_skyMaterials[i];
					LerpSky(start, skyColourSettings, ref _material, progress);
				}
			}
			if (existingPostProcProfile != null && _newRegion.m_data.CameraSettings.PostProcessing != null)
			{
				bool flag = existingPostProcProfile.motionBlur.enabled;
				PostProcessingBehaviourUtils.Lerp(existingPostProcProfile, _newRegion.m_data.CameraSettings.PostProcessing, ref m_postProcessing.profile, progress, m_transitionData.m_postProcLerpFilter);
				existingPostProcProfile.motionBlur.enabled = flag;
			}
			yield return null;
		}
		while (time < m_transitionData.m_duration);
	}

	private void LerpSky(SerializedSceneData.SkyColours _start, SerializedSceneData.SkyColours _end, ref Material _material, float _progress)
	{
		_material.SetColor(m_skyWaterID, Color.Lerp(_start.m_waterColour, _end.m_waterColour, _progress));
		_material.SetColor(m_skyCloudID, Color.Lerp(_start.m_cloudColour, _end.m_cloudColour, _progress));
		_material.SetColor(m_skyCloudShadowID, Color.Lerp(_start.m_cloudShadowColour, _end.m_cloudShadowColour, _progress));
	}

	private PostProcessingProfile CopyCurrentPostProcessingProfile(WorldMapRegion _oldRegion)
	{
		if ((m_transitionRoutine != null || _oldRegion == null) && m_postProcessing != null)
		{
			return UnityEngine.Object.Instantiate(m_postProcessing.profile);
		}
		if (_oldRegion != null && _oldRegion.m_data != null && _oldRegion.m_data.CameraSettings.PostProcessing != null)
		{
			return _oldRegion.m_data.CameraSettings.PostProcessing;
		}
		return null;
	}

	private void Update()
	{
		if (m_transitionRoutine != null && !m_transitionRoutine.MoveNext())
		{
			m_transitionRoutine = null;
		}
	}

	private void OnDestroy()
	{
		if (m_transitioner != null)
		{
			m_transitioner.UnregisterRegionChangeCallback(OnRegionChanged);
		}
	}
}
