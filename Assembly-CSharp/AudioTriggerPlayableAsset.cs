using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class AudioTriggerPlayableAsset : PlayableAsset
{
	[SerializeField]
	private GameOneShotAudioTag m_tag = GameOneShotAudioTag.COUNT;

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
		ScriptPlayable<AudioTriggerPlayableBehaviour> scriptPlayable = ScriptPlayable<AudioTriggerPlayableBehaviour>.Create(graph);
		AudioTriggerPlayableBehaviour behaviour = scriptPlayable.GetBehaviour();
		behaviour.Setup(m_tag);
		return scriptPlayable;
	}
}
