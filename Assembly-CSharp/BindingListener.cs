using InControl;

public class BindingListener : PlayerActionSet
{
	private PlayerAction m_Action;

	private VoidGeneric<Key> m_BindingReceivedCallback;

	public BindingListener()
	{
		m_Action = CreatePlayerAction("Action");
		base.ListenOptions.IncludeControllers = false;
		base.ListenOptions.IncludeNonStandardControls = false;
		base.ListenOptions.MaxAllowedBindings = 1u;
		base.ListenOptions.OnBindingFound = OnBindingFound;
		base.ListenOptions.OnBindingRejected = OnBindingRejected;
	}

	public void StartListening(VoidGeneric<Key> OnBindingReceived)
	{
		m_BindingReceivedCallback = OnBindingReceived;
		m_Action.ListenForBinding();
	}

	public void StopListening()
	{
		m_Action.StopListeningForBinding();
	}

	public bool OnBindingFound(PlayerAction action, BindingSource binding)
	{
		if (binding is KeyBindingSource)
		{
			KeyBindingSource keyBindingSource = binding as KeyBindingSource;
			Key param = keyBindingSource.Control.Get(0);
			if (m_BindingReceivedCallback != null)
			{
				m_BindingReceivedCallback(param);
			}
			return true;
		}
		return false;
	}

	public void OnBindingRejected(PlayerAction action, BindingSource binding, BindingSourceRejectionType reason)
	{
		if (reason == BindingSourceRejectionType.DuplicateBindingOnAction)
		{
			StopListening();
		}
	}
}
