using UnityEngine;

public class ClientFlambeCosmeticDecisions : ClientFryingContentsCosmeticDecisions
{
	private FlambeCosmeticDecisions m_flambeCosmeticDecisions;

	private float m_highlightProp;

	private float m_highlightPropGrad;

	private float m_targetProp;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_flambeCosmeticDecisions = (FlambeCosmeticDecisions)synchronisedObject;
		base.StartSynchronising(synchronisedObject);
	}

	public override void OnCookingPropChanged(float newProp)
	{
		m_targetProp = 1f;
		base.OnCookingPropChanged(newProp);
	}

	protected void Update()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		MathUtils.AdvanceToTarget_Sinusoidal(ref m_highlightProp, ref m_highlightPropGrad, m_targetProp, m_flambeCosmeticDecisions.m_gradLimit, m_flambeCosmeticDecisions.m_timeToMax, deltaTime);
		base.AccessRenderer.material.SetFloat(m_flambeCosmeticDecisions.m_materialFloatName, Mathf.Clamp01(m_highlightProp));
		m_targetProp = 0f;
	}
}
