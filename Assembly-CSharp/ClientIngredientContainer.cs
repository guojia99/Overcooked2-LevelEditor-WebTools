using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientIngredientContainer : ClientSynchroniserBase, IIngredientContents
{
	private List<AssembledDefinitionNode> m_contents = new List<AssembledDefinitionNode>();

	private ContentsChangedCallback m_contentsChangedCallback = delegate
	{
	};

	private IngredientContainer m_ingredientContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_ingredientContainer = base.gameObject.RequireComponent<IngredientContainer>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.IngredientContainer;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		IngredientContainerMessage ingredientContainerMessage = (IngredientContainerMessage)serialisable;
		switch (ingredientContainerMessage.Type)
		{
		case IngredientContainerMessage.MessageType.ActiveState:
			base.gameObject.SetActive(ingredientContainerMessage.ActiveState);
			break;
		case IngredientContainerMessage.MessageType.ContentsChanged:
			m_contents.Clear();
			m_contents.AddRange(ingredientContainerMessage.Contents);
			m_contentsChangedCallback(GetContents());
			break;
		}
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

	public bool CanAddIngredient(AssembledDefinitionNode _orderData)
	{
		return m_contents.Count < m_ingredientContainer.m_capacity;
	}

	public int GetContentsCount()
	{
		return m_contents.Count;
	}

	public bool CanTakeContents(AssembledDefinitionNode[] _contents)
	{
		int num = m_ingredientContainer.m_capacity - m_contents.Count;
		return num >= _contents.Length;
	}

	public void Empty()
	{
		throw new NotImplementedException();
	}

	public bool HasContents()
	{
		return m_contents.Count > 0;
	}

	public void AddIngredient(AssembledDefinitionNode _orderData)
	{
		throw new NotImplementedException();
	}

	public AssembledDefinitionNode RemoveIngredient(int i)
	{
		throw new NotImplementedException();
	}

	public AssembledDefinitionNode GetContentsElement(int i)
	{
		return m_contents[i];
	}
}
