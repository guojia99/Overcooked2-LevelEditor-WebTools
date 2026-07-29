using LevelEditorStub;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorStubIO
{
    public static void ExportStub(GameObject go, LayoutItemDto item)
    {
        if (go == null || item == null)
            return;

        item.stubKind = null;
        item.dispenser = null;
        item.foodSpawner = null;

        var dispenser = go.GetComponent<PseudoPrefabDispenserStub>();
        if (dispenser != null)
        {
            item.stubKind = "Dispenser";
            item.dispenser = new LayoutDispenserStubDto();
            if (dispenser.spawnerItemPrefabSO != null)
            {
                var path = AssetDatabase.GetAssetPath(dispenser.spawnerItemPrefabSO);
                item.dispenser.spawnerItemPrefabGuid = AssetDatabase.AssetPathToGUID(path);
            }
            return;
        }

        var spawner = go.GetComponent<PseudoPrefabAttachingFoodSpawnerStub>();
        if (spawner != null)
        {
            item.stubKind = "AttachingFoodSpawner";
            var dto = new LayoutFoodSpawnerStubDto
            {
                spawnInOrder = spawner.spawnInOrder,
                triggerTime = spawner.triggerTime,
                triggerAtStart = spawner.triggerAtStart
            };

            if (spawner.attachmentPrefabSOs != null && spawner.attachmentPrefabSOs.Length > 0)
            {
                var guids = new string[spawner.attachmentPrefabSOs.Length];
                for (int i = 0; i < spawner.attachmentPrefabSOs.Length; i++)
                {
                    var so = spawner.attachmentPrefabSOs[i];
                    guids[i] = so != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so))
                        : string.Empty;
                }

                dto.attachmentPrefabGuids = guids;
            }

            if (spawner.weights != null && spawner.weights.Length > 0)
                dto.weights = (float[])spawner.weights.Clone();

            item.foodSpawner = dto;
        }
    }

    public static void ApplyStub(GameObject go, LayoutItemDto item)
    {
        if (go == null || item == null || string.IsNullOrEmpty(item.stubKind))
            return;

        if (item.stubKind == "Dispenser" && item.dispenser != null)
        {
            var dispenser = go.GetComponent<PseudoPrefabDispenserStub>();
            if (dispenser == null)
                return;

            Undo.RecordObject(dispenser, "Layout Editor Dispenser");
            if (!string.IsNullOrEmpty(item.dispenser.spawnerItemPrefabGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(item.dispenser.spawnerItemPrefabGuid);
                dispenser.spawnerItemPrefabSO = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
            }
            else
            {
                dispenser.spawnerItemPrefabSO = null;
            }

            return;
        }

        if (item.stubKind == "AttachingFoodSpawner" && item.foodSpawner != null)
        {
            var spawner = go.GetComponent<PseudoPrefabAttachingFoodSpawnerStub>();
            if (spawner == null)
                return;

            var dto = item.foodSpawner;
            Undo.RecordObject(spawner, "Layout Editor Food Spawner");
            spawner.spawnInOrder = dto.spawnInOrder;
            spawner.triggerTime = dto.triggerTime;
            spawner.triggerAtStart = dto.triggerAtStart;

            if (dto.attachmentPrefabGuids != null && dto.attachmentPrefabGuids.Length > 0)
            {
                var sos = new PseudoPrefabSO[dto.attachmentPrefabGuids.Length];
                for (int i = 0; i < dto.attachmentPrefabGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(dto.attachmentPrefabGuids[i]);
                    sos[i] = string.IsNullOrEmpty(path)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
                }

                spawner.attachmentPrefabSOs = sos;
            }

            if (dto.weights != null && dto.weights.Length > 0)
                spawner.weights = dto.weights;
        }
    }
}
