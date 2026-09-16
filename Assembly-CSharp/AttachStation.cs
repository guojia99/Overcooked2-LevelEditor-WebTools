using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/AttachStation")]
[RequireComponent(typeof(Collider))]
public class AttachStation : MonoBehaviour, IParentable
{
	[SerializeField]
	public Transform m_attachPoint;

	[SerializeField]
	public int m_pickupPriority;

	[SerializeField]
	public int m_placementPriority;

	[SerializeField]
	public int m_catchingPriority;

	[SerializeField]
	public bool m_canCatch = true;

	private bool m_bClientSidePrediction;

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_attachPoint;
	}

	public void SetClientSidePredictionEnabled(bool bEnabled)
	{
		m_bClientSidePrediction = bEnabled;
	}

	public bool HasClientSidePrediction()
	{
		return m_bClientSidePrediction;
	}

	public PlacementType CalculatePlacementType<T>(GameObject _item, PlacementContext _context, ICarrier _iCarrier, Vector2 _directionXZ, IBaseHandlePlacement placementHandler, List<Generic<bool, GameObject, PlacementContext>> _placementQuery, ICarrier _holder) where T : class, IBaseHandlePlacement
	{
		if (_item == null)
		{
			if (CanAttachToSelf(_item, _iCarrier.InspectCarriedItem(), _context, _placementQuery))
			{
				return PlacementType.OntoEmpty;
			}
			if (CanAttachContents(_item, _iCarrier.InspectCarriedItem(), _context, _placementQuery))
			{
				return PlacementType.ContentsOntoEmpty;
			}
			return PlacementType.NotValid;
		}
		if (placementHandler != null)
		{
			if (placementHandler.CanHandlePlacement(_iCarrier, _directionXZ, _context))
			{
				if (CanPlaceUnder<T>(_item, _iCarrier, _directionXZ, _holder, _context, _placementQuery))
				{
					return PlacementType.OntoAndUnderOccupant;
				}
				return PlacementType.OntoOccupant;
			}
			return PlacementType.NotValid;
		}
		if (CanPlaceUnder<T>(_item, _iCarrier, _directionXZ, _holder, _context, _placementQuery))
		{
			return PlacementType.UnderOccupant;
		}
		return PlacementType.NotValid;
	}

	public bool CanPlaceUnder<T>(GameObject _item, ICarrier _iCarrier, Vector2 _directionXZ, ICarrier _holder, PlacementContext _context, List<Generic<bool, GameObject, PlacementContext>> _placementQuery) where T : class, IBaseHandlePlacement
	{
		GameObject gameObject = _iCarrier.InspectCarriedItem();
		T val = gameObject.RequestInterface<T>();
		if (val != null && val.CanHandlePlacement(_holder, _directionXZ, _context) && CouldAttachToSelfIfEmpty(gameObject, _context, _placementQuery))
		{
			return _item.RequestInterface<IPlaceUnder>() != null;
		}
		return false;
	}

	public bool CanAttachToSelf(GameObject _item, GameObject _carriedItem, PlacementContext _context, List<Generic<bool, GameObject, PlacementContext>> _placementQuery)
	{
		return _item == null && CouldAttachToSelfIfEmpty(_carriedItem, _context, _placementQuery);
	}

	public bool CouldAttachToSelfIfEmpty(GameObject _item, PlacementContext _context, List<Generic<bool, GameObject, PlacementContext>> _placementQuery)
	{
		if (_item.RequestInterface<IAttachment>() != null)
		{
			return !_placementQuery.CallForResult(false, _item, _context);
		}
		return false;
	}

	public bool CanAttachContents(GameObject _item, GameObject _itemContainer, PlacementContext _context, List<Generic<bool, GameObject, PlacementContext>> _placementQuery)
	{
		if (_item == null)
		{
			IIngredientContents component = _itemContainer.GetComponent<IIngredientContents>();
			if (component != null && component.GetContentsCount() == 1)
			{
				AssembledDefinitionNode assembledDefinitionNode = component.GetContents()[0];
				if (assembledDefinitionNode.m_freeObject != null)
				{
					return CouldAttachToSelfIfEmpty(assembledDefinitionNode.m_freeObject, _context, _placementQuery);
				}
			}
		}
		return false;
	}
}
