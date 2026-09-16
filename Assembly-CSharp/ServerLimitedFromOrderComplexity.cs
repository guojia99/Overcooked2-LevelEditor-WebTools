using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerLimitedFromOrderComplexity : ServerSynchroniserBase
{
	private IOrderDefinition m_iOrderDefinition;

	private LimitedQuantityItemManager m_limitedQuantityItemManager;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_limitedQuantityItemManager = GameUtils.RequireManager<LimitedQuantityItemManager>();
		m_iOrderDefinition = base.gameObject.RequestInterface<IOrderDefinition>();
		if (m_iOrderDefinition != null)
		{
			ServerLimitedQuantityItem serverLimitedQuantityItem = base.gameObject.RequireComponent<ServerLimitedQuantityItem>();
			serverLimitedQuantityItem.AddDestructionScoreModifier(GetOrderComplexity);
		}
	}

	private float GetOrderComplexity()
	{
		AssembledDefinitionNode orderComposition = m_iOrderDefinition.GetOrderComposition();
		if (orderComposition == null)
		{
			return 0f;
		}
		AssembledDefinitionNode assembledDefinitionNode = orderComposition.Simpilfy();
		if (orderComposition == AssembledDefinitionNode.NullNode)
		{
			return 0f;
		}
		return m_limitedQuantityItemManager.m_OrderComplexityMultiplier * (float)assembledDefinitionNode.GetNodeCount();
	}
}
