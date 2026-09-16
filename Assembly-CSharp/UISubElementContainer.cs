using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class UISubElementContainer : MonoBehaviour
{
	protected readonly string c_oldContainerName = "GeneratedChildren_ProgressBar";

	protected readonly string c_containerName = "GeneratedChildren";

	protected GameObject m_container;

	private bool m_debugActivated;

	public void RefreshSubElements()
	{
		EnsureImagesExist();
		m_debugActivated = true;
		RefreshSubObjectProperties();
	}

	protected virtual void OnRefreshSubObjectProperties(GameObject _container)
	{
	}

	protected virtual void OnCreateSubObjects(GameObject _container)
	{
	}

	private void RefreshSubObjectProperties()
	{
		if (!(m_container == null))
		{
			RectTransform rect = m_container.RequireComponent<RectTransform>();
			UIUtils.SetupFillParentAreaRect(rect);
			OnRefreshSubObjectProperties(m_container);
		}
	}

	protected virtual void EnsureImagesExist()
	{
		DestroyAllChildrenWithName(c_oldContainerName);
		DestroyAllChildrenWithName(c_containerName);
		m_container = GameObjectUtils.CreateOnParent<RectTransform>(base.gameObject, c_containerName);
		m_container.SetActive(false);
		OnCreateSubObjects(m_container);
		m_container.SetActive(true);
	}

	private void DestroyAllChildrenWithName(string _name)
	{
		int childCount = base.transform.childCount;
		for (int num = childCount - 1; num >= 0; num--)
		{
			Transform child = base.transform.GetChild(num);
			if (child.name == _name)
			{
				if (Application.isPlaying)
				{
					Object.Destroy(child.gameObject);
				}
				else
				{
					Object.DestroyImmediate(child.gameObject);
				}
			}
		}
	}

	private void DestroyImageComponentsInEditor()
	{
		if ((bool)m_container && m_container.activeInHierarchy)
		{
			Object.DestroyImmediate(m_container);
		}
	}

	protected Image CreateImage(GameObject _parent, string _name)
	{
		GameObject gameObject = GameObjectUtils.CreateOnParent<Image>(_parent, _name);
		return gameObject.GetComponent<Image>();
	}

	protected RectTransform CreateRect(GameObject _parent, string _name)
	{
		GameObject gameObject = GameObjectUtils.CreateOnParent<RectTransform>(_parent, _name);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		UIUtils.SetupFillParentAreaRect(component);
		return component;
	}
}
