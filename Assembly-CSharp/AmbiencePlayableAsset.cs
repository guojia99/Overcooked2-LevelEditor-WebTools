using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class AmbiencePlayableAsset : PlayableAsset
{
	[SerializeField]
	private GameLoopingAudioTag m_tag = GameLoopingAudioTag.COUNT;

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
		ScriptPlayable<AmbiencePlayableBehaviour> scriptPlayable = ScriptPlayable<AmbiencePlayableBehaviour>.Create(graph);
		AmbiencePlayableBehaviour behaviour = scriptPlayable.GetBehaviour();
		behaviour.Setup(m_tag);
		return scriptPlayable;
	}
}
