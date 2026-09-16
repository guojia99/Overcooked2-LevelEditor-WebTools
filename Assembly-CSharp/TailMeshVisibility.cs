using UnityEngine;

[ExecutionDependency(typeof(ChefMeshReplacer))]
[AddComponentMenu("Scripts/Game/Player/TailMeshVisibility")]
public class TailMeshVisibility : MeshVisibilityBase<TailMeshVisibility.VisState>
{
	public enum VisState
	{
		Hidden = 0,
		Visible = 1
	}

	[SerializeField]
	public VisState m_initialVisState = VisState.Visible;

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
