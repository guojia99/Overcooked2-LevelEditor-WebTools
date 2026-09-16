using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
[RequireComponent(typeof(GroundCast))]
public class DynamicLandscapeParenting : MonoBehaviour
{
	private bool m_bEnabled = true;

	public void SetEnabled(bool bEnabled)
	{
		m_bEnabled = bEnabled;
	}

	private void Awake()
	{
		if (GameUtils.GetLevelConfig().m_disableDynamicParenting)
		{
			Object.Destroy(this);
			return;
		}
		GroundCast component = GetComponent<GroundCast>();
		if ((bool)component)
		{
			OnGroundChange(component.GetGroundCollider());
		}
	}

	public void OnGroundChange(Collider _collider)
	{
		if (!base.enabled || !m_bEnabled)
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
