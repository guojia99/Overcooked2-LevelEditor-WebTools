using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class DialogPlayableAsset : PlayableAsset
{
	[Header("Dialogue")]
	[SerializeField]
	private DialogueController.Dialogue m_dialogue;

	[Header("Positioning")]
	[SerializeField]
	private ExposedReference<Transform> m_followObject;

	[SerializeField]
	private Vector2 m_anchor = new Vector2(0.5f, 0.5f);

	[SerializeField]
	private Vector2 m_pivot = new Vector2(0.5f, 0.5f);

	[SerializeField]
	private float m_rotation;

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

	public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
	{
		ScriptPlayable<DialogPlayableBehaviour> scriptPlayable = ScriptPlayable<DialogPlayableBehaviour>.Create(graph);
		DialogPlayableBehaviour behaviour = scriptPlayable.GetBehaviour();
		Transform transform = m_followObject.Resolve(scriptPlayable.GetGraph().GetResolver());
		if (transform != null)
		{
			behaviour.Setup(m_dialogue, transform);
		}
		else
		{
			behaviour.Setup(m_dialogue, m_anchor, m_pivot, m_rotation);
		}
		return scriptPlayable;
	}
}
