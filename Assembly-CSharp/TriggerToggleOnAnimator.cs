using System;
using UnityEngine;

public class TriggerToggleOnAnimator : MonoBehaviour
{
	[SerializeField]
	public string m_triggerToReceive = string.Empty;

	[SerializeField]
	public Animator m_targetAnimator;

	[SerializeField]
	public string m_targetParameter = string.Empty;

	[SerializeField]
	public bool m_initialValue;

	[NonSerialized]
	public int m_targetParameterHash;

	protected virtual void Awake()
	{
		m_targetParameterHash = Animator.StringToHash(m_targetParameter);
	}
}
