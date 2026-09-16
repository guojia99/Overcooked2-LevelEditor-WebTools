using System;
using UnityEngine;

public class ScrollingListControlsHelper
{
	public delegate void SelectionCallback(int _selectionId);

	private IScrollingListUI m_gui;

	private float m_kerchunkInterval;

	private ILogicalButton m_upButton;

	private ILogicalButton m_downButton;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_cancelButton;

	private float m_moveTimer;

	private VoidGeneric<int> m_selectionCallbacks = delegate
	{
	};

	private VoidGeneric<int, int> m_selectionChangeCallbacks = delegate
	{
	};

	private VoidToVoid m_cancelCallback = delegate
	{
	};

	public void Init(IScrollingListUI _gui, float _kerchunkInterval)
	{
		m_gui = _gui;
		m_kerchunkInterval = _kerchunkInterval;
		m_upButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIUp);
		m_downButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIDown);
		m_selectButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
		m_cancelButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
	}

	public void RegisterSelectionCallback(VoidGeneric<int> _callback)
	{
		m_selectionCallbacks = (VoidGeneric<int>)Delegate.Combine(m_selectionCallbacks, _callback);
	}

	public void UnregisterSelectionCallback(VoidGeneric<int> _callback)
	{
		m_selectionCallbacks = (VoidGeneric<int>)Delegate.Remove(m_selectionCallbacks, _callback);
	}

	public void RegisterSelectionChangeCallback(VoidGeneric<int, int> _callback)
	{
		m_selectionChangeCallbacks = (VoidGeneric<int, int>)Delegate.Combine(m_selectionChangeCallbacks, _callback);
	}

	public void UnregisterSelectionChangeCallback(VoidGeneric<int, int> _callback)
	{
		m_selectionChangeCallbacks = (VoidGeneric<int, int>)Delegate.Remove(m_selectionChangeCallbacks, _callback);
	}

	public void RegisterCancelCallback(VoidToVoid _callback)
	{
		m_cancelCallback = (VoidToVoid)Delegate.Combine(m_cancelCallback, _callback);
	}

	public void UnregisterCancelCallback(VoidToVoid _callback)
	{
		m_cancelCallback = (VoidToVoid)Delegate.Remove(m_cancelCallback, _callback);
	}

	public void Update()
	{
		GameObject gameObject = (m_gui as MonoBehaviour).gameObject;
		if (m_upButton.IsDown() || (m_downButton.IsDown() && !TimeManager.IsPaused(gameObject)))
		{
			m_moveTimer += TimeManager.GetDeltaTime(gameObject);
			if (m_moveTimer >= 0f)
			{
				m_moveTimer = 0f - m_kerchunkInterval;
				int selection = m_gui.GetSelection();
				if (m_upButton.IsDown())
				{
					m_gui.MoveUp();
				}
				else
				{
					m_gui.MoveDown();
				}
				int selection2 = m_gui.GetSelection();
				if (selection != selection2)
				{
					m_selectionChangeCallbacks(selection, selection2);
				}
			}
		}
		else
		{
			m_moveTimer = 0f;
		}
		if (m_selectButton.JustPressed() && !TimeManager.IsPaused(gameObject))
		{
			m_selectionCallbacks(m_gui.GetSelection());
		}
		if (m_cancelButton.JustPressed() && !TimeManager.IsPaused(gameObject))
		{
			m_cancelCallback();
		}
	}
}
