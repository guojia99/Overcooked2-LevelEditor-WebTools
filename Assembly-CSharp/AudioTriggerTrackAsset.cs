using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackClipType(typeof(AudioTriggerPlayableAsset))]
[TrackColor(0.24f, 0.42f, 0.78f)]
public class AudioTriggerTrackAsset : TrackAsset
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
		return base.CreateTrackMixer(graph, go, inputCount);
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
