using UnityEngine;

namespace InControl
{
	public class OneAxisInputControl : InputControlBase
	{
		internal void CommitWithSides(InputControl negativeSide, InputControl positiveSide, ulong updateTick, float deltaTime)
		{
			base.LowerDeadZone = Mathf.Max(negativeSide.LowerDeadZone, positiveSide.LowerDeadZone);
			base.UpperDeadZone = Mathf.Min(negativeSide.UpperDeadZone, positiveSide.UpperDeadZone);
			Raw = negativeSide.Raw || positiveSide.Raw;
			float value = Utility.ValueFromSides(negativeSide.RawValue, positiveSide.RawValue);
			CommitWithValue(value, updateTick, deltaTime);
		}
	}
}
