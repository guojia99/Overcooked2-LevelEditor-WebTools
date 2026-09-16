using System.Collections;
using UnityEngine;

public class ClientDirtyPlateStack : ClientPlateStackBase, IClientHandlePlacement, IBaseHandlePlacement
{
	private ClientAnticipateInteractionHighlight m_highlight;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		DirtyPlateStack dirtyPlateStack = (DirtyPlateStack)synchronisedObject;
		if (dirtyPlateStack.m_washedPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, dirtyPlateStack.m_washedPrefab);
		}
		if (dirtyPlateStack.m_cleanPlatePrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, dirtyPlateStack.m_cleanPlatePrefab);
		}
		m_highlight = base.gameObject.RequireComponent<ClientAnticipateInteractionHighlight>();
	}

	protected override void PlateSpawned(GameObject _object)
	{
		m_stack.AddToStack(_object);
		StartCoroutine(DelayUpdateHighlight());
		NotifyPlateAdded(_object);
	}

	protected override void PlateRemoved()
	{
		GameObject plate = m_stack.RemoveFromStack();
		StartCoroutine(DelayUpdateHighlight());
		NotifyPlateRemoved(plate);
	}

	private IEnumerator DelayUpdateHighlight()
	{
		yield return null;
		m_highlight.RebuildHighlightMaterials();
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject obj = _carrier.InspectCarriedItem();
		DirtyPlateStack dirtyPlateStack = obj.RequestComponent<DirtyPlateStack>();
		if (dirtyPlateStack != null)
		{
			DirtyPlateStack dirtyPlateStack2 = (DirtyPlateStack)m_plateStack;
			return dirtyPlateStack.m_plateType == dirtyPlateStack2.m_plateType;
		}
		return false;
	}

	public int GetPlacementPriority()
	{
		return 0;
	}
}
