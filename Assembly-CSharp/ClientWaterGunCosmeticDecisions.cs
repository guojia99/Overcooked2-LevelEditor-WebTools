using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWaterGunCosmeticDecisions : ClientSynchroniserBase
{
	private int m_isHeldParam = -1;

	private WaterGunCosmeticDecisions m_cosmetics;

	private IClientAttachment m_attachment;

	public override void StartSynchronising(Component _synchronisedObject)
	{
		base.StartSynchronising(_synchronisedObject);
		m_cosmetics = (WaterGunCosmeticDecisions)_synchronisedObject;
		m_isHeldParam = Animator.StringToHash(m_cosmetics.m_isHeldParam);
		m_attachment = base.gameObject.RequireInterface<IClientAttachment>();
		m_attachment.RegisterAttachChangedCallback(OnAttachmentChanged);
	}

	private void OnAttachmentChanged(IParentable _parentable)
	{
		bool value = false;
		if (_parentable != null && _parentable is PlayerAttachmentCarrier)
		{
			value = true;
		}
		m_cosmetics.m_animator.SetBool(m_isHeldParam, value);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachment != null)
		{
			m_attachment.UnregisterAttachChangedCallback(OnAttachmentChanged);
		}
	}
}
