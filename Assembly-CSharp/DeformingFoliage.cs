using System.Collections.Generic;
using UnityEngine;

public class DeformingFoliage : MonoBehaviour
{
	public float WobbleTime = 1f;

	public float MaxWobbleAmount = 0.3f;

	public float MinWobbleAmount = 0.1f;

	private List<Material> m_materials = new List<Material>();

	private Transform m_deforemer;

	private Vector4 m_push = new Vector4(0f, 1f, 0f, 0f);

	private float m_wobbleAmount;

	private float m_randomXTiming;

	private float m_randomZTiming;

	private float m_wobbleTimer;

	private const float c_minWobbleTiming = 300f;

	private const float c_maxWobbleTiming = 400f;

	public void AddDeformer(Transform _deformer)
	{
		if (m_deforemer != _deformer && CalculatePushMagnitude(_deformer.position) > 0f)
		{
			m_deforemer = _deformer;
		}
	}

	private void Awake()
	{
		Renderer[] array = base.gameObject.RequestComponentsRecursive<Renderer>();
		for (int i = 0; i < array.Length; i++)
		{
			m_materials.AddRange(array[i].materials);
		}
		m_materials.RemoveAll((Material x) => x == null);
	}

	private void Update()
	{
		if (m_wobbleTimer > WobbleTime && m_deforemer == null)
		{
			Object.Destroy(this);
		}
		else
		{
			if (TimeManager.IsPaused(TimeManager.PauseLayer.Main))
			{
				return;
			}
			bool flag = false;
			Vector4 push = Vector3.zero;
			if (m_deforemer != null)
			{
				float num = CalculatePushMagnitude(m_deforemer.position);
				Vector4 vector = base.transform.position - m_deforemer.position;
				vector.y = 0f;
				Vector4 vector2 = num * vector.normalized;
				push += vector2;
				if (num == 0f)
				{
					flag = true;
				}
			}
			m_push = Vector4.zero;
			if (m_deforemer != null)
			{
				m_push = push;
			}
			if (flag)
			{
				m_wobbleAmount = Random.Range(MinWobbleAmount, MaxWobbleAmount);
				m_randomXTiming = Random.Range(300f, 400f);
				m_randomZTiming = Random.Range(300f, 400f);
				m_deforemer = null;
			}
			Vector3 vector3 = base.transform.InverseTransformDirection(m_push);
			m_wobbleTimer += Time.deltaTime;
			float value = m_wobbleAmount * MathUtils.ClampedRemap(m_wobbleTimer, 0f, WobbleTime, 1f, 0f);
			for (int i = 0; i < m_materials.Count; i++)
			{
				m_materials[i].SetVector("_PushVector", vector3);
				m_materials[i].SetVector("_Pivot", base.transform.position);
				m_materials[i].SetFloat("_WobbleAmount", value);
				m_materials[i].SetFloat("_RandomXTiming", m_randomXTiming);
				m_materials[i].SetFloat("_RandomZTiming", m_randomZTiming);
			}
		}
	}

	private float CalculatePushMagnitude(Vector3 _deformerPos)
	{
		Vector4 vector = GetComponent<Collider>().ClosestPointOnBounds(_deformerPos) - _deformerPos;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		return MathUtils.ClampedRemap(magnitude, 1f, 0.5f, 0f, 0.5f);
	}
}
