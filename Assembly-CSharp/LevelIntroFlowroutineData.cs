using System;
using UnityEngine;

[Serializable]
public class LevelIntroFlowroutineData
{
	public TutorialPopup TutorialPopup = new TutorialPopup();

	public float ReadyDelay = 1f;

	public GameObject ReadyUIPrefab;

	public float ReadyUILifetime = 1f;

	public GameObject GoUIPrefab;

	public float GOUILifetime = 1f;
}
