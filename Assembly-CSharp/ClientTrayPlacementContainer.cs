using UnityEngine;

public class ClientTrayPlacementContainer : ClientPlacementContainer
{
	private ClientTray m_tray;

	private TrayPlacementContainer m_placementContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_placementContainer = (TrayPlacementContainer)synchronisedObject;
		m_tray = base.gameObject.RequireComponent<ClientTray>();
	}

	protected override bool CanCombine(GameObject _placingObject, PlacementContext _context)
	{
		IIngredientContents ingredientContents = m_tray.GetIngredientContents(_placingObject, _context, false);
		if (ingredientContents != null)
		{
			return m_placementContainer.CanCombine(_placingObject, m_allowPlacementCallback, ingredientContents, _context);
		}
		return false;
	}

	public override int GetPlacementPriority()
	{
		return 1;
	}
}
