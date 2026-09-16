using System;
using UnityEngine;

[Serializable]
public class CookingStepData : ScriptableObject
{
	public SubTexture2D m_icon;

	public Sprite m_iconSprite;

	public GameLoopingAudioTag m_sizzleSound;

	public GameOneShotAudioTag m_addToSound;

	[SelfAssignID(true)]
	public int m_uID;
}
