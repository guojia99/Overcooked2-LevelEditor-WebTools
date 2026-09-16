namespace InControl
{
	public class PlayerOneAxisAction : OneAxisInputControl
	{
		private PlayerAction negativeAction;

		private PlayerAction positiveAction;

		internal PlayerOneAxisAction(PlayerAction negativeAction, PlayerAction positiveAction)
		{
			this.negativeAction = negativeAction;
			this.positiveAction = positiveAction;
			Raw = true;
		}

		internal void Update(ulong updateTick, float deltaTime)
		{
			float value = Utility.ValueFromSides(negativeAction, positiveAction);
			CommitWithValue(value, updateTick, deltaTime);
		}
	}
}
