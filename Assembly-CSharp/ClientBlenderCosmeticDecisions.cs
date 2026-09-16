using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientBlenderCosmeticDecisions : ClientSynchroniserBase, IClientMixingNotifed
{
	private BlenderCosmeticDecisions m_blenderCosmeticDecisions;

	private ClientIngredientContainer m_iIngredientContents;

	private Color m_colour = Color.white;

	private bool m_validRecipe = true;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_blenderCosmeticDecisions = (BlenderCosmeticDecisions)synchronisedObject;
		if (m_blenderCosmeticDecisions.m_animator == null)
		{
		}
		m_iIngredientContents = base.gameObject.RequireComponent<ClientIngredientContainer>();
		m_iIngredientContents.RegisterContentsChangedCallback(OnContentChanged);
		m_blenderCosmeticDecisions.m_contentsObject.SetActive(false);
	}

	public void OnMixingStarted()
	{
		if (m_blenderCosmeticDecisions.m_animator.IsActive() && m_blenderCosmeticDecisions.m_hasOnParam)
		{
			m_blenderCosmeticDecisions.m_animator.SetBool(ContentsCosmeticDecisions.c_onParam, true);
		}
	}

	public void OnMixingFinished()
	{
		if (m_blenderCosmeticDecisions.m_animator.IsActive() && m_blenderCosmeticDecisions.m_hasOnParam)
		{
			m_blenderCosmeticDecisions.m_animator.SetBool(ContentsCosmeticDecisions.c_onParam, false);
		}
	}

	public void OnMixingPropChanged(float newProp)
	{
		if (m_blenderCosmeticDecisions.m_animator.IsActive() && m_blenderCosmeticDecisions.m_hasFillParam)
		{
			m_blenderCosmeticDecisions.m_animator.SetInteger(BlenderCosmeticDecisions.c_FillParam, m_iIngredientContents.GetContentsCount());
		}
		if (m_blenderCosmeticDecisions.m_prefabLookup != null)
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
			m_validRecipe = m_blenderCosmeticDecisions.m_prefabLookup.GetPrefabForNode(mixedCompositeAssembledNode) != null;
		}
		if (newProp > 1f)
		{
			float t = Mathf.Min(2f, newProp) - 1f;
			Color b = Color.Lerp(m_colour, Color.black, 0.8f);
			Color surfaceColour = Color.Lerp(m_colour, b, t);
			SetRendererColourRecursive(m_blenderCosmeticDecisions.m_contentsObject, surfaceColour);
		}
	}

	private void OnContentChanged(AssembledDefinitionNode[] _contents)
	{
		if (m_blenderCosmeticDecisions.m_animator.IsActive() && m_blenderCosmeticDecisions.m_hasFillParam)
		{
			m_blenderCosmeticDecisions.m_animator.SetInteger(BlenderCosmeticDecisions.c_FillParam, m_iIngredientContents.GetContentsCount());
		}
		m_colour = GetNewColour(_contents, false);
		SetRendererColourRecursive(m_blenderCosmeticDecisions.m_contentsObject, m_colour);
		if (_contents.Length == 0)
		{
			m_blenderCosmeticDecisions.m_contentsObject.SetActive(false);
			return;
		}
		m_blenderCosmeticDecisions.m_contentsObject.SetActive(true);
		if (m_blenderCosmeticDecisions.m_hasFillParam)
		{
			m_blenderCosmeticDecisions.m_animator.SetInteger(BlenderCosmeticDecisions.c_FillParam, _contents.Length);
		}
	}

	public void SetRendererColourRecursive(GameObject _object, Color _surfaceColour)
	{
		Renderer component = _object.GetComponent<Renderer>();
		if ((bool)component && component.material.name == m_blenderCosmeticDecisions.m_surfaceMaterialName + " (Instance)")
		{
			component.material.SetColor("_MaskColor", _surfaceColour);
		}
		for (int i = 0; i < _object.transform.childCount; i++)
		{
			SetRendererColourRecursive(_object.transform.GetChild(i).gameObject, _surfaceColour);
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
			IngredientAssembledNode ingredientAssembledNode = _contents[i] as IngredientAssembledNode;
			if (ingredientAssembledNode != null)
			{
				num += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.r;
				num2 += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.g;
				num3 += ingredientAssembledNode.m_ingriedientOrderNode.m_colour.b;
				num4++;
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
