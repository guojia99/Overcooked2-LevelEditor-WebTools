using UnityEngine;

[ExecutionDependency(typeof(WorldMapFlipperBase))]
public class WorldMapRampOptimizer : MonoBehaviour, ITileFlipAnimatorProvider, ITileFlipStaticHandler
{
	[SerializeField]
	[AssignChild("Ramp", Editorbility.NonEditable)]
	private GameObject m_ramp;

	[SerializeField]
	[AssignChildRecursive("Top", Editorbility.NonEditable)]
	private GameObject m_top;

	[SerializeField]
	[AssignChildRecursive("Bottom", Editorbility.NonEditable)]
	private GameObject m_bottom;

	[SerializeField]
	private RuntimeAnimatorController m_unfoldController;

	[SerializeField]
	private RuntimeAnimatorController m_foldController;

	[SerializeField]
	private bool m_startStatic;

	[SerializeField]
	private bool m_debugBreak;

	private Animator m_animator;

	private AnimatorCommunications m_animatorComs;

	private bool m_complete;

	public Animator Begin(FlipDirection _direction)
	{
		m_complete = false;
		RuntimeAnimatorController runtimeAnimatorController = ((_direction != FlipDirection.Unfold) ? m_foldController : m_unfoldController);
		m_animator = m_ramp.AddComponent<Animator>();
		if (m_animator == null)
		{
		}
		m_animator.runtimeAnimatorController = runtimeAnimatorController;
		m_animatorComs = m_ramp.AddComponent<AnimatorCommunications>();
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
		if (_direction == FlipDirection.Unfold)
		{
			m_top.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
			if (m_bottom != null)
			{
				m_bottom.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
			}
		}
		else
		{
			m_top.transform.localRotation = Quaternion.Euler(40f, 0f, 0f);
			if (m_bottom != null)
			{
				m_bottom.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
			}
		}
		m_complete = true;
	}

	public bool IsComplete()
	{
		return m_complete;
	}

	private void Awake()
	{
		if (m_debugBreak)
		{
		}
		WorldMapRampFlip componentInParent = base.gameObject.GetComponentInParent<WorldMapRampFlip>();
		if (componentInParent != null && !componentInParent.StartsFlipped && !componentInParent.ShouldBeUnfolded() && m_bottom != null)
		{
			m_bottom.SetActive(false);
		}
		if (!m_complete)
		{
			End(FlipDirection.Fold);
			m_complete = true;
		}
		if (m_startStatic)
		{
			SetAsStatic();
		}
	}

	public void SetAsStatic()
	{
		WorldMapTileFlip worldMapTileFlip = base.gameObject.RequestComponent<WorldMapTileFlip>();
		if (worldMapTileFlip != null)
		{
			m_top.SetActive(worldMapTileFlip.IsFlipped());
			if (m_bottom != null)
			{
				m_bottom.SetActive(!worldMapTileFlip.IsFlipped());
			}
		}
		if (m_bottom != null && !m_bottom.activeInHierarchy)
		{
			Object.Destroy(m_bottom);
		}
	}
}
