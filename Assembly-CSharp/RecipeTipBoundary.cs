using System;
using UnityEngine;

[Serializable]
public class RecipeTipBoundary
{
	[Range(0f, 1f)]
	public float PercentageTimeRemaining;

	public int ScoreValue;

	public RecipeTipBoundary(float _percent, int _score)
	{
		PercentageTimeRemaining = _percent;
		ScoreValue = _score;
	}
}
