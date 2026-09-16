using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCampfireCosmeticDecisions : ClientSynchroniserBase
{
	private CampfireCosmeticDecisions m_cosmetics;

	private ClientAttachStation m_attachStation;

	private ClientHeatedStation m_heatedStation;

	private AudioManager m_audioManager;

	private ParticleSystem[] m_fireParticles = new ParticleSystem[0];

	private object m_highHeatToken = new object();

	private object m_mediumHeatToken = new object();

	private object m_activeToken;

	public override void StartSynchronising(Component _synchronisedObject)
	{
		base.StartSynchronising(_synchronisedObject);
		m_cosmetics = (CampfireCosmeticDecisions)_synchronisedObject;
		m_heatedStation = base.gameObject.RequireComponent<ClientHeatedStation>();
		m_attachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_heatedStation.RegisterHeatRangeChangedCallback(OnHeatRangeChanged);
		ParticleSystem[] array = base.gameObject.RequestComponentsRecursive<ParticleSystem>();
		m_fireParticles = array.AllRemoved_Predicate((ParticleSystem x) => !x.collision.enabled);
		ToggleFireCollision(false);
		UpdateVisuals(HeatRange.Low);
		m_audioManager = GameUtils.RequestManager<AudioManager>();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_heatedStation != null)
		{
			m_heatedStation.UnregisterHeatRangeChangedCallback(OnHeatRangeChanged);
		}
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_audioManager != null && m_activeToken != null)
		{
			m_audioManager.StopAudio(GameLoopingAudioTag.COUNT, m_activeToken);
		}
	}

	private void OnItemAdded(IClientAttachment _attachment)
	{
		ToggleFireCollision(true);
	}

	private void OnItemRemoved(IClientAttachment _attachment)
	{
		ToggleFireCollision(false);
	}

	private void OnHeatRangeChanged(HeatRange _heatRange)
	{
		UpdateVisuals(_heatRange);
		UpdateAudio(_heatRange);
	}

	private void UpdateVisuals(HeatRange _heat)
	{
		ToggleEffect(m_cosmetics.m_highVisuals, _heat == HeatRange.High);
		ToggleEffect(m_cosmetics.m_mediumVisuals, _heat == HeatRange.Moderate);
		ToggleEffect(m_cosmetics.m_lowVisuals, _heat == HeatRange.Low);
	}

	private void UpdateAudio(HeatRange _heat)
	{
		if (m_activeToken != null)
		{
			m_audioManager.StopAudio(GameLoopingAudioTag.COUNT, m_activeToken);
		}
		switch (_heat)
		{
		case HeatRange.High:
			m_activeToken = m_highHeatToken;
			m_audioManager.StartAudio(GameLoopingAudioTag.DLC_05_Fire_Lrg, m_activeToken, base.gameObject.layer);
			break;
		case HeatRange.Moderate:
			m_activeToken = m_mediumHeatToken;
			m_audioManager.StartAudio(GameLoopingAudioTag.DLC_05_Fire_Med, m_activeToken, base.gameObject.layer);
			break;
		default:
			m_activeToken = null;
			m_audioManager.TriggerAudio(GameOneShotAudioTag.DLC_05_Fire_Hiss, base.gameObject.layer);
			break;
		}
	}

	private void ToggleEffect(GameObject _effect, bool _turnOn)
	{
		if (_effect != null)
		{
			_effect.SetActive(_turnOn);
		}
	}

	private void ToggleFireCollision(bool _collide)
	{
		for (int i = 0; i < m_fireParticles.Length; i++)
		{
			ParticleSystem.CollisionModule collision = m_fireParticles[i].collision;
			collision.enabled = _collide;
		}
	}
}
