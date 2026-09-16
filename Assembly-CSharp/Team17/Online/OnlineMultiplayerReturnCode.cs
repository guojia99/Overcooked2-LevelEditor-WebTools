namespace Team17.Online
{
	public class OnlineMultiplayerReturnCode<ReturnCodeType> : SteamOnlineMultiplayerReturnCode
	{
		public bool m_usePlatformError;

		public ReturnCodeType m_returnCode = default(ReturnCodeType);

		public bool DisplayPlatformSpecificError(bool blockAllUnityThreads = false)
		{
			if (m_usePlatformError)
			{
				ShowErrorDialog(blockAllUnityThreads);
				return true;
			}
			return false;
		}
	}
}
