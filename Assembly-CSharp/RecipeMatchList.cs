using System;
using UnityEngine;

[Serializable]
public class RecipeMatchList : ScriptableObject
{
	public RecipeMatchList[] m_includeLists;

	public OrderDefinitionNode[] m_recipes;

	public CookingStepData[] m_cookingSteps;
}
