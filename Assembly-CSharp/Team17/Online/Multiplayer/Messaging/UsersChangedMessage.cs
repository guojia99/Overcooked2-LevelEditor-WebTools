using System.Collections.Generic;
using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class UsersChangedMessage : Serialisable
	{
		public FastList<UserData> m_Users = new FastList<UserData>(4);

		public void Initialise(FastList<User> users)
		{
			m_Users.Clear();
			for (int i = 0; i < users.Count; i++)
			{
				User user = users._items[i];
				AddUser(user);
			}
		}

		public void Initialise(UserData userData)
		{
			m_Users.Clear();
			m_Users.Add(userData);
		}

		public void AddUser(User user)
		{
			UserData userData = new UserData();
			userData.Initialise(user);
			m_Users.Add(userData);
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_Users.Count, 3);
			for (int i = 0; i < m_Users.Count; i++)
			{
				UserData userData = m_Users._items[i];
				userData.Serialise(writer);
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_Users.Clear();
			uint num = reader.ReadUInt32(3);
			for (int i = 0; i < num; i++)
			{
				UserData userData = new UserData();
				userData.Deserialise(reader);
				m_Users.Add(userData);
			}
			return true;
		}
	}
}
