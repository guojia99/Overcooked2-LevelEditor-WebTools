using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlacementItemSwitcher : ClientSynchroniserBase
{
	private PlacementItemSwitcher m_placementItemSwitcher;

	private IngredientPropertiesComponent m_ingredientPropertiesComponent;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_placementItemSwitcher = (PlacementItemSwitcher)synchronisedObject;
		m_ingredientPropertiesComponent = m_placementItemSwitcher.GetComponent<IngredientPropertiesComponent>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		PickupItemSwitcherMessage pickupItemSwitcherMessage = (PickupItemSwitcherMessage)serialisable;
		m_ingredientPropertiesComponent.SetIngredientOrderNode(m_placementItemSwitcher.m_ingredients[pickupItemSwitcherMessage.m_itemIndex]);
		base.gameObject.SendMessage("OnItemSwitched", SendMessageOptions.DontRequireReceiver);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PickupItemSwitcher;
	}
}
