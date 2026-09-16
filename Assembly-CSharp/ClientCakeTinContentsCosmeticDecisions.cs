using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCakeTinContentsCosmeticDecisions : ClientSynchroniserBase, IClientCookingNotifed
{
	private CakeTinContentsCosmeticDecisions m_contentsCosmeticDecisions;

	private ClientIngredientContainer m_iIngredientContents;

	private int m_IngredientCapacity;

	private Color m_uncookedSurfaceColour = Color.white;

	private Color m_uncookedBubbleColour = Color.white;

	private static int m_iCooking = Animator.StringToHash("Cooking");

	private static int m_iProgress = Animator.StringToHash("Progress");

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_contentsCosmeticDecisions = (CakeTinContentsCosmeticDecisions)synchronisedObject;
		m_iIngredientContents = m_contentsCosmeticDecisions.m_gameObject.RequireComponent<ClientIngredientContainer>();
		m_iIngredientContents.RegisterContentsChangedCallback(OnContentChanged);
		m_IngredientCapacity = m_iIngredientContents.gameObject.RequireComponent<IngredientContainer>().m_capacity;
		m_contentsCosmeticDecisions.m_contentsObject.SetActive(false);
	}

	public void OnCookingStarted()
	{
		m_contentsCosmeticDecisions.m_animator.SetBool(m_iCooking, true);
	}

	public void OnCookingFinished()
	{
		m_contentsCosmeticDecisions.m_animator.SetBool(m_iCooking, false);
	}

	public void OnCookingPropChanged(float newProp)
	{
		m_contentsCosmeticDecisions.m_animator.SetFloat(m_iProgress, newProp);
		if (newProp > 1f && newProp < 2f)
		{
			float t = newProp - 1f;
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
		float y = MathUtils.ClampedRemap(_contents.Length, 0f, m_IngredientCapacity, m_contentsCosmeticDecisions.m_contentsYPositionWhenEmpty, m_contentsCosmeticDecisions.m_contentsYPositionWhenFull);
		m_contentsCosmeticDecisions.m_contentsObject.transform.localPosition = m_contentsCosmeticDecisions.m_contentsObject.transform.localPosition.WithY(y);
	}

	public void SetRendererColourRecursive(GameObject _object, Color _surfaceColour, Color _bubbleColour)
	{
		Renderer component = _object.GetComponent<Renderer>();
		if ((bool)component)
		{
			if (component.material.name == m_contentsCosmeticDecisions.m_surfaceMaterialName + " (Instance)")
			{
				component.material.color = _surfaceColour;
			}
			else if (component.material.name == m_contentsCosmeticDecisions.m_bubbleMaterialName + " (Instance)")
			{
				component.material.color = _bubbleColour;
			}
		}
		for (int i = 0; i < _object.transform.childCount; i++)
		{
			SetRendererColourRecursive(_object.transform.GetChild(i).gameObject, _surfaceColour, _bubbleColour);
		}
	}

	private Color GetNewColour(AssembledDefinitionNode[] _contents, bool bubble)
	{
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		int num4 = 0;
		for (int i = 0; i < _contents.Length; i++)
		{
			MixedCompositeAssembledNode mixedCompositeAssembledNode = _contents[i] as MixedCompositeAssembledNode;
			for (int j = 0; j < mixedCompositeAssembledNode.m_composition.Length; j++)
			{
				IngredientAssembledNode ingredientAssembledNode = mixedCompositeAssembledNode.m_composition[j] as IngredientAssembledNode;
				if (ingredientAssembledNode != null)
				{
					num += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.r;
					num2 += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.g;
					num3 += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.b;
					num4++;
				}
			}
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
