using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class PhysicsObjectLerp : EmptyLerp
	{
		public void Start()
		{
			Lerp[] components = GetComponents<Lerp>();
			for (int i = 0; i < components.Length; i++)
			{
				MonoBehaviour monoBehaviour = components[i] as MonoBehaviour;
				if (monoBehaviour != this)
				{
					components[i] = null;
					Object.Destroy(monoBehaviour);
				}
			}
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
		}

		public virtual void ApplyLerpInfo(Vector3 targetPosition, Quaternion targetRotation)
		{
		}

		public override void Reparented()
		{
		}

		public override void UpdateLerp(float _delta)
		{
		}

		public void Update_PositionLerp(float _lerp)
		{
		}

		public void Update_RotationLerp(float _lerp)
		{
		}

		public override void ReceiveServerUpdate(Vector3 localPosition, Quaternion localRotation)
		{
		}

		public override void ReceiveServerEvent(Vector3 localPosition, Quaternion localRotation)
		{
		}

		public void ClientChefCollisionEnter()
		{
		}

		public void ClientChefCollisionExit()
		{
		}

		public void ClientChefOnCillisionStay()
		{
		}

		private void ActivateLerp()
		{
		}

		private void ControlLocally()
		{
		}
	}
}
