using UnityEngine;

public class MapAvatarTransformer : MonoBehaviour
{
	public enum VanType
	{
		LAND = 0,
		WATER = 1,
		FLYING = 2
	}

	[SerializeField]
	private GameObject m_transformParticle;

	private VanType m_currentType;

	private MapAvatarControls m_controls;

	private static readonly int m_iVanType = Animator.StringToHash("VanType");

	public VanType CurrentType
	{
		get
		{
			return m_currentType;
		}
	}

	private void Awake()
	{
		m_controls = base.gameObject.RequireComponent<MapAvatarControls>();
	}

	private void OnEnable()
	{
		ChangeVan(VanType.LAND);
	}

	private void Update()
	{
		GameObject gridOccupant = m_controls.GridManager.GetGridOccupant(m_controls.GridManager.GetUnclampedGridLocationFromPos(base.transform.position));
		if (!(gridOccupant != null))
		{
			return;
		}
		WorldMapTileFlip worldMapTileFlip = gridOccupant.RequestComponent<WorldMapTileFlip>();
		if (worldMapTileFlip != null && worldMapTileFlip.IsFlipped())
		{
			string text = gridOccupant.name;
			if (text.StartsWith("Rapids") || text.StartsWith("Ramp_Rapids"))
			{
				ChangeVan(VanType.WATER);
			}
			else if (text.StartsWith("Balloon") || text.StartsWith("Ramp_Balloon"))
			{
				ChangeVan(VanType.FLYING);
			}
			else if (text.StartsWith("Space") || text.StartsWith("Ramp_Space"))
			{
				ChangeVan(VanType.FLYING);
			}
			else
			{
				ChangeVan(VanType.LAND);
			}
		}
		else
		{
			ChangeVan(VanType.LAND);
		}
	}

	private void ChangeVan(VanType type)
	{
		if (m_currentType != type)
		{
			Animator animator = base.gameObject.RequireComponentRecursive<Animator>();
			if (animator != null)
			{
				animator.SetInteger(m_iVanType, (int)type);
			}
			if (m_transformParticle != null)
			{
				GameObject gameObject = Object.Instantiate(m_transformParticle);
				gameObject.transform.SetParent(base.transform, false);
			}
			m_currentType = type;
		}
	}
}
