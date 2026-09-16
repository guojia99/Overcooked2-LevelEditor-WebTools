namespace Team17.Online
{
	public class OnlineMultiplayerSessionPropertySearchValue
	{
		public enum Operator
		{
			eEquals = 0,
			eNotEquals = 1,
			eLessThan = 2,
			eLessEqualsThan = 3,
			eGreaterThan = 4,
			eGreaterEqualsThan = 5
		}

		public IOnlineMultiplayerSessionProperty m_property;

		public uint m_value;

		public uint m_valueMinRange;

		public uint m_valueMaxRange;

		public Operator m_operator;
	}
}
