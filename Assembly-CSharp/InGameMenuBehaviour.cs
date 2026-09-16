using UnityEngine;

public class InGameMenuBehaviour : BaseMenuBehaviour
{
	public bool m_bSelectTopElementOnShow = true;

	public string MenuName = "GIVE ME A TITLE!";

	protected GameObject m_ObjectSelectedBeforeShow;

	protected PlayerManager m_IPlayerManager;

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (m_CachedEventSystem != null)
		{
			m_ObjectSelectedBeforeShow = m_CachedEventSystem.currentSelectedGameObject;
			if (m_BorderSelectables.selectOnUp != null && m_bSelectTopElementOnShow)
			{
				m_CachedEventSystem.SetSelectedGameObject(null);
				m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnUp.gameObject);
				T17StandaloneInputModule t17StandaloneInputModule = (T17StandaloneInputModule)m_CachedEventSystem.currentInputModule;
				if (t17StandaloneInputModule != null)
				{
					t17StandaloneInputModule.SetLastSelected(m_BorderSelectables.selectOnUp.gameObject);
				}
			}
		}
		m_IPlayerManager = GameUtils.RequireManager<PlayerManager>();
		return true;
	}

	protected override void Update()
	{
		base.Update();
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		T17EventSystem cachedEventSystem = m_CachedEventSystem;
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		if (cachedEventSystem != null)
		{
			cachedEventSystem.ForceDeselectSelectionObject();
			if (m_ObjectSelectedBeforeShow != null)
			{
				cachedEventSystem.SetSelectedGameObject(m_ObjectSelectedBeforeShow);
				m_ObjectSelectedBeforeShow = null;
			}
		}
		return true;
	}

	public virtual void Close()
	{
		Hide();
	}

	public new void InvokeNavigateOnUICancel()
	{
		if (m_NavigateOnUICancel != null && m_NavigateOnUICancel.m_DoThisOnUICancel != null)
		{
			m_NavigateOnUICancel.m_DoThisOnUICancel.Invoke();
		}
	}
}
