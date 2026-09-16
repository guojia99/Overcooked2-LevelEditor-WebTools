using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BoundsMultiplier : MonoBehaviour
{
	[SerializeField]
	private Vector3 m_multiplier = Vector3.one;

	private void Awake()
	{
		MeshFilter component = base.gameObject.GetComponent<MeshFilter>();
		Bounds bounds = component.mesh.bounds;
		component.mesh.bounds = new Bounds(bounds.center, bounds.size.MultipliedBy(m_multiplier));
	}
}
