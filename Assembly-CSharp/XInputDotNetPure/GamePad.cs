using System;
using System.Runtime.InteropServices;

namespace XInputDotNetPure
{
	public class GamePad
	{
		private static IntPtr gamePadStatePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GamePadState.RawState)));

		public unsafe static GamePadState GetState(PlayerIndex playerIndex)
		{
			uint num = Imports.XInputGamePadGetState((uint)playerIndex, gamePadStatePointer);
			GamePadState.RawState rawState = *(GamePadState.RawState*)(void*)gamePadStatePointer;
			return new GamePadState(num == 0, rawState);
		}

		public static void SetVibration(PlayerIndex playerIndex, float leftMotor, float rightMotor)
		{
			Imports.XInputGamePadSetState((uint)playerIndex, leftMotor, rightMotor);
		}
	}
}
