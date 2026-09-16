using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RoundData : RoundDataBase
{
	protected class RoundInstanceData : RoundInstanceDataBase
	{
		public int RecipeCount;

		public int[] CumulativeFrequencies;
	}

	public RecipeList m_recipes;

	public float m_roundTimer = 150f;

	public override RoundInstanceDataBase InitialiseRound()
	{
		RoundInstanceData roundInstanceData = new RoundInstanceData();
		roundInstanceData.CumulativeFrequencies = new int[m_recipes.m_recipes.Length];
		return roundInstanceData;
	}

	public override RecipeList.Entry[] GetNextRecipe(RoundInstanceDataBase _data)
	{
		RoundInstanceData data = _data as RoundInstanceData;
		data.RecipeCount++;
		KeyValuePair<int, RecipeList.Entry> weightedRandomElement = m_recipes.m_recipes.GetWeightedRandomElement((int i, RecipeList.Entry e) => GetWeight(data, i));
		data.CumulativeFrequencies[weightedRandomElement.Key]++;
		return new RecipeList.Entry[1] { weightedRandomElement.Value };
	}

	private float GetWeight(RoundInstanceData _instance, int _recipeIndex)
	{
		int num = _instance.CumulativeFrequencies.Collapse((int f, int total) => total + f);
		float num2 = (float)(num + 2) / (float)m_recipes.m_recipes.Length;
		return Mathf.Max(num2 - (float)_instance.CumulativeFrequencies[_recipeIndex], 0f);
	}

	private void DebugPrint(RoundInstanceData _instance)
	{
		for (int i = 0; i < m_recipes.m_recipes.Length; i++)
		{
			RecipeList.Entry entry = m_recipes.m_recipes[i];
			float weight = GetWeight(_instance, i);
		}
	}
}
