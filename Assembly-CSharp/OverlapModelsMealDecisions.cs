using System.Collections.Generic;
using UnityEngine;

public class OverlapModelsMealDecisions : MealCosmeticDecisions
{
	[SerializeField]
	private OrderToPrefabLookup m_prefabLookup;

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequireInterface<IClientOrderDefinition>();
	}

	protected override void Start()
	{
		if (m_prefabLookup != null)
		{
			m_prefabLookup.CacheAssembledOrderNodes();
		}
		base.Start();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		DestroyAllRenderingInstances();
		if (_contents != null)
		{
			CreateRenderChildren(_contents);
		}
	}

	protected virtual void CreateRenderChildren(AssembledDefinitionNode _contents)
	{
		IEnumerator<AssembledDefinitionNode> enumerator = ShallowIteration(_contents);
		while (enumerator.MoveNext())
		{
			AssembledDefinitionNode current = enumerator.Current;
			GameObject renderPefabForNode = GetRenderPefabForNode(current);
			if (renderPefabForNode != null)
			{
				GameObject gameObject = renderPefabForNode.InstantiateOnParent(m_container.transform);
			}
		}
	}

	protected IEnumerator<AssembledDefinitionNode> ShallowIteration(AssembledDefinitionNode _contents)
	{
		if (_contents is CompositeAssembledNode)
		{
			AssembledDefinitionNode[] contents = (_contents as CompositeAssembledNode).m_composition;
			for (int i = 0; i < contents.Length; i++)
			{
				if (_contents is CookedCompositeAssembledNode)
				{
					CookedCompositeAssembledNode cookedNode = _contents as CookedCompositeAssembledNode;
					CookedCompositeAssembledNode cookedWrapper = new CookedCompositeAssembledNode();
					cookedWrapper.ReplaceData(cookedNode);
					cookedWrapper.m_composition = new AssembledDefinitionNode[1] { contents[i] };
					yield return cookedWrapper;
				}
				else
				{
					yield return contents[i];
				}
			}
		}
		else
		{
			yield return _contents;
		}
	}

	protected virtual void DestroyAllRenderingInstances()
	{
		for (int num = m_container.transform.childCount - 1; num >= 0; num--)
		{
			Transform child = m_container.transform.GetChild(num);
			child.transform.SetParent(null);
			Object.Destroy(child.gameObject);
		}
	}

	protected virtual GameObject GetRenderPefabForNode(AssembledDefinitionNode _order)
	{
		return m_prefabLookup.GetPrefabForNode(_order);
	}
}
