using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerEmoteWheel : ServerSynchroniserBase
{
	private EmoteWheel m_emoteWheel;

	private EmoteWheelMessage m_message = new EmoteWheelMessage();

	protected virtual void Awake()
	{
		m_emoteWheel = base.gameObject.RequireComponent<EmoteWheel>();
		Mailbox.Server.RegisterForMessageType(MessageType.EmoteWheel, ProcessClientMessage);
	}

	protected void ProcessClientMessage(IOnlineMultiplayerSessionUserId _sender, Serialisable _serialisable)
	{
		EmoteWheelMessage emoteWheelMessage = _serialisable as EmoteWheelMessage;
		if (emoteWheelMessage.m_player == m_emoteWheel.m_player && emoteWheelMessage.m_player != PlayerInputLookup.Player.Count && emoteWheelMessage.m_forUI == m_emoteWheel.ForUI)
		{
			StartEmote(_sender, emoteWheelMessage);
		}
	}

	protected virtual void Update()
	{
		if (m_emoteWheel.ForUI && ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			Object.Destroy(this);
		}
	}

	protected void StartEmote(IOnlineMultiplayerSessionUserId _sender, EmoteWheelMessage _message)
	{
		EmoteWheelOption emoteWheelOption = m_emoteWheel.m_emoteWheelOptions.m_options[_message.m_emoteIdx];
		if ((emoteWheelOption.m_type == EmoteWheelOption.EmoteType.Animation || emoteWheelOption.m_type == EmoteWheelOption.EmoteType.Both) && emoteWheelOption.m_triggerForCode)
		{
			if (m_emoteWheel.m_codeTriggerTarget != null)
			{
				m_emoteWheel.m_codeTriggerTarget.SendTrigger(emoteWheelOption.m_animTrigger);
			}
			else
			{
				base.gameObject.SendTrigger(emoteWheelOption.m_animTrigger);
			}
		}
		m_message.InitialiseStartEmote(_message.m_emoteIdx, m_emoteWheel.m_player, _message.m_forUI);
		ServerMessenger.EmoteWheelMessage(m_message);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Server.UnregisterForMessageType(MessageType.EmoteWheel, ProcessClientMessage);
	}
}
