using UnityEngine;

public class KebabCosmeticDecisions : MealCosmeticDecisions
{
	[SerializeField]
	private OrderToPrefabLookup m_prefabLookup;

	protected override void Start()
	{
		if (m_prefabLookup != null)
		{
			m_prefabLookup.CacheAssembledOrderNodes();
		}
		base.Start();
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterface<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		DestroyContents();
		if (_contents != null)
		{
			CreateContents(_contents);
		}
	}

	private void CreateContents(AssembledDefinitionNode _contents)
	{
		GameObject prefabForNode = m_prefabLookup.GetPrefabForNode(_contents);
		if (prefabForNode != null)
		{
			prefabForNode.InstantiateOnParent(m_container.transform);
		}
	}

	private void DestroyContents()
	{
		int childCount = m_container.transform.childCount;
		for (int num = childCount - 1; num >= 0; num--)
		{
			Object.Destroy(m_container.transform.GetChild(num).gameObject);
		}
	}
}
