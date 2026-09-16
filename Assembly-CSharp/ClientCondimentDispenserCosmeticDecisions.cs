using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCondimentDispenserCosmeticDecisions : ClientSynchroniserBase
{
	private CondimentDispenserCosmeticDecisions m_condimentDispenserCosmeticDecisions;

	private static readonly int[] m_iCondiments = new int[2]
	{
		Animator.StringToHash("Condiment1"),
		Animator.StringToHash("Condiment2")
	};

	private Animator m_animator;

	private IOrderDefinition m_orderDefinition;

	private PlacementItemSwitcher m_placementItemSwitcher;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_condimentDispenserCosmeticDecisions = (CondimentDispenserCosmeticDecisions)synchronisedObject;
		m_animator = base.gameObject.RequestComponentRecursive<Animator>();
		m_orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
		m_placementItemSwitcher = base.gameObject.RequireComponent<PlacementItemSwitcher>();
		OnItemSwitched();
	}

	public void OnPickupItem()
	{
		for (int i = 0; i < m_condimentDispenserCosmeticDecisions.m_particleEffects.Length; i++)
		{
			m_condimentDispenserCosmeticDecisions.m_particleEffects[i].SetActive(false);
		}
		AssembledDefinitionNode orderComposition = m_orderDefinition.GetOrderComposition();
		for (int j = 0; j < m_placementItemSwitcher.m_ingredients.Length; j++)
		{
			if (!AssembledDefinitionNode.MatchingAlreadySimple(orderComposition, new IngredientAssembledNode(m_placementItemSwitcher.m_ingredients[j])))
			{
				continue;
			}
			for (int k = 0; k < m_condimentDispenserCosmeticDecisions.m_particleEffects.Length; k++)
			{
				if (k == j)
				{
					m_condimentDispenserCosmeticDecisions.m_particleEffects[k].SetActive(true);
					GameUtils.TriggerAudio(m_condimentDispenserCosmeticDecisions.m_audioTag, base.gameObject.layer);
				}
				else
				{
					m_condimentDispenserCosmeticDecisions.m_particleEffects[k].SetActive(false);
				}
			}
		}
	}

	public void OnItemSwitched()
	{
		AssembledDefinitionNode orderComposition = m_orderDefinition.GetOrderComposition();
		for (int i = 0; i < m_placementItemSwitcher.m_ingredients.Length; i++)
		{
			if (AssembledDefinitionNode.MatchingAlreadySimple(orderComposition, new IngredientAssembledNode(m_placementItemSwitcher.m_ingredients[i])))
			{
				m_animator.SetTrigger(m_iCondiments[i]);
			}
			else
			{
				m_animator.ResetTrigger(m_iCondiments[i]);
			}
		}
	}
}
