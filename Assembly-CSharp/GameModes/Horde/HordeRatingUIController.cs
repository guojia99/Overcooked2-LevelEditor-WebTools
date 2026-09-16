using System.Collections;
using Team17.Online;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameModes.Horde
{
	public class HordeRatingUIController : UIControllerBase
	{
		public struct ScoreData
		{
			public float m_health;

			public int m_moneyEarned;

			public int m_enemiesDefeated;
		}

		[SerializeField]
		private T17Text m_levelTitleText;

		[SerializeField]
		private T17Text m_legendText;

		[SerializeField]
		private UIPlayerRootMenu m_uiPlayers;

		[SerializeField]
		public GameObject m_waitingForPlayers;

		[SerializeField]
		private ProgressBarUI m_healthBar;

		[SerializeField]
		private DisplayIntUIController m_health;

		[SerializeField]
		private DisplayIntUIController m_money;

		[SerializeField]
		private DisplayIntUIController m_enemies;

		[SerializeField]
		private Animator m_onionKingAnimator;

		[SerializeField]
		private Animator m_kevinAnimator;

		[SerializeField]
		[Range(0.001f, 10f)]
		private float m_tickUpDuration = 1f;

		private float m_tickUpProgress;

		private Animator m_animator;

		public string m_LegendText_Emote_NoRestart = "Text.Menu.Legend03";

		public string m_LegendText_NoEmote_NoRestart = "Text.Menu.Legend03NoEmote";

		public string m_LegendText_Emote_Restart = "Text.Menu.Legend03Restart";

		public string m_LegendText_NoEmote_Restart = "Text.Menu.Legend03RestartNoEmote";

		private static readonly string m_focusedLegendText = "Text.Menu.RoundResultsCancel";

		private static readonly string m_healthLocalisationTag = "Horde.Health";

		private static readonly int m_iHealthAnimHash = Animator.StringToHash("Health");

		private static readonly int m_iHealthBarAnimHash = Animator.StringToHash("DoProgress");

		private static readonly int m_iScoreAnimHash = Animator.StringToHash("Score");

		private ILogicalButton m_focusPlayersButton;

		private ILogicalButton m_backButton;

		private bool m_focusedOnPlayers;

		private const float c_timeForHealthBar = 2f;

		private void Awake()
		{
			m_animator = base.gameObject.RequireComponent<Animator>();
			m_focusPlayersButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIResultsToggleProfile);
			m_backButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
			m_uiPlayers.AllowSettingFocus = false;
			m_waitingForPlayers.SetActive(false);
		}

		private void Start()
		{
			UpdateLegend();
			if (T17EventSystemsManager.Instance != null)
			{
				T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
				if (eventSystemForEngagementSlot != null)
				{
					eventSystemForEngagementSlot.SetSelectedGameObject(null);
					((EventSystem)eventSystemForEngagementSlot).SetSelectedGameObject((GameObject)null);
				}
			}
		}

		private void UpdateLegend()
		{
			if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
			{
				m_legendText.SetLocalisedTextCatchAll(m_LegendText_Emote_NoRestart);
			}
			else if (!UserSystemUtils.AnySplitPadUsers())
			{
				m_legendText.SetLocalisedTextCatchAll((ClientGameSetup.Mode != GameMode.Campaign) ? m_LegendText_Emote_NoRestart : m_LegendText_Emote_Restart);
			}
			else
			{
				m_legendText.SetLocalisedTextCatchAll((ClientGameSetup.Mode != GameMode.Campaign) ? m_LegendText_NoEmote_NoRestart : m_LegendText_NoEmote_Restart);
			}
		}

		public void SetScoreData(object _scoreData)
		{
			ScoreData scoreData = (ScoreData)_scoreData;
			GameSession gameSession = GameUtils.GetGameSession();
			SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
			int levelID = GameUtils.GetLevelID();
			GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(levelID);
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelID];
			SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = gameSession.LevelSettings.SceneDirectoryVarientEntry;
			m_levelTitleText.SetLocalisedTextCatchAll(sceneDirectoryEntry.Label);
			m_health.Value = Mathf.RoundToInt(scoreData.m_health * 100f);
			m_money.Value = scoreData.m_moneyEarned;
			m_enemies.Value = scoreData.m_enemiesDefeated;
			if (m_uiPlayers != null)
			{
				GamepadUser user = GameUtils.RequestManager<PlayerManager>().GetUser(EngagementSlot.One);
				m_uiPlayers.Show(user, null, null);
			}
			if (m_onionKingAnimator != null)
			{
				m_onionKingAnimator.SetFloat(m_iScoreAnimHash, scoreData.m_health);
				m_onionKingAnimator.Update(0f);
			}
			if (m_kevinAnimator != null)
			{
				m_kevinAnimator.SetFloat(m_iScoreAnimHash, scoreData.m_health);
				m_kevinAnimator.Update(0f);
			}
			StartCoroutine(TickUpScore(scoreData));
		}

		private IEnumerator TickUpScore(ScoreData _scoreData)
		{
			m_healthBar.Value = 0f;
			while (!m_animator.GetBool("Ready"))
			{
				yield return null;
			}
			m_animator.SetBool(m_iHealthBarAnimHash, true);
			m_tickUpProgress = 0f;
			while (m_tickUpProgress < 1f)
			{
				m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / 2f, 1f);
				m_healthBar.Value = _scoreData.m_health * m_tickUpProgress;
				m_animator.SetInteger(m_iHealthAnimHash, Mathf.RoundToInt(_scoreData.m_health * 100f * m_tickUpProgress));
				yield return null;
			}
			m_animator.SetBool(m_iHealthBarAnimHash, false);
		}

		public bool HasAnimationSettled()
		{
			return !m_animator.GetBool(m_iHealthBarAnimHash);
		}

		public bool AllowedToSkip()
		{
			if (T17EventSystemsManager.Instance != null)
			{
				T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
				if (eventSystemForEngagementSlot != null)
				{
					return eventSystemForEngagementSlot.currentSelectedGameObject == null;
				}
			}
			return true;
		}

		public bool AllowedToRestart()
		{
			return !m_focusedOnPlayers;
		}

		private void Update()
		{
			if (!(m_uiPlayers != null))
			{
				return;
			}
			if (m_focusPlayersButton.JustPressed())
			{
				m_focusPlayersButton.ClaimPressEvent();
				if (T17EventSystemsManager.Instance != null)
				{
					T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
					if (eventSystemForEngagementSlot != null && eventSystemForEngagementSlot.currentSelectedGameObject == null)
					{
						m_focusedOnPlayers = true;
						m_uiPlayers.FocusOnFirstPlayer(true);
						m_legendText.SetLocalisedTextCatchAll(m_focusedLegendText);
					}
				}
			}
			if (!m_backButton.JustPressed())
			{
				return;
			}
			m_backButton.ClaimPressEvent();
			if (T17EventSystemsManager.Instance == null)
			{
				return;
			}
			T17EventSystem eventSystemForEngagementSlot2 = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
			if (eventSystemForEngagementSlot2 != null)
			{
				if (eventSystemForEngagementSlot2.currentSelectedGameObject != null)
				{
					m_uiPlayers.CloseAllPlayerMenus();
					eventSystemForEngagementSlot2.SetSelectedGameObject(null);
					((EventSystem)eventSystemForEngagementSlot2).SetSelectedGameObject((GameObject)null);
					UpdateLegend();
				}
				else
				{
					m_focusedOnPlayers = false;
				}
			}
		}
	}
}
