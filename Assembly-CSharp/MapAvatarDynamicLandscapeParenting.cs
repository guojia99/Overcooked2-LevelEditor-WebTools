using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
[RequireComponent(typeof(MapAvatarGroundCast))]
public class MapAvatarDynamicLandscapeParenting : MonoBehaviour
{
	private void Awake()
	{
		MapAvatarGroundCast component = GetComponent<MapAvatarGroundCast>();
		if ((bool)component)
		{
			OnGroundChange(component.GetGroundCollider());
		}
	}

	public void OnGroundChange(Collider _collider)
	{
		if (!base.enabled)
		{
			return;
		}
		if (_collider != null)
		{
			IParentable parentable = _collider.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
			if (parentable != null)
			{
				Transform attachPoint = parentable.GetAttachPoint(base.gameObject);
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
		else
		{
			base.transform.SetParent(null);
		}
	}
}
