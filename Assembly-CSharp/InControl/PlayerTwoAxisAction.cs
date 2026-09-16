namespace InControl
{
	public class PlayerTwoAxisAction : TwoAxisInputControl
	{
		private PlayerAction negativeXAction;

		private PlayerAction positiveXAction;

		private PlayerAction negativeYAction;

		private PlayerAction positiveYAction;

		public bool InvertYAxis { get; set; }

		internal PlayerTwoAxisAction(PlayerAction negativeXAction, PlayerAction positiveXAction, PlayerAction negativeYAction, PlayerAction positiveYAction)
		{
			this.negativeXAction = negativeXAction;
			this.positiveXAction = positiveXAction;
			this.negativeYAction = negativeYAction;
			this.positiveYAction = positiveYAction;
			InvertYAxis = false;
			Raw = true;
		}

		internal void Update(ulong updateTick, float deltaTime)
		{
			float x = Utility.ValueFromSides(negativeXAction, positiveXAction, false);
			float y = Utility.ValueFromSides(negativeYAction, positiveYAction, InputManager.InvertYAxis || InvertYAxis);
			UpdateWithAxes(x, y, updateTick, deltaTime);
		}
	}
}
