using UnityEngine;

public class ComboCosmeticDecisions : MealCosmeticDecisions
{
	[SerializeField]
	private ComboOrderToPrefabLookup m_comboPrefabLookup;

	[SerializeField]
	public bool m_maintainScale = true;

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequireInterface<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		DestroyContents();
		if (_contents != null)
		{
			CreateContents(_contents);
		}
	}

	protected override void Start()
	{
		if (m_comboPrefabLookup != null)
		{
			m_comboPrefabLookup.CacheAssembledOrderNodes();
		}
		base.Start();
	}

	private void CreateContents(AssembledDefinitionNode _contents)
	{
		GameObject prefabForNode = m_comboPrefabLookup.GetPrefabForNode(_contents);
		if (prefabForNode != null)
		{
			prefabForNode.InstantiateOnParent(m_container.transform, m_maintainScale);
		}
	}

	private void DestroyContents()
	{
		for (int num = m_container.transform.childCount - 1; num >= 0; num--)
		{
			Transform child = m_container.transform.GetChild(num);
			child.transform.SetParent(null);
			Object.Destroy(child.gameObject);
		}
	}
}
