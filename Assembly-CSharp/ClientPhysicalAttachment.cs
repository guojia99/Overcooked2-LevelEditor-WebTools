using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPhysicalAttachment : ClientSynchroniserBase, IClientAttachment
{
	private bool m_isHeld;

	private AttachChangedCallback m_attachChangedCallback = delegate
	{
	};

	private IParentable m_Parent;

	private PhysicalAttachment m_physicalAttachment;

	private ClientWorldObjectSynchroniser m_worldObjSync;

	private bool m_bClientSidePredicted;

	public IClientSidePredicted m_Prediction;

	public IClientSidePredicted GetClientSidePrediction()
	{
		return m_Prediction;
	}

	public void SetClientSidePrediction(CreateClientSidePredictionCallback prediction)
	{
		m_Prediction = prediction();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PhysicalAttach;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_isHeld = false;
		m_physicalAttachment = (PhysicalAttachment)synchronisedObject;
		if (GetComponent<ServerPhysicalAttachment>() == null)
		{
			GameObject gameObject = new GameObject();
			gameObject.name = base.name + " MeshLerper";
			m_physicalAttachment.m_meshLerper = gameObject.AddComponent<MeshLerper>();
			m_physicalAttachment.m_meshLerper.SetLerpActive(m_physicalAttachment.GetFakeMeshActive());
			Transform transform = gameObject.transform;
			transform.position = base.transform.position;
			transform.rotation = base.transform.rotation;
			for (int num = base.transform.childCount - 1; num >= 0; num--)
			{
				base.transform.GetChild(num).SetParent(transform);
			}
			transform.SetParent(base.transform);
			transform.position = base.transform.position;
			m_physicalAttachment.m_meshLerper.SetPosition(base.transform.position);
		}
		m_worldObjSync = base.gameObject.RequireComponent<ClientWorldObjectSynchroniser>();
		m_worldObjSync.RegisterOnParentChanged(OnParentChanged);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		PhysicalAttachMessage physicalAttachMessage = (PhysicalAttachMessage)serialisable;
		m_isHeld = physicalAttachMessage.m_parentable != null;
		if (physicalAttachMessage.m_parentable != null)
		{
			m_bClientSidePredicted = m_isHeld && physicalAttachMessage.m_parentable.HasClientSidePrediction();
			m_Parent = physicalAttachMessage.m_parentable;
			if (!m_bClientSidePredicted && m_Prediction != null)
			{
				m_Prediction.Clear();
				base.transform.localPosition = Vector3.zero;
				base.transform.localRotation = Quaternion.identity;
			}
		}
		if (m_physicalAttachment.m_groundCast != null)
		{
			m_physicalAttachment.m_groundCast.enabled = !m_isHeld;
		}
		m_attachChangedCallback(physicalAttachMessage.m_parentable);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_Prediction != null && m_bClientSidePredicted)
		{
			m_Prediction.Update();
		}
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

	private void OnParentChanged()
	{
		m_worldObjSync.CorrectScale();
	}

	public void RegisterAttachChangedCallback(AttachChangedCallback _callback)
	{
		m_attachChangedCallback = (AttachChangedCallback)Delegate.Combine(m_attachChangedCallback, _callback);
	}

	public void UnregisterAttachChangedCallback(AttachChangedCallback _callback)
	{
		m_attachChangedCallback = (AttachChangedCallback)Delegate.Remove(m_attachChangedCallback, _callback);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_worldObjSync != null)
		{
			m_worldObjSync.UnregisterOnParentChanged(OnParentChanged);
		}
	}
}
