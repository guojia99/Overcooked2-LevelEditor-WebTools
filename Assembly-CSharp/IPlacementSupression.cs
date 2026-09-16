using UnityEngine;

public interface IPlacementSupression
{
	void RegisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback);

	void UnregisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback);
}
