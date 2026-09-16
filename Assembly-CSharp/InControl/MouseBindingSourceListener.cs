namespace InControl
{
	public class MouseBindingSourceListener : BindingSourceListener
	{
		private Mouse detectFound;

		private int detectPhase;

		public void Reset()
		{
			detectFound = Mouse.None;
			detectPhase = 0;
		}

		public BindingSource Listen(BindingListenOptions listenOptions, InputDevice device)
		{
			if (!listenOptions.IncludeMouseButtons)
			{
				return null;
			}
			if (detectFound != Mouse.None && !MouseBindingSource.ButtonIsPressed(detectFound) && detectPhase == 2)
			{
				MouseBindingSource result = new MouseBindingSource(detectFound);
				Reset();
				return result;
			}
			Mouse mouse = ListenForControl();
			if (mouse != Mouse.None)
			{
				if (detectPhase == 1)
				{
					detectFound = mouse;
					detectPhase = 2;
				}
			}
			else if (detectPhase == 0)
			{
				detectPhase = 1;
			}
			return null;
		}

		private Mouse ListenForControl()
		{
			for (Mouse mouse = Mouse.None; mouse <= Mouse.Button9; mouse++)
			{
				if (MouseBindingSource.ButtonIsPressed(mouse))
				{
					return mouse;
				}
			}
			return Mouse.None;
		}
	}
}
