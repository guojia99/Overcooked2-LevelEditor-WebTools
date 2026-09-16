namespace Team17.Online.Multiplayer.Messaging
{
	public class ServerClientPhysicsObjectSynchroniser : ClientWorldObjectSynchroniser
	{
		public override EntityType GetEntityType()
		{
			return EntityType.PhysicsObject;
		}

		public override void Pause()
		{
		}

		public override void Resume()
		{
		}

		public override void OnResumeDataReceived(Serialisable _data)
		{
		}

		public override bool IsReadyToResume()
		{
			return true;
		}
	}
}
