using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ProjectileAnimation
{
	public AnimationCurve m_RotationCurve;

	public AnimationCurve m_HeightCurve;

	public bool m_bUseStartRotation;

	[HideInInspectorTest("m_bUseStartRotation", true)]
	public Vector3 m_StartRotationValue = default(Vector3);

	public Vector3 m_TargetRotationValue = default(Vector3);

	public float m_MaxHeightValue = 30f;

	public float m_CurveTime = 1f;

	public IEnumerator Run(GameObject obj, Transform target)
	{
		float time = 0f;
		float length = m_CurveTime;
		float endTime = time + length;
		Quaternion startingRotation = obj.transform.rotation;
		if (m_bUseStartRotation)
		{
			startingRotation = Quaternion.Euler(m_StartRotationValue);
		}
		Vector3 targetPosition = target.position;
		Quaternion targetRotation = Quaternion.Euler(target.eulerAngles + m_TargetRotationValue);
		Vector3 startingPosition = obj.transform.position;
		float startingHeight = obj.transform.position.y;
		float maxHeight = startingHeight + m_MaxHeightValue;
		while (time < endTime && !(obj == null))
		{
			float position = 1f - (endTime - time) / length;
			obj.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, m_RotationCurve.Evaluate(position));
			Vector3 newPosition = Vector3.Lerp(startingPosition, targetPosition, position);
			if (position < 0.5f)
			{
				newPosition.y = Mathf.Lerp(startingHeight, maxHeight, m_HeightCurve.Evaluate(position));
			}
			else
			{
				newPosition.y = Mathf.Lerp(targetPosition.y, maxHeight, m_HeightCurve.Evaluate(position));
			}
			obj.transform.position = newPosition;
			time += TimeManager.GetDeltaTime(obj);
			yield return null;
		}
		if (obj != null)
		{
			obj.transform.rotation = targetRotation;
			obj.transform.position = targetPosition;
		}
	}
}
