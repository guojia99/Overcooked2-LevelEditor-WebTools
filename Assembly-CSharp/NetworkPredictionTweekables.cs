using UnityEngine;

[CreateAssetMenu(fileName = "NetworkPredictionTweekables", menuName = "Team17/Create Network Prediction Tweekables")]
public class NetworkPredictionTweekables : ScriptableObject
{
	public float ChefRadius = 0.4f;

	public float PenetrationAngle = 0.65f;

	public float PenitrationMinSpeed = 7.5f;

	public float ChefMovingTowardsUsAngle = 0.4f;

	public float LerpDistanceFactor = 0.4f;

	public float LerpDistanceFactorMax = 32f;

	public float LerpTime = 0.25f;

	public float LerpFactorMax = 1.1f;

	public float LerpMinimumSpeed = 2f;

	public float RotationSpeed = 720f;
}
