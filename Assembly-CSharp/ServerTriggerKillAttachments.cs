using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerKillAttachments : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerKillAttachments m_triggerKillAttachments;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerKillAttachments = (TriggerKillAttachments)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_triggerKillAttachments.m_trigger)
		{
			KillAttachments(base.gameObject);
		}
	}

	private void KillAttachments(GameObject _root)
	{
		if (MaskUtils.HasFlag(m_triggerKillAttachments.m_killMode, TriggerKillAttachments.KillMode.Loose))
		{
			GameObject gameObject = base.gameObject;
			IAttachment[] componentsInChildren = gameObject.GetComponentsInChildren<IAttachment>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && !(componentsInChildren[i].AccessGameObject() == null) && !componentsInChildren[i].IsAttached())
				{
					ServerPlayerRespawnManager.KillOrRespawn(componentsInChildren[i].AccessGameObject(), null);
				}
			}
		}
		if (!MaskUtils.HasFlag(m_triggerKillAttachments.m_killMode, TriggerKillAttachments.KillMode.Attached))
		{
			return;
		}
		ServerAttachStation[] array = _root.RequestComponentsRecursive<ServerAttachStation>();
		for (int j = 0; j < array.Length; j++)
		{
			if (!(array[j] == null) && !(array[j].gameObject == null) && array[j].HasItem())
			{
				GameObject gameObject2 = array[j].TakeItem();
				if (gameObject2 != null)
				{
					ServerPlayerRespawnManager.KillOrRespawn(gameObject2, null);
				}
			}
		}
	}
}
