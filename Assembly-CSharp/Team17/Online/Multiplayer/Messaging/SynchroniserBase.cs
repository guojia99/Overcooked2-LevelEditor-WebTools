using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public abstract class SynchroniserBase : MonoBehaviour, Synchroniser
	{
		public bool ActiveAndEnabled;

		private Component m_Component;

		public abstract void StartSynchronising(Component synchronisedObject);

		public virtual void StopSynchronising()
		{
		}

		public bool IsSynchronising()
		{
			return ActiveAndEnabled;
		}

		public abstract void UpdateSynchronising();

		public abstract EntityType GetEntityType();

		public virtual void SetSynchronisedComponent(Component component)
		{
			m_Component = component;
		}

		protected virtual void OnEnable()
		{
			ActiveAndEnabled = true;
		}

		protected virtual void OnDisable()
		{
			ActiveAndEnabled = false;
		}

		public virtual Component GetSynchronisedComponent()
		{
			return m_Component;
		}
	}
}
