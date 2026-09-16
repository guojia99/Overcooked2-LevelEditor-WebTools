using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerIngredientContainer : ServerSynchroniserBase, IIngredientContents
{
	private IngredientContainer m_ingredientContainer;

	private List<AssembledDefinitionNode> m_contents = new List<AssembledDefinitionNode>();

	private ContentsChangedCallback m_contentsChangedCallback = delegate
	{
	};

	private ServerPhysicalAttachment m_attachment;

	private IngredientContainerMessage m_ServerData = new IngredientContainerMessage();

	private bool m_bCachedActive;

	private static List<ServerIngredientContainer> ms_AllIngredientContainers = new List<ServerIngredientContainer>();

	public static List<ServerIngredientContainer> GetAllIngredientContainers()
	{
		return ms_AllIngredientContainers;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		ms_AllIngredientContainers.Add(this);
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		ms_AllIngredientContainers.Remove(this);
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_ingredientContainer = (IngredientContainer)synchronisedObject;
		m_attachment = base.gameObject.GetComponent<ServerPhysicalAttachment>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.IngredientContainer;
	}

	public bool HasContents()
	{
		return m_contents.Count > 0;
	}

	public bool CanTakeContents(AssembledDefinitionNode[] _contents)
	{
		int num = m_ingredientContainer.m_capacity - m_contents.Count;
		return num >= _contents.Length;
	}

	public bool CanAddIngredient(AssembledDefinitionNode _orderData)
	{
		return m_contents.Count < m_ingredientContainer.m_capacity;
	}

	public virtual void AddIngredient(AssembledDefinitionNode _orderData)
	{
		m_contents.Add(_orderData);
		OnContentsChanged();
	}

	public AssembledDefinitionNode RemoveIngredient(int i)
	{
		if (m_contents.Count > i)
		{
			AssembledDefinitionNode assembledDefinitionNode = m_contents[i];
			m_contents.Remove(assembledDefinitionNode);
			OnContentsChanged();
			return assembledDefinitionNode;
		}
		return null;
	}

	public int GetContentsCount()
	{
		return m_contents.Count;
	}

	public AssembledDefinitionNode GetContentsElement(int i)
	{
		return m_contents[i];
	}

	public void Empty()
	{
		m_contents.Clear();
		OnContentsChanged();
	}

	public bool CanTakeCopiedContents(ServerIngredientContainer _container)
	{
		return _container.m_contents.Count > 0 && CanTakeContents(_container.GetContents());
	}

	public void CopyContents(ServerIngredientContainer _container)
	{
		CopyContents(_container.GetContents());
	}

	public void CopyContents(AssembledDefinitionNode[] _contents)
	{
		m_contents.AddRange(_contents);
		OnContentsChanged();
	}

	public AssembledDefinitionNode[] GetContents()
	{
		return m_contents.ToArray();
	}

	public void RegisterContentsChangedCallback(ContentsChangedCallback _callback)
	{
		m_contentsChangedCallback = (ContentsChangedCallback)Delegate.Combine(m_contentsChangedCallback, _callback);
	}

	public void UnregisterContentsChangedCallback(ContentsChangedCallback _callback)
	{
		m_contentsChangedCallback = (ContentsChangedCallback)Delegate.Remove(m_contentsChangedCallback, _callback);
	}

	public void InformOfInternalChange()
	{
		OnContentsChanged();
	}

	private void OnContentsChanged()
	{
		AssembledDefinitionNode[] contents = GetContents();
		m_contentsChangedCallback(contents);
		m_ServerData.Initialise(contents);
		SendServerEvent(m_ServerData);
	}

	public override void UpdateSynchronising()
	{
		if (m_ingredientContainer != null && m_ingredientContainer.m_onSurfaceTriggerZone != null && m_attachment != null)
		{
			m_ingredientContainer.m_onSurfaceTriggerZone.enabled = m_attachment.IsAttached();
		}
	}

	public override Serialisable GetServerUpdate()
	{
		if (m_bCachedActive == base.gameObject.activeSelf)
		{
			return null;
		}
		m_bCachedActive = base.gameObject.activeSelf;
		m_ServerData.Initialise(base.gameObject.activeSelf);
		SendServerEvent(m_ServerData);
		return null;
	}
}
