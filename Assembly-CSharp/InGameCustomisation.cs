using System;
using Team17.Online;
using UnityEngine;

public class InGameCustomisation : InGameMenuBehaviour
{
	[SerializeField]
	private UIPlayerRootMenu m_uiPlayerRoot;

	[SerializeField]
	private FrontendChefCustomisation[] m_chefCustomisation;

	[SerializeField]
	private GameObject m_mouseBlock;

	private ChefCustomiser m_customiser = new ChefCustomiser();

	private GenericVoid<bool> m_onActiveToggle;

	private static InGameCustomisation s_instance;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_cancelButton;

	public static InGameCustomisation Instance
	{
		get
		{
			return s_instance;
		}
	}

	protected override void Awake()
	{
		if (s_instance != null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		s_instance = this;
		base.Awake();
		if (m_mouseBlock != null)
		{
			m_mouseBlock.SetActive(false);
		}
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_customiser.SetChefs(m_chefCustomisation);
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		m_selectButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.UISelect, PlayerInputLookup.Player.One, PadSide.Both);
		m_cancelButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.UICancel, PlayerInputLookup.Player.One, PadSide.Both);
		UpdateChefs();
		m_customiser.ActivateChefCustomisation(true);
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		if (m_selectButton != null)
		{
			m_selectButton.ClaimPressEvent();
			m_selectButton.ClaimReleaseEvent();
		}
		m_customiser.CacheCurrentAvatars();
		if (m_mouseBlock != null)
		{
			m_mouseBlock.SetActive(true);
		}
		OnActiveToggle(true);
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_customiser.ActivateChefCustomisation(false);
		m_customiser.PlaySelectedAnimations();
		if (m_mouseBlock != null)
		{
			m_mouseBlock.SetActive(false);
		}
		OnActiveToggle(false);
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	private void UpdateChefs()
	{
		for (int i = 0; i < m_chefCustomisation.Length; i++)
		{
			FrontendChefCustomisation frontendChefCustomisation = m_chefCustomisation[i];
			UIPlayerMenuBehaviour uIPlayerMenuBehaviour = ((m_uiPlayerRoot.UIPlayers.Count <= i) ? null : m_uiPlayerRoot.UIPlayers[i]);
			ChefMeshReplacer chefMeshReplacer = null;
			Animator chefAnimator = null;
			if (uIPlayerMenuBehaviour != null)
			{
				chefMeshReplacer = uIPlayerMenuBehaviour.gameObject.RequestComponent<ChefMeshReplacer>();
				if (chefMeshReplacer != null && chefMeshReplacer.ChefModel != null)
				{
					chefAnimator = chefMeshReplacer.ChefModel.RequireComponent<Animator>();
				}
			}
			frontendChefCustomisation.SetChefMeshReplacer(chefMeshReplacer);
			frontendChefCustomisation.SetChefAnimator(chefAnimator);
			frontendChefCustomisation.m_actualPlayer = (PlayerInputLookup.Player)i;
		}
	}

	private void OnUsersChanged()
	{
		UpdateChefs();
	}

	private void OnActiveToggle(bool _active)
	{
		if (m_onActiveToggle != null)
		{
			m_onActiveToggle(_active);
		}
	}

	public void RegisterOnActiveToggle(GenericVoid<bool> _callback)
	{
		m_onActiveToggle = (GenericVoid<bool>)Delegate.Combine(m_onActiveToggle, _callback);
	}

	public void UnregisterOnActiveToggle(GenericVoid<bool> _callback)
	{
		m_onActiveToggle = (GenericVoid<bool>)Delegate.Remove(m_onActiveToggle, _callback);
	}

	protected override void Update()
	{
		base.Update();
		if (m_selectButton.JustPressed())
		{
			m_selectButton.ClaimPressEvent();
			m_selectButton.ClaimReleaseEvent();
			Hide();
		}
		if (m_cancelButton.JustPressed())
		{
			m_cancelButton.ClaimPressEvent();
			m_cancelButton.ClaimReleaseEvent();
		}
	}

	public override void Close()
	{
		m_customiser.RevertAvatars();
		base.Close();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		s_instance = null;
	}
}
