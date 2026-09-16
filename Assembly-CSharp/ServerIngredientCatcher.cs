using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[RequireComponent(typeof(IngredientContainer))]
public class ServerIngredientCatcher : ServerSynchroniserBase, IHandleCatch
{
	private IngredientCatcher m_catcher;

	private IAttachment m_attachment;

	private QueryForCatching m_allowCatchingCallback = (GameObject _object) => true;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_catcher = (IngredientCatcher)synchronisedObject;
		m_attachment = base.gameObject.RequestInterface<IAttachment>();
	}

	public bool CanHandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		if (!_object.AllowCatch(this, _directionXZ))
		{
			return false;
		}
		if (m_catcher.m_requireAttached)
		{
			if (m_attachment == null)
			{
				return false;
			}
			if (!m_attachment.IsAttached())
			{
				return false;
			}
		}
		if (!m_allowCatchingCallback(_object.AccessGameObject()))
		{
			return false;
		}
		IOrderDefinition orderDefinition = _object.AccessGameObject().RequestInterface<IOrderDefinition>();
		if (orderDefinition != null && orderDefinition.GetOrderComposition().Simpilfy() != AssembledDefinitionNode.NullNode)
		{
			IContainerTransferBehaviour containerTransferBehaviour = _object.AccessGameObject().RequestInterface<IContainerTransferBehaviour>();
			ServerIngredientContainer component = base.gameObject.GetComponent<ServerIngredientContainer>();
			if (containerTransferBehaviour != null && component != null && containerTransferBehaviour.CanTransferToContainer(component))
			{
				return component.CanAddIngredient(orderDefinition.GetOrderComposition());
			}
		}
		return false;
	}

	public void HandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		GameObject gameObject = _object.AccessGameObject();
		IThrowable throwable = gameObject.RequireInterface<IThrowable>();
		IThrower thrower = throwable.GetThrower();
		if (thrower != null)
		{
			GameObject gameObject2 = (thrower as MonoBehaviour).gameObject;
			ServerMessenger.Achievement(gameObject2, 12);
		}
		IOrderDefinition orderDefinition = gameObject.RequireInterface<IOrderDefinition>();
		if (orderDefinition.GetOrderComposition().Simpilfy() != AssembledDefinitionNode.NullNode)
		{
			IContainerTransferBehaviour containerTransferBehaviour = gameObject.RequireInterface<IContainerTransferBehaviour>();
			ServerIngredientContainer component = base.gameObject.GetComponent<ServerIngredientContainer>();
			if (containerTransferBehaviour != null && component != null && containerTransferBehaviour.CanTransferToContainer(component))
			{
				containerTransferBehaviour.TransferToContainer(null, component, true);
				component.InformOfInternalChange();
				gameObject.SetActive(false);
				NetworkUtils.DestroyObject(gameObject);
			}
		}
	}

	public void AlertToThrownItem(ICatchable _thrown, IThrower _thrower, Vector2 _directionXZ)
	{
	}

	public int GetCatchingPriority()
	{
		return 0;
	}

	public void RegisterAllowItemCatching(QueryForCatching _allowCatchingCallback)
	{
		m_allowCatchingCallback = (QueryForCatching)Delegate.Combine(m_allowCatchingCallback, _allowCatchingCallback);
	}

	public void UnregisterAllowItemCatching(QueryForCatching _allowCatchingCallback)
	{
		m_allowCatchingCallback = (QueryForCatching)Delegate.Remove(m_allowCatchingCallback, _allowCatchingCallback);
	}
}
