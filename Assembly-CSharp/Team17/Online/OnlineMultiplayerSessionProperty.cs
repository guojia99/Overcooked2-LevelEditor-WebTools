namespace Team17.Online
{
	public class OnlineMultiplayerSessionProperty : IOnlineMultiplayerSessionProperty
	{
		public string m_name;

		public uint m_id;

		public uint m_index;

		public string Name
		{
			get
			{
				return m_name;
			}
		}

		public uint Id
		{
			get
			{
				return m_id;
			}
		}

		public uint Index
		{
			get
			{
				return m_index;
			}
		}
	}
}
