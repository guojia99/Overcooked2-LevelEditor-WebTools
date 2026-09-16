using UnityEngine;

namespace InControl
{
	public class UnityButtonSource : InputControlSource
	{
		private static string[,] buttonQueries;

		public int ButtonId;

		public UnityButtonSource()
		{
			SetupButtonQueries();
		}

		public UnityButtonSource(int buttonId)
		{
			ButtonId = buttonId;
			SetupButtonQueries();
		}

		public float GetValue(InputDevice inputDevice)
		{
			return (!GetState(inputDevice)) ? 0f : 1f;
		}

		public bool GetState(InputDevice inputDevice)
		{
			int joystickId = (inputDevice as UnityInputDevice).JoystickId;
			string buttonKey = GetButtonKey(joystickId, ButtonId);
			return Input.GetKey(buttonKey);
		}

		private static void SetupButtonQueries()
		{
			if (buttonQueries != null)
			{
				return;
			}
			buttonQueries = new string[11, 20];
			for (int i = 1; i <= 11; i++)
			{
				for (int j = 0; j < 20; j++)
				{
					buttonQueries[i - 1, j] = "joystick " + i + " button " + j;
				}
			}
		}

		private static string GetButtonKey(int joystickId, int buttonId)
		{
			return buttonQueries[joystickId - 1, buttonId];
		}
	}
}
