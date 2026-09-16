using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class MusicPlayableAsset : PlayableAsset
{
	[SerializeField]
	private ExposedReference<AudioClip> m_audioFile;

	[SerializeField]
	private bool m_killPrevious;

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
		ScriptPlayable<MusicPlayableBehaviour> scriptPlayable = ScriptPlayable<MusicPlayableBehaviour>.Create(graph);
		MusicPlayableBehaviour behaviour = scriptPlayable.GetBehaviour();
		AudioClip clip = m_audioFile.Resolve(scriptPlayable.GetGraph().GetResolver());
		behaviour.Setup(clip, m_killPrevious);
		return scriptPlayable;
	}
}
