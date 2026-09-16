using System;
using System.Reflection;
using UnityEngine;

public class RandomizeStateBehaviourOnState : StateMachineBehaviourEx
{
	[SerializeField]
	private StateMachineBehaviourEx m_otherBehaviour;

	[SerializeField]
	private string m_propertyName;

	[SerializeField]
	private float m_minValue;

	[SerializeField]
	private float m_maxValue = 1f;

	private StateMachineBehaviourEx m_otherBehaviourInstance;

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		StateMachineBehaviourEx instance = m_otherBehaviour.GetInstance(_animator);
		Type type = instance.GetType();
		FieldInfo field = type.GetField(m_propertyName);
		float num = UnityEngine.Random.Range(m_minValue, m_maxValue);
		field.SetValue(instance, num);
	}
}
