using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ServerPhysicsObjectSynchroniser : ServerWorldObjectSynchroniser
	{
		public class SerialisationEntryTransformPair
		{
			public Transform m_Transform;

			public EntitySerialisationEntry m_Entry;

			public SerialisationEntryTransformPair(Transform _transform, EntitySerialisationEntry _entry)
			{
				m_Transform = _transform;
				m_Entry = _entry;
			}
		}

		private class CollidingPlayer
		{
			public uint ID;

			public Transform Transform;

			public int InstanceID;

			public float LastCollisionTime;

			public Vector3 ContactVelocity = default(Vector3);

			public CollidingPlayer(uint _id, Transform _transform, int _instanceID, float _collisionTime, Vector3 _contactVelocity)
			{
				ID = _id;
				Transform = _transform;
				InstanceID = _instanceID;
				LastCollisionTime = _collisionTime;
				ContactVelocity = _contactVelocity;
			}
		}

		public bool Serialising = true;

		private static List<SerialisationEntryTransformPair> ms_ServerPhysicsObjectSytnchroniserTransforms = new List<SerialisationEntryTransformPair>();

		private IOnlineMultiplayerSessionCoordinator m_SessionCoordinator;

		private Rigidbody m_RigidBody;

		private PhysicsObjectMessage m_PhysicsObjectData = new PhysicsObjectMessage();

		private FastList<CollidingPlayer> m_CollidingChefs = new FastList<CollidingPlayer>();

		private CollisionRecorder m_CollisionRecorder;

		private int m_PlayerLayer;

		private const float kCollisionLingerTime = 0.2f;

		private bool m_bPhysicsActive;

		public override void Awake()
		{
			base.Awake();
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_SessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
			m_PlayerLayer = LayerMask.NameToLayer("Players");
			m_RigidBody = base.gameObject.RequireComponent<Rigidbody>();
			m_CollisionRecorder = base.gameObject.AddComponent<CollisionRecorder>();
			m_CollisionRecorder.SetFilter(CollisionFilter);
			ms_ServerPhysicsObjectSytnchroniserTransforms.Add(new SerialisationEntryTransformPair(m_Transform, EntitySerialisationRegistry.GetEntry(base.gameObject)));
		}

		public override void OnDestroy()
		{
			for (int i = 0; i < ms_ServerPhysicsObjectSytnchroniserTransforms.Count; i++)
			{
				if (ms_ServerPhysicsObjectSytnchroniserTransforms[i].m_Transform == m_Transform)
				{
					ms_ServerPhysicsObjectSytnchroniserTransforms.RemoveAt(i);
					break;
				}
			}
			base.OnDestroy();
		}

		public static List<SerialisationEntryTransformPair> GetAllSynchroniserSerialisationEntryTransformPairs()
		{
			return ms_ServerPhysicsObjectSytnchroniserTransforms;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			m_RigidBody = GetComponent<Rigidbody>();
			base.StartSynchronising(synchronisedObject);
			m_PhysicsObjectData.WorldObject = GetMessageData();
			ServerWorldObjectSynchroniser[] components = GetComponents<ServerWorldObjectSynchroniser>();
			for (int i = 0; i < components.Length; i++)
			{
				if (components[i].GetType() != typeof(ServerPhysicsObjectSynchroniser))
				{
					Object.Destroy(components[i]);
				}
			}
		}

		public override EntityType GetEntityType()
		{
			return EntityType.PhysicsObject;
		}

		public override Serialisable GetServerUpdate()
		{
			if (Serialising)
			{
				base.GetServerUpdate();
				if (m_RigidBody == null)
				{
					m_RigidBody = GetComponent<Rigidbody>();
				}
				if (m_PhysicsObjectData.Velocity != m_RigidBody.velocity)
				{
					m_PhysicsObjectData.Velocity = m_RigidBody.velocity;
					m_bPhysicsActive = true;
				}
				else
				{
					m_bPhysicsActive = false;
				}
				StoreContactInformation();
			}
			return null;
		}

		private bool IsPhysicsObjectActive()
		{
			return m_bPhysicsActive || IsWorldObjectActive();
		}

		public override bool HasTargetedServerUpdates()
		{
			return Serialising;
		}

		public override Serialisable GetServerUpdateForRecipient(IOnlineMultiplayerSessionUserId recipient)
		{
			if (recipient == null)
			{
				return null;
			}
			if (!Serialising)
			{
				return null;
			}
			if (IsPhysicsObjectActive())
			{
				return m_PhysicsObjectData;
			}
			return null;
		}

		public override void SendServerEvent(Serialisable message)
		{
			if (!Serialising || m_SessionCoordinator == null)
			{
				return;
			}
			IOnlineMultiplayerSessionUserId[] array = m_SessionCoordinator.Members();
			if (array != null)
			{
				m_PhysicsObjectData.Velocity.Set(0f, 0f, 0f);
				StoreContactInformation();
				for (int i = 0; i < array.Length; i++)
				{
					SendServerEventToRecipient(array[i], m_PhysicsObjectData);
				}
			}
		}

		private void StoreContactInformation()
		{
			for (int num = m_CollidingChefs.Count - 1; num >= 0; num--)
			{
				CollidingPlayer collidingPlayer = m_CollidingChefs._items[num];
				if (collidingPlayer.Transform == null)
				{
					m_CollidingChefs.RemoveAt(num);
				}
			}
			m_PhysicsObjectData.ContactCount = (uint)m_CollidingChefs.Count;
			for (int i = 0; i < m_CollidingChefs.Count; i++)
			{
				CollidingPlayer collidingPlayer2 = m_CollidingChefs._items[i];
				m_PhysicsObjectData.Contacts[i] = collidingPlayer2.ID;
				m_PhysicsObjectData.RelativePositions[i] = m_Transform.position - collidingPlayer2.Transform.position;
				m_PhysicsObjectData.ContactTimes[i] = collidingPlayer2.LastCollisionTime;
				m_PhysicsObjectData.ContactVelocitys[i] = collidingPlayer2.ContactVelocity;
			}
		}

		private bool CollisionFilter(Collision _collision)
		{
			if (_collision.gameObject.layer != m_PlayerLayer)
			{
				return false;
			}
			Rigidbody rigidbody = _collision.rigidbody;
			if (rigidbody == null)
			{
				return false;
			}
			if (rigidbody.velocity.sqrMagnitude <= 0.001f)
			{
				return false;
			}
			return true;
		}

		public override void UpdateSynchronising()
		{
			List<Collision> recentCollisions = m_CollisionRecorder.GetRecentCollisions();
			float num = ClientTime.Time();
			for (int i = 0; i < recentCollisions.Count; i++)
			{
				Collision collision = recentCollisions[i];
				int instanceID = collision.gameObject.GetInstanceID();
				bool flag = true;
				for (int j = 0; j < m_CollidingChefs.Count; j++)
				{
					CollidingPlayer collidingPlayer = m_CollidingChefs._items[j];
					if (instanceID == collidingPlayer.InstanceID)
					{
						flag = false;
						collidingPlayer.LastCollisionTime = num;
						break;
					}
				}
				if (flag)
				{
					EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(collision.gameObject);
					if (entry != null)
					{
						m_CollidingChefs.Add(new CollidingPlayer(entry.m_Header.m_uEntityID, collision.transform, instanceID, num, collision.gameObject.GetComponent<Rigidbody>().velocity));
					}
				}
			}
			float num2 = num - 0.2f;
			for (int num3 = m_CollidingChefs.Count - 1; num3 >= 0; num3--)
			{
				CollidingPlayer collidingPlayer2 = m_CollidingChefs._items[num3];
				if (collidingPlayer2.LastCollisionTime < num2)
				{
					m_CollidingChefs.RemoveAt(num3);
				}
			}
		}

		protected override bool SendResumeData(IOnlineMultiplayerSessionUserId sessionUserId)
		{
			RefreshParent();
			m_PhysicsObjectData.WorldObject.LocalPosition = m_Transform.localPosition;
			m_PhysicsObjectData.WorldObject.LocalRotation = m_Transform.localRotation;
			m_PhysicsObjectData.Velocity = m_RigidBody.velocity;
			StoreContactInformation();
			ServerMessenger.ResumePhysicsObjectSync(base.gameObject, m_PhysicsObjectData, sessionUserId);
			return true;
		}
	}
}
