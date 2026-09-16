using UnityEngine;

public class MixerEffectsCosmeticDecisions : MonoBehaviour, IClientMixingNotifed
{
	[SerializeField]
	private ParticleSystem m_activeEffect;

	[SerializeField]
	private ParticleSystem m_overmixedEffect;

	private void Awake()
	{
		SetActiveEffectOnOff(false);
		SetOvermixedEffectOnOff(false);
	}

	private void OnEnable()
	{
		SetActiveEffectOnOff(false);
		SetOvermixedEffectOnOff(false);
	}

	private void OnDisable()
	{
		SetActiveEffectOnOff(false);
		SetOvermixedEffectOnOff(false);
	}

	public void OnMixingStarted()
	{
		SetActiveEffectOnOff(true);
	}

	public void OnMixingFinished()
	{
		SetActiveEffectOnOff(false);
	}

	public void OnMixingPropChanged(float newProp)
	{
		SetOvermixedEffectOnOff(newProp >= 2f);
	}

	private void SetActiveEffectOnOff(bool bOn)
	{
		if (m_activeEffect != null)
		{
			if (bOn)
			{
				m_activeEffect.Play();
			}
			else
			{
				m_activeEffect.Stop();
			}
		}
	}

	private void SetOvermixedEffectOnOff(bool bOn)
	{
		if (m_overmixedEffect != null)
		{
			if (bOn)
			{
				m_overmixedEffect.Play();
			}
			else
			{
				m_overmixedEffect.Stop();
			}
		}
	}
}
