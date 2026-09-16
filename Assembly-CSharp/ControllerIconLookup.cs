using System;
using UnityEngine;

[AddComponentMenu("Scripts/Core/Input/ControllerIconLookup")]
public class ControllerIconLookup : Manager
{
	private interface IGetSprite
	{
		Sprite GetSprite(ControlPadInput.Button _button);

		float GetIconScale(ControlPadInput.Button _button);
	}

	[Serializable]
	private class ButtonIcons : IGetSprite
	{
		public Sprite Action1;

		public Sprite Action2;

		public Sprite Action3;

		public Sprite Action4;

		public Sprite LeftBumper;

		public Sprite RightBumper;

		public Sprite LeftTrigger;

		public Sprite RightTrigger;

		public Sprite LeftStick;

		public float LeftStickScale = 1f;

		public Sprite RightStick;

		public float RightStickScale = 1f;

		public Sprite DPadUp;

		public Sprite DPadRight;

		public Sprite DPadLeft;

		public Sprite GetSprite(ControlPadInput.Button _button)
		{
			switch (_button)
			{
			case ControlPadInput.Button.A:
				return (!PlayerManagerShared<PCPlayerManager.PCPlayerProfile>.AcceptAndCancelButtonsInverted) ? Action1 : Action2;
			case ControlPadInput.Button.B:
				return (!PlayerManagerShared<PCPlayerManager.PCPlayerProfile>.AcceptAndCancelButtonsInverted) ? Action2 : Action1;
			case ControlPadInput.Button.X:
				return Action3;
			case ControlPadInput.Button.Y:
				return Action4;
			case ControlPadInput.Button.LB:
				return LeftBumper;
			case ControlPadInput.Button.RB:
				return RightBumper;
			case ControlPadInput.Button.LTrigger:
				return LeftTrigger;
			case ControlPadInput.Button.RTrigger:
				return RightTrigger;
			case ControlPadInput.Button.LeftAnalog:
				return LeftStick;
			case ControlPadInput.Button.RightAnalog:
				return RightStick;
			case ControlPadInput.Button.DPadUp:
				return DPadUp;
			case ControlPadInput.Button.DPadRight:
				return DPadRight;
			case ControlPadInput.Button.DPadLeft:
				return DPadLeft;
			default:
				return null;
			}
		}

		public float GetIconScale(ControlPadInput.Button _button)
		{
			switch (_button)
			{
			case ControlPadInput.Button.LeftAnalog:
				return LeftStickScale;
			case ControlPadInput.Button.RightAnalog:
				return RightStickScale;
			default:
				return 1f;
			}
		}
	}

	[Serializable]
	private class NXButtonIcons : IGetSprite
	{
		public Sprite A;

		public Sprite B;

		public Sprite X;

		public Sprite Y;

		public Sprite LeftBumper;

		public Sprite RightBumper;

		public Sprite LeftTrigger;

		public Sprite RightTrigger;

		public Sprite LeftStick;

		public float LeftStickScale = 1f;

		public Sprite RightStick;

		public float RightStickScale = 1f;

		public Sprite DPadUp;

		public Sprite DPadDown;

		public Sprite DPadLeft;

		public Sprite DPadRight;

		public Sprite LeftSL;

		public Sprite LeftSR;

		public Sprite RightSL;

		public Sprite RightSR;

		public Sprite LRButton;

		public Sprite TopFaceButton;

		public Sprite LeftFaceButton;

		public Sprite RightFaceButton;

		public Sprite BottomFaceButton;

		public Sprite AmbiStick;

		public float AmbipadScale = 0.5f;

		public Sprite GetSprite(ControlPadInput.Button _button)
		{
			return A;
		}

		public float GetIconScale(ControlPadInput.Button _button)
		{
			return 1f;
		}
	}

	[Serializable]
	private class PlatformSet
	{
		public ButtonIcons XboxOne;

		public ButtonIcons PS4;

		public NXButtonIcons NX;

		public ButtonIcons Keyboard;

		public ButtonIcons KeyboardSplit;

		public IGetSprite GetIconPack(DeviceContext _device)
		{
			switch (_device)
			{
			case DeviceContext.Pad:
				return XboxOne;
			case DeviceContext.Keyboard:
				return Keyboard;
			case DeviceContext.SplitKeyboard:
				return KeyboardSplit;
			default:
				return XboxOne;
			}
		}
	}

	public enum IconContext
	{
		Borderless = 0,
		Bordered = 1
	}

	public enum DeviceContext
	{
		Keyboard = 0,
		Pad = 1,
		SplitKeyboard = 2
	}

	[SerializeField]
	private PlatformSet m_borderlessIcons;

	[SerializeField]
	private PlatformSet m_borderedIcons;

	private IGetSprite GetIconPack(IconContext _context, DeviceContext _device)
	{
		return GetPlatformSet(_context).GetIconPack(_device);
	}

	private PlatformSet GetPlatformSet(IconContext _context)
	{
		switch (_context)
		{
		case IconContext.Borderless:
			return m_borderlessIcons;
		case IconContext.Bordered:
			return m_borderedIcons;
		default:
			return null;
		}
	}

	public float GetIconScale(ControlPadInput.Button _button, IconContext _context = IconContext.Bordered, DeviceContext _device = DeviceContext.Pad)
	{
		IGetSprite iconPack = GetIconPack(_context, _device);
		return iconPack.GetIconScale(_button);
	}

	public Sprite GetIcon(ControlPadInput.Button _button, IconContext _context = IconContext.Bordered, DeviceContext _device = DeviceContext.Pad)
	{
		IGetSprite iconPack = GetIconPack(_context, _device);
		return iconPack.GetSprite(_button);
	}
}
