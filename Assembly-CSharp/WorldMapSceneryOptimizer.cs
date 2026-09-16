using UnityEngine;

public class WorldMapSceneryOptimizer : MonoBehaviour, ITileFlipAnimatorProvider
{
	[SerializeField]
	private RuntimeAnimatorController m_controller;

	private Animator m_animator;

	private AnimatorCommunications m_animatorComs;

	private bool m_complete;

	[SerializeField]
	[AssignChild("Mesh", Editorbility.Editable)]
	private GameObject m_mesh;

	public GameObject Mesh
	{
		get
		{
			return m_mesh;
		}
	}

	private void Awake()
	{
		if (!m_complete)
		{
			if (m_mesh != null)
			{
				m_mesh.SetActive(false);
			}
			m_complete = true;
		}
	}

	public Animator Begin(FlipDirection _direction)
	{
		m_animator = base.gameObject.AddComponent<Animator>();
		m_animator.runtimeAnimatorController = m_controller;
		m_animatorComs = base.gameObject.AddComponent<AnimatorCommunications>();
		return m_animator;
	}

	public void End(FlipDirection _direction)
	{
		if (m_animatorComs != null)
		{
			Object.Destroy(m_animatorComs);
			m_animatorComs = null;
		}
		if (m_animator != null)
		{
			Object.Destroy(m_animator);
			m_animator = null;
		}
		if (m_mesh != null)
		{
			m_mesh.SetActive(true);
		}
		m_complete = true;
	}

	public bool IsComplete()
	{
		return m_complete;
	}
}
