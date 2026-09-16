using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPhysicalAttachment : ServerSynchroniserBase, IAttachment
{
	private const float c_MinSurfaceMoveVelocity = 0.05f;

	private PhysicalAttachment m_physicalAttachment;

	private Collider m_collider;

	private bool m_isHeld;

	private AttachChangedCallback m_attachChangedCallback = delegate
	{
	};

	private PhysicalAttachMessage m_ServerData = new PhysicalAttachMessage();

	private ServerLimitedQuantityItem m_LimitedQuantityItem;

	private Generic<float> m_AttachedDestructionScoreModifier;

	private bool m_bIsClientSidePredicted;

	private Transform m_transform;

	private Transform m_rigidbodyTransform;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_physicalAttachment = (PhysicalAttachment)synchronisedObject;
		m_collider = GetComponent<Collider>();
		m_isHeld = false;
		m_LimitedQuantityItem = GetComponent<ServerLimitedQuantityItem>();
		m_rigidbodyTransform = AccessRigidbody().transform;
		LimitedQuantityItemManager limitedMan = GameUtils.RequireManager<LimitedQuantityItemManager>();
		m_AttachedDestructionScoreModifier = () => limitedMan.m_AttachedDeletionScoreModifier;
	}

	public virtual void Awake()
	{
		m_transform = base.transform;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PhysicalAttach;
	}

	public void RegisterAttachChangedCallback(AttachChangedCallback _callback)
	{
		m_attachChangedCallback = (AttachChangedCallback)Delegate.Combine(m_attachChangedCallback, _callback);
	}

	public void UnregisterAttachChangedCallback(AttachChangedCallback _callback)
	{
		m_attachChangedCallback = (AttachChangedCallback)Delegate.Remove(m_attachChangedCallback, _callback);
	}

	public void Attach(IParentable _parentable)
	{
		DetachFromRigidBodyContainer();
		m_transform.SetParent(_parentable.GetAttachPoint(base.gameObject));
		Vector3 lossyScale = m_transform.lossyScale;
		Vector3 b = new Vector3(1f / lossyScale.x, 1f / lossyScale.y, 1f / lossyScale.z);
		m_transform.localScale = m_transform.localScale.MultipliedBy(b);
		bool flag = _parentable.HasClientSidePrediction();
		if (!m_bIsClientSidePredicted || !flag)
		{
			m_transform.localPosition = Vector3.zero;
			m_transform.localRotation = Quaternion.identity;
			ServerWorldObjectSynchroniser component = GetComponent<ServerWorldObjectSynchroniser>();
			component.ResumePositions();
			ServerPhysicsObjectSynchroniser component2 = AccessRigidbody().GetComponent<ServerPhysicsObjectSynchroniser>();
			if (null != component2)
			{
				component2.Serialising = true;
			}
		}
		else
		{
			ServerWorldObjectSynchroniser component3 = GetComponent<ServerWorldObjectSynchroniser>();
			component3.PausePositions();
			ServerPhysicsObjectSynchroniser component4 = AccessRigidbody().GetComponent<ServerPhysicsObjectSynchroniser>();
			if (null != component4)
			{
				component4.Serialising = false;
			}
		}
		m_bIsClientSidePredicted = flag;
		m_isHeld = true;
		if (null != m_LimitedQuantityItem)
		{
			m_LimitedQuantityItem.AddDestructionScoreModifier(m_AttachedDestructionScoreModifier);
		}
		OnAttachChanged(_parentable);
	}

	public void Detach()
	{
		m_collider.transform.SetParent(null);
		AttachToRigidBodyContainer();
		m_isHeld = false;
		if (null != m_LimitedQuantityItem)
		{
			m_LimitedQuantityItem.RemoveDestructionScoreModifier(m_AttachedDestructionScoreModifier);
		}
		OnAttachChanged(null);
		m_transform.localScale = Vector3.one;
		m_physicalAttachment.m_groundCast.ClearGround();
	}

	public bool IsAttached()
	{
		return m_isHeld;
	}

	public GameObject AccessGameObject()
	{
		return base.gameObject;
	}

	public Rigidbody AccessRigidbody()
	{
		return m_physicalAttachment.m_container;
	}

	public RigidbodyMotion AccessMotion()
	{
		return m_physicalAttachment.m_motion;
	}

	public override void UpdateSynchronising()
	{
		if (!(m_physicalAttachment != null))
		{
			return;
		}
		if (m_isHeld)
		{
			m_rigidbodyTransform.position = m_transform.position;
			return;
		}
		Vector3 velocity = m_physicalAttachment.m_surfaceMovable.GetVelocity();
		if (velocity.sqrMagnitude >= 0.05f)
		{
			AccessMotion().Movement(velocity);
		}
	}

	public void ManualEnable()
	{
		if (!m_isHeld)
		{
			AttachToRigidBodyContainer();
		}
	}

	public void ManualDisable(bool _clearPredicted = false)
	{
		if (!m_isHeld)
		{
			if (_clearPredicted)
			{
				m_bIsClientSidePredicted = false;
			}
			DetachFromRigidBodyContainer();
		}
	}

	private void AttachToRigidBodyContainer()
	{
		Rigidbody rigidbody = AccessRigidbody();
		rigidbody.transform.position = m_transform.position;
		rigidbody.transform.rotation = m_transform.rotation;
		rigidbody.transform.SetParent(m_transform.parent);
		m_transform.SetParent(rigidbody.transform);
		AccessMotion().SetKinematic(false);
	}

	private void DetachFromRigidBodyContainer()
	{
		Rigidbody rigidbody = AccessRigidbody();
		m_collider.transform.SetParent(null, false);
		m_collider.transform.SetParent(rigidbody.transform.parent, false);
		m_collider.transform.localPosition = rigidbody.transform.localPosition;
		m_collider.transform.localRotation = rigidbody.transform.localRotation;
		AccessMotion().SetKinematic(true);
	}

	private void OnAttachChanged(IParentable _parentable)
	{
		m_attachChangedCallback(_parentable);
		m_ServerData.m_parentable = _parentable;
		SendServerEvent(m_ServerData);
	}
}
