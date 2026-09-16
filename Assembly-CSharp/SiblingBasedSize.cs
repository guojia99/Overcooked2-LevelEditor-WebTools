using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(LayoutElement))]
public class SiblingBasedSize : UIBehaviour
{
	[Serializable]
	public class SizeOption
	{
		[SerializeField]
		public int m_numSiblings;

		[SerializeField]
		public Vector2 m_dimensions;
	}

	private RectTransform m_transform;

	[SerializeField]
	private List<SizeOption> m_sizeOptions;

	protected override void Awake()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		UpdateRectTransform();
	}

	protected SizeOption GetSizeOption()
	{
		if (base.transform.parent != null)
		{
			int activeSiblings = 0;
			int childCount = base.transform.parent.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Transform child = base.transform.parent.GetChild(i);
				if (child != base.transform && child.gameObject.activeInHierarchy)
				{
					activeSiblings++;
				}
			}
			return m_sizeOptions.Find((SizeOption x) => x.m_numSiblings == activeSiblings);
		}
		return null;
	}

	private void OnUsersChanged()
	{
		UpdateRectTransform();
	}

	public void UpdateRectTransform()
	{
		SizeOption sizeOption = GetSizeOption();
		if (sizeOption != null)
		{
			RectTransform rectTransform = base.transform as RectTransform;
			rectTransform.sizeDelta = sizeOption.m_dimensions;
			LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		}
	}

	protected override void OnDestroy()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}
}
