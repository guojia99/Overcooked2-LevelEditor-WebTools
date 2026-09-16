using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class FlambeCosmeticDecisions : FryingContentsCosmeticDecisions
{
	[SerializeField]
	public float m_gradLimit = 2f;

	[SerializeField]
	public float m_timeToMax = 0.1f;

	[SerializeField]
	public string m_materialFloatName = "FlambeProp";
}
