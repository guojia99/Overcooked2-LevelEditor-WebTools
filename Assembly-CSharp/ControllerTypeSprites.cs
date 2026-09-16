using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ControllerTypeSprites", menuName = "Team17/ControllerTypeSprites")]
public class ControllerTypeSprites : ScriptableObject
{
	[Serializable]
	public class ControllerSprites
	{
		[Header("Split-Left Controller Sprites")]
		[SerializeField]
		public Sprite m_left;

		[SerializeField]
		public Sprite m_leftEngagement;

		[Header("Full Controller Sprites")]
		[SerializeField]
		public Sprite m_full;

		[SerializeField]
		public Sprite m_fullEngagement;

		[Header("Split-Right Controller Sprites")]
		[SerializeField]
		public Sprite m_right;

		[SerializeField]
		public Sprite m_rightEngagement;
	}

	public enum PCPadVisuals
	{
		NX = 0,
		PS4 = 1,
		X1 = 2
	}

	[Header("Pad Sprites")]
	[SerializeField]
	public ControllerSprites m_padSpritesNX;

	[SerializeField]
	public ControllerSprites m_padSpritesPS4;

	[SerializeField]
	public ControllerSprites m_padSpritesX1;

	[Space]
	[Header("PC Pad")]
	[SerializeField]
	private PCPadVisuals m_padToUseOnPC = PCPadVisuals.X1;

	[Space]
	[Header("Keyboard Sprites")]
	[SerializeField]
	public ControllerSprites m_keyboardSprites;

	public Sprite GetImage(PadSide _side, GamepadUser.ControlTypeEnum _type)
	{
		ControllerSprites controllerSpritesForPlatform = GetControllerSpritesForPlatform(_type);
		switch (_side)
		{
		case PadSide.Both:
			return controllerSpritesForPlatform.m_full;
		case PadSide.Left:
			return controllerSpritesForPlatform.m_left;
		case PadSide.Right:
			return controllerSpritesForPlatform.m_right;
		default:
			return null;
		}
	}

	public Sprite GetEngagementImage(PadSide _side, GamepadUser.ControlTypeEnum _type)
	{
		ControllerSprites controllerSpritesForPlatform = GetControllerSpritesForPlatform(_type);
		switch (_side)
		{
		case PadSide.Both:
			return controllerSpritesForPlatform.m_fullEngagement;
		case PadSide.Left:
			return controllerSpritesForPlatform.m_leftEngagement;
		case PadSide.Right:
			return controllerSpritesForPlatform.m_rightEngagement;
		default:
			return null;
		}
	}

	protected ControllerSprites GetControllerSpritesForPlatform(GamepadUser.ControlTypeEnum _type)
	{
		ControllerSprites result = null;
		if (_type == GamepadUser.ControlTypeEnum.Keyboard)
		{
			result = m_keyboardSprites;
		}
		else
		{
			switch (m_padToUseOnPC)
			{
			case PCPadVisuals.NX:
				result = m_padSpritesNX;
				break;
			case PCPadVisuals.PS4:
				result = m_padSpritesPS4;
				break;
			case PCPadVisuals.X1:
				result = m_padSpritesX1;
				break;
			}
		}
		return result;
	}
}
