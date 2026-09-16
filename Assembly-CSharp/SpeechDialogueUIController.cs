using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public class SpeechDialogueUIController : HoverIconUIController
{
	private class Tag
	{
		public int StartIndex;

		public string Text;

		public Tag(int _index, string _text)
		{
			StartIndex = _index;
			Text = _text;
		}
	}

	[SerializeField]
	private T17Text m_text;

	[SerializeField]
	private float m_perLetterDelay = 0.05f;

	private Tag[] m_tags = new Tag[0];

	private string m_localisedDialogue = string.Empty;

	private bool m_autoPrint = true;

	private float m_printage;

	private float m_letterTimer;

	private StringInfo m_localisedDialogueInfo;

	public void Setup(string _dialogueTag, bool _autoPrint = true)
	{
		m_localisedDialogue = Localization.Get(_dialogueTag);
		MatchCollection matchCollection = Regex.Matches(m_localisedDialogue, "<.+/>");
		m_tags = new Tag[matchCollection.Count];
		for (int i = 0; i < matchCollection.Count; i++)
		{
			m_tags[i] = new Tag(matchCollection[i].Index, matchCollection[i].Value);
		}
		Array.Sort(m_tags, (Tag x, Tag y) => x.StartIndex.CompareTo(y.StartIndex));
		m_text.SetNewLocalizationTag(string.Empty);
		m_localisedDialogueInfo = new StringInfo(m_localisedDialogue);
		m_autoPrint = _autoPrint;
		m_printage = 0f;
	}

	public override void LateUpdate()
	{
		if (m_autoPrint)
		{
			m_letterTimer += TimeManager.GetDeltaTime(base.gameObject);
			m_printage = Mathf.Clamp01(m_letterTimer / m_perLetterDelay / (float)CollapsedLength());
		}
		m_text.SetNewLocalizationTag(m_localisedDialogueInfo.SubstringByTextElements(0, GetLetterCount(m_printage)));
		base.LateUpdate();
	}

	public void SetPrintage(float _printage)
	{
		m_printage = _printage;
	}

	public bool IsPrinting()
	{
		return GetCollapsedLetterCount(m_printage) < CollapsedLength();
	}

	public void SkipPrinting()
	{
		m_letterTimer = (float)(m_localisedDialogueInfo.LengthInTextElements + 1) * m_perLetterDelay;
	}

	private int GetLetterCount(float _printage)
	{
		int num = GetCollapsedLetterCount(_printage);
		for (int i = 0; i < m_tags.Length; i++)
		{
			if (num > m_tags[i].StartIndex)
			{
				num += m_tags[i].Text.Length - 1;
			}
		}
		return num;
	}

	private int GetCollapsedLetterCount(float _printage)
	{
		int value = Mathf.FloorToInt(_printage * (float)CollapsedLength());
		return Mathf.Clamp(value, 0, CollapsedLength());
	}

	private int CollapsedLength()
	{
		int num = 0;
		for (int i = 0; i < m_tags.Length; i++)
		{
			num += m_tags[i].Text.Length - 1;
		}
		return m_localisedDialogueInfo.LengthInTextElements - num;
	}
}
