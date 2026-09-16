namespace Team17.Online.Multiplayer.Connection
{
	public class MessageTransmitter
	{
		protected Generic<bool, byte[], int, bool> m_SendData;

		public virtual void Initialise(Generic<bool, byte[], int, bool> sendData)
		{
			m_SendData = sendData;
		}

		public virtual bool Transmit(byte[] data, int size, bool bReliable)
		{
			return m_SendData(data, size, bReliable);
		}

		public virtual void Update()
		{
		}
	}
}
