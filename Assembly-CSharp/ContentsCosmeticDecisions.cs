using System;
using UnityEngine;

public class ContentsCosmeticDecisions : AnimationInspectionBase
{
	[Serializable]
	public class ContentsScale
	{
		[SerializeField]
		public Vector3 m_empty = new Vector3(1f, 1f, 1f);

		[SerializeField]
		public Vector3 m_full = new Vector3(1f, 1f, 1f);

		[SerializeField]
		public AnimationCurve m_curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	}

	[SerializeField]
	public GameObject m_gameObject;

	[SerializeField]
	public GameObject m_contentsObject;

	[SerializeField]
	public OrderToPrefabLookup m_prefabLookup;

	[SerializeField]
	public string m_surfaceMaterialName;

	[SerializeField]
	public string m_bubbleMaterialName;

	[SerializeField]
	public float m_contentsYPositionWhenFull = 0.2f;

	[SerializeField]
	public float m_contentsYPositionWhenEmpty;

	[SerializeField]
	public ContentsScale m_contentsScale = new ContentsScale();

	[HideInInspector]
	public bool m_hasOnParam;

	[HideInInspector]
	public bool m_hasProgressParam;

	public static int c_onParam = Animator.StringToHash("On");

	public static int c_progressParam = Animator.StringToHash("Progress");

	private void Awake()
	{
		if (m_animator != null)
		{
			m_hasOnParam = m_animator.HasParameter(c_onParam);
			m_hasProgressParam = m_animator.HasParameter(c_progressParam);
		}
	}

	private void Start()
	{
		if (m_prefabLookup != null)
		{
			m_prefabLookup.CacheAssembledOrderNodes();
		}
	}
}
