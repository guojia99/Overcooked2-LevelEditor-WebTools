using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerIngredientDisposalBehaviour : ServerSynchroniserBase, IDisposalBehaviour
{
	private IAttachment m_iAttachment;

	private bool m_isDestroyed;

	public void AddToDisposer(ICarrier _carrier, IDisposer _iDisposer)
	{
		GameObject gameObject = _carrier.TakeItem();
		_iDisposer.PassToDestroy(m_iAttachment);
		ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.TrashCan, base.gameObject.layer);
	}

	public void AddToDisposer(IDisposer _iDisposer)
	{
		ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.TrashCan, base.gameObject.layer);
		_iDisposer.PassToDestroy(m_iAttachment);
	}

	public bool WillBeDestroyed()
	{
		return true;
	}

	private void Awake()
	{
		m_iAttachment = base.gameObject.RequestInterface<IAttachment>();
	}

	public void Destroying(IDisposer disposer)
	{
		if (!m_isDestroyed)
		{
			ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.TrashCan, base.gameObject.layer);
			m_isDestroyed = true;
		}
	}
}
