using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public abstract class LevelConfigBase : ScriptableObject
{
	public HazardInfo m_hazardInfo;

	public bool m_disableDynamicParenting = true;

	public bool m_gridSelection = true;

	public bool m_showIconPrompts;

	[FormerlySerializedAs("m_enableLevelBoundsCheck")]
	public bool m_enableRespawnBounds;

	public LevelObjectiveBase[] m_objectives;

	public RecipeMatchList m_recipeMatchingList;

	public virtual List<OrderDefinitionNode> GetAllRecipes()
	{
		return new List<OrderDefinitionNode>();
	}
}
