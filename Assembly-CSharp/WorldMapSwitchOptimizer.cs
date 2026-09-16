using UnityEngine;

public class WorldMapSwitchOptimizer : MonoBehaviour, ITileFlipAnimatorProvider
{
	[SerializeField]
	[AssignChild("Base", Editorbility.NonEditable)]
	private MeshRenderer m_base;

	[SerializeField]
	[AssignChild("Pad", Editorbility.NonEditable)]
	private MeshRenderer m_pad;

	[SerializeField]
	private RuntimeAnimatorController m_controller;

	private Animator m_animator;

	private AnimatorCommunications m_animatorComs;

	private bool m_complete;

	private event CallbackVoid m_OnSwitchFlipBegin = delegate
	{
	};

	private event CallbackVoid m_OnSwitchFlipEnd = delegate
	{
	};

	public void RegisterOnSwitchFlipBegin(CallbackVoid callback)
	{
		m_OnSwitchFlipBegin += callback;
	}

	public void RegisterOnSwitchFlipEnd(CallbackVoid callback)
	{
		m_OnSwitchFlipEnd += callback;
	}

	public void UnRegisterOnSwitchFlipBegin(CallbackVoid callback)
	{
		m_OnSwitchFlipBegin -= callback;
	}

	public void UnRegisterOnSwitchFlipEnd(CallbackVoid callback)
	{
		m_OnSwitchFlipEnd -= callback;
	}

	private void Awake()
	{
		if (!m_complete)
		{
			base.gameObject.SetRendering(false);
			m_complete = true;
		}
	}

	public Animator Begin(FlipDirection _direction)
	{
		m_animator = base.gameObject.AddComponent<Animator>();
		m_animator.runtimeAnimatorController = m_controller;
		m_animatorComs = base.gameObject.AddComponent<AnimatorCommunications>();
		this.m_OnSwitchFlipBegin();
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
		base.gameObject.SetRendering(true);
		m_complete = true;
		this.m_OnSwitchFlipEnd();
	}

	public bool IsComplete()
	{
		return m_complete;
	}
}
