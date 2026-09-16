using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public abstract class BaseProgressBarUI : UISubElementContainer
{
	[Serializable]
	public class ColourChangingImageConfig
	{
		public ColorPropCouple[] ColorSequence = new ColorPropCouple[1]
		{
			new ColorPropCouple(Color.white, 0f)
		};

		public Sprite SourceImage;

		public Image.Type imageType;
	}

	[Serializable]
	public class ColorPropCouple
	{
		public Color StageColor;

		public float Prop;

		public ColorPropCouple(Color _color, float _prop)
		{
			StageColor = _color;
			Prop = _prop;
		}
	}

	protected enum ImageType
	{
		Background = 0,
		Filled = 1,
		Cap = 2,
		Length = 3
	}

	[SerializeField]
	private ColourChangingImageConfig m_fillImageConfig = new ColourChangingImageConfig();

	[SerializeField]
	private ColourChangingImageConfig m_capImageConfig = new ColourChangingImageConfig();

	[SerializeField]
	private ColourChangingImageConfig m_backgroundImageConfig = new ColourChangingImageConfig();

	[SerializeField]
	private Sprite[] m_notchSprite;

	[SerializeField]
	private float[] m_notchPositions;

	[SerializeField]
	private Color m_notchColor;

	[SerializeField]
	[Range(0f, 1f)]
	private float m_value;

	protected Image[] m_images = new Image[3];

	protected Image[] m_notches = new Image[0];

	public float Value
	{
		get
		{
			return m_value;
		}
		set
		{
			SetValue(value);
		}
	}

	public Image FillImage
	{
		get
		{
			int num = 1;
			if (num < m_images.Length)
			{
				return m_images[num];
			}
			return null;
		}
	}

	public Image CapImage
	{
		get
		{
			int num = 2;
			if (num < m_images.Length)
			{
				return m_images[num];
			}
			return null;
		}
	}

	public void SetSprites(ColourChangingImageConfig _background, ColourChangingImageConfig _fill)
	{
		m_backgroundImageConfig = _background;
		m_fillImageConfig = _fill;
	}

	public void SetNotchs(Sprite[] _notchSprites, float[] _notchPositions, Color _color)
	{
		m_notchSprite = _notchSprites;
		m_notchPositions = _notchPositions;
		m_notchColor = _color;
	}

	private void OnEnable()
	{
		UpdateColors();
		UpdateFill();
	}

	private void Update()
	{
		UpdateColors();
	}

	public void SetValue(float _value)
	{
		m_value = Mathf.Clamp01(_value);
		UpdateColors();
		UpdateFill();
	}

	protected abstract void UpdateFill();

	protected abstract Image CreateFillImage(GameObject _rect);

	protected abstract void PositionNotch(Image _notch, float _position);

	private void UpdateColors()
	{
		for (int i = 0; i < m_images.Length; i++)
		{
			if (m_images[i] != null)
			{
				m_images[i].color = GetSequenceColour(GetImageConfig((ImageType)i).ColorSequence, m_value);
			}
		}
	}

	private Color GetSequenceColour(ColorPropCouple[] _colorSequence, float _propLeft)
	{
		ColorPropCouple colorPropCouple = _colorSequence[0];
		ColorPropCouple colorPropCouple2 = _colorSequence[0];
		float num = float.MaxValue;
		float num2 = float.MinValue;
		foreach (ColorPropCouple colorPropCouple3 in _colorSequence)
		{
			if (colorPropCouple3.Prop >= _propLeft && colorPropCouple3.Prop < num)
			{
				num = colorPropCouple3.Prop;
				colorPropCouple = colorPropCouple3;
			}
			if (colorPropCouple3.Prop <= _propLeft && colorPropCouple3.Prop > num2)
			{
				num2 = colorPropCouple3.Prop;
				colorPropCouple2 = colorPropCouple3;
			}
		}
		if (num - num2 > 0.001f)
		{
			float t = MathUtils.ClampedRemap(_propLeft, num2, num, 0f, 1f);
			return Color.Lerp(colorPropCouple2.StageColor, colorPropCouple.StageColor, t);
		}
		return colorPropCouple2.StageColor;
	}

	private void Awake()
	{
		m_images = new Image[3];
		for (int i = 0; i < m_images.Length; i++)
		{
			Transform parent = base.transform;
			ImageType imageType = (ImageType)i;
			Transform transform = parent.FindChildRecursive("ProgressBarUI_" + imageType);
			if (transform != null)
			{
				if (transform.Find("SubImage") != null)
				{
					transform = transform.Find("SubImage");
				}
				m_images[i] = transform.gameObject.RequireComponent<Image>();
			}
		}
	}

	protected override void OnRefreshSubObjectProperties(GameObject _container)
	{
		for (int i = 0; i < m_images.Length; i++)
		{
			if (m_images[i] != null)
			{
				Image image = m_images[i];
				ColourChangingImageConfig imageConfig = GetImageConfig((ImageType)i);
				Color sequenceColour = GetSequenceColour(imageConfig.ColorSequence, m_value);
				image.color = sequenceColour;
				image.sprite = imageConfig.SourceImage;
				image.type = imageConfig.imageType;
			}
		}
		UpdateFill();
	}

	protected override void OnCreateSubObjects(GameObject _container)
	{
		m_images = new Image[3];
		for (int i = 0; i < 3; i++)
		{
			Image[] images = m_images;
			int num = i;
			int imageType = i;
			ImageType imageType2 = (ImageType)i;
			images[num] = CreateImage(_container, (ImageType)imageType, "ProgressBarUI_" + imageType2);
		}
		if (m_notchPositions != null)
		{
			m_notches = new Image[m_notchPositions.Length];
			for (int j = 0; j < m_notches.Length; j++)
			{
				m_notches[j] = CreateNotchImage(_container, "Notch_" + j, j);
			}
		}
	}

	private ColourChangingImageConfig GetImageConfig(ImageType _image)
	{
		switch (_image)
		{
		case ImageType.Background:
			return m_backgroundImageConfig;
		case ImageType.Filled:
			return m_fillImageConfig;
		case ImageType.Cap:
			return m_capImageConfig;
		default:
			return null;
		}
	}

	private Image CreateImage(GameObject _parent, ImageType _imageType, string _name)
	{
		switch (_imageType)
		{
		case ImageType.Background:
		{
			GameObject gameObject3 = GameObjectUtils.CreateOnParent<Image>(_parent, _name);
			gameObject3.hideFlags = HideFlags.NotEditable;
			return gameObject3.GetComponent<Image>();
		}
		case ImageType.Filled:
		{
			GameObject gameObject2 = GameObjectUtils.CreateOnParent<RectTransform>(_parent, _name);
			gameObject2.hideFlags = HideFlags.NotEditable;
			return CreateFillImage(gameObject2);
		}
		case ImageType.Cap:
		{
			GameObject gameObject = GameObjectUtils.CreateOnParent<Image>(_parent, _name);
			gameObject.hideFlags = HideFlags.NotEditable;
			return gameObject.GetComponent<Image>();
		}
		default:
			return null;
		}
	}

	private Image CreateNotchImage(GameObject _parent, string _name, int _index)
	{
		Image image = CreateImage(_parent, "Notch_" + _index);
		PositionNotch(image, m_notchPositions[_index]);
		if (m_notchSprite[_index] != null)
		{
			image.sprite = m_notchSprite[_index];
			image.color = m_notchColor;
		}
		return image;
	}
}
