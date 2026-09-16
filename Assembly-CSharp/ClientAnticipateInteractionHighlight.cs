using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientAnticipateInteractionHighlight : ClientSynchroniserBase, IAnticipateInteractionNotifications
{
	private int m_highlightCount;

	private List<ClientAnticipateInteractionHighlight> m_children = new List<ClientAnticipateInteractionHighlight>();

	private List<Renderer> m_renderers = new List<Renderer>();

	private float[][] m_startingBrightness;

	private AnticipateInteractionHighlight m_highlight;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_highlight = (AnticipateInteractionHighlight)synchronisedObject;
		RebuildHighlightMaterials();
		ClientAttachStation clientAttachStation = base.gameObject.RequestComponent<ClientAttachStation>();
		if (clientAttachStation != null)
		{
			clientAttachStation.RegisterOnItemAdded(OnItemAttachmentAdded);
			clientAttachStation.RegisterOnItemRemoved(OnItemAttachmentRemoved);
		}
		ClientPlateStackBase clientPlateStackBase = base.gameObject.RequestComponent<ClientPlateStackBase>();
		if (clientPlateStackBase != null)
		{
			clientPlateStackBase.RegisterOnPlateRemoved(OnPlateRemoved);
		}
		ClientIngredientContainer clientIngredientContainer = base.gameObject.RequestComponent<ClientIngredientContainer>();
		if (clientIngredientContainer != null)
		{
			clientIngredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
		}
	}

	protected override void OnDestroy()
	{
		ClientAttachStation clientAttachStation = base.gameObject.RequestComponent<ClientAttachStation>();
		if (clientAttachStation != null)
		{
			clientAttachStation.UnregisterOnItemAdded(OnItemAttachmentAdded);
			clientAttachStation.UnregisterOnItemRemoved(OnItemAttachmentRemoved);
		}
		ClientPlateStackBase clientPlateStackBase = base.gameObject.RequestComponent<ClientPlateStackBase>();
		if (clientPlateStackBase != null)
		{
			clientPlateStackBase.UnregisterOnPlateRemoved(OnPlateRemoved);
		}
		ClientIngredientContainer clientIngredientContainer = base.gameObject.RequestComponent<ClientIngredientContainer>();
		if (clientIngredientContainer != null)
		{
			clientIngredientContainer.UnregisterContentsChangedCallback(OnContentsChanged);
		}
		base.OnDestroy();
	}

	public void RebuildHighlightMaterials()
	{
		bool flag = IsHighlighted();
		if (flag)
		{
			Unhighlight();
		}
		FindRenderers();
		SaveRenderersStartingBrightness();
		if (flag)
		{
			Highlight();
		}
	}

	private void FindRenderers()
	{
		GameObject highlightObjectOverride = m_highlight.m_highlightObjectOverride;
		if (highlightObjectOverride == null)
		{
			highlightObjectOverride = base.gameObject;
		}
		m_renderers.Clear();
		FindRenderersRecursive(highlightObjectOverride);
	}

	private void FindRenderersRecursive(GameObject target)
	{
		Renderer[] collection = target.RequestComponents<Renderer>();
		m_renderers.AddRange(collection);
		for (int i = 0; i < target.transform.childCount; i++)
		{
			GameObject gameObject = target.transform.GetChild(i).gameObject;
			ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight = gameObject.RequestComponent<ClientAnticipateInteractionHighlight>();
			if (clientAnticipateInteractionHighlight == null || clientAnticipateInteractionHighlight == this)
			{
				FindRenderersRecursive(gameObject);
			}
		}
	}

	private void SaveRenderersStartingBrightness()
	{
		m_startingBrightness = new float[m_renderers.Count][];
		for (int i = 0; i < m_renderers.Count; i++)
		{
			Material[] sharedMaterials = m_renderers[i].sharedMaterials;
			int num = sharedMaterials.Length;
			m_startingBrightness[i] = new float[num];
			for (int j = 0; j < num; j++)
			{
				Material material = sharedMaterials[j];
				if (CanHighlight(material))
				{
					m_startingBrightness[i][j] = material.GetFloat("_Brightness");
				}
			}
		}
	}

	private void Highlight()
	{
		for (int i = 0; i < m_renderers.Count; i++)
		{
			if (m_renderers[i] == null)
			{
				RebuildHighlightMaterials();
				break;
			}
			Material[] materials = m_renderers[i].materials;
			foreach (Material material in materials)
			{
				if (CanHighlight(material))
				{
					material.SetFloat("_Brightness", m_highlight.m_brightnessModifier);
				}
			}
		}
	}

	private void Unhighlight()
	{
		for (int i = 0; i < m_renderers.Count; i++)
		{
			if (m_renderers[i] == null)
			{
				continue;
			}
			Material[] materials = m_renderers[i].materials;
			for (int j = 0; j < materials.Length; j++)
			{
				Material material = materials[j];
				if (CanHighlight(material))
				{
					material.SetFloat("_Brightness", m_startingBrightness[i][j]);
				}
			}
		}
	}

	private void IncrementHighlight()
	{
		m_highlightCount++;
		if (m_highlightCount == 1)
		{
			Highlight();
			for (int i = 0; i < m_children.Count; i++)
			{
				m_children[i].IncrementHighlight();
			}
		}
	}

	private void DecrementHighlight()
	{
		m_highlightCount--;
		if (m_highlightCount == 0)
		{
			Unhighlight();
			for (int i = 0; i < m_children.Count; i++)
			{
				m_children[i].DecrementHighlight();
			}
		}
	}

	public void AddChild(ClientAnticipateInteractionHighlight child)
	{
		if (!m_children.Contains(child))
		{
			m_children.Add(child);
			if (IsHighlighted())
			{
				child.IncrementHighlight();
			}
		}
	}

	public void RemoveChild(ClientAnticipateInteractionHighlight removedChild)
	{
		for (int i = 0; i < m_children.Count; i++)
		{
			ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight = m_children[i];
			if (!(clientAnticipateInteractionHighlight != removedChild))
			{
				if (IsHighlighted())
				{
					removedChild.DecrementHighlight();
				}
				m_children.RemoveAt(i);
				break;
			}
		}
	}

	private bool CanHighlight(Material _material)
	{
		return _material != null && _material.HasProperty("_Brightness");
	}

	private bool IsHighlighted()
	{
		return m_highlightCount > 0;
	}

	private void OnItemAttachmentAdded(IClientAttachment attachment)
	{
		ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight = attachment.AccessGameObject().RequestComponent<ClientAnticipateInteractionHighlight>();
		if (!(clientAnticipateInteractionHighlight == null))
		{
			AddChild(clientAnticipateInteractionHighlight);
		}
	}

	private void OnItemAttachmentRemoved(IClientAttachment attachment)
	{
		ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight = attachment.AccessGameObject().RequestComponent<ClientAnticipateInteractionHighlight>();
		if (!(clientAnticipateInteractionHighlight == null))
		{
			RemoveChild(clientAnticipateInteractionHighlight);
		}
	}

	private void OnContentsChanged(AssembledDefinitionNode[] node)
	{
		if (base.isActiveAndEnabled)
		{
			StartCoroutine(DelayedContentsChanged());
		}
	}

	private IEnumerator DelayedContentsChanged()
	{
		yield return null;
		RebuildHighlightMaterials();
	}

	public void OnInteractionAnticipationStart(InteractionType _type, GameObject _player)
	{
		IncrementHighlight();
	}

	public void OnInteractionAnticipationEnded(InteractionType _type, GameObject _player)
	{
		DecrementHighlight();
	}

	private void OnPlateRemoved(GameObject _plate)
	{
		ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight = _plate.RequestComponent<ClientAnticipateInteractionHighlight>();
		if (!(clientAnticipateInteractionHighlight == null))
		{
			RemoveChild(clientAnticipateInteractionHighlight);
		}
	}
}
