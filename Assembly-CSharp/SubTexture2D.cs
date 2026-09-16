using System;
using UnityEngine;

[Serializable]
public class SubTexture2D
{
	public Texture2D m_atlasTexture;

	public Rect m_subRect;

	public SubTexture2D DeepClone()
	{
		return MemberwiseClone() as SubTexture2D;
	}

	public static implicit operator bool(SubTexture2D _texture)
	{
		return _texture != null && _texture.m_atlasTexture != null && _texture.m_subRect.width >= 1f && _texture.m_subRect.height >= 1f;
	}

	public void Draw(Rect _rect, float _angle = 0f)
	{
		Draw(this, _rect, _angle);
	}

	public void Draw(Vector2 _center, float _scale = 1f, float _angle = 0f)
	{
		Rect rect = new Rect(_center.x - _scale * 0.5f * m_subRect.width, _center.y - _scale * 0.5f * m_subRect.height, _scale * m_subRect.width, _scale * m_subRect.height);
		Draw(this, rect, _angle);
	}

	public static void Draw(SubTexture2D _subTexture, Rect _rect, float _angle = 0f)
	{
		Matrix4x4 matrix = GUI.matrix;
		GUIUtility.RotateAroundPivot(57.29578f * _angle, _rect.center);
		GUI.BeginGroup(_rect);
		Texture2D atlasTexture = _subTexture.m_atlasTexture;
		Rect subRect = _subTexture.m_subRect;
		float width = _rect.width * (float)atlasTexture.width / subRect.width;
		float num = _rect.width * subRect.x / subRect.width;
		float height = _rect.height * (float)atlasTexture.height / subRect.height;
		float num2 = _rect.height * subRect.y / subRect.height;
		GUI.DrawTexture(new Rect(0f - num, 0f - num2, width, height), atlasTexture);
		GUI.EndGroup();
		GUI.matrix = matrix;
	}
}
