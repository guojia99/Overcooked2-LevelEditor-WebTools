using UnityEngine;

public class ServerTrayPlacementContainer : ServerPlacementContainer
{
	private ServerTray m_tray;

	private TrayPlacementContainer m_trayPlacementContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_trayPlacementContainer = (TrayPlacementContainer)synchronisedObject;
		m_tray = base.gameObject.RequireComponent<ServerTray>();
	}

	protected override bool CanCombine(GameObject _placingObject, PlacementContext _context)
	{
		if (!m_ingredientContainer.HasContents() && _placingObject.RequestComponent<ServerTray>() != null)
		{
			return true;
		}
		IIngredientContents ingredientContents = m_tray.GetIngredientContents(_placingObject, _context, true);
		if (ingredientContents == null)
		{
			return false;
		}
		return m_trayPlacementContainer.CanCombine(_placingObject, m_allowPlacementCallbacks, ingredientContents, _context);
	}

	public override int GetPlacementPriority()
	{
		return 1;
	}

	public override void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		if (!m_ingredientContainer.HasContents() && gameObject.RequestComponent<ServerTray>() != null)
		{
			ServerIngredientContainer serverIngredientContainer = gameObject.RequireComponent<ServerIngredientContainer>();
			for (int i = 0; i < serverIngredientContainer.GetContentsCount(); i++)
			{
				m_ingredientContainer.AddIngredient(serverIngredientContainer.GetContentsElement(i));
			}
			serverIngredientContainer.Empty();
			return;
		}
		if (CanCombine(gameObject, _context))
		{
			IContainerTransferBehaviour containerTransferBehaviour = gameObject.RequireInterface<IContainerTransferBehaviour>();
			IIngredientContents ingredientContents = m_tray.GetIngredientContents(gameObject, _context, true);
			if (ingredientContents != null)
			{
				containerTransferBehaviour.TransferToContainer(_carrier, ingredientContents, false);
			}
			return;
		}
		ServerIngredientContainer serverIngredientContainer2 = gameObject.RequireComponent<ServerIngredientContainer>();
		IContainerTransferBehaviour containerTransferBehaviour2 = base.gameObject.RequireInterface<IContainerTransferBehaviour>();
		if (containerTransferBehaviour2.CanTransferToContainer(serverIngredientContainer2))
		{
			containerTransferBehaviour2.TransferToContainer(null, serverIngredientContainer2, false);
			serverIngredientContainer2.InformOfInternalChange();
		}
	}
}
