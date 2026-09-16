using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "WorldMapRegionDataData", menuName = "Team17/Create World Map Region Data")]
public class WorldMapRegionData : SerializedSceneData
{
	[Header("World Map Region Data")]
	[SerializeField]
	public SkyColours SkyColourSettings = new SkyColours();
}
