using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientSwitchMapNode : ClientSynchroniserBase
{
	private SwitchMapNode m_baseObject;

	private WorldMapFlowController m_worldMapFlowController;

	private void Awake()
	{
		m_worldMapFlowController = GameUtils.RequireManager<WorldMapFlowController>();
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (SwitchMapNode)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.SwitchMapNode;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession.DLC == -1)
		{
			OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
			if (overcookedAchievementManager != null)
			{
				overcookedAchievementManager.AddIDStat(8, m_baseObject.SwitchID, ControlPadInput.PadNum.One);
			}
		}
		gameSession.Progress.RecordSwitchActivated(m_baseObject.SwitchID);
		gameSession.SaveSession();
		ClientWorldMapFlowController component = m_worldMapFlowController.GetComponent<ClientWorldMapFlowController>();
		component.UnfoldSwitchMapNode(m_baseObject);
	}
}
