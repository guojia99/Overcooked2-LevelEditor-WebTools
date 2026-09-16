using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class BasicLerp : EmptyLerp
	{
		private const float kMaxLerpTime = 1f;

		private Transform m_PreviousParent;

		protected Transform m_Transform;

		protected Vector3 m_TargetLocalPosition = Vector3.zero;

		protected Vector3 m_PositionWhenReceived = Vector3.zero;

		protected Quaternion m_TargetLocalRotation = Quaternion.identity;

		protected Quaternion m_RotationWhenReceived = Quaternion.identity;

		protected float m_fLerpDelta;

		protected float m_fLerpTime = 0.1f;

		protected float m_fLastMessageTime;

		private bool m_bInitialPositionSet;

		public override void StartSynchronising(Component synchronisedObject)
		{
			m_bInitialPositionSet = false;
			m_Transform = base.transform;
			m_PreviousParent = m_Transform.parent;
			Reset();
			base.StartSynchronising(synchronisedObject);
		}

		public virtual void ApplyLerpInfo(Vector3 targetPosition, Quaternion targetRotation)
		{
			m_TargetLocalPosition = targetPosition;
			m_PositionWhenReceived = m_Transform.localPosition;
			m_fLerpDelta = 0f;
			m_TargetLocalRotation = targetRotation;
			m_RotationWhenReceived = m_Transform.localRotation;
		}

		public override void Reset()
		{
			m_TargetLocalPosition = m_Transform.localPosition;
			m_PositionWhenReceived = m_Transform.localPosition;
			m_TargetLocalRotation = m_Transform.localRotation;
			m_RotationWhenReceived = m_Transform.localRotation;
			m_fLastMessageTime = Time.time;
			base.Reset();
		}

		public override void Reparented()
		{
			Matrix4x4 matrix4x = Matrix4x4.identity;
			Matrix4x4 matrix4x2 = Matrix4x4.identity;
			if (m_PreviousParent != null)
			{
				matrix4x = m_PreviousParent.worldToLocalMatrix.inverse;
			}
			if (m_Transform.parent != null)
			{
				matrix4x2 = m_Transform.parent.worldToLocalMatrix;
			}
			m_PositionWhenReceived = matrix4x2 * matrix4x * new Vector4(m_PositionWhenReceived.x, m_PositionWhenReceived.y, m_PositionWhenReceived.z, 1f);
			base.Reparented();
		}

		public override void UpdateLerp(float delta)
		{
			if (m_Transform != null && m_fLerpTime != 0f)
			{
				m_fLerpDelta += delta;
				float t = m_fLerpDelta / m_fLerpTime;
				m_Transform.localPosition = Vector3.Lerp(m_PositionWhenReceived, m_TargetLocalPosition, t);
				m_Transform.localRotation = Quaternion.Lerp(m_RotationWhenReceived, m_TargetLocalRotation, t);
			}
		}

		public override void ReceiveServerUpdate(Vector3 localPosition, Quaternion localRotation)
		{
			if (m_bInitialPositionSet)
			{
				float time = Time.time;
				float num = time - m_fLastMessageTime;
				if (num < 1f)
				{
					m_fLerpTime = Mathf.Lerp(m_fLerpTime, num + 0.1f, 0.01f);
				}
				m_fLastMessageTime = time;
				ApplyLerpInfo(localPosition, localRotation);
			}
			else
			{
				m_Transform.localPosition = localPosition;
				m_Transform.localRotation = localRotation;
				m_TargetLocalPosition = localPosition;
				m_TargetLocalRotation = localRotation;
				m_bInitialPositionSet = true;
			}
		}

		public override void ReceiveServerEvent(Vector3 localPosition, Quaternion localRotation)
		{
			if (m_bInitialPositionSet)
			{
				ApplyLerpInfo(localPosition, localRotation);
				return;
			}
			m_Transform.localPosition = localPosition;
			m_Transform.localRotation = localRotation;
			m_TargetLocalPosition = localPosition;
			m_TargetLocalRotation = localRotation;
			m_bInitialPositionSet = true;
		}
	}
}
