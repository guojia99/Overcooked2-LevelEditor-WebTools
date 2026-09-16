using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTutorialPopupController : ClientSynchroniserBase
{
	private TutorialPopupController m_controller;

	private GameObject m_popup;

	private Canvas m_hudCanvas;

	private Canvas m_hoverIconCanvas;

	private bool m_dismissed;

	public override EntityType GetEntityType()
	{
		return EntityType.TutorialPopup;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_controller = (TutorialPopupController)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TutorialDismissMessage tutorialDismissMessage = (TutorialDismissMessage)serialisable;
		if (tutorialDismissMessage != null)
		{
			m_dismissed = true;
		}
	}

	public IEnumerator ShowTutorial(TutorialPopup _data, Generic<IEnumerator, GameObject> _dismisser)
	{
		m_popup = _data.Spawn();
		m_popup.SetActive(false);
		if (m_hudCanvas == null)
		{
			m_hudCanvas = GameUtils.GetNamedCanvas("ScalingHUDCanvas").RequireComponent<Canvas>();
		}
		if (m_hoverIconCanvas == null)
		{
			m_hoverIconCanvas = GameUtils.GetNamedCanvas("HoverIconCanvas").RequireComponent<Canvas>();
		}
		return RunTutorial(m_popup, _dismisser);
	}

	public void Shutdown()
	{
		if (m_popup != null)
		{
			Object.Destroy(m_popup);
			m_popup = null;
		}
	}

	private IEnumerator RunTutorial(GameObject _popup, Generic<IEnumerator, GameObject> _dismisser)
	{
		if (!(_popup != null))
		{
			yield break;
		}
		TimeManager timeManager = GameUtils.RequireManager<TimeManager>();
		timeManager.SetPaused(TimeManager.PauseLayer.Main, true, this);
		timeManager.SetPaused(TimeManager.PauseLayer.Camera, true, this);
		m_hudCanvas.enabled = false;
		m_hoverIconCanvas.enabled = false;
		_popup.SetActive(true);
		GameUtils.TriggerAudio(GameOneShotAudioTag.TutorialPopIn, base.gameObject.layer);
		m_dismissed = false;
		IEnumerator dismissRoutine = _dismisser(_popup);
		while (!m_dismissed)
		{
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				if (!dismissRoutine.MoveNext())
				{
					m_dismissed = true;
					m_controller.OnTutorialDismissed();
				}
				yield return null;
			}
			else
			{
				yield return null;
			}
		}
		_popup.SetActive(false);
		GameUtils.TriggerAudio(GameOneShotAudioTag.TutorialPopOut, base.gameObject.layer);
		m_hudCanvas.enabled = true;
		m_hoverIconCanvas.enabled = true;
		timeManager.SetPaused(TimeManager.PauseLayer.Camera, false, this);
		timeManager.SetPaused(TimeManager.PauseLayer.Main, false, this);
	}
}
