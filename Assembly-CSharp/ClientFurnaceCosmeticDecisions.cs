using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientFurnaceCosmeticDecisions : ClientSynchroniserBase
{
	private FurnaceCosmeticDecisions m_cosmetics;

	private ClientHeatedStation m_heatedStation;

	private AudioManager m_audioManager;

	private object m_highHeatToken = new object();

	private object m_mediumHeatToken = new object();

	private object m_activeToken;

	public override void StartSynchronising(Component _synchronisedObject)
	{
		base.StartSynchronising(_synchronisedObject);
		m_cosmetics = (FurnaceCosmeticDecisions)_synchronisedObject;
		m_heatedStation = base.gameObject.RequireComponent<ClientHeatedStation>();
		m_heatedStation.RegisterHeatRangeChangedCallback(OnHeatRangeChanged);
		m_heatedStation.RegisterOnItemAddedCallback(OnItemAdded);
		UpdateVisuals(HeatRange.Low);
		m_audioManager = GameUtils.RequestManager<AudioManager>();
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		float value = MathUtils.ClampedRemap(m_heatedStation.HeatValue, 0f, 1f, m_cosmetics.m_heatedAnimatorMinSpeed, m_cosmetics.m_heatedAnimatorMaxSpeed);
		for (int i = 0; i < m_cosmetics.m_heatedAnimators.Length; i++)
		{
			if (m_cosmetics.m_heatedAnimators[i] != null)
			{
				m_cosmetics.m_heatedAnimators[i].SetFloat(FurnaceCosmeticDecisions.s_heatedAnimatorParameterHash, value);
			}
		}
	}

	private void OnItemAdded()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Coal_Dispense, base.gameObject.layer);
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
			GameUtils.StartAudio(GameLoopingAudioTag.DLC_07_Furnace_Burn_Lrg, m_activeToken, base.gameObject.layer);
			break;
		case HeatRange.Moderate:
			m_activeToken = m_mediumHeatToken;
			GameUtils.StartAudio(GameLoopingAudioTag.DLC_07_Furnace_Burn_Med, m_activeToken, base.gameObject.layer);
			break;
		default:
			m_activeToken = null;
			GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Furnace_Die, base.gameObject.layer);
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
		if (m_heatedStation != null)
		{
			m_heatedStation.UnregisterHeatRangeChangedCallback(OnHeatRangeChanged);
			m_heatedStation.UnregisterOnItemAddedCallback(OnItemAdded);
		}
	}
}
