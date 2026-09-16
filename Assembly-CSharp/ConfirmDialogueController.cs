using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmDialogueController : UIControllerBase
{
	[SerializeField]
	[AssignChild("ConfirmTitle", Editorbility.NonEditable)]
	private Text m_titleObject;

	[SerializeField]
	[AssignChild("ExplainationText", Editorbility.NonEditable)]
	private Text m_explanationTextObject;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	private FrontendGUI m_frontendGUI;

	[SerializeField]
	private bool m_autoBegin;

	[SerializeField]
	[HideInInspectorTest("m_autoBegin", true)]
	private string m_titleText;

	[SerializeField]
	[HideInInspectorTest("m_autoBegin", true)]
	private string m_explanationText;

	[SerializeField]
	[HideInInspectorTest("m_autoBegin", true)]
	private string m_pveOptionText;

	[SerializeField]
	[HideInInspectorTest("m_autoBegin", true)]
	private string m_nveOptionText;

	private IEnumerator m_iterator;

	public event VoidGeneric<bool> ResultCallback = delegate
	{
	};

	private void Awake()
	{
		if (m_autoBegin)
		{
			Begin(m_titleText, m_explanationText, m_pveOptionText, m_nveOptionText);
		}
	}

	public void Begin(string _title, string _explanation, string _pveOption, string _nveOption)
	{
		m_titleObject.text = _title;
		m_explanationTextObject.text = _explanation;
		m_iterator = Run(m_frontendGUI, _pveOption, _nveOption);
	}

	private void Update()
	{
		if (m_iterator != null && !m_iterator.MoveNext())
		{
			m_iterator = null;
		}
	}

	public IEnumerator Run(FrontendGUI _ui, string _pveOption, string _nveOption)
	{
		FrontendListEntry.NameData[] names = new FrontendListEntry.NameData[2]
		{
			new FrontendListEntry.NameData(_pveOption, true, false),
			new FrontendListEntry.NameData(_nveOption, true, false)
		};
		_ui.SetNames(names);
		ScrollingListControlsHelper scrollingControlsHelper = new ScrollingListControlsHelper();
		scrollingControlsHelper.Init(_ui, 0.12f);
		bool finished = false;
		scrollingControlsHelper.RegisterSelectionCallback(delegate(int _s)
		{
			finished = true;
			this.ResultCallback(_s == 0);
		});
		scrollingControlsHelper.RegisterCancelCallback(delegate
		{
			finished = true;
			this.ResultCallback(false);
		});
		while (!finished)
		{
			scrollingControlsHelper.Update();
			yield return null;
		}
		Object.Destroy(base.gameObject);
	}
}
