using UnityEngine;

public class BurgerBunCosmeticDecisions : MealCosmeticDecisions
{
	[SerializeField]
	private GameObject m_lid;

	[SerializeField]
	private GameObject m_base;

	[SerializeField]
	private GameObject m_empty;

	[SerializeField]
	private OrderToPrefabLookup m_prefabLookup;

	[SerializeField]
	private OrderDefinitionNode m_bapOrderDefinition;

	protected override void Start()
	{
		if (m_prefabLookup != null)
		{
			m_prefabLookup.CacheAssembledOrderNodes();
		}
		base.Start();
		Mesh mesh = m_base.RequireComponent<MeshFilter>().mesh;
		Vector3 position = m_base.transform.TransformPoint(new Vector3(0f, mesh.bounds.size.y, 0f));
		m_container.transform.localPosition = m_container.transform.parent.InverseTransformPoint(position);
		UpdateAppearance(m_iOrderDefinition.GetOrderComposition());
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		AssembledDefinitionNode assembledDefinitionNode = _contents.Simpilfy();
		CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
		if (compositeAssembledNode != null && compositeAssembledNode.m_composition.Length > 0)
		{
			bool active = ContainsBap(compositeAssembledNode);
			m_lid.SetActive(active);
			m_base.SetActive(active);
			m_empty.SetActive(false);
			LayoutContents(compositeAssembledNode);
		}
		else
		{
			m_lid.SetActive(false);
			m_base.SetActive(false);
			m_empty.SetActive(true);
			DestroyContents();
		}
	}

	private bool ContainsBap(CompositeAssembledNode _composite)
	{
		for (int i = 0; i < _composite.m_composition.Length; i++)
		{
			if (AssembledDefinitionNode.Matching(_composite.m_composition[i], m_bapOrderDefinition))
			{
				return true;
			}
		}
		return false;
	}

	private void DestroyContents()
	{
		for (int i = 0; i < m_container.transform.childCount; i++)
		{
			Object.Destroy(m_container.transform.GetChild(i).gameObject);
		}
	}

	private void LayoutContents(CompositeAssembledNode _composite)
	{
		DestroyContents();
		Vector3 zero = Vector3.zero;
		for (int i = 0; i < _composite.m_composition.Length; i++)
		{
			GameObject burgerPrefab = GetBurgerPrefab(_composite.m_composition[i]);
			if (burgerPrefab != null)
			{
				GameObject gameObject = burgerPrefab.InstantiateOnParent(m_container.transform);
				MeshFilter meshFilter = gameObject.RequireComponentRecursive<MeshFilter>();
				gameObject.transform.localPosition = zero;
				float y = meshFilter.sharedMesh.bounds.size.y;
				zero += Vector3.up * y * gameObject.transform.localScale.y;
			}
		}
		Vector3 position = m_container.transform.TransformPoint(zero);
		m_lid.transform.localPosition = m_lid.transform.parent.InverseTransformPoint(position);

		// patch
		Transform parent = transform.parent;
		if (parent != null && parent.GetComponent<PreparationContainer>() != null && parent.GetComponent<BoxCollider>() != null)
		{
			BoxCollider collider = parent.GetComponent<BoxCollider>();
			collider.size = collider.size.WithY(zero.y + 0.35f);
			collider.center = collider.center.WithY(zero.y / 2 + 0.175f);
		}
		// patch
	}

	private GameObject GetBurgerPrefab(AssembledDefinitionNode _providedNode)
	{
		GameObject prefabForNode = m_prefabLookup.GetPrefabForNode(_providedNode);
		if ((bool)prefabForNode)
		{
			return prefabForNode;
		}
		return null;
	}
}
