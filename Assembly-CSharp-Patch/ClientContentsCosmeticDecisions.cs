using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientContentsCosmeticDecisions : ClientSynchroniserBase, IClientCookingNotifed, IClientMixingNotifed
{
	private ContentsCosmeticDecisions m_contentsCosmeticDecisions;

	private ClientIngredientContainer m_iIngredientContents;

	private int m_IngredientCapacity;

	private Color m_uncookedSurfaceColour = Color.white;

	private Color m_uncookedBubbleColour = Color.white;

	private bool m_validRecipe = true;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_contentsCosmeticDecisions = (ContentsCosmeticDecisions)synchronisedObject;
		if (m_contentsCosmeticDecisions.m_animator == null)
		{
		}
		m_iIngredientContents = m_contentsCosmeticDecisions.m_gameObject.RequireComponent<ClientIngredientContainer>();
		m_iIngredientContents.RegisterContentsChangedCallback(OnContentChanged);
		m_IngredientCapacity = m_iIngredientContents.gameObject.RequireComponent<IngredientContainer>().m_capacity;
		m_contentsCosmeticDecisions.m_contentsObject.SetActive(false);
	}

	public void OnCookingStarted()
	{
		if (m_contentsCosmeticDecisions.m_animator.IsActive() && m_contentsCosmeticDecisions.m_hasOnParam)
		{
			m_contentsCosmeticDecisions.m_animator.SetBool(ContentsCosmeticDecisions.c_onParam, true);
		}
	}

	public void OnCookingFinished()
	{
		if (m_contentsCosmeticDecisions.m_animator.IsActive() && m_contentsCosmeticDecisions.m_hasOnParam)
		{
			m_contentsCosmeticDecisions.m_animator.SetBool(ContentsCosmeticDecisions.c_onParam, false);
		}
	}

	public void OnCookingPropChanged(float newProp)
	{
		if (m_contentsCosmeticDecisions.m_animator.IsActive() && m_contentsCosmeticDecisions.m_hasProgressParam)
		{
			m_contentsCosmeticDecisions.m_animator.SetFloat(ContentsCosmeticDecisions.c_progressParam, newProp);
		}
		if (newProp > 1f)
		{
			float t = Mathf.Min(2f, newProp) - 1f;
			if (!m_validRecipe)
			{
				t = 1f;
			}
			Color b = Color.Lerp(m_uncookedSurfaceColour, Color.black, 0.8f);
			Color b2 = Color.Lerp(m_uncookedBubbleColour, Color.black, 0.8f);
			Color surfaceColour = Color.Lerp(m_uncookedSurfaceColour, b, t);
			Color bubbleColour = Color.Lerp(m_uncookedBubbleColour, b2, t);
			SetRendererColourRecursive(m_contentsCosmeticDecisions.m_contentsObject, surfaceColour, bubbleColour);
		}
	}

	public void OnMixingStarted()
	{
		if (m_contentsCosmeticDecisions.m_animator.IsActive() && m_contentsCosmeticDecisions.m_hasOnParam)
		{
			m_contentsCosmeticDecisions.m_animator.SetBool(ContentsCosmeticDecisions.c_onParam, true);
		}
	}

	public void OnMixingFinished()
	{
		if (m_contentsCosmeticDecisions.m_animator.IsActive() && m_contentsCosmeticDecisions.m_hasOnParam)
		{
			m_contentsCosmeticDecisions.m_animator.SetBool(ContentsCosmeticDecisions.c_onParam, false);
		}
	}

	public void OnMixingPropChanged(float newProp)
	{
		if (m_contentsCosmeticDecisions.m_animator.IsActive() && m_contentsCosmeticDecisions.m_hasProgressParam)
		{
			m_contentsCosmeticDecisions.m_animator.SetFloat(ContentsCosmeticDecisions.c_progressParam, newProp);
		}
		if (m_contentsCosmeticDecisions.m_prefabLookup != null)
		{
			MixedCompositeAssembledNode mixedCompositeAssembledNode = new MixedCompositeAssembledNode();
			mixedCompositeAssembledNode.m_composition = m_iIngredientContents.GetContents();
			mixedCompositeAssembledNode.m_recordedProgress = newProp;
			if (newProp >= 1f)
			{
				if (newProp < 2f)
				{
					mixedCompositeAssembledNode.m_progress = MixedCompositeOrderNode.MixingProgress.Mixed;
				}
				else
				{
					mixedCompositeAssembledNode.m_progress = MixedCompositeOrderNode.MixingProgress.OverMixed;
				}
			}
			else
			{
				mixedCompositeAssembledNode.m_progress = MixedCompositeOrderNode.MixingProgress.Unmixed;
			}
			m_validRecipe = m_contentsCosmeticDecisions.m_prefabLookup.GetPrefabForNode(mixedCompositeAssembledNode) != null;
		}
		if (newProp > 1f)
		{
			float t = Mathf.Min(2f, newProp) - 1f;
			if (!m_validRecipe)
			{
				t = 1f;
			}
			Color b = Color.Lerp(m_uncookedSurfaceColour, Color.black, 0.8f);
			Color b2 = Color.Lerp(m_uncookedBubbleColour, Color.black, 0.8f);
			Color surfaceColour = Color.Lerp(m_uncookedSurfaceColour, b, t);
			Color bubbleColour = Color.Lerp(m_uncookedBubbleColour, b2, t);
			SetRendererColourRecursive(m_contentsCosmeticDecisions.m_contentsObject, surfaceColour, bubbleColour);
		}
	}

	private void OnContentChanged(AssembledDefinitionNode[] _contents)
	{
		m_uncookedSurfaceColour = GetNewColour(_contents, false);
		m_uncookedBubbleColour = GetNewColour(_contents, true);
		SetRendererColourRecursive(m_contentsCosmeticDecisions.m_contentsObject, m_uncookedSurfaceColour, m_uncookedBubbleColour);
		if (_contents.Length == 0)
		{
			m_contentsCosmeticDecisions.m_contentsObject.SetActive(false);
			return;
		}
		m_contentsCosmeticDecisions.m_contentsObject.SetActive(true);
		float num = Mathf.Clamp01((float)_contents.Length / (float)m_IngredientCapacity);
		float y = num * (m_contentsCosmeticDecisions.m_contentsYPositionWhenFull - m_contentsCosmeticDecisions.m_contentsYPositionWhenEmpty) + m_contentsCosmeticDecisions.m_contentsYPositionWhenEmpty;
		m_contentsCosmeticDecisions.m_contentsObject.transform.localPosition = m_contentsCosmeticDecisions.m_contentsObject.transform.localPosition.WithY(y);
		m_contentsCosmeticDecisions.m_contentsObject.transform.localScale = Vector3.Lerp(m_contentsCosmeticDecisions.m_contentsScale.m_empty, m_contentsCosmeticDecisions.m_contentsScale.m_full, m_contentsCosmeticDecisions.m_contentsScale.m_curve.Evaluate(num));
	}

	public void SetRendererColourRecursive(GameObject _object, Color _surfaceColour, Color _bubbleColour)
	{
		Renderer component = _object.GetComponent<Renderer>();
		if ((bool)component)
		{
			if (component.material.name == m_contentsCosmeticDecisions.m_surfaceMaterialName + " (Instance)")
			{
				component.material.SetColor("_MaskColor", _surfaceColour);
			}
			else if (component.material.name == m_contentsCosmeticDecisions.m_bubbleMaterialName + " (Instance)")
			{
				component.material.SetColor("_MaskColor", _bubbleColour);
			}
		}
		for (int i = 0; i < _object.transform.childCount; i++)
		{
			SetRendererColourRecursive(_object.transform.GetChild(i).gameObject, _surfaceColour, _bubbleColour);
		}
	}

	// patch
	private List<IngredientAssembledNode> GetAllIngredients(AssembledDefinitionNode node)
	{
		List<IngredientAssembledNode> ingredients = new List<IngredientAssembledNode>();
		if (node is IngredientAssembledNode)
			ingredients.Add(node as IngredientAssembledNode);
		else if (node is CompositeAssembledNode)
		{
			foreach (AssembledDefinitionNode node1 in (node as CompositeAssembledNode).m_composition)
				ingredients.AddRange(GetAllIngredients(node1));
		}
		return ingredients;
	}
	// patch

	private Color GetNewColour(AssembledDefinitionNode[] _contents, bool bubble)
	{
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		int num4 = 0;
		for (int i = 0; i < _contents.Length; i++)
		{
			// patch
			var ingredients = GetAllIngredients(_contents[i]);
			foreach (IngredientAssembledNode ingredient in ingredients)
			{
                num += ingredient.m_ingriedientOrderNode.m_colour.r;
                num2 += ingredient.m_ingriedientOrderNode.m_colour.g;
                num3 += ingredient.m_ingriedientOrderNode.m_colour.b;
                num4++;
            }
			//IngredientAssembledNode ingredientAssembledNode = _contents[i] as IngredientAssembledNode;
			//if (ingredientAssembledNode != null)
			//{
			//	num += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.r;
			//	num2 += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.g;
			//	num3 += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.b;
			//	num4++;
			//}
			// patch
		}
		if (num4 > 0)
		{
			num /= (float)num4;
			num2 /= (float)num4;
			num3 /= (float)num4;
		}
		float num5 = 0.25f;
		if (bubble)
		{
			num += num5;
			num2 += num5;
			num3 += num5;
		}
		return new Color(num, num2, num3, 1f);
	}
}
