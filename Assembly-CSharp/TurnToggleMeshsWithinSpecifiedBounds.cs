using UnityEngine;

public class TurnToggleMeshsWithinSpecifiedBounds : MonoBehaviour
{
	[SerializeField]
	private GameObject[] objectsToHandle;

	[SerializeField]
	private Transform boxBottomLeftWorldSpace;

	[SerializeField]
	private Transform boxTopRightWorldSpace;

	[SerializeField]
	private bool checkXBounds;

	[SerializeField]
	private bool checkYBounds;

	[SerializeField]
	private bool checkZBounds;

	private Renderer[][] cachedRenderers;

	private void Start()
	{
		cachedRenderers = new Renderer[objectsToHandle.Length][];
		int num = objectsToHandle.Length;
		for (int i = 0; i < num; i++)
		{
			cachedRenderers[i] = objectsToHandle[i].GetComponentsInChildren<Renderer>();
		}
	}

	private void Update()
	{
		for (int i = 0; i < objectsToHandle.Length; i++)
		{
			GameObject gameObject = objectsToHandle[i];
			if (!(gameObject != null))
			{
				continue;
			}
			Transform transform = gameObject.transform;
			if (transform != null)
			{
				if (IsWithinXRegion(transform) && IsWithinYRegion(transform) && IsWithinZRegion(transform))
				{
					SetRenderersToState(cachedRenderers[i], true);
				}
				else
				{
					SetRenderersToState(cachedRenderers[i], false);
				}
			}
		}
	}

	private bool IsWithinXRegion(Transform targetTransform)
	{
		if (!checkXBounds || IsValueInRangeInclusive(targetTransform.position.x, boxBottomLeftWorldSpace.position.x, boxTopRightWorldSpace.position.x))
		{
			return true;
		}
		return false;
	}

	private bool IsWithinYRegion(Transform targetTransform)
	{
		if (!checkYBounds || IsValueInRangeInclusive(targetTransform.position.y, boxBottomLeftWorldSpace.position.y, boxTopRightWorldSpace.position.y))
		{
			return true;
		}
		return false;
	}

	private bool IsWithinZRegion(Transform targetTransform)
	{
		if (!checkZBounds || IsValueInRangeInclusive(targetTransform.position.z, boxBottomLeftWorldSpace.position.z, boxTopRightWorldSpace.position.z))
		{
			return true;
		}
		return false;
	}

	private void SetRenderersToState(Renderer[] targetRenderers, bool state)
	{
		int num = targetRenderers.Length;
		for (int i = 0; i < num; i++)
		{
			targetRenderers[i].enabled = state;
		}
	}

	private bool IsValueInRangeInclusive(float value, float min, float max)
	{
		return (value >= min && value <= max) ? true : false;
	}
}
