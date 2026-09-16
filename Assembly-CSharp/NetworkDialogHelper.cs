using System;

internal static class NetworkDialogHelper
{
	public static void ShowRemoveSplitPadUsersDialog(T17DialogBox.DialogEvent OnConfirm, T17DialogBox.DialogEvent OnCancel)
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.Invite.WarnRemoveLocals", "Text.Button.Continue", null, "Text.Button.Cancel");
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, OnConfirm);
			dialog.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnCancel, OnCancel);
			dialog.Show();
		}
	}

	public static void ShowNoOnlineUsersDialog(T17DialogBox.DialogEvent OnConfirm)
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.NoOnlineUsers", "Text.Button.Continue", string.Empty, string.Empty);
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, OnConfirm);
			dialog.Show();
		}
	}

	public static void ShowFullLobbyDialog()
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.FullKitchen", "Text.Button.Continue", string.Empty, string.Empty);
			dialog.Show();
		}
	}

	public static void ShowGoingOfflineDialog(T17DialogBox.DialogEvent OnConfirm)
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.WarnGoingOffline", "Text.Button.Continue", null, "Text.Button.Cancel");
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, OnConfirm);
			dialog.Show();
		}
	}

	public static void ShowMoreUsersRequiredDialog()
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.MoreUsersNeeded", "Text.Button.Okay", string.Empty, string.Empty);
			dialog.Show();
		}
	}

	public static void ShowNoWirelessUsersDialog()
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.NoWirelessUsers", "Text.Button.Continue", string.Empty, string.Empty);
			dialog.Show();
		}
	}

	public static void ShowTooManyLocalUsersForJoining()
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Warning", "MainMenu.Kitchen.TooManyLocalUsersForJoining", "Text.Button.Continue", string.Empty, string.Empty);
			dialog.Show();
		}
	}
}
