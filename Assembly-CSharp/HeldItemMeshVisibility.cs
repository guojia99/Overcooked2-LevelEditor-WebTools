using UnityEngine;

[AddComponentMenu("Scripts/Game/Player/HeldItemMeshVisibility")]
public class HeldItemMeshVisibility : MeshVisibilityBase<HeldItemMeshVisibility.VisState>
{
	public enum VisState
	{
		Carrying = 0,
		NotCarrying = 1
	}
}
