using UnityEngine;

public interface Lerp
{
	void StartSynchronising(Component synchronisedObject);

	void Reset();

	void Reparented();

	void ReceiveServerUpdate(Vector3 localPosition, Quaternion localRotation);

	void ReceiveServerEvent(Vector3 localPosition, Quaternion localRotation);

	void UpdateLerp(float delta);
}
