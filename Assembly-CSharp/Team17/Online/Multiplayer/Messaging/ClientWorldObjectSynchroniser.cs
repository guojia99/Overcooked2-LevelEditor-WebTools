using System;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ClientWorldObjectSynchroniser : ClientSynchroniserBase
	{
		protected Transform m_Transform;

		protected IParentable m_Parent;

		protected uint m_ParentEntityID;

		protected bool m_bHasParent;

		private Vector3 m_ServerPosition = default(Vector3);

		protected Lerp m_Lerper;

		protected bool m_bPaused;

		protected Serialisable m_PendingResumeData;

		private PhysicalAttachment m_PhysicalAttachment;

		private GenericVoid m_parentChanged;

		public bool m_bHasEverReceived;

		public virtual void Awake()
		{
			m_Transform = base.transform;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			if (m_Lerper == null)
			{
				m_Lerper = base.gameObject.GetComponent<Lerp>();
			}
			if (m_Lerper != null)
			{
				m_Lerper.StartSynchronising(synchronisedObject);
			}
			m_Transform = base.transform;
			m_PhysicalAttachment = GetComponent<PhysicalAttachment>();
			m_bHasParent = m_Transform.parent != null;
			m_Parent = null;
			m_ParentEntityID = 0u;
			if (!(m_Transform.parent != null))
			{
				return;
			}
			IParentable parentable = m_Transform.parent.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
			if (parentable != null)
			{
				m_Transform.SetParent(parentable.GetAttachPoint(base.gameObject), true);
				OnParentChanged();
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(((MonoBehaviour)parentable).gameObject);
				if (entry != null)
				{
					m_ParentEntityID = entry.m_Header.m_uEntityID;
				}
			}
		}

		public override EntityType GetEntityType()
		{
			return EntityType.WorldObject;
		}

		public override void ApplyServerUpdate(Serialisable serialisable)
		{
			if (m_bPaused)
			{
				return;
			}
			WorldObjectMessage worldObjectMessage = (WorldObjectMessage)serialisable;
			bool flag = ParentingLogic(worldObjectMessage);
			if (worldObjectMessage.HasPositions)
			{
				m_ServerPosition = worldObjectMessage.LocalPosition;
				if (flag && m_Lerper != null)
				{
					m_Lerper.ReceiveServerUpdate(worldObjectMessage.LocalPosition, worldObjectMessage.LocalRotation);
				}
			}
			m_bHasEverReceived = true;
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
			if (m_bPaused)
			{
				return;
			}
			WorldObjectMessage worldObjectMessage = (WorldObjectMessage)serialisable;
			bool flag = ParentingLogic(worldObjectMessage);
			if (worldObjectMessage.HasPositions)
			{
				m_ServerPosition = worldObjectMessage.LocalPosition;
				if (flag && m_Lerper != null)
				{
					m_Lerper.ReceiveServerEvent(worldObjectMessage.LocalPosition, worldObjectMessage.LocalRotation);
				}
			}
			m_bHasEverReceived = true;
		}

		public Vector3 GetGlobalServerPosition()
		{
			if (m_Transform.parent != null)
			{
				return m_Transform.parent.position + m_Transform.parent.rotation * m_ServerPosition;
			}
			return m_ServerPosition;
		}

		public void CorrectScale()
		{
			Vector3 lossyScale = m_PhysicalAttachment.transform.lossyScale;
			Vector3 b = new Vector3(1f / lossyScale.x, 1f / lossyScale.y, 1f / lossyScale.z);
			m_PhysicalAttachment.transform.localScale = m_PhysicalAttachment.transform.localScale.MultipliedBy(b);
		}

		public bool ParentingLogic(WorldObjectMessage dataReceived)
		{
			bool result = true;
			if (m_bHasParent != dataReceived.HasParent || m_ParentEntityID != dataReceived.ParentEntityID)
			{
				IParentable parent = m_Parent;
				m_bHasParent = dataReceived.HasParent;
				m_ParentEntityID = dataReceived.ParentEntityID;
				m_Parent = null;
				if (m_bHasParent)
				{
					if (m_ParentEntityID != 0)
					{
						EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_ParentEntityID);
						if (entry != null)
						{
							m_Parent = entry.m_GameObject.RequestInterface<IParentable>();
							result = DoReparenting(parent, m_Parent);
						}
					}
				}
				else
				{
					result = DoReparenting(parent, m_Parent);
				}
			}
			return result;
		}

		public virtual bool DoReparenting(IParentable _oldParent, IParentable _newParent)
		{
			if (m_Transform.parent != null)
			{
				ClientPhysicsObjectSynchroniser component = m_Transform.parent.gameObject.GetComponent<ClientPhysicsObjectSynchroniser>();
				if (component != null)
				{
					component.ChildRemoved(this);
				}
			}
			if (_newParent != null)
			{
				ObjectContainer objectContainer = _newParent as ObjectContainer;
				if (objectContainer != null)
				{
					ClientPhysicsObjectSynchroniser component2 = objectContainer.GetComponent<ClientPhysicsObjectSynchroniser>();
					if (component2 != null)
					{
						component2.ChildAttached(this);
					}
				}
			}
			if (null == m_Transform)
			{
				m_Transform = base.transform;
			}
			if (_newParent != null)
			{
				m_Transform.SetParent(_newParent.GetAttachPoint(base.gameObject));
				OnParentChanged();
				if (m_PhysicalAttachment != null && m_PhysicalAttachment.m_container != null)
				{
					m_PhysicalAttachment.m_container.position = base.transform.position;
					m_PhysicalAttachment.m_container.rotation = base.transform.rotation;
				}
			}
			else
			{
				m_Transform.SetParent(null, true);
				OnParentChanged();
			}
			bool flag = _oldParent == null || !_oldParent.HasClientSidePrediction() || _newParent == null || !_newParent.HasClientSidePrediction();
			if (m_Lerper != null && flag)
			{
				m_Lerper.Reparented();
			}
			return flag;
		}

		private void OnParentChanged()
		{
			if (m_parentChanged != null)
			{
				m_parentChanged();
			}
		}

		public void RegisterOnParentChanged(GenericVoid _callback)
		{
			m_parentChanged = (GenericVoid)Delegate.Combine(m_parentChanged, _callback);
		}

		public void UnregisterOnParentChanged(GenericVoid _callback)
		{
			m_parentChanged = (GenericVoid)Delegate.Remove(m_parentChanged, _callback);
		}

		public override void UpdateSynchronising()
		{
			if (!m_bPaused && (m_Parent == null || !m_Parent.HasClientSidePrediction()) && m_Lerper != null)
			{
				m_Lerper.UpdateLerp(TimeManager.GetDeltaTime(base.gameObject));
			}
		}

		public virtual void Pause()
		{
			m_bPaused = true;
		}

		public virtual void Resume()
		{
			if (m_bPaused)
			{
				m_bPaused = false;
				ApplyResumeData(m_PendingResumeData);
			}
			m_PendingResumeData = null;
		}

		protected virtual void ApplyResumeData(Serialisable _data)
		{
			WorldObjectMessage worldObjectMessage = (WorldObjectMessage)_data;
			ParentingLogic(worldObjectMessage);
			if (worldObjectMessage.HasPositions)
			{
				m_ServerPosition = worldObjectMessage.LocalPosition;
				m_Transform.localPosition = worldObjectMessage.LocalPosition;
				m_Transform.localRotation = worldObjectMessage.LocalRotation;
			}
			if (m_Lerper != null)
			{
				m_Lerper.Reset();
			}
		}

		public virtual void OnResumeDataReceived(Serialisable _data)
		{
			WorldObjectMessage other = (WorldObjectMessage)_data;
			WorldObjectMessage worldObjectMessage = new WorldObjectMessage();
			worldObjectMessage.Copy(other);
			m_PendingResumeData = worldObjectMessage;
		}

		public virtual bool IsReadyToResume()
		{
			return m_PendingResumeData != null;
		}
	}
}
