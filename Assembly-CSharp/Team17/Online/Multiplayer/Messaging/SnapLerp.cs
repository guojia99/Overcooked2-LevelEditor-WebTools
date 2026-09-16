using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class SnapLerp : EmptyLerp
	{
		private bool m_bSetNextData;

		public override void ReceiveServerUpdate(Vector3 localPosition, Quaternion localRotation)
		{
			ReceiveData(localPosition, localRotation);
		}

		public override void ReceiveServerEvent(Vector3 localPosition, Quaternion localRotation)
		{
			ReceiveData(localPosition, localRotation);
		}

		private void ReceiveData(Vector3 localPosition, Quaternion localRotation)
		{
			if (m_bSetNextData)
			{
				m_bSetNextData = false;
				base.transform.localPosition = localPosition;
				base.transform.localRotation = localRotation;
			}
		}

		public override void Reparented()
		{
			m_bSetNextData = true;
		}
	}
}
