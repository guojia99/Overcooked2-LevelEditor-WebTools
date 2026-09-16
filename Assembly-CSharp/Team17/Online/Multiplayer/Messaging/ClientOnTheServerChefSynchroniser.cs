using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ClientOnTheServerChefSynchroniser : ClientWorldObjectSynchroniser
	{
		public const float kFastFoodRadius = 2f;

		private PlayerIDProvider m_idProvider;

		public override void Awake()
		{
			base.Awake();
			m_idProvider = GetComponent<PlayerIDProvider>();
			m_bHasEverReceived = true;
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
		}

		public override EntityType GetEntityType()
		{
			return EntityType.Chef;
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
		}

		public override void ApplyServerUpdate(Serialisable serialisable)
		{
		}

		public override void Pause()
		{
		}

		public override void Resume()
		{
		}

		public override void OnResumeDataReceived(Serialisable _data)
		{
		}

		public override bool IsReadyToResume()
		{
			return true;
		}

		public override void UpdateSynchronising()
		{
			base.UpdateSynchronising();
			if (m_idProvider.IsLocallyControlled())
			{
				return;
			}
			List<ServerPhysicsObjectSynchroniser.SerialisationEntryTransformPair> allSynchroniserSerialisationEntryTransformPairs = ServerPhysicsObjectSynchroniser.GetAllSynchroniserSerialisationEntryTransformPairs();
			Vector3 position = base.transform.position;
			for (int i = 0; i < allSynchroniserSerialisationEntryTransformPairs.Count; i++)
			{
				ServerPhysicsObjectSynchroniser.SerialisationEntryTransformPair serialisationEntryTransformPair = allSynchroniserSerialisationEntryTransformPairs[i];
				if ((serialisationEntryTransformPair.m_Transform.position - position).sqrMagnitude < 4f)
				{
					serialisationEntryTransformPair.m_Entry.SetRequiresUrgentUpdate(true);
				}
			}
		}
	}
}
