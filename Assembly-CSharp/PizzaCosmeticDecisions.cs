using UnityEngine;

public class PizzaCosmeticDecisions : MealCosmeticDecisions
{
	[SerializeField]
	private OrderToPrefabLookup m_cookedPrefabLookup;

	[SerializeField]
	private OrderToPrefabLookup m_uncookedPrefabLookup;

	[SerializeField]
	private ParticleSystem m_steamPFX;

	[SerializeField]
	[AssignResource("Dough", Editorbility.NonEditable)]
	private IngredientOrderNode m_doughIngredient;

	[SerializeField]
	private GameObject m_uncookedBase;

	[SerializeField]
	private GameObject m_cookedBase;

	[SerializeField]
	private float m_cookedContentsOffset = 0.04f;

	private bool m_cooked;

	private bool m_burnt;

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void Start()
	{
		if (m_cookedPrefabLookup != null)
		{
			m_cookedPrefabLookup.CacheAssembledOrderNodes();
		}
		if (m_uncookedPrefabLookup != null)
		{
			m_uncookedPrefabLookup.CacheAssembledOrderNodes();
		}
		base.Start();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		DestroyAllRenderChildren();
		if (_contents != null)
		{
			CreateRenderChildren(_contents);
		}
	}

	private void DestroyAllRenderChildren()
	{
		for (int num = m_container.transform.childCount - 1; num >= 0; num--)
		{
			Transform child = m_container.transform.GetChild(num);
			child.transform.SetParent(null);
			Object.Destroy(child.gameObject);
		}
	}

	private void CreateRenderChildren(AssembledDefinitionNode _order)
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = _order as CookedCompositeAssembledNode;
		m_cooked = cookedCompositeAssembledNode != null && cookedCompositeAssembledNode.m_progress != CookedCompositeOrderNode.CookingProgress.Raw;
		m_burnt = cookedCompositeAssembledNode != null && cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt;
		bool flag = false;
		CompositeAssembledNode compositeAssembledNode = _order as CompositeAssembledNode;
		if (compositeAssembledNode != null)
		{
			flag = compositeAssembledNode.m_composition.FindIndex_Predicate((AssembledDefinitionNode x) => AssembledDefinitionNode.Matching(x, m_doughIngredient)) != -1;
		}
		m_uncookedBase.SetActive(flag && !m_cooked);
		m_cookedBase.SetActive(flag && m_cooked);
		m_steamPFX.gameObject.SetActive(m_cooked);
		if (m_cooked)
		{
			m_steamPFX.Play();
		}
		else if (m_cooked)
		{
			m_steamPFX.Stop();
		}
		ParticleSystem.MainModule main = m_steamPFX.main;
		if (m_burnt)
		{
			main.startColor = Color.black;
		}
		else
		{
			main.startColor = Color.white;
		}
		if (compositeAssembledNode != null)
		{
			for (int num = 0; num < compositeAssembledNode.m_composition.Length; num++)
			{
				GameObject prefabForNode = GetPrefabForNode(compositeAssembledNode.m_composition[num]);
				if (prefabForNode != null)
				{
					prefabForNode.InstantiateOnParent(m_container.transform);
				}
			}
		}
		Vector3 zero = Vector3.zero;
		m_container.transform.localPosition = zero;
	}

	private GameObject GetPrefabForNode(AssembledDefinitionNode _order)
	{
		if (!AssembledDefinitionNode.Matching(_order, m_doughIngredient))
		{
			if (m_cooked)
			{
				return m_cookedPrefabLookup.GetPrefabForNode(_order);
			}
			return m_uncookedPrefabLookup.GetPrefabForNode(_order);
		}
		return null;
	}
}
