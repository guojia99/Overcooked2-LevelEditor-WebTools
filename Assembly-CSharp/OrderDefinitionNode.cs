using System;
using UnityEngine;

[Serializable]
public abstract class OrderDefinitionNode : ScriptableObject
{
	public RecipeWidgetUIController.RecipeTileData[] m_orderGuiDescription;

	public PlatingStepData m_platingStep;

	public GameObject m_platingPrefab;

	[SelfAssignID(true)]
	public int m_uID;

	public AssembledDefinitionNode Simpilfy()
	{
		AssembledDefinitionNode assembledDefinitionNode = Convert();
		return assembledDefinitionNode.Simpilfy();
	}

	public abstract AssembledDefinitionNode Convert();

	public override bool Equals(object _node)
	{
		return AssembledDefinitionNode.Matching(this, _node as OrderDefinitionNode);
	}
}
