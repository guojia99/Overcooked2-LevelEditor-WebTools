using UnityEngine;

public interface IMovingSurface
{
	Vector3 CalculateVelocityAtPoint(Vector3 _point, IMovingSurface _prevSurface);

	Quaternion CalculateRotationAtPoint(Vector3 _point, Quaternion _prevRotation);
}
