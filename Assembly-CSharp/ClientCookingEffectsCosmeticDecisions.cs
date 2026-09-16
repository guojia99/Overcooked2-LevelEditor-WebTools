using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCookingEffectsCosmeticDecisions : ClientSynchroniserBase, IClientCookingNotifed
{
	private CookingEffectsCosmeticDecisions m_cookingEffectsCosmeticDecisions;

	private ClientFlammable m_flammable;

	private float m_cookingProp;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cookingEffectsCosmeticDecisions = (CookingEffectsCosmeticDecisions)synchronisedObject;
		ClientIngredientContainer clientIngredientContainer = base.gameObject.RequestInterface<ClientIngredientContainer>();
		if ((bool)clientIngredientContainer)
		{
			clientIngredientContainer.RegisterContentsChangedCallback(OnContentChanged);
		}
		IClientAttachment clientAttachment = base.gameObject.RequestInterface<IClientAttachment>();
		if (clientAttachment != null)
		{
			clientAttachment.RegisterAttachChangedCallback(OnAttachmentChanged);
		}
	}

	public void OnCookingStarted()
	{
		if (m_cookingEffectsCosmeticDecisions.m_animator.IsActive() && m_cookingEffectsCosmeticDecisions.m_hasOnParam)
		{
			m_cookingEffectsCosmeticDecisions.m_animator.SetBool(CookingEffectsCosmeticDecisions.c_onParam, true);
		}
	}

	public void OnCookingFinished()
	{
		if (m_cookingEffectsCosmeticDecisions.m_animator.IsActive() && m_cookingEffectsCosmeticDecisions.m_hasOnParam)
		{
			m_cookingEffectsCosmeticDecisions.m_animator.SetBool(CookingEffectsCosmeticDecisions.c_onParam, false);
		}
		if (m_cookingProp < 1f)
		{
			m_cookingEffectsCosmeticDecisions.SetColor(Color.white);
			m_cookingEffectsCosmeticDecisions.SetEmissionRate(0f);
		}
	}

	public void OnCookingPropChanged(float _newProp)
	{
		m_cookingProp = _newProp;
		if (m_flammable == null || !m_flammable.OnFire())
		{
			if (_newProp < 1f)
			{
				m_cookingEffectsCosmeticDecisions.SetColor(Color.white);
				m_cookingEffectsCosmeticDecisions.SetEmissionRate(0f);
			}
			else if (_newProp >= 1f && _newProp < 2f)
			{
				m_cookingEffectsCosmeticDecisions.SetEmissionRate(20f);
			}
			else if (_newProp >= 2f)
			{
				m_cookingEffectsCosmeticDecisions.SetColor(Color.black);
				m_cookingEffectsCosmeticDecisions.SetEmissionRate(15f);
			}
		}
		else
		{
			m_cookingEffectsCosmeticDecisions.SetEmissionRate(0f);
		}
		m_cookingProp = _newProp;
	}

	private void OnIgnitionChange(bool _state)
	{
		OnCookingPropChanged(m_cookingProp);
	}

	private void OnAttachmentChanged(IParentable _parentable)
	{
		if ((bool)m_flammable)
		{
			m_flammable.UnregisterIgnitionCallback(OnIgnitionChange);
		}
		m_flammable = null;
		if (_parentable != null)
		{
			m_flammable = _parentable.GetAttachPoint(base.gameObject).parent.gameObject.GetComponent<ClientFlammable>();
		}
		if ((bool)m_flammable)
		{
			m_flammable.RegisterIgnitionCallback(OnIgnitionChange);
		}
		OnCookingPropChanged(m_cookingProp);
	}

	private void OnContentChanged(AssembledDefinitionNode[] _contents)
	{
		if (_contents.Length == 0)
		{
			if (m_cookingEffectsCosmeticDecisions.m_animator.IsActive() && m_cookingEffectsCosmeticDecisions.m_hasCookingParam)
			{
				m_cookingEffectsCosmeticDecisions.m_animator.SetBool(CookingEffectsCosmeticDecisions.c_cookingParam, false);
			}
			m_cookingEffectsCosmeticDecisions.SetEmissionRate(0f);
		}
		else if (m_cookingEffectsCosmeticDecisions.m_animator.IsActive() && m_cookingEffectsCosmeticDecisions.m_hasCookingParam)
		{
			m_cookingEffectsCosmeticDecisions.m_animator.SetBool(CookingEffectsCosmeticDecisions.c_cookingParam, true);
		}
	}
}
