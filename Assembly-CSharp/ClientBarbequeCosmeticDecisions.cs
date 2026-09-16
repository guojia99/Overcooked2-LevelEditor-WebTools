using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientBarbequeCosmeticDecisions : ClientSynchroniserBase
{
	private BarbequeCosmeticDecisions m_cosmetics;

	private ClientHeatedStation m_heatedStation;

	private ClientAttachStation m_attachStation;

	private AudioManager m_audioManager;

	private object m_highHeatToken = new object();

	private object m_mediumHeatToken = new object();

	private object m_activeToken;

	public override void StartSynchronising(Component _synchronisedObject)
	{
		base.StartSynchronising(_synchronisedObject);
		m_cosmetics = (BarbequeCosmeticDecisions)_synchronisedObject;
		m_attachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(ItemAdded);
		m_heatedStation = base.gameObject.RequireComponent<ClientHeatedStation>();
		m_heatedStation.RegisterHeatRangeChangedCallback(OnHeatRangeChanged);
		UpdateVisuals(HeatRange.Low);
		m_audioManager = GameUtils.RequestManager<AudioManager>();
	}

	private void ItemAdded(IClientAttachment _item)
	{
		GameObject gameObject = _item.AccessGameObject();
		if (gameObject != null)
		{
			gameObject.transform.localRotation = Quaternion.identity;
		}
	}

	private void OnHeatRangeChanged(HeatRange _heatRange)
	{
		UpdateVisuals(_heatRange);
		UpdateAudio(_heatRange);
	}

	private void UpdateVisuals(HeatRange _heat)
	{
		ToggleEffect(m_cosmetics.m_highEffect, _heat == HeatRange.High);
		ToggleEffect(m_cosmetics.m_mediumEffect, _heat == HeatRange.Moderate);
		ToggleEffect(m_cosmetics.m_lowEffect, _heat == HeatRange.Low);
	}

	private void UpdateAudio(HeatRange _heat)
	{
		if (m_activeToken != null)
		{
			GameUtils.StopAudio(GameLoopingAudioTag.COUNT, m_activeToken);
		}
		switch (_heat)
		{
		case HeatRange.High:
			m_activeToken = m_highHeatToken;
			GameUtils.StartAudio(GameLoopingAudioTag.DLC_02_BBQ_Idle, m_activeToken, base.gameObject.layer);
			break;
		case HeatRange.Moderate:
			m_activeToken = m_mediumHeatToken;
			GameUtils.StartAudio(GameLoopingAudioTag.DLC_02_BBQ_Smoke, m_activeToken, base.gameObject.layer);
			break;
		default:
			m_activeToken = null;
			GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_02_BBQ_Death, base.gameObject.layer);
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

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_audioManager != null && m_activeToken != null)
		{
			m_audioManager.StopAudio(GameLoopingAudioTag.COUNT, m_activeToken);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterOnItemAdded(ItemAdded);
		}
		if (m_heatedStation != null)
		{
			m_heatedStation.UnregisterHeatRangeChangedCallback(OnHeatRangeChanged);
		}
	}
}
