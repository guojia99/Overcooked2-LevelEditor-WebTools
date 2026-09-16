using UnityEngine;

namespace InControl
{
	public class TouchTrackControl : TouchControl
	{
		[Header("Dimensions")]
		[SerializeField]
		private TouchUnitType areaUnitType;

		[SerializeField]
		private Rect activeArea = new Rect(25f, 25f, 50f, 50f);

		[Header("Analog Target")]
		public AnalogTarget target = AnalogTarget.LeftStick;

		public float scale = 1f;

		private Rect worldActiveArea;

		private Vector3 lastPosition;

		private Vector3 thisPosition;

		private Touch currentTouch;

		private bool dirty;

		public Rect ActiveArea
		{
			get
			{
				return activeArea;
			}
			set
			{
				if (activeArea != value)
				{
					activeArea = value;
					dirty = true;
				}
			}
		}

		public TouchUnitType AreaUnitType
		{
			get
			{
				return areaUnitType;
			}
			set
			{
				if (areaUnitType != value)
				{
					areaUnitType = value;
					dirty = true;
				}
			}
		}

		public override void CreateControl()
		{
			ConfigureControl();
		}

		public override void DestroyControl()
		{
			if (currentTouch != null)
			{
				TouchEnded(currentTouch);
				currentTouch = null;
			}
		}

		public override void ConfigureControl()
		{
			worldActiveArea = TouchManager.ConvertToWorld(activeArea, areaUnitType);
		}

		public override void DrawGizmos()
		{
			Utility.DrawRectGizmo(worldActiveArea, Color.yellow);
		}

		private void Update()
		{
			if (dirty)
			{
				ConfigureControl();
				dirty = false;
			}
		}

		public override void SubmitControlState(ulong updateTick, float deltaTime)
		{
			Vector3 vector = thisPosition - lastPosition;
			SubmitRawAnalogValue(target, vector * scale, updateTick, deltaTime);
			lastPosition = thisPosition;
		}

		public override void CommitControlState(ulong updateTick, float deltaTime)
		{
			CommitAnalog(target);
		}

		public override void TouchBegan(Touch touch)
		{
			if (currentTouch == null)
			{
				Vector3 point = TouchManager.ScreenToWorldPoint(touch.position);
				if (worldActiveArea.Contains(point))
				{
					thisPosition = TouchManager.ScreenToViewPoint(touch.position * 100f);
					lastPosition = thisPosition;
					currentTouch = touch;
				}
			}
		}

		public override void TouchMoved(Touch touch)
		{
			if (currentTouch == touch)
			{
				thisPosition = TouchManager.ScreenToViewPoint(touch.position * 100f);
			}
		}

		public override void TouchEnded(Touch touch)
		{
			if (currentTouch == touch)
			{
				thisPosition = Vector3.zero;
				lastPosition = Vector3.zero;
				currentTouch = null;
			}
		}
	}
}
