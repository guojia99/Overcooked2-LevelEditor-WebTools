#define ANALYTICS
using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

public class FrontendChefCustomisation : MonoBehaviour
{
	[SerializeField]
	public PlayerInputLookup.Player m_actualPlayer;

	[SerializeField]
	private ChefMeshReplacer m_chef;

	[SerializeField]
	private Image leftArrow;

	[SerializeField]
	private Image rightArrow;

	private PlayerGameInput m_assignedInput;

	private ILogicalButton m_leftButton;

	private ILogicalButton m_rightButton;

	private ChefAvatarData[] m_unlockedAvatars;

	private ChefAvatarData[] m_avatarDirectory;

	private int m_chefSelection = -1;

	private GameSession.SelectedChefData m_chefData;

	private Animator leftArrowAnimator;

	private Animator rightArrowAnimator;

	private Animator m_ChefAnimator;

	private bool m_bPlaySelecting;

	private static readonly int m_iTrigChooseChef = Animator.StringToHash("TrigChooseChef");

	private static readonly int m_iTrigChefChosen = Animator.StringToHash("TrigChefChosen");

	private static readonly int m_iPressed = Animator.StringToHash("Pressed");

	private int m_adminLayerMask;

	public bool IsActive()
	{
		return m_assignedInput != null;
	}

	private int SortAvatarsByDlc(ChefAvatarData a, ChefAvatarData b)
	{
		int num = ((!(a.ForDlc != null)) ? (-1) : a.ForDlc.m_DLCID);
		int value = ((!(b.ForDlc != null)) ? (-1) : b.ForDlc.m_DLCID);
		int num2 = num.CompareTo(value);
		if (num2 == 0)
		{
			int num3 = m_avatarDirectory.FindIndex_Predicate((ChefAvatarData x) => x == a);
			int value2 = m_avatarDirectory.FindIndex_Predicate((ChefAvatarData x) => x == b);
			num2 = num3.CompareTo(value2);
		}
		return num2;
	}

	public void RebindInput(PlayerGameInput _input)
	{
		if (base.enabled)
		{
			m_assignedInput = _input;
			m_leftButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UILeftPlayerSpecific, _input);
			m_rightButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UIRightPlayerSpecific, _input);
		}
	}

	public void Activate(PlayerGameInput _input, GameSession.SelectedChefData _default = null)
	{
		m_assignedInput = _input;
		m_chefData = _default;
		m_leftButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UILeftPlayerSpecific, _input);
		m_rightButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UIRightPlayerSpecific, _input);
		m_avatarDirectory = GameUtils.GetAvatarDirectoryData().Avatars;
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		m_unlockedAvatars = metaGameProgress.GetUnlockedAvatars();
		Array.Sort(m_unlockedAvatars, SortAvatarsByDlc);
		if (_default != null)
		{
			if (T17FrontendFlow.Instance != null && T17FrontendFlow.Instance.AutoOpenFrontendDlcData != null)
			{
				int dlcID = T17FrontendFlow.Instance.AutoOpenFrontendDlcData.m_DLCID;
				SetChefSelection(m_unlockedAvatars.FindIndex_Predicate((ChefAvatarData x) => x.ForDlc != null && x.ForDlc.m_DLCID == dlcID));
			}
			else
			{
				m_chefSelection = m_unlockedAvatars.FindIndex_Predicate((ChefAvatarData x) => x == _default.Character);
			}
		}
		if (leftArrow != null)
		{
			leftArrow.transform.parent.gameObject.SetActive(true);
			leftArrow.enabled = true;
			leftArrowAnimator = leftArrow.GetComponent<Animator>();
		}
		if (rightArrow != null)
		{
			rightArrow.transform.parent.gameObject.SetActive(true);
			rightArrow.enabled = true;
			rightArrowAnimator = rightArrow.GetComponent<Animator>();
		}
		m_adminLayerMask = LayerMask.NameToLayer("Administration");
		base.enabled = true;
	}

	public void ActivateLayoutOnly()
	{
		leftArrow.transform.parent.gameObject.SetActive(true);
		leftArrow.enabled = false;
		rightArrow.transform.parent.gameObject.SetActive(true);
		rightArrow.enabled = false;
		UnbindInput();
	}

	public void Deactivate()
	{
		if (m_chefData != null)
		{
			Analytics.LogEvent("Chef Picked", "Chef " + m_chefData.Character.HeadName, m_chefSelection);
		}
		UnbindInput();
		m_chefData = null;
		if (leftArrow != null)
		{
			leftArrow.transform.parent.gameObject.SetActive(false);
			leftArrow.enabled = false;
		}
		if (rightArrow != null)
		{
			rightArrow.transform.parent.gameObject.SetActive(false);
			rightArrow.enabled = false;
		}
		base.enabled = false;
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.AutoOpenChefSelectionMenu(null);
		}
	}

	private void UnbindInput()
	{
		m_assignedInput = null;
		m_leftButton = null;
		m_rightButton = null;
	}

	public GameSession.SelectedChefData GetCurrentSelection()
	{
		return m_chefData;
	}

	public PlayerGameInput GetAssignedInput()
	{
		return m_assignedInput;
	}

	private void Update()
	{
		if (m_leftButton == null || m_rightButton == null)
		{
			return;
		}
		if (T17DialogBoxManager.HasAnyOpenDialogs())
		{
			m_leftButton.ClaimPressEvent();
			m_rightButton.ClaimPressEvent();
			return;
		}
		if (m_leftButton.JustPressed())
		{
			OnClickLeftButton();
		}
		if (m_rightButton.JustPressed())
		{
			OnClickRightButton();
		}
	}

	public void OnClickLeftButton()
	{
		SetChefSelection(m_chefSelection - 1);
		leftArrowAnimator.SetTrigger(m_iPressed);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIButtonDown, m_adminLayerMask);
	}

	public void OnClickRightButton()
	{
		SetChefSelection(m_chefSelection + 1);
		rightArrowAnimator.SetTrigger(m_iPressed);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIButtonDown, m_adminLayerMask);
	}

	private void SetChefSelection(int _selection)
	{
		if (m_unlockedAvatars.Length > 0)
		{
			m_chefSelection = MathUtils.Wrap(_selection, 0, m_unlockedAvatars.Length);
			ChefAvatarData newAvatar = m_unlockedAvatars[m_chefSelection];
			m_chefData.Character = newAvatar;
			int num = m_avatarDirectory.FindIndex_Predicate((ChefAvatarData x) => x == newAvatar);
			uint num2 = 0u;
			num2 = ((num >= 0) ? ((uint)num) : 127u);
			FastList<User> users = ClientUserSystem.m_Users;
			if ((int)m_actualPlayer < users.Count)
			{
				ClientMessenger.ChefAvatar(num2, users._items[(int)m_actualPlayer]);
			}
		}
	}

	public void SetChefMeshReplacer(ChefMeshReplacer _mesh)
	{
		m_chef = _mesh;
	}

	public void SetChefAnimator(Animator animator)
	{
		m_ChefAnimator = animator;
		if (m_bPlaySelecting)
		{
			PlaySelectingAnimation();
			m_bPlaySelecting = false;
		}
	}

	public void PlaySelectedAnimation()
	{
		if (m_ChefAnimator != null)
		{
			m_ChefAnimator.SetTrigger(m_iTrigChefChosen);
		}
	}

	public void PlaySelectingAnimation()
	{
		if (m_ChefAnimator != null)
		{
			m_ChefAnimator.SetTrigger(m_iTrigChooseChef);
		}
	}
}
