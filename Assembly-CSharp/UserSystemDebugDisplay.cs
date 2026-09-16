using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

internal class UserSystemDebugDisplay : DebugDisplay
{
	private IOnlinePlatformManager m_OnlinePlatformManager;

	public override void OnSetUp()
	{
		m_OnlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
	}

	public override void OnUpdate()
	{
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		FastList<User> users = ServerUserSystem.m_Users;
		int count = users.Count;
		if (count > 0)
		{
			if (ServerUserSystem.GetEngagementPrivilegeCheckStatus().GetProgress() == eConnectionModeSwitchProgress.InProgress)
			{
				DrawText(ref rect, style, ServerUserSystem.GetEngagementPrivilegeCheckStatus().GetLocalisedProgressDescription());
			}
			DrawText(ref rect, style, "Server users");
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				DrawText(ref rect, style, "nm:" + user.DisplayName + " mchn:" + user.Machine.ToString() + " ents:" + user.EntityID + "," + user.Entity2ID + " slt:" + user.Engagement.ToString() + " ste:" + user.GameState.ToString() + " tm:" + user.Team.ToString() + " party: " + user.PartyPersist.ToString() + " col:" + user.Colour);
			}
		}
		users = ClientUserSystem.m_Users;
		count = users.Count;
		if (count > 0)
		{
			DrawText(ref rect, style, "Client users");
			for (int j = 0; j < count; j++)
			{
				User user2 = users._items[j];
				DrawText(ref rect, style, "nm:" + user2.DisplayName + " mchn:" + user2.Machine.ToString() + " ents:" + user2.EntityID + "," + user2.Entity2ID + " slt:" + user2.Engagement.ToString() + " ste:" + user2.GameState.ToString() + " tm:" + user2.Team.ToString() + " party: " + user2.PartyPersist.ToString() + " col:" + user2.Colour);
			}
		}
	}
}
