public interface IMenuEventDelegate
{
	event MenuChangedHandler MenuChangedEvent;

	void ChildMenuChanged(IMenuEventDelegate sender = null, IMenuEventDelegate changedItem = null);
}
