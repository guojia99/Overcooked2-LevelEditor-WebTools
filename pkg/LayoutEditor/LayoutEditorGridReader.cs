using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LayoutEditorGridReader
{
    public static GridInfoDto ReadFromActiveScene()
    {
        var result = new GridInfoDto
        {
            found = false,
            cellSize = LayoutVector3.From(new Vector3(1.2f, 1f, 1.2f)),
            origin = 0f,
            gridHalfSizeX = 0,
            gridHalfSizeZ = 0,
            worldPosition = LayoutVector3.From(Vector3.zero)
        };

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return result;

        foreach (var root in scene.GetRootGameObjects())
        {
            var grids = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in grids)
            {
                if (mb == null)
                    continue;

                if (mb.GetType().Name != "QuadGridManager")
                    continue;

                if (!TryReadGridComponent(mb, result))
                    continue;

                result.found = true;
                return result;
            }
        }

        return result;
    }

    private static bool TryReadGridComponent(MonoBehaviour mb, GridInfoDto dto)
    {
        var so = new SerializedObject(mb);
        so.Update();

        var half = so.FindProperty("m_gridHalfSize");
        if (half != null)
        {
            var x = half.FindPropertyRelative("X");
            var z = half.FindPropertyRelative("Z");
            if (x != null && z != null)
            {
                dto.gridHalfSizeX = x.intValue;
                dto.gridHalfSizeZ = z.intValue;
            }
            else if (half.propertyType == SerializedPropertyType.Vector3)
            {
                dto.gridHalfSizeX = Mathf.RoundToInt(half.vector3Value.x);
                dto.gridHalfSizeZ = Mathf.RoundToInt(half.vector3Value.z);
            }
        }
        else
        {
            half = so.FindProperty("gridHalfSize");
            if (half != null && half.propertyType == SerializedPropertyType.Vector3)
            {
                dto.gridHalfSizeX = Mathf.RoundToInt(half.vector3Value.x);
                dto.gridHalfSizeZ = Mathf.RoundToInt(half.vector3Value.z);
            }
        }

        var size = so.FindProperty("m_size");
        if (size != null && size.propertyType == SerializedPropertyType.Vector3)
            dto.cellSize = LayoutVector3.From(size.vector3Value);
        else
        {
            size = so.FindProperty("size");
            if (size != null && size.propertyType == SerializedPropertyType.Vector3)
                dto.cellSize = LayoutVector3.From(size.vector3Value);
        }

        ReadOrigin(so, "m_origin", dto);
        if (dto.origin == 0f)
            ReadOrigin(so, "origin", dto);

        dto.worldPosition = LayoutVector3.From(mb.transform.position);
        return half != null || dto.gridHalfSizeX > 0;
    }

    private static void ReadOrigin(SerializedObject so, string propertyName, GridInfoDto dto)
    {
        var origin = so.FindProperty(propertyName);
        if (origin == null)
            return;

        if (origin.propertyType == SerializedPropertyType.Float)
            dto.origin = origin.floatValue;
        else if (origin.propertyType == SerializedPropertyType.Integer)
            dto.origin = origin.intValue;
        else if (origin.propertyType == SerializedPropertyType.Vector3)
            dto.origin = origin.vector3Value.x;
    }
}
