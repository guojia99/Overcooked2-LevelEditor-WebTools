using Steamworks;
using UnityEngine;

public class HideInBigPicture : MonoBehaviour
{
	protected void Awake()
	{
		if (SteamUtils.IsSteamInBigPictureMode())
		{
			base.gameObject.SetActive(false);
		}
	}
}
