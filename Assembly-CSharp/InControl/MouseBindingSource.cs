using System;
using System.IO;
using UnityEngine;

namespace InControl
{
	public class MouseBindingSource : BindingSource
	{
		public static float ScaleX = 0.2f;

		public static float ScaleY = 0.2f;

		public static float ScaleZ = 0.2f;

		private static readonly int[] buttonTable = new int[16]
		{
			-1, 0, 1, 2, -1, -1, -1, -1, -1, -1,
			3, 4, 5, 6, 7, 8
		};

		public Mouse Control { get; protected set; }

		public override string Name
		{
			get
			{
				return Control.ToString();
			}
		}

		public override string DeviceName
		{
			get
			{
				return "Mouse";
			}
		}

		internal override BindingSourceType BindingSourceType
		{
			get
			{
				return BindingSourceType.MouseBindingSource;
			}
		}

		internal MouseBindingSource()
		{
		}

		public MouseBindingSource(Mouse mouseControl)
		{
			Control = mouseControl;
		}

		internal static bool SafeGetMouseButton(int button)
		{
			try
			{
				return Input.GetMouseButton(button);
			}
			catch (ArgumentException)
			{
			}
			return false;
		}

		internal static bool ButtonIsPressed(Mouse control)
		{
			int num = buttonTable[(int)control];
			if (num >= 0)
			{
				return SafeGetMouseButton(num);
			}
			return false;
		}

		public override float GetValue(InputDevice inputDevice)
		{
			int num = buttonTable[(int)Control];
			if (num >= 0)
			{
				return (!SafeGetMouseButton(num)) ? 0f : 1f;
			}
			switch (Control)
			{
			case Mouse.NegativeX:
				return 0f - Mathf.Min(Input.GetAxisRaw("mouse x") * ScaleX, 0f);
			case Mouse.PositiveX:
				return Mathf.Max(0f, Input.GetAxisRaw("mouse x") * ScaleX);
			case Mouse.NegativeY:
				return 0f - Mathf.Min(Input.GetAxisRaw("mouse y") * ScaleY, 0f);
			case Mouse.PositiveY:
				return Mathf.Max(0f, Input.GetAxisRaw("mouse y") * ScaleY);
			case Mouse.NegativeScrollWheel:
				return 0f - Mathf.Min(Input.GetAxisRaw("mouse z") * ScaleZ, 0f);
			case Mouse.PositiveScrollWheel:
				return Mathf.Max(0f, Input.GetAxisRaw("mouse z") * ScaleZ);
			default:
				return 0f;
			}
		}

		public override bool GetState(InputDevice inputDevice)
		{
			return Utility.IsNotZero(GetValue(inputDevice));
		}

		public override bool Equals(BindingSource other)
		{
			if (other == null)
			{
				return false;
			}
			MouseBindingSource mouseBindingSource = other as MouseBindingSource;
			if (mouseBindingSource != null)
			{
				return Control == mouseBindingSource.Control;
			}
			return false;
		}

		public override bool Equals(object other)
		{
			if (other == null)
			{
				return false;
			}
			MouseBindingSource mouseBindingSource = other as MouseBindingSource;
			if (mouseBindingSource != null)
			{
				return Control == mouseBindingSource.Control;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return Control.GetHashCode();
		}

		internal override void Save(BinaryWriter writer)
		{
			writer.Write((int)Control);
		}

		internal override void Load(BinaryReader reader)
		{
			Control = (Mouse)reader.ReadInt32();
		}
	}
}
