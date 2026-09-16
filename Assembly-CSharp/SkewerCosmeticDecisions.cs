using UnityEngine;

public class SkewerCosmeticDecisions : MealCosmeticDecisions
{
	[SerializeField]
	[AssignResource("KebobCosmeticPrefabs", Editorbility.Editable)]
	private OrderToPrefabLookup m_prefabLookup;

	[SerializeField]
	[AssignResource("CookedKebobCosmeticPrefabs", Editorbility.Editable)]
	private OrderToPrefabLookup m_cookedPrefabLookup;

	[SerializeField]
	public GameObject m_burntPrefab;

	[SerializeField]
	public float m_minimumPrefabWidth = 0.15f;

	[SerializeField]
	public float m_contentsOffset = -0.4f;

	[SerializeField]
	public float m_contentsBounds = 1f;

	[SerializeField]
	public int m_maxMeshesPerIngredient = 2;

	protected override void Start()
	{
		if (m_prefabLookup != null)
		{
			m_prefabLookup.CacheAssembledOrderNodes();
		}
		if (m_cookedPrefabLookup != null)
		{
			m_cookedPrefabLookup.CacheAssembledOrderNodes();
		}
		base.Start();
		m_container.transform.localRotation = Quaternion.AngleAxis(90f, Vector3.up);
		m_container.transform.localPosition = m_container.transform.localPosition.WithX(m_contentsOffset);
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		bool flag = false;
		CookedCompositeAssembledNode cookedCompositeAssembledNode = _contents as CookedCompositeAssembledNode;
		if (cookedCompositeAssembledNode != null)
		{
			flag = cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt;
		}
		if (flag)
		{
			DestroyContents();
		}
		else
		{
			CreateContents(_contents);
		}
		if ((!flag && (_contents.Simpilfy() == AssembledDefinitionNode.NullNode || m_container.transform.childCount != 0)) || !(m_burntPrefab != null))
		{
			return;
		}
		CompositeAssembledNode compositeAssembledNode = _contents as CompositeAssembledNode;
		if (compositeAssembledNode != null)
		{
			float prefabLength = GetPrefabLength(m_burntPrefab);
			int b = Mathf.FloorToInt(m_contentsBounds / prefabLength);
			int a = m_maxMeshesPerIngredient * compositeAssembledNode.m_composition.Length;
			a = Mathf.Min(a, b);
			float num = 0f;
			for (int i = 0; i < a; i++)
			{
				GameObject gameObject = m_burntPrefab.InstantiateOnParent(m_container.transform);
				PlacePrefabAtOffset(gameObject.transform, prefabLength, num);
				num += prefabLength;
			}
		}
	}

	private void CreateContents(AssembledDefinitionNode _contents)
	{
		DestroyContents();
		CompositeAssembledNode compositeAssembledNode = _contents as CompositeAssembledNode;
		if (compositeAssembledNode == null || compositeAssembledNode.m_composition.Length == 0)
		{
			return;
		}
		CookedCompositeAssembledNode cookedCompositeAssembledNode = compositeAssembledNode as CookedCompositeAssembledNode;
		OrderToPrefabLookup orderToPrefabLookup = ((cookedCompositeAssembledNode == null || cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Raw) ? m_prefabLookup : m_cookedPrefabLookup);
		int num = 0;
		GameObject[] array = new GameObject[compositeAssembledNode.m_composition.Length];
		for (int i = 0; i < array.Length; i++)
		{
			GameObject prefabForNode = orderToPrefabLookup.GetPrefabForNode(compositeAssembledNode.m_composition[i]);
			if (prefabForNode != null)
			{
				array[num++] = prefabForNode;
			}
		}
		float[] array2 = new float[num];
		for (int j = 0; j < num; j++)
		{
			array2[j] = Mathf.Max(m_minimumPrefabWidth, GetPrefabLength(array[j]));
		}
		float num2 = 0f;
		for (int k = 0; k < m_maxMeshesPerIngredient; k++)
		{
			if (!(num2 < m_contentsBounds))
			{
				break;
			}
			for (int l = 0; l < num; l++)
			{
				if (!(num2 < m_contentsBounds))
				{
					break;
				}
				GameObject gameObject = array[l].InstantiateOnParent(m_container.transform);
				PlacePrefabAtOffset(gameObject.transform, array2[l], num2);
				num2 += array2[l];
			}
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

	private void PlacePrefabAtOffset(Transform _element, float _size, float _offset)
	{
		_element.localRotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
		_element.localPosition += Vector3.forward * (_offset + _size / 2f);
	}

	private float GetPrefabLength(GameObject _gameObject)
	{
		Bounds bounds = default(Bounds);
		MeshFilter[] array = _gameObject.RequestComponentsRecursive<MeshFilter>();
		for (int i = 0; i < array.Length; i++)
		{
			Mesh sharedMesh = array[i].sharedMesh;
			if (sharedMesh != null)
			{
				bounds.Encapsulate(sharedMesh.bounds);
			}
		}
		return bounds.size.z;
	}
}
