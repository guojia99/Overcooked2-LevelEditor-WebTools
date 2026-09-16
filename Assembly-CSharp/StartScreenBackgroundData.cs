using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "StartScreenBackgroundData", menuName = "Team17/Create Start Screen Background Data")]
public class StartScreenBackgroundData : SerializedSceneData
{
	[Header("Start Screen Data")]
	[SerializeField]
	public string BackgroundScene;

	[SerializeField]
	public HatMeshVisibility.VisState ChefHat = HatMeshVisibility.VisState.Fancy;
}
