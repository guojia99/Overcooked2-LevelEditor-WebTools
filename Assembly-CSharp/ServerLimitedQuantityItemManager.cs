using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;

public class ServerLimitedQuantityItemManager : ServerSynchroniserBase
{
	private LimitedQuantityItemManager m_BaseObject;

	private FastList<ServerLimitedQuantityItem> m_AllObjects;

	public void Awake()
	{
		m_BaseObject = GetComponent<LimitedQuantityItemManager>();
		m_AllObjects = new FastList<ServerLimitedQuantityItem>(m_BaseObject.m_MaxObjects);
	}

	public void AddItemToList(ServerLimitedQuantityItem item)
	{
		int num = m_AllObjects.Count - m_BaseObject.m_MaxObjects + 1;
		for (int i = 0; i < num; i++)
		{
			ServerLimitedQuantityItem serverLimitedQuantityItem = null;
			float num2 = float.MinValue;
			for (int j = 0; j < m_AllObjects.Count; j++)
			{
				ServerLimitedQuantityItem serverLimitedQuantityItem2 = m_AllObjects._items[j];
				if (!serverLimitedQuantityItem2.IsInvincible())
				{
					float destructionScore = serverLimitedQuantityItem2.GetDestructionScore();
					if (destructionScore > num2)
					{
						serverLimitedQuantityItem = serverLimitedQuantityItem2;
						num2 = destructionScore;
					}
				}
			}
			if (null != serverLimitedQuantityItem)
			{
				NetworkUtils.DestroyObject(serverLimitedQuantityItem.gameObject);
				m_AllObjects.Remove(serverLimitedQuantityItem);
				continue;
			}
			break;
		}
		m_AllObjects.Add(item);
	}

	public void RemoveItemFromList(ServerLimitedQuantityItem item)
	{
		m_AllObjects.Remove(item);
	}

	public override void OnDestroy()
	{
		m_AllObjects.Clear();
	}
}
