using UnityEngine;

public class StaticLandscapeParenting : MonoBehaviour
{
	[SerializeField]
	private LayerMask m_landscapeMask;

	private void Start()
	{
		Vector3 position = base.transform.position;
		RaycastHit hitInfo;
		if (Physics.Raycast(new Ray(position + Vector3.up, -Vector3.up), out hitInfo, 2f, m_landscapeMask))
		{
			GameObject gameObject = hitInfo.collider.gameObject;
			if (gameObject.transform != base.transform.parent)
			{
				AttachToGround(gameObject.transform);
			}
		}
		Object.Destroy(this);
	}

	private void AttachToGround(Transform _target)
	{
		IParentable parentable = _target.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
		if (parentable != null)
		{
			Transform attachPoint = parentable.GetAttachPoint(_target.gameObject);
			if (base.transform.parent != attachPoint)
			{
				base.transform.SetParent(attachPoint, true);
			}
		}
		else
		{
			base.transform.SetParent(null);
		}
	}
}
