using UnityEngine;

[ExecutionDependency(typeof(ChefMeshReplacer))]
[AddComponentMenu("Scripts/Game/Player/HatMeshVisibility")]
public class HatMeshVisibility : MeshVisibilityBase<HatMeshVisibility.VisState>
{
	public enum VisState
	{
		None = 0,
		Cap = 1,
		Tall = 2,
		Fancy = 3,
		Festive = 4,
		Baseball = 5
	}

	[SerializeField]
	public VisState m_initialVisState = VisState.Fancy;
}
