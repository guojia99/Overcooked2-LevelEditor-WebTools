using UnityEngine;

public class FryingPanCosmeticDecisions : OverlapModelsMealDecisions
{
	[SerializeField]
	public GameObject m_burntPrefab;

	[SerializeField]
	public Vector3 m_burntPrefabOffset;

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void CreateRenderChildren(AssembledDefinitionNode _contents)
	{
		base.CreateRenderChildren(_contents);
		if (_contents.Simpilfy() != AssembledDefinitionNode.NullNode && m_container.transform.childCount == 0 && m_burntPrefab != null)
		{
			GameObject gameObject = m_burntPrefab.InstantiateOnParent(m_container.transform);
			gameObject.transform.localPosition = m_burntPrefabOffset;
		}
	}
}
