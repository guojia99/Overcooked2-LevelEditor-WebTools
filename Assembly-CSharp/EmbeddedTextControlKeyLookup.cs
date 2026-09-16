using System.Collections.Generic;
using System.Text.RegularExpressions;
using InControl;
using UnityEngine;
using UnityEngine.UI;

public class EmbeddedTextControlKeyLookup : MonoBehaviour, ITextProcessor
{
	private struct ControlKeyReplacementInfo
	{
		public int m_Index;

		public int m_Length;

		public string m_Replacement;
	}

	[SerializeField]
	private PlayerInputLookup.LogicalButtonID[] m_ButtonIds;

	private const string kControlKeysLookupPattern = "<key\\s*index\\s*=\\s*(\\d+)\\s*/>";

	private const string kBracketColorMarkup = "<color=#bcaba3ff>";

	private const string kKeyColorMarkup = "<color=#f3eaedff>";

	private const string kColorEndMarkup = "</color>";

	private const string kOpenBraketString = "<color=#bcaba3ff>[</color>";

	private const string kCloseBraketString = "<color=#bcaba3ff>]</color>";

	public bool HasEmbeddedControlKeys(string markupString)
	{
		return Regex.IsMatch(markupString, "<key\\s*index\\s*=\\s*(\\d+)\\s*/>");
	}

	public bool ProcessText(ref string markupString)
	{
		if (!HasEmbeddedControlKeys(markupString))
		{
			return false;
		}
		List<ControlKeyReplacementInfo> list = new List<ControlKeyReplacementInfo>();
		Match match = Regex.Match(markupString, "<key\\s*index\\s*=\\s*(\\d+)\\s*/>");
		while (match.Success)
		{
			int index = int.Parse(match.Groups[1].Value);
			list.Add(new ControlKeyReplacementInfo
			{
				m_Index = match.Index,
				m_Length = match.Length,
				m_Replacement = GetKeyText(index)
			});
			match = match.NextMatch();
		}
		for (int num = list.Count - 1; num >= 0; num--)
		{
			markupString = markupString.Remove(list[num].m_Index, list[num].m_Length);
			markupString = markupString.Insert(list[num].m_Index, list[num].m_Replacement);
		}
		return true;
	}

	private string GetKeyText(int _index)
	{
		return (_index < 0 || _index >= m_ButtonIds.Length) ? " " : GetBindingText(m_ButtonIds[_index]);
	}

	private string GetBindingText(PlayerInputLookup.LogicalButtonID logicalButtonId)
	{
		PadSide side = PlayerInputLookup.GetInputConfig().GetInputData(PlayerInputLookup.Player.One).Side;
		ControlPadInput.Button[] realButtons = PlayerInputLookup.GetRealButtons(logicalButtonId, side);
		for (int i = 0; i < realButtons.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realButtons[i], side != PadSide.Both);
			if (bindings != null && bindings.Count > 0)
			{
				string text = bindings[0].ToString();
				if (text.Length == 1)
				{
					return GetKeyString(text.ToUpperInvariant());
				}
				return GetKeyString(Localization.Get("Text.ControlsMenu." + text.ToUpperInvariant()));
			}
		}
		return " ";
	}

	private string GetKeyString(string key)
	{
		return "<color=#bcaba3ff>[</color><color=#f3eaedff>" + key + "</color><color=#bcaba3ff>]</color>";
	}

	public bool OnPopulateMesh(VertexHelper _helper)
	{
		return false;
	}

	public bool HasEmbeddedImages(string inputString)
	{
		return false;
	}
}
