using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackClipType(typeof(DialogPlayableAsset))]
[TrackColor(0.56f, 0.1f, 0.52f)]
public class DialogTrackAsset : TrackAsset
{
	public override IEnumerable<PlayableBinding> outputs
	{
		get
		{
			return base.outputs;
		}
	}

	protected override Playable CreatePlayable(PlayableGraph graph, GameObject go, TimelineClip clip)
	{
		return base.CreatePlayable(graph, go, clip);
	}

	public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
	{
		ScriptPlayable<DialogMixerBehaviour> scriptPlayable = ScriptPlayable<DialogMixerBehaviour>.Create(graph, inputCount);
		DialogMixerBehaviour behaviour = scriptPlayable.GetBehaviour();
		behaviour.CreateLoopsFromClips(GetClips());
		return scriptPlayable;
	}

	public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
	{
		base.GatherProperties(director, driver);
	}

	public override void OnEnable()
	{
		base.OnEnable();
	}
}
