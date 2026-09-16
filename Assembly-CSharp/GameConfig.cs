using System;
using UnityEngine;

[Serializable]
public class GameConfig : ScriptableObject
{
	public RecipeTipBoundary[] TipBoundaries = new RecipeTipBoundary[4]
	{
		new RecipeTipBoundary(0f, 0),
		new RecipeTipBoundary(0.25f, 2),
		new RecipeTipBoundary(0.5f, 4),
		new RecipeTipBoundary(0.75f, 6)
	};

	public int DefaultDeliveryAward = 20;

	public int RecipeTimeOutPointLoss = 10;

	public int SingleplayerChopTimeMultiplier = 5;
}
