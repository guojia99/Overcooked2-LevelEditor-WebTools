using System;
using System.Reflection;
using UnityEngine;

public class SetStateBehaviourVariableOnState : StateMachineBehaviourEx
{
	[SerializeField]
	private StateMachineBehaviourEx m_otherBehaviour;

	[SerializeField]
	private string m_behaviourVariable;

	[SerializeField]
	private string m_animatorVariable;

	[SerializeField]
	private AnimatorVariableType m_valueType;

	private int m_animatorVariableHash;

	protected virtual void Awake()
	{
		m_animatorVariableHash = Animator.StringToHash(m_animatorVariable);
	}

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		StateMachineBehaviourEx instance = m_otherBehaviour.GetInstance(_animator);
		Type type = instance.GetType();
		FieldInfo field = type.GetField(m_behaviourVariable);
		object value = AnimatorUtils.GetValue(_animator, m_animatorVariableHash, m_valueType);
		field.SetValue(instance, value);
	}
}
