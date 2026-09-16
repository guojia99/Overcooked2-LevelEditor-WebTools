using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class AnimatorStatePlayableAsset : PlayableAsset
{
	[SerializeField]
	private string m_variableName;

	[SerializeField]
	private AnimatorVariableType m_variableType;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Bool)]
	private bool m_boolValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Int)]
	private int m_intValue;

	[SerializeField]
	[HideInInspectorTest("m_variableType", AnimatorVariableType.Float)]
	private float m_floatValue;

	public override double duration
	{
		get
		{
			return base.duration;
		}
	}

	public override IEnumerable<PlayableBinding> outputs
	{
		get
		{
			return base.outputs;
		}
	}

	public object GetValue()
	{
		switch (m_variableType)
		{
		case AnimatorVariableType.Bool:
			return m_boolValue;
		case AnimatorVariableType.Int:
			return m_intValue;
		case AnimatorVariableType.Float:
			return m_floatValue;
		default:
			return null;
		}
	}

	public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
	{
		ScriptPlayable<AnimatorStatePlayableBehaviour> scriptPlayable = ScriptPlayable<AnimatorStatePlayableBehaviour>.Create(graph);
		AnimatorStatePlayableBehaviour behaviour = scriptPlayable.GetBehaviour();
		behaviour.Setup(m_variableName, m_variableType, GetValue());
		return scriptPlayable;
	}
}
