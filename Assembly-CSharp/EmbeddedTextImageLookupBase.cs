using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public abstract class EmbeddedTextImageLookupBase : MonoBehaviour, ITextProcessor
{
	private class Element
	{
		public ImageOverlay Overlay;

		public Image Image;

		public int ImageMaterialNum;
	}

	private class ImageOverlay
	{
		public float X;

		public float Y;

		public float Width;

		public float Height;

		public Sprite Icon;

		public ImageOverlay(float _x, float _y, float _width, float _height, Sprite _icon)
		{
			X = _x;
			Y = _y;
			Width = _width;
			Height = _height;
			Icon = _icon;
		}
	}

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Text m_text;

	[SerializeField]
	private Vector2 m_offset;

	private Dictionary<int, Element> m_elements = new Dictionary<int, Element>();

	private List<Element> m_buttonToDestroy = new List<Element>();

	private static string m_lookupPattern = "<quad\\s*material\\s*=\\s*(\\d+)";

	public Text Text
	{
		get
		{
			return m_text;
		}
		set
		{
			m_text = value;
		}
	}

	protected abstract Sprite GetIcon(int _iconID);

	public bool HasEmbeddedImages(string markupString)
	{
		return Regex.IsMatch(markupString, m_lookupPattern);
	}

	public void RefreshImage()
	{
		foreach (KeyValuePair<int, Element> element in m_elements)
		{
			Element value = element.Value;
			if (value.Overlay != null)
			{
				value.Overlay.Icon = GetIcon(value.ImageMaterialNum);
			}
			if (value.Image != null)
			{
				value.Image.sprite = GetIcon(value.ImageMaterialNum);
			}
		}
	}

	public bool OnPopulateMesh(VertexHelper _helper)
	{
		if (Application.isPlaying)
		{
			UpdateMesh(_helper);
		}
		return true;
	}

	private void LateUpdate()
	{
		foreach (KeyValuePair<int, Element> element2 in m_elements)
		{
			Element value = element2.Value;
			if (value.Overlay != null)
			{
				if (value.Image == null)
				{
					GameObject gameObject = GameObjectUtils.CreateOnParent(base.gameObject, "ChildImage");
					value.Image = gameObject.AddComponent<Image>();
				}
				RectTransform rectTransform = base.gameObject.RequireComponent<RectTransform>();
				float num = value.Overlay.X + m_offset.x;
				float num2 = value.Overlay.Y + m_offset.y;
				value.Image.rectTransform.anchorMin = rectTransform.pivot;
				value.Image.rectTransform.anchorMax = rectTransform.pivot;
				value.Image.rectTransform.localScale = Vector3.one;
				value.Image.rectTransform.offsetMax = new Vector3(num + value.Overlay.Width, num2 + value.Overlay.Height, 0f);
				value.Image.rectTransform.offsetMin = new Vector3(num, num2, 0f);
				value.Image.sprite = value.Overlay.Icon;
				value.Overlay = null;
			}
		}
		for (int i = 0; i < m_buttonToDestroy.Count; i++)
		{
			Element element = m_buttonToDestroy[i];
			if (element.Image != null)
			{
				Object.Destroy(element.Image.gameObject);
			}
		}
		m_buttonToDestroy.Clear();
	}

	private void UpdateMesh(VertexHelper _helper)
	{
		Match match = Regex.Match(m_text.text, m_lookupPattern);
		int count = 0;
		while (match.Success)
		{
			int num = match.Index * 4;
			if (_helper.currentVertCount > num + 4)
			{
				UIVertex[] array = new UIVertex[4]
				{
					default(UIVertex),
					default(UIVertex),
					default(UIVertex),
					default(UIVertex)
				};
				for (int i = 0; i < 4; i++)
				{
					_helper.PopulateUIVertex(ref array[i], num + i);
					_helper.SetUIVertex(new UIVertex
					{
						position = array[i].position,
						normal = array[i].normal,
						tangent = array[i].tangent,
						uv0 = Vector2.zero,
						uv1 = Vector2.zero
					}, num + i);
				}
				float num2 = Mathf.Min(array[0].position.x, array[1].position.x, array[2].position.x, array[3].position.x);
				float num3 = Mathf.Min(array[0].position.y, array[1].position.y, array[2].position.y, array[3].position.y);
				float width = Mathf.Max(array[0].position.x, array[1].position.x, array[2].position.x, array[3].position.x) - num2;
				float height = Mathf.Max(array[0].position.y, array[1].position.y, array[2].position.y, array[3].position.y) - num3;
				int num4 = int.Parse(match.Groups[1].Value);
				Element element = m_elements.CreationGet(count);
				element.Overlay = new ImageOverlay(num2, num3, width, height, GetIcon(num4));
				element.ImageMaterialNum = num4;
				count++;
				match = match.NextMatch();
				continue;
			}
			break;
		}
		Dictionary<int, Element>.Enumerator enumerator = m_elements.GetEnumerator();
		while (enumerator.MoveNext())
		{
			if (enumerator.Current.Key >= count)
			{
				m_buttonToDestroy.Add(enumerator.Current.Value);
			}
		}
		m_elements.RemoveAll((KeyValuePair<int, Element> x) => x.Key >= count);
	}

	public bool ProcessText(ref string inputString)
	{
		return false;
	}
}
