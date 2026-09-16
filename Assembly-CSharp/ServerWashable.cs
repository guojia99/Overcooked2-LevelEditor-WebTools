using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWashable : ServerSynchroniserBase
{
	private struct WashData
	{
		public object Source;

		public float Rate;
	}

	private Washable m_washable;

	private WashableMessage m_data = new WashableMessage();

	private static List<ServerWashable> s_allWashables = new List<ServerWashable>();

	private float m_washDuration;

	private int m_durationMultiplier = 1;

	private List<WashData> m_activeWashers = new List<WashData>();

	private float m_progress;

	private bool m_hasFinished;

	private float m_previousWashRate;

	private List<Generic<bool>> m_allowWashCallbacks = new List<Generic<bool>>();

	private CallbackVoid m_finishedCallback = delegate
	{
	};

	public override EntityType GetEntityType()
	{
		return EntityType.Washable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_washable = (Washable)synchronisedObject;
		m_washDuration = m_washable.m_duration;
		m_durationMultiplier = m_washable.GetWashTimeMultiplier();
	}

	private void SynchroniseWashingProgress(float _progress, float _target)
	{
		m_data.Initialise_Progress(_progress, _target);
		SendServerEvent(m_data);
	}

	private void SynchroniseWashingRate(float _rate)
	{
		m_data.Initialise_Rate(_rate, m_progress);
		SendServerEvent(m_data);
	}

	public static List<ServerWashable> GetAllWashable()
	{
		return s_allWashables;
	}

	public void RegisterAllowWashCallback(Generic<bool> _callback)
	{
		m_allowWashCallbacks.Add(_callback);
	}

	public void UnregisterAllowWashCallback(Generic<bool> _callback)
	{
		m_allowWashCallbacks.Remove(_callback);
	}

	public void RegisterFinishedCallback(CallbackVoid _callback)
	{
		m_finishedCallback = (CallbackVoid)Delegate.Combine(m_finishedCallback, _callback);
	}

	public void UnregisterFinishedCallback(CallbackVoid _callback)
	{
		m_finishedCallback = (CallbackVoid)Delegate.Remove(m_finishedCallback, _callback);
	}

	private void WashingFinishedAchievement()
	{
		for (int i = 0; i < m_activeWashers.Count; i++)
		{
			WashData washData = m_activeWashers[i];
			if (washData.Source != null && washData.Source.GetType() == typeof(ServerWaterGunSpray))
			{
				ServerWaterGunSpray serverWaterGunSpray = (ServerWaterGunSpray)washData.Source;
				GameObject carrier = serverWaterGunSpray.Carrier;
				if (carrier != null)
				{
					ServerStack serverStack = base.gameObject.RequestComponent<ServerStack>();
					ServerMessenger.Achievement(carrier, 101, (!(serverStack != null)) ? 1 : serverStack.GetSize());
				}
			}
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (!HasFinished())
		{
			float num = m_washDuration * (float)m_durationMultiplier;
			float num2 = CalculateWashRate();
			if (num2 > 0f || num2 < 0f)
			{
				m_progress += num2 * TimeManager.GetDeltaTime(base.gameObject.layer);
				m_progress = Mathf.Clamp(m_progress, 0f, num);
			}
			if (m_progress >= num)
			{
				m_finishedCallback();
				m_hasFinished = true;
				SynchroniseWashingProgress(m_progress, m_washDuration);
				WashingFinishedAchievement();
			}
			else if (num2 != m_previousWashRate)
			{
				SynchroniseWashingRate(num2);
				m_previousWashRate = num2;
			}
		}
	}

	private float CalculateWashRate()
	{
		if (!m_allowWashCallbacks.CallForResult(false))
		{
			float num = 0f;
			for (int i = 0; i < m_activeWashers.Count; i++)
			{
				num += m_activeWashers[i].Rate;
			}
			return num;
		}
		return 0f;
	}

	public void StartWashing(object _source, float _rate)
	{
		if (!m_activeWashers.Exists((WashData x) => x.Source == _source))
		{
			m_activeWashers.Add(new WashData
			{
				Source = _source,
				Rate = _rate
			});
		}
	}

	public void StopWashing(object _source)
	{
		m_activeWashers.RemoveAll((WashData x) => x.Source == _source);
	}

	public void SetDuration(float _duration)
	{
		if (m_washDuration != _duration)
		{
			m_washDuration = _duration;
			SynchroniseWashingProgress(m_progress, m_washDuration);
		}
	}

	public bool HasFinished()
	{
		return m_progress >= m_washDuration * (float)m_durationMultiplier;
	}

	private void Awake()
	{
		Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		if (!s_allWashables.Contains(this))
		{
			s_allWashables.Add(this);
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		s_allWashables.Remove(this);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_durationMultiplier = m_washable.GetWashTimeMultiplier();
		}
	}
}
