using UnityEngine;

public interface IParentable
{
	Transform GetAttachPoint(GameObject gameObject);

	bool HasClientSidePrediction();
}
