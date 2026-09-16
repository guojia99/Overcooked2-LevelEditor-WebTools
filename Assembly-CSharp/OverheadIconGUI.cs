using System;
using UnityEngine;

[AddComponentMenu("Scripts/Game/GUI/OverheadIconGUI")]
public class OverheadIconGUI : MonoBehaviour
{
	[SerializeField]
	private SubTexture2D m_backgroundTexture;

	[SerializeField]
	private SubTexture2D m_subTexture;

	[SerializeField]
	private Vector3 m_worldOffset;

	[SerializeField]
	private Vector2 m_screenSpaceOffset;

	[SerializeField]
	private float m_fadeInOutTime = 1f;

	[SerializeField]
	private float m_subToBackRatio = 1f;

	[SerializeField]
	private float m_iconScale = 1f;

	[SerializeField]
	private float m_lifetime;

	[SerializeField]
	private bool m_destroyOnLifeOver;

	private float m_alphaTimer;

	private float m_alphaProp;

	private bool m_dead;

	public void SetSubTexture(SubTexture2D _texture)
	{
		m_subTexture = _texture;
	}

	public void SetBackground(SubTexture2D _texture)
	{
		m_backgroundTexture = _texture;
	}

	public void SetScreenOffset(Vector2 _offset)
	{
		m_screenSpaceOffset = _offset;
	}

	public void SetWorldOffset(Vector3 _offset)
	{
		m_worldOffset = _offset;
	}

	public void SetIconScale(float _scale)
	{
		m_iconScale = _scale;
	}

	public void SetLifeTime(float _lifetime, bool _destroyOnLifeOver = true)
	{
		m_dead = false;
		m_lifetime = _lifetime;
		m_destroyOnLifeOver = _destroyOnLifeOver;
	}

	private void Update()
	{
		if (m_dead)
		{
			return;
		}
		m_alphaTimer += TimeManager.GetDeltaTime(base.gameObject);
		float num = Mathf.Clamp01(m_alphaTimer / m_fadeInOutTime);
		if (m_lifetime > 0f)
		{
			m_lifetime -= TimeManager.GetDeltaTime(base.gameObject);
			num = Mathf.Min(num, Mathf.Clamp01(m_lifetime / m_fadeInOutTime));
			if (m_lifetime < 0f)
			{
				if (m_destroyOnLifeOver)
				{
					UnityEngine.Object.Destroy(this);
				}
				m_dead = true;
				m_alphaTimer = 0f;
			}
		}
		m_alphaProp = 0.5f * (1f - Mathf.Cos(num * (float)Math.PI));
	}

	private void OnGUI()
	{
		if (m_dead || m_subTexture == null || m_subTexture.m_atlasTexture == null)
		{
			return;
		}
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera camera in allCameras)
		{
			Rect position = new Rect(camera.pixelRect.x, (float)Screen.height - (camera.pixelRect.height + camera.pixelRect.y), camera.pixelRect.width, camera.pixelRect.height);
			GUI.BeginGroup(position);
			Vector3 vector = camera.WorldToViewportPoint(base.transform.position + base.transform.rotation * m_worldOffset);
			vector.y = 1f - vector.y;
			vector += VectorUtils.FromXY(m_screenSpaceOffset, 0f);
			Vector2 vCenter = new Vector2(camera.pixelRect.width * vector.x, camera.pixelRect.height * vector.y);
			GUI.color = new Color(1f, 1f, 1f, m_alphaProp);
			if ((bool)m_backgroundTexture)
			{
				DrawScaledSubTexture(m_backgroundTexture, vCenter, m_iconScale);
			}
			if ((bool)m_subTexture)
			{
				DrawScaledSubTexture(m_subTexture, vCenter, m_subToBackRatio * m_iconScale);
			}
			GUI.EndGroup();
		}
	}

	private static void DrawScaledSubTexture(SubTexture2D _texture, Vector2 _vCenter, float _nScale)
	{
		Rect subRect = _texture.m_subRect;
		Vector2 vector = _vCenter - _nScale * 0.5f * new Vector2(subRect.width, subRect.height);
		Rect rect = new Rect(vector.x, vector.y, _nScale * subRect.width, _nScale * subRect.height);
		_texture.Draw(rect);
	}
}
