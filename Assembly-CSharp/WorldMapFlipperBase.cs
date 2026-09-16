using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecutionDependency(typeof(GridAutoParenting))]
[DisallowMultipleComponent]
public abstract class WorldMapFlipperBase : MonoBehaviour, ITileFlipStartup
{
	[SerializeField]
	protected bool m_startFlipped;

	[SerializeField]
	protected bool m_startCollidable;

	[SerializeField]
	protected float m_startFlipDelay;

	[SerializeField]
	private float m_childFlipDelay = 0.5f;

	[SerializeField]
	private bool m_foldChildrenOnFlip;

	[SerializeField]
	private bool m_debugBreak;

	private bool m_shouldBeFlipped;

	private bool m_isFlipping;

	private Animator m_animator;

	private Collider[] m_colliders;

	private ITileFlipAnimatorProvider m_iTileAnimator;

	[SerializeField]
	public WorldMapInfoPopup m_infoPopup;

	public static readonly int m_iFlipped = Animator.StringToHash("Flipped");

	public static readonly int m_iSnapping = Animator.StringToHash("Snapping");

	public static readonly int m_iPostFlipIdle = Animator.StringToHash("InPostFlipIdle");

	public static readonly int m_iPreFlipIdle = Animator.StringToHash("InPreFlipIdle");

	private WaitForSeconds m_waitForStartDelay;

	private WaitForSeconds m_waitForChildDelay;

	private static int depthTest = 0;

	public bool StartsFlipped
	{
		get
		{
			return m_startFlipped;
		}
	}

	public virtual void StartUp()
	{
		ITileFlipStartup[] array = base.gameObject.RequestInterfacesRecursive<ITileFlipStartup>();
		array = array.AllRemoved_Predicate(Equals);
		int num = array.Length;
		for (int i = 0; i < num; i++)
		{
			depthTest++;
			array[i].StartUp();
			depthTest--;
		}
	}

	private void Setup()
	{
		if (m_animator == null && m_iTileAnimator == null)
		{
			m_iTileAnimator = base.gameObject.RequestInterface<ITileFlipAnimatorProvider>();
			if (m_iTileAnimator == null)
			{
				m_iTileAnimator = base.gameObject.RequestInterfaceInImmediateChildren<ITileFlipAnimatorProvider>();
				if (m_iTileAnimator == null)
				{
					m_animator = base.gameObject.RequireComponentRecursive<Animator>();
				}
			}
		}
		if (m_colliders == null)
		{
			m_colliders = base.gameObject.RequestComponentsInImmediateChildren<Collider>();
		}
	}

	protected virtual void Awake()
	{
		Setup();
		SetCollidable(m_startCollidable);
		if (m_startFlipped)
		{
			StartInstantUnfold();
		}
	}

	private void Start()
	{
		m_waitForStartDelay = new WaitForSeconds(m_startFlipDelay);
		m_waitForChildDelay = new WaitForSeconds(m_childFlipDelay);
	}

	private void SetCollidable(bool _collidable)
	{
		for (int i = 0; i < m_colliders.Length; i++)
		{
			m_colliders[i].enabled = _collidable;
		}
	}

	public bool IsFlipped()
	{
		if (!Application.isPlaying)
		{
			return true;
		}
		return m_shouldBeFlipped;
	}

	public bool IsFinishedFlipping()
	{
		bool flag = false;
		if (m_iTileAnimator != null)
		{
			flag = m_iTileAnimator.IsComplete();
		}
		else
		{
			int id = ((!m_shouldBeFlipped) ? m_iPreFlipIdle : m_iPostFlipIdle);
			flag = m_animator.GetBool(id);
		}
		return !m_isFlipping && flag;
	}

	public virtual void StartUnfoldFlow()
	{
		StartCoroutine(UnfoldFlow());
	}

	private IEnumerator UnfoldFlow()
	{
		m_shouldBeFlipped = true;
		m_isFlipping = true;
		SetCollidable(true);
		WorldMapFlipperBase[] iFlippers = base.gameObject.RequestComponentsRecursive<WorldMapFlipperBase>();
		iFlippers = iFlippers.AllRemoved_Predicate(Equals);
		for (int i = 0; i < iFlippers.Length; i++)
		{
			while (!iFlippers[i].IsFinishedFlipping())
			{
				yield return null;
			}
		}
		if (m_iTileAnimator != null)
		{
			m_animator = m_iTileAnimator.Begin(FlipDirection.Unfold);
		}
		yield return m_waitForStartDelay;
		m_animator.SetBool(m_iFlipped, true);
		yield return m_waitForChildDelay;
		for (int j = 0; j < iFlippers.Length; j++)
		{
			iFlippers[j].StartUnfoldFlow();
			yield return m_waitForChildDelay;
		}
		for (int k = 0; k < iFlippers.Length; k++)
		{
			while (!iFlippers[k].IsFinishedFlipping())
			{
				yield return null;
			}
		}
		while (!m_animator.GetBool(m_iPostFlipIdle))
		{
			yield return null;
		}
		if (m_iTileAnimator != null)
		{
			m_iTileAnimator.End(FlipDirection.Unfold);
			m_animator = null;
		}
		m_isFlipping = false;
	}

	public List<ClientWorldMapInfoPopup.InfoPopupShowRequest> GetPopups()
	{
		WorldMapFlipperBase[] array = base.gameObject.RequestComponentsRecursive<WorldMapFlipperBase>();
		List<ClientWorldMapInfoPopup.InfoPopupShowRequest> list = new List<ClientWorldMapInfoPopup.InfoPopupShowRequest>();
		for (int i = 0; i < array.Length; i++)
		{
			WorldMapInfoPopup infoPopup = array[i].m_infoPopup;
			if (infoPopup != null)
			{
				ClientWorldMapInfoPopup popup = infoPopup.gameObject.RequireComponent<ClientWorldMapInfoPopup>();
				ClientWorldMapInfoPopup.InfoPopupShowRequest item = new ClientWorldMapInfoPopup.InfoPopupShowRequest(array[i].transform, popup);
				list.Add(item);
			}
		}
		return list;
	}

	public virtual void StartInstantUnfold()
	{
		Setup();
		m_shouldBeFlipped = true;
		SetCollidable(true);
		if (m_iTileAnimator == null)
		{
			m_animator.SetBool(m_iSnapping, true);
			m_animator.SetBool(m_iFlipped, true);
		}
		WorldMapFlipperBase[] array = base.gameObject.RequestComponentsInImmediateChildren<WorldMapFlipperBase>();
		array = array.AllRemoved_Predicate(Equals);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].StartInstantUnfold();
		}
		if (m_iTileAnimator != null)
		{
			m_iTileAnimator.End(FlipDirection.Unfold);
			m_animator = null;
		}
	}

	public virtual void StartFoldFlow()
	{
		StartCoroutine(FoldFlow());
	}

	private IEnumerator FoldFlow()
	{
		m_shouldBeFlipped = false;
		m_isFlipping = true;
		SetCollidable(false);
		WorldMapFlipperBase[] iFlippers = base.gameObject.RequestComponentsInImmediateChildren<WorldMapFlipperBase>();
		iFlippers = iFlippers.AllRemoved_Predicate(Equals);
		for (int i = 0; i < iFlippers.Length; i++)
		{
			if (iFlippers[i].IsFlipped())
			{
				iFlippers[i].StartFoldFlow();
				yield return new WaitForSeconds(m_childFlipDelay);
			}
		}
		if (m_iTileAnimator != null)
		{
			m_animator = m_iTileAnimator.Begin(FlipDirection.Fold);
		}
		m_animator.SetBool(m_iFlipped, true);
		while (!m_animator.GetBool(m_iPostFlipIdle))
		{
			yield return null;
		}
		if (m_iTileAnimator != null)
		{
			m_iTileAnimator.End(FlipDirection.Fold);
			m_animator = null;
		}
		m_isFlipping = false;
	}
}
