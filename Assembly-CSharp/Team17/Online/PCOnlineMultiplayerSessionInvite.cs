namespace Team17.Online
{
	public abstract class PCOnlineMultiplayerSessionInvite
	{
		public virtual bool WasAcceptedBy(GamepadUser localUser)
		{
			return false;
		}
	}
}
