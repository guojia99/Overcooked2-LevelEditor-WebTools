using System.Collections.Generic;
using System.Linq;
using InControl;
using UnityEngine;

public abstract class KeyboardRebindElement : MonoBehaviour
{
	[SerializeField]
	private T17Text m_ActionText;

	[SerializeField]
	private T17Text m_KeyBindingsText;

	[SerializeField]
	protected PadSide m_Side;

	[SerializeField]
	protected bool m_AllowSecondaryKey;

	public string ActionTag
	{
		get
		{
			return m_ActionText.m_LocalizationTag;
		}
	}

	public KeyboardRebindElementSet ElementSet { get; private set; }

	protected KeyboardRebindController RebindController { get; private set; }

	public abstract void SetBinding(Key key);

	public abstract void UnsetBinding(Key key);

	public abstract bool HasAnyBindings();

	public abstract void RefreshBindingText();

	public void Awake()
	{
		ElementSet = GetComponentInParent<KeyboardRebindElementSet>();
		RebindController = GetComponentInParent<KeyboardRebindController>();
	}

	public void Start()
	{
		RefreshBindingText();
	}

	public void OnStartRebind()
	{
		RebindController.StartRebind(this);
	}

	protected string KeysToString(List<Key> keys)
	{
		string text = string.Empty;
		keys = keys.Distinct().ToList();
		for (int i = 0; i < keys.Count; i++)
		{
			if (text.Length != 0)
			{
				text += "    ";
			}
			string text2 = keys[i].ToString();
			text = ((text2.Length != 1) ? (text + "[" + Localization.Get("Text.ControlsMenu." + text2.ToUpperInvariant()) + "]") : (text + "[" + text2.ToUpperInvariant() + "]"));
		}
		return text;
	}

	protected void SetKeyBindingsText(string keys)
	{
		if (m_KeyBindingsText != null)
		{
			if (!string.IsNullOrEmpty(keys))
			{
				m_KeyBindingsText.color = Color.white;
				m_KeyBindingsText.SetNonLocalizedText(keys);
			}
			else
			{
				m_KeyBindingsText.color = Color.red;
				m_KeyBindingsText.SetLocalisedTextCatchAll("Text.ControlsMenu.MissingBindings");
			}
		}
	}
}
