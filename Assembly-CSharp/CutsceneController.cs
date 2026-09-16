using System;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneController : MonoBehaviour
{
	public class SetupData
	{
		public bool skippable;

		public bool postplaybackUIEnabled;
	}

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	public PlayableDirector m_director;

	[SerializeField]
	[AssignResource("CutsceneSkipUI", Editorbility.NonEditable)]
	public GameObject m_skipUIPrefab;

	[SerializeField]
	public GameObject m_gameCamera;

	private CallbackVoid m_skippedCallback = delegate
	{
	};

	public void RegisterSkipCallback(CallbackVoid _callback)
	{
		m_skippedCallback = (CallbackVoid)Delegate.Combine(m_skippedCallback, _callback);
	}

	public void DeregisterSkipCallback(CallbackVoid _callback)
	{
		m_skippedCallback = (CallbackVoid)Delegate.Remove(m_skippedCallback, _callback);
	}

	public void OnCutsceneSkipped()
	{
		m_skippedCallback();
	}
}
