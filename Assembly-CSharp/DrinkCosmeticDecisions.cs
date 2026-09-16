using System;
using UnityEngine;

public class DrinkCosmeticDecisions : MealCosmeticDecisions
{
	[Serializable]
	public class OrderToMaterial
	{
		public OrderDefinitionNode m_content;

		public Material m_material;
	}

	[SerializeField]
	private OrderToMaterial[] m_materialLookup = new OrderToMaterial[0];

	private Renderer[] m_renderers;

	protected override void Start()
	{
		m_renderers = base.gameObject.GetComponentsInChildren<Renderer>();
		base.Start();
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		AssembledDefinitionNode assembledDefinitionNode = _contents.Simpilfy();
		CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
		Material material = null;
		if (compositeAssembledNode != null)
		{
			for (int i = 0; i < compositeAssembledNode.m_composition.Length; i++)
			{
				material = GetDrinkMaterial(compositeAssembledNode.m_composition[i]);
				if ((bool)material)
				{
					break;
				}
			}
		}
		else
		{
			material = GetDrinkMaterial(_contents);
		}
		if ((bool)material)
		{
			for (int j = 0; j < m_renderers.Length; j++)
			{
				m_renderers[j].material = material;
			}
		}
	}

	private Material GetDrinkMaterial(AssembledDefinitionNode _node)
	{
		for (int i = 0; i < m_materialLookup.Length; i++)
		{
			if (AssembledDefinitionNode.Matching(_node, m_materialLookup[i].m_content))
			{
				return m_materialLookup[i].m_material;
			}
		}
		return null;
	}
}
