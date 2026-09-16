using UnityEngine;

[ExecutionDependency(typeof(ChefMeshReplacer))]
[AddComponentMenu("Scripts/Game/Player/HeldItemsMeshVisibility")]
public class HeldItemsMeshVisibility : MeshVisibilityBase<HeldItemsMeshVisibility.VisState>
{
	public enum VisState
	{
		Chopping = 0,
		Carrying = 1,
		Idle = 2,
		Washing = 3,
		Repairing = 4
	}

	protected override Renderer FindMesh(string _name, Renderer[] renderers = null)
	{
		if (renderers == null)
		{
			renderers = base.gameObject.RequestComponentsRecursive<Renderer>();
		}
		foreach (Renderer renderer in renderers)
		{
			if (renderer.gameObject.activeInHierarchy && renderer.name.StartsWith(_name))
			{
				return renderer;
			}
		}
		return null;
	}
}
