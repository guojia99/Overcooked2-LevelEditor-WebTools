using UnityEngine;

namespace InControl
{
	public class UnityAnalogSource : InputControlSource
	{
		private static string[,] analogQueries;

		public int AnalogId;

		public UnityAnalogSource()
		{
			SetupAnalogQueries();
		}

		public UnityAnalogSource(int analogId)
		{
			AnalogId = analogId;
			SetupAnalogQueries();
		}

		public float GetValue(InputDevice inputDevice)
		{
			int joystickId = (inputDevice as UnityInputDevice).JoystickId;
			string analogKey = GetAnalogKey(joystickId, AnalogId);
			return Input.GetAxisRaw(analogKey);
		}

		public bool GetState(InputDevice inputDevice)
		{
			return Utility.IsNotZero(GetValue(inputDevice));
		}

		private static void SetupAnalogQueries()
		{
			if (analogQueries != null)
			{
				return;
			}
			analogQueries = new string[11, 20];
			for (int i = 1; i <= 11; i++)
			{
				for (int j = 0; j < 20; j++)
				{
					analogQueries[i - 1, j] = "joystick " + i + " analog " + j;
				}
			}
		}

		private static string GetAnalogKey(int joystickId, int analogId)
		{
			return analogQueries[joystickId - 1, analogId];
		}
	}
}
