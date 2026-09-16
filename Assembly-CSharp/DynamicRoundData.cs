using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DynamicRoundData : RoundData
{
	[Serializable]
	public class Phase
	{
		public RecipeList Recipes;

		public float Duration;
	}

	protected class DynamicRoundInstanceData : RoundInstanceData
	{
		public int CurrentPhase;
	}

	public Phase[] Phases = new Phase[0];

	public override RoundInstanceDataBase InitialiseRound()
	{
		DynamicRoundInstanceData dynamicRoundInstanceData = new DynamicRoundInstanceData();
		Phase phase = Phases[0];
		dynamicRoundInstanceData.CumulativeFrequencies = new int[phase.Recipes.m_recipes.Length];
		return dynamicRoundInstanceData;
	}

	public virtual void MoveToNextPhase(RoundInstanceDataBase _data)
	{
		if (GetRemainingPhases(_data) > 0)
		{
			DynamicRoundInstanceData dynamicRoundInstanceData = _data as DynamicRoundInstanceData;
			dynamicRoundInstanceData.CurrentPhase++;
			Phase phase = Phases[dynamicRoundInstanceData.CurrentPhase];
			dynamicRoundInstanceData.RecipeCount = 0;
			dynamicRoundInstanceData.CumulativeFrequencies = new int[phase.Recipes.m_recipes.Length];
		}
	}

	public float GetCurrentPhaseDuration(RoundInstanceDataBase _data)
	{
		DynamicRoundInstanceData dynamicRoundInstanceData = _data as DynamicRoundInstanceData;
		return Phases[dynamicRoundInstanceData.CurrentPhase].Duration;
	}

	public int GetRemainingPhases(RoundInstanceDataBase _data)
	{
		DynamicRoundInstanceData dynamicRoundInstanceData = _data as DynamicRoundInstanceData;
		return Phases.Length - 1 - dynamicRoundInstanceData.CurrentPhase;
	}

	public override RecipeList.Entry[] GetNextRecipe(RoundInstanceDataBase _data)
	{
		DynamicRoundInstanceData instance = _data as DynamicRoundInstanceData;
		Phase phase = Phases[instance.CurrentPhase];
		instance.RecipeCount++;
		KeyValuePair<int, RecipeList.Entry> weightedRandomElement = phase.Recipes.m_recipes.GetWeightedRandomElement((int i, RecipeList.Entry e) => GetWeight(instance, i));
		instance.CumulativeFrequencies[weightedRandomElement.Key]++;
		return new RecipeList.Entry[1] { weightedRandomElement.Value };
	}

	private float GetWeight(RoundInstanceData _data, int _recipeIndex)
	{
		DynamicRoundInstanceData dynamicRoundInstanceData = _data as DynamicRoundInstanceData;
		Phase phase = Phases[dynamicRoundInstanceData.CurrentPhase];
		int num = 0;
		for (int i = 0; i < _data.CumulativeFrequencies.Length; i++)
		{
			num += _data.CumulativeFrequencies[i];
		}
		float num2 = (float)(num + 2) / (float)phase.Recipes.m_recipes.Length;
		return Mathf.Max(num2 - (float)_data.CumulativeFrequencies[_recipeIndex], 0f);
	}
}
