using UnityEngine;

public class FrontendMenuBehaviour : BaseMenuBehaviour
{
	public bool m_bSelectTopElementOnShow = true;

	public string MenuName = "GIVE ME A TITLE!";

	protected GameObject m_ObjectSelectedBeforeShow;

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (m_CachedEventSystem != null)
		{
			m_ObjectSelectedBeforeShow = m_CachedEventSystem.currentSelectedGameObject;
			if (m_ObjectSelectedBeforeShow == null)
			{
				m_ObjectSelectedBeforeShow = m_CachedEventSystem.GetLastRequestedSelectedGameobject();
			}
			if (m_BorderSelectables.selectOnUp != null && m_bSelectTopElementOnShow)
			{
				m_CachedEventSystem.SetSelectedGameObject(null);
				m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnUp.gameObject);
			}
		}
		Animator[] componentsInChildren = base.gameObject.GetComponentsInChildren<Animator>(true);
		if (componentsInChildren != null)
		{
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].speed = 1f;
			}
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		T17EventSystem cachedEventSystem = m_CachedEventSystem;
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		if (cachedEventSystem != null && m_ObjectSelectedBeforeShow != null)
		{
			cachedEventSystem.SetSelectedGameObject(null);
			cachedEventSystem.SetSelectedGameObject(m_ObjectSelectedBeforeShow);
			m_ObjectSelectedBeforeShow = null;
		}
		return true;
	}

	public virtual void Close()
	{
		Hide();
	}
}
