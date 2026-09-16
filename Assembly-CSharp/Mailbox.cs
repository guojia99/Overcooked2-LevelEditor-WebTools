using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Messaging;

internal class Mailbox
{
	public class SequenceNumberInformation
	{
		public FastList<int> sequenceNumbers = new FastList<int>(42);

		public IOnlineMultiplayerSessionUserId sessionUserId;
	}

	public static Mailbox Client = new Mailbox();

	public static Mailbox Server = new Mailbox();

	private IOnlineMultiplayerSessionCoordinator m_SessionCoordinator;

	private Predicate<SequenceNumberInformation> HasValidSessionUser = (SequenceNumberInformation info) => info.sessionUserId != null;

	private FastList<FastList<OrderedMessageReceivedCallback>> m_OrderedEvents = new FastList<FastList<OrderedMessageReceivedCallback>>(42);

	private FastList<SequenceNumberInformation> m_OrderedEventSequenceNumbers = new FastList<SequenceNumberInformation>(4);

	private FastList<FastList<UnorderedMessageReceivedCallback>> m_UnorderedEvents = new FastList<FastList<UnorderedMessageReceivedCallback>>(42);

	private FastList<SequenceNumberInformation> toRemove = new FastList<SequenceNumberInformation>();

	private NetworkPeer m_Peer;

	public void Initialise(NetworkPeer peer)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_SessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		for (int i = 0; i < 42; i++)
		{
			m_OrderedEvents.Add(new FastList<OrderedMessageReceivedCallback>());
		}
		ResolveSessionMembers();
		for (int j = 0; j < 42; j++)
		{
			m_UnorderedEvents.Add(new FastList<UnorderedMessageReceivedCallback>());
		}
		m_Peer = peer;
		m_Peer.OnMessageReceived += OnMessageReceived;
		ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
	}

	public void Shutdown()
	{
		m_Peer.OnMessageReceived -= OnMessageReceived;
		m_Peer = null;
		ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Remove(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
	}

	public void Clear()
	{
		for (int i = 0; i < m_OrderedEvents.Count; i++)
		{
			m_OrderedEvents._items[i].Clear();
		}
		m_OrderedEventSequenceNumbers.Clear();
		for (int j = 0; j < 42; j++)
		{
			m_UnorderedEvents._items[j].Clear();
		}
	}

	private void OnUserRemoved(User user)
	{
		SequenceNumberInformation sequenceNumberInformation = FindSequenceInfo(user.SessionId);
		if (sequenceNumberInformation == null)
		{
			m_OrderedEventSequenceNumbers.Remove(sequenceNumberInformation);
		}
	}

	public void ResolveSessionMembers()
	{
		if (m_SessionCoordinator != null && m_SessionCoordinator.Members() != null)
		{
			IOnlineMultiplayerSessionUserId[] array = m_SessionCoordinator.Members();
			foreach (IOnlineMultiplayerSessionUserId sessionUserId in array)
			{
				SequenceNumberInformation sequenceNumberInformation = FindSequenceInfo(sessionUserId);
				if (sequenceNumberInformation == null)
				{
					sequenceNumberInformation = AddSequenceInfoForSessionUser(sessionUserId);
				}
			}
			toRemove.Clear();
			for (int j = 0; j < m_OrderedEventSequenceNumbers.Count; j++)
			{
				SequenceNumberInformation sequenceNumberInformation2 = m_OrderedEventSequenceNumbers._items[j];
				if (!array.Contains(sequenceNumberInformation2.sessionUserId))
				{
					toRemove.Add(sequenceNumberInformation2);
				}
			}
			for (int k = 0; k < toRemove.Count; k++)
			{
				m_OrderedEventSequenceNumbers.Remove(toRemove._items[k]);
			}
		}
		else
		{
			m_OrderedEventSequenceNumbers.RemoveAll(HasValidSessionUser);
		}
	}

	private SequenceNumberInformation AddSequenceInfoForSessionUser(IOnlineMultiplayerSessionUserId sessionUserId)
	{
		SequenceNumberInformation sequenceNumberInformation = new SequenceNumberInformation();
		sequenceNumberInformation.sessionUserId = sessionUserId;
		for (int i = 0; i < 42; i++)
		{
			sequenceNumberInformation.sequenceNumbers._items[i] = -1;
		}
		m_OrderedEventSequenceNumbers.Add(sequenceNumberInformation);
		return sequenceNumberInformation;
	}

	public void ResetSequenceNumbers()
	{
		m_OrderedEventSequenceNumbers.Clear();
	}

	public void RegisterForMessageType(MessageType type, OrderedMessageReceivedCallback func)
	{
		m_OrderedEvents._items[(int)type].Add(func);
	}

	public void UnregisterForMessageType(MessageType type, OrderedMessageReceivedCallback func)
	{
		FastList<OrderedMessageReceivedCallback> fastList = m_OrderedEvents._items[(int)type];
		if (fastList != null && fastList.Contains(func))
		{
			fastList.Remove(func);
		}
	}

	public void RegisterForMessageType(MessageType type, UnorderedMessageReceivedCallback func)
	{
		m_UnorderedEvents._items[(int)type].Add(func);
	}

	public void UnregisterForMessageType(MessageType type, UnorderedMessageReceivedCallback func)
	{
		FastList<UnorderedMessageReceivedCallback> fastList = m_UnorderedEvents._items[(int)type];
		if (fastList != null && fastList.Contains(func))
		{
			fastList.Remove(func);
		}
	}

	private SequenceNumberInformation FindSequenceInfo(IOnlineMultiplayerSessionUserId sessionUserId)
	{
		if (sessionUserId == null)
		{
			return null;
		}
		SequenceNumberInformation result = null;
		for (int i = 0; i < m_OrderedEventSequenceNumbers.Count; i++)
		{
			if (sessionUserId == m_OrderedEventSequenceNumbers._items[i].sessionUserId)
			{
				result = m_OrderedEventSequenceNumbers._items[i];
			}
		}
		return result;
	}

	private void OnMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, MessageType type, Serialisable message, uint sequence, bool bReliable)
	{
		int iLastSequence = -1;
		SequenceNumberInformation sequenceNumberInformation = null;
		if (!bReliable && sessionUserId != null)
		{
			sequenceNumberInformation = FindSequenceInfo(sessionUserId);
			if (sequenceNumberInformation == null)
			{
				sequenceNumberInformation = AddSequenceInfoForSessionUser(sessionUserId);
			}
			iLastSequence = sequenceNumberInformation.sequenceNumbers._items[(int)type];
		}
		if (bReliable || sequenceNumberInformation == null || CheckSequenced(iLastSequence, (int)sequence))
		{
			if (sequenceNumberInformation != null)
			{
				sequenceNumberInformation.sequenceNumbers._items[(int)type] = (int)sequence;
			}
			FastList<OrderedMessageReceivedCallback> fastList = m_OrderedEvents._items[(int)type];
			for (int i = 0; i < fastList.Count; i++)
			{
				fastList._items[i](sessionUserId, message);
			}
		}
		FastList<UnorderedMessageReceivedCallback> fastList2 = m_UnorderedEvents._items[(int)type];
		for (int j = 0; j < fastList2.Count; j++)
		{
			fastList2._items[j](sessionUserId, message, sequence);
		}
	}

	public static bool CheckSequenced(int iLastSequence, int iCurrentSequence)
	{
		if (iLastSequence == -1)
		{
			return true;
		}
		if (iLastSequence == iCurrentSequence)
		{
			return true;
		}
		if (iCurrentSequence >= iLastSequence)
		{
			if (iCurrentSequence < iLastSequence + 32767 && iCurrentSequence <= 65535)
			{
				return true;
			}
		}
		else if (iLastSequence > 32768 && iCurrentSequence < 32767 - (65535 - iLastSequence))
		{
			return true;
		}
		return false;
	}
}
