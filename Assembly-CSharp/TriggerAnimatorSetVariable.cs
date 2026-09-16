using System;
using UnityEngine;

public class TriggerAnimatorSetVariable : MonoBehaviour
{
	[SerializeField]
	public bool m_onAwake;

	[SerializeField]
	[HideInInspectorTest("m_onAwake", false)]
	public string m_triggerToReceive;

	[SerializeField]
	[AssignComponent(Editorbility.Editable)]
	public Animator m_targetAnimator;

	[SerializeField]
	public string m_variableName;

	[SerializeField]
	public AnimatorVariableType m_variableType;

	[SerializeField]
	public bool m_randomValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Bool, "m_randomValue", false)]
	public bool m_boolValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Int, "m_randomValue", false)]
	public int m_intValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Int, "m_randomValue", true)]
	public int m_minIntValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Int, "m_randomValue", true)]
	public int m_maxIntValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Float, "m_randomValue", false)]
	public float m_floatValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Float, "m_randomValue", true)]
	public float m_minFloatValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Float, "m_randomValue", true)]
	public float m_maxFloatValue;

	[NonSerialized]
	public int m_variableNameHash;

	protected virtual void Awake()
	{
		m_variableNameHash = Animator.StringToHash(m_variableName);
	}
}
