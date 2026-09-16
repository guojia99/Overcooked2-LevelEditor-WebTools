using UnityEngine;

public class EmptyLerp : MonoBehaviour, Lerp
{
	public virtual void StartSynchronising(Component synchronisedObject)
	{
	}

	public virtual void Reset()
	{
	}

	public virtual void Reparented()
	{
	}

	public virtual void ReceiveServerUpdate(Vector3 localPosition, Quaternion localRotation)
	{
	}

	public virtual void ReceiveServerEvent(Vector3 localPosition, Quaternion localRotation)
	{
	}

	public virtual void UpdateLerp(float delta)
	{
	}
}
