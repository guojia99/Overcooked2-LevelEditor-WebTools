using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlateStackBase : ServerSynchroniserBase, IAddToStack
{
	protected PlateStackBase m_plateStack;

	private PlateStackMessage m_data = new PlateStackMessage();

	protected ServerStack m_stack;

	public override EntityType GetEntityType()
	{
		return EntityType.PlateStack;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_plateStack = (PlateStackBase)synchronisedObject;
		m_stack = base.gameObject.RequireComponent<ServerStack>();
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_plateStack.m_platePrefab);
	}

	protected virtual GameObject RemoveFromStack()
	{
		GameObject result = m_stack.RemoveFromStack();
		SendServerEvent(m_data);
		return result;
	}

	public virtual void AddToStack()
	{
		GameObject item = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_plateStack.m_platePrefab);
		m_stack.AddToStack(item);
	}

	public int GetSize()
	{
		return m_stack.GetSize();
	}

	public PlatingStepData GetPlatingStep()
	{
		return m_plateStack.GetPlatingStep();
	}
}
