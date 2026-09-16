using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class PhysicsObjectSynchroniser : MonoBehaviour
	{
		private PhysicalAttachment m_PhysicalAttachment;

		public void SetPhysicalAttachment(PhysicalAttachment _physicalAttachment)
		{
			m_PhysicalAttachment = _physicalAttachment;
		}

		public PhysicalAttachment GetPhysicalAttachment()
		{
			return m_PhysicalAttachment;
		}
	}
}
