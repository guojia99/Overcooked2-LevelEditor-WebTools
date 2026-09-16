using UnityEngine;

public class BlenderCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public GameObject m_contentsObject;

	[SerializeField]
	public OrderToPrefabLookup m_prefabLookup;

	[SerializeField]
	public Animator m_animator;

	[SerializeField]
	public string m_surfaceMaterialName;

	[HideInInspector]
	public bool m_hasOnParam;

	[HideInInspector]
	public bool m_hasFillParam;

	public static int c_onParam = Animator.StringToHash("On");

	public static int c_FillParam = Animator.StringToHash("Fill");

	private void Awake()
	{
		if (m_animator != null)
		{
			m_hasOnParam = m_animator.HasParameter(c_onParam);
			m_hasFillParam = m_animator.HasParameter(c_FillParam);
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
