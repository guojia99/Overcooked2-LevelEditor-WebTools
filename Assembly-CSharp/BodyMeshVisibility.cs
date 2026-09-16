using UnityEngine;

[ExecutionDependency(typeof(ChefMeshReplacer))]
[AddComponentMenu("Scripts/Game/Player/BodyMeshVisibility")]
public class BodyMeshVisibility : MeshVisibilityBase<BodyMeshVisibility.VisState>
{
	public enum VisState
	{
		Hidden = 0,
		Visible = 1
	}

	[SerializeField]
	public VisState m_initialVisState = VisState.Visible;

	private void Awake()
	{
		if (m_stateFlags.Length == 2)
		{
			m_stateFlags[1] = (int)Mathf.Pow(2f, m_meshes.Length) - 1;
		}
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
