using System;
using UnityEngine;

public class SetVariableOnState : TriggerAfterTimeState
{
	[Serializable]
	private class BoolVariableData
	{
		public string VariableName;

		public bool Value;

		[HideInInspector]
		private int VariableNameHash;

		public int GetNameHash()
		{
			return VariableNameHash;
		}

		public void MakeHash()
		{
			VariableNameHash = Animator.StringToHash(VariableName);
		}
	}

	[Serializable]
	private class IntVariableData
	{
		public string VariableName;

		public int Value;

		[HideInInspector]
		private int VariableNameHash;

		public int GetNameHash()
		{
			return VariableNameHash;
		}

		public void MakeHash()
		{
			VariableNameHash = Animator.StringToHash(VariableName);
		}
	}

	[Serializable]
	private class FloatVariableData
	{
		public string VariableName;

		public float Value;

		[HideInInspector]
		private int VariableNameHash;

		public int GetNameHash()
		{
			return VariableNameHash;
		}

		public void MakeHash()
		{
			VariableNameHash = Animator.StringToHash(VariableName);
		}
	}

	[SerializeField]
	private string m_animatorName = string.Empty;

	[SerializeField]
	private BoolVariableData m_boolVariable;

	[SerializeField]
	private IntVariableData m_intVariable;

	[SerializeField]
	private FloatVariableData m_floatVariable;

	protected virtual void Awake()
	{
		if (m_boolVariable != null)
		{
			if (string.IsNullOrEmpty(m_boolVariable.VariableName))
			{
				m_boolVariable = null;
			}
			else
			{
				m_boolVariable.MakeHash();
			}
		}
		if (m_intVariable != null)
		{
			if (string.IsNullOrEmpty(m_intVariable.VariableName))
			{
				m_intVariable = null;
			}
			else
			{
				m_intVariable.MakeHash();
			}
		}
		if (m_floatVariable != null)
		{
			if (string.IsNullOrEmpty(m_floatVariable.VariableName))
			{
				m_floatVariable = null;
			}
			else
			{
				m_floatVariable.MakeHash();
			}
		}
	}

	protected override void PerformAction(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		Animator animator = _animator;
		if (m_animatorName != string.Empty)
		{
			animator = GameObject.Find(m_animatorName).RequireComponent<Animator>();
		}
		if (m_boolVariable != null && m_boolVariable.GetNameHash() != 0)
		{
			animator.SetBool(m_boolVariable.GetNameHash(), m_boolVariable.Value);
		}
		if (m_intVariable != null && m_intVariable.GetNameHash() != 0)
		{
			animator.SetInteger(m_intVariable.GetNameHash(), m_intVariable.Value);
		}
		if (m_floatVariable != null && m_floatVariable.GetNameHash() != 0)
		{
			animator.SetFloat(m_floatVariable.GetNameHash(), m_floatVariable.Value);
		}
	}
}
