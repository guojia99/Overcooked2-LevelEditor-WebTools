using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ManualAnimation
{
	public AnimationCurve m_ScaleCurve;

	public AnimationCurve m_PositionCurve;

	public bool m_bUseStartPosition;

	[HideInInspectorTest("m_bUseStartPosition", true)]
	public Vector3 m_StartPositionValue = default(Vector3);

	public Vector3 m_TargetPositionValue = default(Vector3);

	public bool m_bUseStartScale;

	[HideInInspectorTest("m_bUseStartPosition", true)]
	public Vector3 m_StartScaleValue = default(Vector3);

	public Vector3 m_TargetScaleValue = default(Vector3);

	public float m_CurveTime = 1f;

	public IEnumerator Run(GameObject obj, Transform attachPoint, Vector3 offset)
	{
		float time = 0f;
		float length = m_CurveTime;
		float endTime = time + length;
		Vector3 startingScale = obj.transform.localScale;
		if (m_bUseStartScale)
		{
			startingScale = m_StartScaleValue;
		}
		Vector3 startingPosition = obj.transform.position;
		if (m_bUseStartPosition)
		{
			startingPosition = attachPoint.rotation * m_StartPositionValue + attachPoint.position;
		}
		Vector3 targetScale = m_TargetScaleValue;
		Vector3 targetPosition = attachPoint.rotation * m_TargetPositionValue + attachPoint.position + offset;
		while (time < endTime)
		{
			float position = 1f - (endTime - time) / length;
			obj.transform.localScale = Vector3.Lerp(startingScale, targetScale, m_ScaleCurve.Evaluate(position));
			obj.transform.position = Vector3.Lerp(startingPosition, targetPosition, m_PositionCurve.Evaluate(position));
			time += TimeManager.GetDeltaTime(obj);
			yield return null;
		}
		obj.transform.localScale = targetScale;
		obj.transform.position = targetPosition;
	}
}
