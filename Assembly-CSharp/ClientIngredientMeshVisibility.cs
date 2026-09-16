using UnityEngine;

public class ClientIngredientMeshVisibility : ClientMeshVisibilityBase<IngredientMeshVisibility.VisState>
{
	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		Setup(IngredientMeshVisibility.VisState.Whole);
	}

	public void SetVisState(IngredientMeshVisibility.VisState _visState)
	{
		SetState(_visState);
	}
}
