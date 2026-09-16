using System;
using UnityEngine;

[ExecutionDependency(typeof(AudioManager))]
[RequireComponent(typeof(MapAvatarControls), typeof(MapAvatarTransformer))]
public class MapAvatarCosmeticDecisions : MonoBehaviour
{
	[Serializable]
	public class MapAvatarEngineTags
	{
		[SerializeField]
		public GameLoopingAudioTag m_landTag = GameLoopingAudioTag.VanEngine;

		[SerializeField]
		public GameLoopingAudioTag m_waterTag = GameLoopingAudioTag.WorldMapBoatEngine;

		[SerializeField]
		public GameLoopingAudioTag m_flyingTag = GameLoopingAudioTag.WorldMapPlaneEngine;
	}

	private class AudioToken
	{
		public GameLoopingAudioTag m_tag;

		public GameObject m_gameObject;

		public AudioToken(GameLoopingAudioTag _tag, GameObject _gameObject)
		{
			m_tag = _tag;
			m_gameObject = _gameObject;
		}
	}

	[SerializeField]
	private MapAvatarEngineTags m_engineTags;

	[SerializeField]
	private float m_minPitch = 1f;

	[SerializeField]
	private float m_maxPitch = 2f;

	[SerializeField]
	private float m_gradientLimit = 1f;

	[SerializeField]
	private float m_timeToMax = 0.1f;

	private MapAvatarControls m_controls;

	private MapAvatarTransformer m_avatarTransformer;

	private AudioManager m_audioManager;

	private AudioSource m_source;

	private GameLoopingAudioTag m_sourceTag = GameLoopingAudioTag.COUNT;

	private float m_pitchGradient;

	private AudioToken m_audioToken;

	private MapAvatarTransformer.VanType m_currentVanType;

	private void Awake()
	{
		m_controls = base.gameObject.RequireComponent<MapAvatarControls>();
		m_avatarTransformer = base.gameObject.RequireComponent<MapAvatarTransformer>();
		m_audioManager = GameUtils.RequireManager<AudioManager>();
		TransitionToVanType(m_avatarTransformer.CurrentType);
	}

	private void TransitionToVanType(MapAvatarTransformer.VanType _type)
	{
		m_currentVanType = _type;
		if (m_sourceTag != GameLoopingAudioTag.COUNT)
		{
			m_audioManager.StopAudio(m_sourceTag, m_audioToken);
			m_audioToken = null;
			m_source = null;
			m_sourceTag = GameLoopingAudioTag.COUNT;
		}
		GameLoopingAudioTag gameLoopingAudioTag = GameLoopingAudioTag.COUNT;
		switch (_type)
		{
		case MapAvatarTransformer.VanType.LAND:
			gameLoopingAudioTag = m_engineTags.m_landTag;
			break;
		case MapAvatarTransformer.VanType.WATER:
			gameLoopingAudioTag = m_engineTags.m_waterTag;
			break;
		case MapAvatarTransformer.VanType.FLYING:
			gameLoopingAudioTag = m_engineTags.m_flyingTag;
			break;
		}
		if (gameLoopingAudioTag != GameLoopingAudioTag.COUNT)
		{
			m_audioToken = new AudioToken(gameLoopingAudioTag, base.gameObject);
			m_source = m_audioManager.StartAudio(gameLoopingAudioTag, m_audioToken, base.gameObject.layer);
			m_sourceTag = gameLoopingAudioTag;
		}
	}

	public float GetClampedMovementSpeed()
	{
		return Mathf.Clamp01(m_controls.GetUnclampedMovementSpeed());
	}

	private void Update()
	{
		if (m_currentVanType != m_avatarTransformer.CurrentType)
		{
			TransitionToVanType(m_avatarTransformer.CurrentType);
		}
		if (m_source != null)
		{
			float clampedMovementSpeed = GetClampedMovementSpeed();
			float nTargetX = MathUtils.Remap(clampedMovementSpeed, 0f, 1f, m_minPitch, m_maxPitch);
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			float _nCurrentX = m_source.pitch;
			MathUtils.AdvanceToTarget_Sinusoidal(ref _nCurrentX, ref m_pitchGradient, nTargetX, m_gradientLimit, m_timeToMax, deltaTime);
			m_source.pitch = _nCurrentX;
		}
	}
}
