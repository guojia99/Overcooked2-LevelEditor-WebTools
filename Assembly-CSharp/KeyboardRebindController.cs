using System;
using InControl;
using UnityEngine;

public class KeyboardRebindController : MonoBehaviour
{
	private KeyboardRebindElementSet[] m_RebindElementSets;

	private BaseMenuBehaviour m_ParentScreen;

	private T17DialogBox m_RebindDialog;

	private T17DialogBox m_UnsavedChangesDialog;

	private T17DialogBox m_InvalidDialog;

	private bool m_UnsavedChanges;

	private ILogicalButton m_uiCancelButton;

	public bool IsRebinding
	{
		get
		{
			return m_RebindDialog != null;
		}
	}

	public bool UnsavedChanges
	{
		get
		{
			return m_UnsavedChanges;
		}
	}

	public void SingleTimeInitialize()
	{
		m_RebindElementSets = GetComponentsInChildren<KeyboardRebindElementSet>(true);
		m_uiCancelButton = PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UICancel);
	}

	public void OnShow(BaseMenuBehaviour parentScreen)
	{
		m_ParentScreen = parentScreen;
		m_UnsavedChanges = false;
		RefreshBindingElements();
	}

	public void StartRebind(KeyboardRebindElement element)
	{
		if (!ShowRebindDialog(element))
		{
			return;
		}
		PCPadInputProvider.StartListeningForBinding(delegate(Key key)
		{
			HideRebindDialog();
			if (key != Key.Escape)
			{
				UpdateBinding(element, key);
			}
		});
	}

	private void Update()
	{
		if (m_uiCancelButton != null && m_uiCancelButton.JustReleased())
		{
			m_uiCancelButton.ClaimPressEvent();
			m_uiCancelButton.ClaimReleaseEvent();
			HideRebindDialog();
		}
	}

	public void CancelRebind()
	{
		PCPadInputProvider.StopListeningForBinding();
		HideRebindDialog();
	}

	public void CancelAndCloseAllDialogs()
	{
		CancelRebind();
		if (m_UnsavedChanges)
		{
			DiscardChanges();
			m_UnsavedChanges = false;
		}
		if (null != m_UnsavedChangesDialog)
		{
			m_UnsavedChangesDialog.Hide();
			m_UnsavedChangesDialog = null;
		}
		if (null != m_InvalidDialog)
		{
			m_InvalidDialog.Hide();
			m_InvalidDialog = null;
		}
	}

	private void UpdateBinding(KeyboardRebindElement element, Key key)
	{
		m_UnsavedChanges = true;
		KeyboardRebindElementSet elementSet = element.ElementSet;
		for (int i = 0; i < elementSet.ElementCount; i++)
		{
			if (elementSet[i] == element)
			{
				elementSet[i].SetBinding(key);
			}
			else
			{
				elementSet[i].UnsetBinding(key);
			}
		}
		RefreshBindingElements();
	}

	public bool Validate()
	{
		if (m_RebindElementSets != null)
		{
			for (int i = 0; i < m_RebindElementSets.Length; i++)
			{
				for (int j = 0; j < m_RebindElementSets[i].ElementCount; j++)
				{
					if (!m_RebindElementSets[i][j].HasAnyBindings())
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	private bool ShowRebindDialog(KeyboardRebindElement element)
	{
		if (m_RebindDialog != null)
		{
			return false;
		}
		m_ParentScreen.CachedEventSystem.SetSelectedGameObject(element.gameObject);
		m_RebindDialog = T17DialogBoxManager.GetDialog(false);
		if (m_RebindDialog != null)
		{
			string message = Localization.Get("Text.ControlsMenu.RemapBody") + Localization.Get(element.ActionTag) + Localization.Get("Text.ControlsMenu.Bracket");
			m_RebindDialog.Initialize("Text.ControlsMenu.RemapTitle", message, null, null, null, T17DialogBox.Symbols.Unassigned, true, false);
			m_RebindDialog.Show();
		}
		return m_RebindDialog != null;
	}

	private void HideRebindDialog()
	{
		if (m_RebindDialog != null)
		{
			m_RebindDialog.Hide();
			m_RebindDialog = null;
		}
	}

	public bool ShowUnsavedChangesDialog()
	{
		if (m_UnsavedChangesDialog != null)
		{
			return true;
		}
		m_UnsavedChangesDialog = T17DialogBoxManager.GetDialog(false);
		if (m_UnsavedChangesDialog != null)
		{
			m_UnsavedChangesDialog.Initialize("Text.Warning", "Text.Menu.UnsavedChanges.Body", "Text.Button.Discard", "Text.Button.Save", null);
			T17DialogBox unsavedChangesDialog = m_UnsavedChangesDialog;
			unsavedChangesDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(unsavedChangesDialog.OnConfirm, (T17DialogBox.DialogEvent)delegate
			{
				m_UnsavedChangesDialog = null;
				DiscardChanges();
				m_ParentScreen.Hide();
			});
			T17DialogBox unsavedChangesDialog2 = m_UnsavedChangesDialog;
			unsavedChangesDialog2.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(unsavedChangesDialog2.OnDecline, (T17DialogBox.DialogEvent)delegate
			{
				m_UnsavedChangesDialog = null;
				if (Validate())
				{
					SaveChanges();
					m_ParentScreen.Hide();
				}
				else
				{
					ShowInvalidDialog();
				}
			});
			m_UnsavedChangesDialog.Show();
		}
		return m_UnsavedChangesDialog != null;
	}

	public bool ShowInvalidDialog()
	{
		if (m_InvalidDialog != null)
		{
			return true;
		}
		m_InvalidDialog = T17DialogBoxManager.GetDialog(false);
		if (m_InvalidDialog != null)
		{
			m_InvalidDialog.Initialize("Text.Warning", "Text.ControlsMenu.Warning", "Text.Button.Okay", null, null);
			T17DialogBox invalidDialog = m_InvalidDialog;
			invalidDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(invalidDialog.OnConfirm, (T17DialogBox.DialogEvent)delegate
			{
				m_InvalidDialog = null;
			});
			m_InvalidDialog.Show();
		}
		return m_InvalidDialog != null;
	}

	public void OnDefaultsPressed()
	{
		ControlSchemeToggle componentInParent = GetComponentInParent<ControlSchemeToggle>();
		if (componentInParent != null)
		{
			if (componentInParent.IsCurrentSchemeSplit())
			{
				PCPadInputProvider.RestoreDefaultSplitBindings();
			}
			else
			{
				PCPadInputProvider.RestoreDefaultCombinedBindings();
			}
		}
		else
		{
			PCPadInputProvider.RestoreDefaultBindings();
		}
		RefreshBindingElements();
		m_UnsavedChanges = true;
	}

	public void OnApplyPressed()
	{
		if (!Validate())
		{
			ShowInvalidDialog();
			return;
		}
		SaveChanges();
		m_ParentScreen.Hide();
	}

	private void RefreshBindingElements()
	{
		if (m_RebindElementSets == null)
		{
			return;
		}
		for (int i = 0; i < m_RebindElementSets.Length; i++)
		{
			for (int j = 0; j < m_RebindElementSets[i].ElementCount; j++)
			{
				m_RebindElementSets[i][j].RefreshBindingText();
			}
		}
	}

	private void SaveChanges()
	{
		SaveManager saveManager = GameUtils.RequestManager<SaveManager>();
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		if (saveManager != null && metaGameProgress != null && metaGameProgress.SaveData != null)
		{
			PCPadInputProvider.SaveBindings(metaGameProgress.SaveData);
			saveManager.SaveMetaProgress();
		}
	}

	private void DiscardChanges()
	{
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		if (metaGameProgress != null && metaGameProgress.SaveData != null)
		{
			PCPadInputProvider.LoadBindings(metaGameProgress.SaveData);
		}
	}

	private void OnDestroy()
	{
		if (m_UnsavedChanges)
		{
			DiscardChanges();
		}
	}
}
