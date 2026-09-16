using UnityEngine;

public class HotdogCosmeticDecisions : MealCosmeticDecisions
{
	[SerializeField]
	private GameObject m_emptyBun;

	[SerializeField]
	private OrderDefinitionNode m_emptyHotdogDefinition;

	[SerializeField]
	private OrderToPrefabLookup m_prefabLookup;

	protected override void Start()
	{
		m_prefabLookup.CacheAssembledOrderNodes();
		base.Start();
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		DestroyCurrentModels();
		if (AssembledDefinitionNode.Matching(m_emptyHotdogDefinition, _contents))
		{
			m_emptyBun.SetActive(true);
		}
		else
		{
			m_emptyBun.SetActive(false);
		}
		GameObject prefabForNode = m_prefabLookup.GetPrefabForNode(_contents);
		if (prefabForNode != null)
		{
			prefabForNode.InstantiateOnParent(m_container.transform);
		}
	}

	private void DestroyCurrentModels()
	{
		for (int num = m_container.transform.childCount - 1; num >= 0; num--)
		{
			Object.Destroy(m_container.transform.GetChild(num).gameObject);
		}
	}
}
