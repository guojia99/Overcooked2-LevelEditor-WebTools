using System;
using UnityEngine;

[Serializable]
public class AvatarDirectoryData : ScriptableObject
{
	public int DirectoryID;

	public ChefAvatarData[] Avatars = new ChefAvatarData[0];

	public ChefColourData[] Colours = new ChefColourData[0];
}
