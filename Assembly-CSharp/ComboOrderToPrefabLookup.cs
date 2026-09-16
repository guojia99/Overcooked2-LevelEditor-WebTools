using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ComboOrderToPrefabLookup : ScriptableObject
{
	[Serializable]
	public class ContentPrefabLookup
	{
		public OrderDefinitionNode[] m_content;

		public GameObject m_prefab;

		[NonSerialized]
		private AssembledDefinitionNode[] m_assembledContent;

		public AssembledDefinitionNode GetAssembledContent(int index)
		{
			return m_assembledContent[index];
		}

		public void CacheAssembledOrderNodes()
		{
			m_assembledContent = new AssembledDefinitionNode[m_content.Length];
			for (int i = 0; i < m_content.Length; i++)
			{
				m_assembledContent[i] = m_content[i].Simpilfy();
			}
		}
	}

	[SerializeField]
	private ContentPrefabLookup[] m_lookupArray = new ContentPrefabLookup[0];

	[SerializeField]
	private ContentPrefabLookup[] m_fallbackArray = new ContentPrefabLookup[0];

	[SerializeField]
	private GameObject m_defaultFallback;

	private List<OrderContentRestriction> m_orderContentRestriction;

	[NonSerialized]
	private bool m_cachedAssembledOrderNodes;

	public void CacheAssembledOrderNodes()
	{
		if (!m_cachedAssembledOrderNodes)
		{
			m_cachedAssembledOrderNodes = true;
			for (int i = 0; i < m_lookupArray.Length; i++)
			{
				m_lookupArray[i].CacheAssembledOrderNodes();
			}
			for (int j = 0; j < m_fallbackArray.Length; j++)
			{
				m_fallbackArray[j].CacheAssembledOrderNodes();
			}
		}
	}

	public List<OrderContentRestriction> GetContentRestrictions()
	{
		if (m_orderContentRestriction == null)
		{
			m_orderContentRestriction = new List<OrderContentRestriction>();
			for (int i = 0; i < m_lookupArray.Length; i++)
			{
				OrderDefinitionNode[] content = m_lookupArray[i].m_content;
				Dictionary<OrderDefinitionNode, int> dictionary = new Dictionary<OrderDefinitionNode, int>();
				for (int j = 0; j < content.Length; j++)
				{
					if (!dictionary.ContainsKey(content[j]))
					{
						dictionary.Add(content[j], 1);
					}
					else
					{
						dictionary[content[j]]++;
					}
				}
				foreach (KeyValuePair<OrderDefinitionNode, int> nodeCountPair in dictionary)
				{
					OrderContentRestriction orderContentRestriction = m_orderContentRestriction.Find((OrderContentRestriction x) => x.m_content == nodeCountPair.Key);
					if (orderContentRestriction != null)
					{
						orderContentRestriction.m_amountAllowed = Mathf.Max(orderContentRestriction.m_amountAllowed, nodeCountPair.Value);
						continue;
					}
					orderContentRestriction = new OrderContentRestriction();
					orderContentRestriction.m_content = nodeCountPair.Key;
					orderContentRestriction.m_amountAllowed = nodeCountPair.Value;
					m_orderContentRestriction.Add(orderContentRestriction);
				}
			}
		}
		return m_orderContentRestriction;
	}

	public GameObject GetPrefabForNode(AssembledDefinitionNode _node)
	{
		if (!m_cachedAssembledOrderNodes)
		{
			CacheAssembledOrderNodes();
		}
		if (_node != null)
		{
			CookedCompositeAssembledNode cookedCompositeAssembledNode = _node as CookedCompositeAssembledNode;
			if (cookedCompositeAssembledNode != null)
			{
				AssembledDefinitionNode[] array = new AssembledDefinitionNode[cookedCompositeAssembledNode.m_composition.Length];
				for (int i = 0; i < cookedCompositeAssembledNode.m_composition.Length; i++)
				{
					CookedCompositeAssembledNode cookedCompositeAssembledNode2 = new CookedCompositeAssembledNode();
					cookedCompositeAssembledNode2.ReplaceData(cookedCompositeAssembledNode);
					cookedCompositeAssembledNode2.m_composition = new AssembledDefinitionNode[1] { cookedCompositeAssembledNode.m_composition[i] };
					array[i] = cookedCompositeAssembledNode2;
				}
				return FindPrefab(array);
			}
			CompositeAssembledNode compositeAssembledNode = _node as CompositeAssembledNode;
			if (compositeAssembledNode != null)
			{
				AssembledDefinitionNode[] array = compositeAssembledNode.m_composition;
				return FindPrefab(array);
			}
			IngredientAssembledNode ingredientAssembledNode = _node as IngredientAssembledNode;
			if (ingredientAssembledNode != null)
			{
				AssembledDefinitionNode[] array = new AssembledDefinitionNode[1] { ingredientAssembledNode };
				return FindPrefab(array);
			}
		}
		return null;
	}

	private GameObject FindPrefab(AssembledDefinitionNode[] ingredients)
	{
		int num = 0;
		for (int num2 = m_lookupArray.Length - 1; num2 >= 0; num2--)
		{
			for (int num3 = ingredients.Length - 1; num3 >= 0; num3--)
			{
				for (int num4 = m_lookupArray[num2].m_content.Length - 1; num4 >= 0; num4--)
				{
					AssembledDefinitionNode assembledContent = m_lookupArray[num2].GetAssembledContent(num4);
					if (AssembledDefinitionNode.Matching(ingredients[num3], assembledContent))
					{
						num++;
					}
				}
			}
			if (num == ingredients.Length && num == m_lookupArray[num2].m_content.Length && m_lookupArray[num2].m_prefab != null)
			{
				return m_lookupArray[num2].m_prefab;
			}
			num = 0;
		}
		int num5 = -1;
		for (int i = 0; i < m_fallbackArray.Length; i++)
		{
			if (m_fallbackArray[i].m_content == null || !(m_fallbackArray[i].m_prefab != null))
			{
				continue;
			}
			for (int j = 0; j < m_fallbackArray[i].m_content.Length; j++)
			{
				bool flag = false;
				for (int k = 0; k < ingredients.Length; k++)
				{
					AssembledDefinitionNode assembledContent2 = m_fallbackArray[i].GetAssembledContent(j);
					if (AssembledDefinitionNode.Matching(ingredients[k], assembledContent2))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					break;
				}
				if (j == m_fallbackArray[i].m_content.Length - 1 && (num5 == -1 || m_fallbackArray[num5].m_content.Length < m_fallbackArray[i].m_content.Length))
				{
					num5 = i;
				}
			}
		}
		return (num5 == -1) ? m_defaultFallback : m_fallbackArray[num5].m_prefab;
	}
}
