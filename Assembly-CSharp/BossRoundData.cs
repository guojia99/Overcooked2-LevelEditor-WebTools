using System;
using GameModes;
using UnityEngine;

[Serializable]
public class BossRoundData : DynamicRoundData
{
	protected class BossRoundInstanceData : DynamicRoundInstanceData
	{
	}

	private Kind m_gameModeKind;

	public override RoundInstanceDataBase InitialiseRound()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameModeKind = gameSession.GameModeKind;
		return new BossRoundInstanceData();
	}

	public override RecipeList.Entry[] GetNextRecipe(RoundInstanceDataBase _data)
	{
		BossRoundInstanceData bossRoundInstanceData = _data as BossRoundInstanceData;
		Phase phase = Phases[bossRoundInstanceData.CurrentPhase];
		if (bossRoundInstanceData.CurrentPhase == Phases.Length - 1 && (m_gameModeKind == Kind.Practice || m_gameModeKind == Kind.Survival))
		{
			return new RecipeList.Entry[1] { phase.Recipes.m_recipes[UnityEngine.Random.Range(0, phase.Recipes.m_recipes.Length)] };
		}
		if (NoMoreRecipesToIssueInPhase(_data))
		{
			return new RecipeList.Entry[0];
		}
		RecipeList.Entry entry = phase.Recipes.m_recipes[bossRoundInstanceData.RecipeCount++];
		return new RecipeList.Entry[1] { entry };
	}

	public bool NoMoreRecipesToIssueInPhase(RoundInstanceDataBase _data)
	{
		BossRoundInstanceData bossRoundInstanceData = _data as BossRoundInstanceData;
		return bossRoundInstanceData.RecipeCount >= Phases[bossRoundInstanceData.CurrentPhase].Recipes.m_recipes.Length;
	}
}
