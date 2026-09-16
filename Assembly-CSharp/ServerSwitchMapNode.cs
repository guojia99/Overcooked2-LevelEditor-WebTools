using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerSwitchMapNode : ServerSynchroniserBase
{
	private SwitchMapNode m_baseObject;

	private SwitchMapNodeMessage m_Message = new SwitchMapNodeMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (SwitchMapNode)synchronisedObject;
		if (!m_baseObject.IsSwitchPressed() && m_baseObject.IsSwitchedDueToCompletion())
		{
			GameSession gameSession = GameUtils.GetGameSession();
			gameSession.Progress.RecordSwitchActivated(m_baseObject.SwitchID);
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.SwitchMapNode;
	}

	public void OnSwitchPressed(MapAvatarControls _avatar, WorldMapSwitch _switch)
	{
		SendServerEvent(m_Message);
		GameSession session = GameUtils.GetGameSession();
		session.Progress.RecordSwitchActivated(m_baseObject.SwitchID);
		GameUtils.RequireManager<SaveManager>().RegisterOnIdle(delegate
		{
			session.SaveSession();
		});
	}
}
