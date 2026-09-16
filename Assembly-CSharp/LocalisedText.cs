using UnityEngine;

[ExecutionDependency(typeof(BootstrapManager))]
[ExecutionDependency(typeof(MetaEnvironmentFactory))]
[ExecutionDependency(typeof(Localization))]
public class LocalisedText : TextEx
{
	[SerializeField]
	private string m_tag;

	public override string text
	{
		get
		{
			return m_Text;
		}
		set
		{
			m_tag = value;
			if (Application.isPlaying)
			{
				base.text = Localization.Get(m_tag);
			}
			else
			{
				base.text = "<" + m_tag + ">";
			}
		}
	}

	public string literalText
	{
		get
		{
			return m_Text;
		}
		set
		{
			m_Text = value;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		m_Text = Localization.Get(m_tag);
	}
}
