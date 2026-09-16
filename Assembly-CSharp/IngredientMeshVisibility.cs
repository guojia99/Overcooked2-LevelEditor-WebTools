using UnityEngine;

[AddComponentMenu("Scripts/Game/Ingredients/IngredientMeshVisibility")]
public class IngredientMeshVisibility : MeshVisibilityBase<IngredientMeshVisibility.VisState>
{
	public enum VisState
	{
		Whole = 0,
		Working = 1
	}
}
