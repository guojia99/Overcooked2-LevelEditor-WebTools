using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.UI;

public abstract class ClientIconTutorialBase : ClientSynchroniserBase
{
	public class IconData
	{
		public ActiveQuery ActiveCallback;

		public HoverIconUIController Icon;

		private bool m_active;

		private IconTutorialBase m_tutorialBase;

		public IconData(IconTutorialBase _tutorial, Sprite _sprite, Transform _parent, ActiveQuery _callback)
		{
			m_tutorialBase = _tutorial;
			ActiveCallback = _callback;
			GameObject obj = GameUtils.InstantiateHoverIconUIController(_tutorial.m_iconPrefab, _parent, "HoverIconCanvas");
			Icon = obj.RequireComponent<HoverIconUIController>();
			GameObject gameObject = Icon.transform.Find("Icon").gameObject;
			Image image = gameObject.RequireComponent<Image>();
			image.sprite = _sprite;
			m_active = false;
			Icon.gameObject.SetActive(m_active);
		}

		public void UpdateSynchronising()
		{
			Transform _followParent = Icon.GetFollowTransform();
			Transform transform = _followParent;
			bool flag = ActiveCallback(ref _followParent, Icon);
			if (_followParent != transform)
			{
				Icon.SetFollowTransform(_followParent);
			}
			if (m_active != flag)
			{
				Icon.gameObject.SetActive(flag);
				m_active = flag;
				if (flag)
				{
					GameUtils.InstantiateHoverIconUIController(m_tutorialBase.m_attentionDrawer, Icon.GetFollowTransform(), "HoverIconCanvas");
				}
			}
		}
	}

	public delegate bool ActiveQuery(ref Transform _followParent, HoverIconUIController _controller);

	private IconTutorialBase m_iconTutorial;

	protected IFlowController m_iClientFlowController;

	protected IconData[] m_icons = new IconData[0];

	private bool m_tutorialActive;

	private bool m_completed;

	private GameObject m_activeTutorialPanel;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_iconTutorial = (IconTutorialBase)synchronisedObject;
		FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
		m_iClientFlowController = flowControllerBase.gameObject.RequireComponent<ClientFlowControllerBase>();
		m_iClientFlowController.RoundActivatedCallback += EnterRound;
		m_iClientFlowController.RoundDeactivatedCallback += ExitRound;
	}

	public override void UpdateSynchronising()
	{
		if (m_tutorialActive)
		{
			OnTutorialUpdate();
			for (int i = 0; i < m_icons.Length; i++)
			{
				m_icons[i].UpdateSynchronising();
			}
		}
	}

	protected virtual void OnStartTutorial()
	{
	}

	protected virtual void OnStopTutorial()
	{
	}

	protected void DisableIcons()
	{
		for (int i = 0; i < m_icons.Length; i++)
		{
			Object.Destroy(m_icons[i].Icon.gameObject);
		}
		m_icons = new IconData[0];
	}

	private void EnterRound()
	{
		if (!m_completed)
		{
			m_tutorialActive = true;
			OnStartTutorial();
		}
	}

	private void ExitRound()
	{
		if (m_tutorialActive)
		{
			DisableIcons();
			if (m_activeTutorialPanel != null)
			{
				Object.Destroy(m_activeTutorialPanel);
			}
			m_tutorialActive = false;
			OnStopTutorial();
		}
	}

	protected void CompleteTutorial()
	{
		ExitRound();
		m_completed = true;
	}

	protected virtual void OnTutorialUpdate()
	{
	}

	protected IEnumerator RunTutorialPanel(GameObject _uiPrefab)
	{
		ILogicalButton iLogicalButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
		float t = 0f;
		Generic<bool> testSelect = delegate
		{
			t += Time.unscaledDeltaTime;
			if (t > 1f)
			{
				return iLogicalButton.JustPressed();
			}
			iLogicalButton.ClaimPressEvent();
			return false;
		};
		IEnumerator panelRoutine = RunPanel(_uiPrefab, testSelect);
		while (panelRoutine.MoveNext())
		{
			yield return null;
		}
	}

	protected IEnumerator RunPanel(GameObject _uiPrefab, Generic<bool> _waitForEnd)
	{
		while (m_activeTutorialPanel != null)
		{
			yield return null;
		}
		TimeManager timeManager = GameUtils.RequestManager<TimeManager>();
		timeManager.SetPaused(TimeManager.PauseLayer.Main, true, this);
		m_activeTutorialPanel = GameUtils.InstantiateUIController(_uiPrefab, "UICanvas");
		while (!_waitForEnd())
		{
			yield return null;
		}
		Object.Destroy(m_activeTutorialPanel);
		timeManager.SetPaused(TimeManager.PauseLayer.Main, false, this);
	}
}
