using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackClipType(typeof(AmbiencePlayableAsset))]
[TrackColor(0.16f, 0.41f, 0.45f)]
public class AmbienceTrackAsset : TrackAsset
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
