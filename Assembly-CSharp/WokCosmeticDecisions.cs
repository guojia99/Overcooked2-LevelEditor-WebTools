using System;
using System.Collections.Generic;
using UnityEngine;

public class WokCosmeticDecisions : MealCosmeticDecisions
{
	[Serializable]
	private struct PrefabLookups
	{
		[AssignResource("DLC04_LargePotCookableObjectsLookup", Editorbility.Editable)]
		[SerializeField]
		public OrderToPrefabLookup m_rawPrefabLookup;

		[AssignResource("DLC04_LargePotCookableObjectsLookup", Editorbility.Editable)]
		[SerializeField]
		public OrderToPrefabLookup m_cookedPrefabLookup;

		[SerializeField]
		public OrderToPrefabLookup m_burntPrefabLookup;

		public void CacheAssembledOrderNodes()
		{
			if (m_rawPrefabLookup != null)
			{
				m_rawPrefabLookup.CacheAssembledOrderNodes();
			}
			if (m_cookedPrefabLookup != null)
			{
				m_cookedPrefabLookup.CacheAssembledOrderNodes();
			}
			if (m_burntPrefabLookup != null)
			{
				m_burntPrefabLookup.CacheAssembledOrderNodes();
			}
		}

		public OrderToPrefabLookup GetPrefabLookup(CookedCompositeOrderNode.CookingProgress _progress)
		{
			switch (_progress)
			{
			case CookedCompositeOrderNode.CookingProgress.Raw:
				return m_rawPrefabLookup;
			case CookedCompositeOrderNode.CookingProgress.Cooked:
				return m_cookedPrefabLookup;
			case CookedCompositeOrderNode.CookingProgress.Burnt:
				return m_burntPrefabLookup;
			default:
				return null;
			}
		}
	}

	[Serializable]
	public class ContentsOffset
	{
		[SerializeField]
		public Vector3 m_empty = new Vector3(0f, 0f, 0f);

		[SerializeField]
		public Vector3 m_full = new Vector3(0f, 0f, 0f);

		[SerializeField]
		public AnimationCurve m_curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	}

	[Serializable]
	private enum RotationType
	{
		None = 0,
		Random = 1,
		Ordered = 2
	}

	[Serializable]
	private struct OrderedData
	{
		[SerializeField]
		public float[] m_angles;
	}

	[SerializeField]
	private PrefabLookups m_prefabLookups;

	[SerializeField]
	public ContentsOffset m_contentsOffset = new ContentsOffset();

	[SerializeField]
	private RotationType m_rotationType;

	[SerializeField]
	[HideInInspectorTest("m_rotationType", RotationType.Ordered)]
	private OrderedData m_orderedData;

	private List<GameObject> m_prefabsToSpawn = new List<GameObject>();

	private IngredientContainer m_ingredientContainer;

	protected override void Start()
	{
		m_prefabLookups.CacheAssembledOrderNodes();
		base.Start();
		m_ingredientContainer = base.gameObject.RequestComponentUpwardsRecursive<IngredientContainer>();
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		DestroyContents();
		CompositeAssembledNode compositeAssembledNode = _contents as CompositeAssembledNode;
		if (compositeAssembledNode != null && compositeAssembledNode.m_composition.Length != 0)
		{
			CreateContents(compositeAssembledNode);
		}
	}

	private void CreateContents(CompositeAssembledNode _composite)
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = _composite as CookedCompositeAssembledNode;
		OrderToPrefabLookup prefabLookup = m_prefabLookups.GetPrefabLookup((cookedCompositeAssembledNode != null) ? cookedCompositeAssembledNode.m_progress : CookedCompositeOrderNode.CookingProgress.Raw);
		if (prefabLookup == null)
		{
			return;
		}
		for (int i = 0; i < _composite.m_composition.Length; i++)
		{
			GameObject prefabForNode = prefabLookup.GetPrefabForNode(_composite.m_composition[i]);
			if (!(prefabForNode == null))
			{
				m_prefabsToSpawn.Add(prefabForNode);
			}
		}
		float time = Mathf.Clamp01((float)_composite.m_composition.Length / (float)m_ingredientContainer.m_capacity);
		m_container.transform.localPosition = Vector3.Lerp(m_contentsOffset.m_empty, m_contentsOffset.m_full, m_contentsOffset.m_curve.Evaluate(time));
		for (int j = 0; j < m_prefabsToSpawn.Count; j++)
		{
			GameObject gameObject = m_prefabsToSpawn[j];
			if (gameObject == null)
			{
				continue;
			}
			GameObject gameObject2 = gameObject.InstantiateOnParent(m_container.transform);
			if (m_rotationType != RotationType.None)
			{
				float angle = 0f;
				switch (m_rotationType)
				{
				case RotationType.Ordered:
					angle = m_orderedData.m_angles[j];
					break;
				case RotationType.Random:
					angle = UnityEngine.Random.Range(-180f, 180f);
					break;
				}
				gameObject2.transform.localRotation = Quaternion.AngleAxis(angle, gameObject2.transform.up);
			}
		}
		m_prefabsToSpawn.Clear();
	}

	private void DestroyContents()
	{
		int childCount = m_container.transform.childCount;
		for (int num = childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(m_container.transform.GetChild(num).gameObject);
		}
	}
}
