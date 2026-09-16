using UnityEngine;

namespace InControl
{
	public abstract class TouchControl : MonoBehaviour
	{
		public enum ButtonTarget
		{
			None = 0,
			Action1 = 15,
			Action2 = 16,
			Action3 = 17,
			Action4 = 18,
			LeftTrigger = 19,
			RightTrigger = 20,
			LeftBumper = 21,
			RightBumper = 22,
			DPadDown = 12,
			DPadLeft = 13,
			DPadRight = 14,
			DPadUp = 11,
			Menu = 30,
			Button0 = 62,
			Button1 = 63,
			Button2 = 64,
			Button3 = 65,
			Button4 = 66,
			Button5 = 67,
			Button6 = 68,
			Button7 = 69,
			Button8 = 70,
			Button9 = 71,
			Button10 = 72,
			Button11 = 73,
			Button12 = 74,
			Button13 = 75,
			Button14 = 76,
			Button15 = 77,
			Button16 = 78,
			Button17 = 79,
			Button18 = 80,
			Button19 = 81
		}

		public enum AnalogTarget
		{
			None = 0,
			LeftStick = 1,
			RightStick = 2,
			Both = 3
		}

		public enum SnapAngles
		{
			None = 0,
			Four = 4,
			Eight = 8,
			Sixteen = 0x10
		}

		public abstract void CreateControl();

		public abstract void DestroyControl();

		public abstract void ConfigureControl();

		public abstract void SubmitControlState(ulong updateTick, float deltaTime);

		public abstract void CommitControlState(ulong updateTick, float deltaTime);

		public abstract void TouchBegan(Touch touch);

		public abstract void TouchMoved(Touch touch);

		public abstract void TouchEnded(Touch touch);

		public abstract void DrawGizmos();

		private void OnEnable()
		{
			TouchManager.OnSetup += Setup;
		}

		private void OnDisable()
		{
			DestroyControl();
			Resources.UnloadUnusedAssets();
		}

		private void Setup()
		{
			if (base.enabled)
			{
				CreateControl();
				ConfigureControl();
			}
		}

		protected Vector3 OffsetToWorldPosition(TouchControlAnchor anchor, Vector2 offset, TouchUnitType offsetUnitType, bool lockAspectRatio)
		{
			Vector3 vector = ((offsetUnitType == TouchUnitType.Pixels) ? ((Vector3)(TouchUtility.RoundVector(offset) * TouchManager.PixelToWorld)) : ((!lockAspectRatio) ? Vector3.Scale(offset, TouchManager.ViewSize) : ((Vector3)offset * TouchManager.PercentToWorld)));
			return TouchManager.ViewToWorldPoint(TouchUtility.AnchorToViewPoint(anchor)) + vector;
		}

		protected void SubmitButtonState(ButtonTarget target, bool state, ulong updateTick, float deltaTime)
		{
			if (TouchManager.Device != null && target != ButtonTarget.None)
			{
				InputControl control = TouchManager.Device.GetControl((InputControlType)target);
				if (control != null)
				{
					control.UpdateWithState(state, updateTick, deltaTime);
				}
			}
		}

		protected void CommitButton(ButtonTarget target)
		{
			if (TouchManager.Device != null && target != ButtonTarget.None)
			{
				InputControl control = TouchManager.Device.GetControl((InputControlType)target);
				if (control != null)
				{
					control.Commit();
				}
			}
		}

		protected void SubmitAnalogValue(AnalogTarget target, Vector2 value, float lowerDeadZone, float upperDeadZone, ulong updateTick, float deltaTime)
		{
			if (TouchManager.Device != null)
			{
				Vector2 value2 = Utility.ApplyCircularDeadZone(value, lowerDeadZone, upperDeadZone);
				if (target == AnalogTarget.LeftStick || target == AnalogTarget.Both)
				{
					TouchManager.Device.UpdateLeftStickWithValue(value2, updateTick, deltaTime);
				}
				if (target == AnalogTarget.RightStick || target == AnalogTarget.Both)
				{
					TouchManager.Device.UpdateRightStickWithValue(value2, updateTick, deltaTime);
				}
			}
		}

		protected void CommitAnalog(AnalogTarget target)
		{
			if (TouchManager.Device != null)
			{
				if (target == AnalogTarget.LeftStick || target == AnalogTarget.Both)
				{
					TouchManager.Device.CommitLeftStick();
				}
				if (target == AnalogTarget.RightStick || target == AnalogTarget.Both)
				{
					TouchManager.Device.CommitRightStick();
				}
			}
		}

		protected void SubmitRawAnalogValue(AnalogTarget target, Vector2 rawValue, ulong updateTick, float deltaTime)
		{
			if (TouchManager.Device != null)
			{
				if (target == AnalogTarget.LeftStick || target == AnalogTarget.Both)
				{
					TouchManager.Device.UpdateLeftStickWithRawValue(rawValue, updateTick, deltaTime);
				}
				if (target == AnalogTarget.RightStick || target == AnalogTarget.Both)
				{
					TouchManager.Device.UpdateRightStickWithRawValue(rawValue, updateTick, deltaTime);
				}
			}
		}

		protected static Vector2 SnapTo(Vector2 vector, SnapAngles snapAngles)
		{
			if (snapAngles == SnapAngles.None)
			{
				return vector;
			}
			float snapAngle = 360f / (float)snapAngles;
			return SnapTo(vector, snapAngle);
		}

		protected static Vector2 SnapTo(Vector2 vector, float snapAngle)
		{
			float num = Vector2.Angle(vector, Vector2.up);
			if (num < snapAngle / 2f)
			{
				return Vector2.up * vector.magnitude;
			}
			if (num > 180f - snapAngle / 2f)
			{
				return -Vector2.up * vector.magnitude;
			}
			float num2 = Mathf.Round(num / snapAngle);
			float angle = num2 * snapAngle - num;
			Vector3 axis = Vector3.Cross(Vector2.up, vector);
			Quaternion quaternion = Quaternion.AngleAxis(angle, axis);
			return quaternion * vector;
		}

		private void OnDrawGizmosSelected()
		{
			if (base.enabled && TouchManager.ControlsShowGizmos == TouchManager.GizmoShowOption.WhenSelected && !Utility.GameObjectIsCulledOnCurrentCamera(base.gameObject))
			{
				if (!Application.isPlaying)
				{
					ConfigureControl();
				}
				DrawGizmos();
			}
		}

		private void OnDrawGizmos()
		{
			if (!base.enabled)
			{
				return;
			}
			if (TouchManager.ControlsShowGizmos == TouchManager.GizmoShowOption.UnlessPlaying)
			{
				if (Application.isPlaying)
				{
					return;
				}
			}
			else if (TouchManager.ControlsShowGizmos != TouchManager.GizmoShowOption.Always)
			{
				return;
			}
			if (!Utility.GameObjectIsCulledOnCurrentCamera(base.gameObject))
			{
				if (!Application.isPlaying)
				{
					ConfigureControl();
				}
				DrawGizmos();
			}
		}
	}
}
