using Steamworks;
using UnityEngine;

public class SteamSaveManager : PCSaveManager
{
	protected override string GetSaveDirectory()
	{
		return Application.persistentDataPath + "/" + GetUserSaveDirectory() + "/";
	}

	private string GetUserSaveDirectory()
	{
		return SteamUser.GetSteamID().ToString();
	}
}
