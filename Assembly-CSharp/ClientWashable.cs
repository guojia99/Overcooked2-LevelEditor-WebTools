using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWashable : ClientSynchroniserBase
{
	private Washable m_washable;

	private float m_progress;

	private float m_rateOfChange;

	private float m_duration;

	private int m_durationMultiplier = 1;

	private ProgressUIController m_progressBar;

	public float ProgressPercent
	{
		get
		{
			return m_progress / m_duration;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.Washable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_washable = (Washable)synchronisedObject;
		m_duration = m_washable.m_duration;
		m_durationMultiplier = m_washable.GetWashTimeMultiplier();
		if (m_washable.m_progressUIPrefab != null)
		{
			GameObject gameObject = GameUtils.InstantiateUIController(m_washable.m_progressUIPrefab.gameObject, "HoverIconCanvas");
			m_progressBar = gameObject.GetComponent<ProgressUIController>();
			m_progressBar.SetFollowTransform(base.transform, Vector3.zero);
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		WashableMessage washableMessage = (WashableMessage)serialisable;
		switch (washableMessage.m_msgType)
		{
		case WashableMessage.MsgType.Progress:
			m_progress = washableMessage.m_progress;
			m_duration = washableMessage.m_target;
			break;
		case WashableMessage.MsgType.Rate:
			m_rateOfChange = washableMessage.m_rate;
			m_progress = washableMessage.m_progress;
			break;
		}
	}

	private void Awake()
	{
		Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		if (m_progressBar != null && m_progressBar.gameObject != null)
		{
			Object.Destroy(m_progressBar.gameObject);
		}
		m_progressBar = null;
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_durationMultiplier = m_washable.GetWashTimeMultiplier();
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_rateOfChange != 0f)
		{
			float num = m_duration * (float)m_durationMultiplier;
			m_progress += m_rateOfChange * TimeManager.GetDeltaTime(base.gameObject.layer);
			m_progress = Mathf.Clamp(m_progress, 0f, num);
			if (m_progress >= 0f && m_progress < num)
			{
				UpdateUIProgressBar();
			}
		}
	}

	private void UpdateUIProgressBar()
	{
		if (m_progressBar != null)
		{
			float progress = Mathf.Clamp01(m_progress / (m_duration * (float)m_durationMultiplier));
			m_progressBar.SetProgress(progress);
		}
	}
}
