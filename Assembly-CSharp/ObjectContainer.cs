using UnityEngine;

public class ObjectContainer : MonoBehaviour, IParentable
{
	public Transform GetAttachPoint(GameObject gameObject)
	{
		return base.transform;
	}

	public bool HasClientSidePrediction()
	{
		return false;
	}
}
