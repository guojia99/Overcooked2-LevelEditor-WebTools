using UnityEngine;

public class BurritoCosmeticDecisions : OverlapModelsMealDecisions
{
	[SerializeField]
	private GameObject m_fullTortilla;

	[SerializeField]
	private GameObject m_emptyTortilla;

	[SerializeField]
	private OrderDefinitionNode m_tortillaOrderDefinition;

	protected override void Start()
	{
		base.Start();
		m_container.transform.localRotation = Quaternion.AngleAxis(90f, Vector3.up);
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	private bool HasContents(CompositeAssembledNode _composite)
	{
		if (_composite == null)
		{
			return false;
		}
		AssembledDefinitionNode[] composition = _composite.m_composition;
		foreach (AssembledDefinitionNode node in composition)
		{
			if (!AssembledDefinitionNode.Matching(node, m_tortillaOrderDefinition))
			{
				return true;
			}
		}
		return false;
	}

	protected override void DestroyAllRenderingInstances()
	{
		base.DestroyAllRenderingInstances();
		m_fullTortilla.SetActive(false);
		m_emptyTortilla.SetActive(false);
	}

	protected override GameObject GetRenderPefabForNode(AssembledDefinitionNode _order)
	{
		if (!AssembledDefinitionNode.Matching(_order, m_tortillaOrderDefinition))
		{
			return base.GetRenderPefabForNode(_order);
		}
		AssembledDefinitionNode assembledDefinitionNode = null;
		assembledDefinitionNode = m_iOrderDefinition.GetOrderComposition();
		bool flag = assembledDefinitionNode != null && HasContents(assembledDefinitionNode as CompositeAssembledNode);
		m_fullTortilla.SetActive(flag);
		m_emptyTortilla.SetActive(!flag);
		return null;
	}
}
