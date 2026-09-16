using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public interface Synchroniser
	{
		void StartSynchronising(Component synchronisedObject);

		void StopSynchronising();

		bool IsSynchronising();

		void UpdateSynchronising();

		EntityType GetEntityType();

		void SetSynchronisedComponent(Component component);

		Component GetSynchronisedComponent();
	}
}
