using System;

[Serializable]
public class ScriptedRoundData : RoundData
{
	public RecipeList.Entry[] m_manualOrder;

	public override RecipeList.Entry[] GetNextRecipe(RoundInstanceDataBase _data)
	{
		RoundInstanceData roundInstanceData = _data as RoundInstanceData;
		if (roundInstanceData.RecipeCount < m_manualOrder.Length)
		{
			return new RecipeList.Entry[1] { m_manualOrder[roundInstanceData.RecipeCount++] };
		}
		return base.GetNextRecipe((RoundInstanceDataBase)roundInstanceData);
	}
}
