namespace InControl
{
	public class Logger
	{
		public delegate void LogMessageHandler(LogMessage message);

		public static event LogMessageHandler OnLogMessage;

		public static void LogInfo(string text)
		{
			if (Logger.OnLogMessage != null)
			{
				LogMessage message = new LogMessage
				{
					text = text,
					type = LogMessageType.Info
				};
				Logger.OnLogMessage(message);
			}
		}

		public static void LogWarning(string text)
		{
			if (Logger.OnLogMessage != null)
			{
				LogMessage message = new LogMessage
				{
					text = text,
					type = LogMessageType.Warning
				};
				Logger.OnLogMessage(message);
			}
		}

		public static void LogError(string text)
		{
			if (Logger.OnLogMessage != null)
			{
				LogMessage message = new LogMessage
				{
					text = text,
					type = LogMessageType.Error
				};
				Logger.OnLogMessage(message);
			}
		}
	}
}
