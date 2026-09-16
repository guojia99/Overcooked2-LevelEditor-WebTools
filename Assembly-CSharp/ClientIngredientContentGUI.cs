using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientIngredientContentGUI : ClientSynchroniserBase
{
	private IngredientContentGUI m_ingredientContentGUI;

	private IClientOrderDefinition[] m_iOrderDefinitions;

	private IngredientContentsUIContainer m_uiInstance;

	private AssembledDefinitionNode m_mostRecentChange;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_ingredientContentGUI = (IngredientContentGUI)synchronisedObject;
		m_iOrderDefinitions = base.gameObject.RequestInterfaces<IClientOrderDefinition>();
		for (int i = 0; i < m_iOrderDefinitions.Length; i++)
		{
			m_iOrderDefinitions[i].RegisterOrderCompositionChangedCallback(OnOrderChanged);
			OnOrderChanged(m_iOrderDefinitions[i].GetOrderComposition());
		}
		if (m_iOrderDefinitions.Length != 0)
		{
		}
	}

	private void OnOrderChanged(AssembledDefinitionNode _orderDefinition)
	{
		m_mostRecentChange = _orderDefinition;
		if (!base.enabled || !base.gameObject.activeInHierarchy)
		{
			return;
		}
		if (_orderDefinition.Simpilfy() != AssembledDefinitionNode.NullNode || m_ingredientContentGUI.m_displayEmptyElements)
		{
			if (m_uiInstance == null)
			{
				GameObject obj = GameUtils.InstantiateHoverIconUIController(m_ingredientContentGUI.m_ingredientContentUIPrefab.gameObject, NetworkUtils.FindVisualRoot(base.gameObject), "HoverIconCanvas", m_ingredientContentGUI.m_Offset);
				m_uiInstance = obj.RequireComponent<IngredientContentsUIContainer>();
			}
			else
			{
				m_uiInstance.gameObject.SetActive(true);
			}
			if (m_ingredientContentGUI.m_displayEmptyElements)
			{
				IngredientContainer ingredientContainer = base.gameObject.RequestComponent<IngredientContainer>();
				if (ingredientContainer != null)
				{
					int minimumSize = ingredientContainer.GetCapacity();
					if (_orderDefinition.Simpilfy() == AssembledDefinitionNode.NullNode)
					{
						minimumSize = 1;
					}
					m_uiInstance.SetMinimumSize(minimumSize);
				}
			}
		}
		else if (m_uiInstance != null)
		{
			m_uiInstance.gameObject.SetActive(false);
		}
		if (m_uiInstance != null)
		{
			m_uiInstance.SetOrder(_orderDefinition);
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_iOrderDefinitions == null)
		{
			return;
		}
		if (m_mostRecentChange != null)
		{
			OnOrderChanged(m_mostRecentChange);
			return;
		}
		for (int i = 0; i < m_iOrderDefinitions.Length; i++)
		{
			OnOrderChanged(m_iOrderDefinitions[i].GetOrderComposition());
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_uiInstance != null)
		{
			m_uiInstance.gameObject.SetActive(false);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_uiInstance != null)
		{
			Object.Destroy(m_uiInstance.gameObject);
			m_uiInstance = null;
		}
	}
}
