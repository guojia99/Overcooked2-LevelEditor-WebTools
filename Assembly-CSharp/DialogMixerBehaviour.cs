using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class DialogMixerBehaviour : PlayableBehaviour
{
	private struct Loop
	{
		public int input;

		public double start;

		public double end;

		public double duration
		{
			get
			{
				return end - start;
			}
		}
	}

	private List<Loop> m_loops = new List<Loop>();

	private PlayableDirector m_playableDirector;

	private double m_previousTime;

	private DialogPlayableBehaviour m_loopedBehaviour;

	public void CreateLoopsFromClips(IEnumerable<TimelineClip> _clips)
	{
		m_loops.Clear();
		int num = 0;
		foreach (TimelineClip _clip in _clips)
		{
			RegisterLoop(num++, _clip.start, _clip.end);
		}
	}

	private void RegisterLoop(int input, double startTime, double endTime)
	{
		Loop item = new Loop
		{
			input = input,
			start = startTime,
			end = endTime
		};
		int num = m_loops.FindLastIndex((Loop x) => x.start <= startTime);
		if (num >= 0)
		{
			m_loops.Insert(num, item);
		}
		else
		{
			m_loops.Add(item);
		}
	}

	public override void OnPlayableCreate(Playable playable)
	{
		base.OnPlayableCreate(playable);
		m_playableDirector = (PlayableDirector)playable.GetGraph().GetResolver();
	}

	public override void OnPlayableDestroy(Playable playable)
	{
		base.OnPlayableCreate(playable);
		m_loops.Clear();
		m_playableDirector = null;
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		base.OnBehaviourPlay(playable, info);
		m_previousTime = m_playableDirector.time;
		m_loopedBehaviour = null;
		for (int i = 0; i < m_loops.Count; i++)
		{
			DialogPlayableBehaviour dialogPlayableBehaviour = ExtractBehaviourForLoop(m_loops[i], playable);
			dialogPlayableBehaviour.Mixer = this;
		}
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		base.OnBehaviourPause(playable, info);
		m_loopedBehaviour = null;
	}

	public override void PrepareFrame(Playable playable, FrameData info)
	{
		if (info.evaluationType == FrameData.EvaluationType.Evaluate)
		{
			return;
		}
		if (info.seekOccurred)
		{
			m_previousTime = playable.GetTime();
		}
		else
		{
			if (!Application.isPlaying)
			{
				return;
			}
			double previousTime = m_previousTime;
			double time = m_playableDirector.time;
			Loop? loop = null;
			for (int i = 0; i < m_loops.Count; i++)
			{
				Loop loop2 = m_loops[i];
				if (previousTime >= loop2.start && previousTime <= loop2.end && time >= loop2.end && (!loop.HasValue || !(loop2.duration > loop.Value.duration)))
				{
					DialogPlayableBehaviour dialogPlayableBehaviour = ExtractBehaviourForLoop(loop2, playable);
					if (dialogPlayableBehaviour != null && dialogPlayableBehaviour.IsLoopActive())
					{
						loop = loop2;
					}
				}
			}
			if (!loop.HasValue)
			{
			}
			if (loop.HasValue)
			{
				DialogPlayableBehaviour loopedBehaviour = ExtractBehaviourForLoop(loop.Value, playable);
				m_loopedBehaviour = loopedBehaviour;
				m_playableDirector.time = loop.Value.start + (double)info.deltaTime;
				m_previousTime = loop.Value.start;
			}
			else
			{
				m_previousTime = m_playableDirector.time;
			}
		}
	}

	private DialogPlayableBehaviour ExtractBehaviourForLoop(Loop _loop, Playable _playable)
	{
		Playable input = _playable.GetInput(_loop.input);
		return ((ScriptPlayable<DialogPlayableBehaviour>)input).GetBehaviour();
	}

	public bool HasPendingLoop(DialogPlayableBehaviour _behaviour)
	{
		return _behaviour == m_loopedBehaviour;
	}

	public void CompletePendingLoop(DialogPlayableBehaviour _behaviour)
	{
		if (m_loopedBehaviour == _behaviour)
		{
			m_loopedBehaviour = null;
		}
	}
}
