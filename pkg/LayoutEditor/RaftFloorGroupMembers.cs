using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 木筏地板矩形 → 写回生成的拼板 items：动画组 / 摇杆组烘焙时并入 members
/// （木筏无 Plane，floor.instanceId 无法像主题地板那样解析到单个 GO）。
/// </summary>
public static class RaftFloorGroupMembers
{
    public static bool IsRaftPlankItem(LayoutItemDto item)
    {
        if (item == null)
            return false;
        var id = "";
        if (!string.IsNullOrEmpty(item.prefabAssetPath))
            id = System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath);
        if (string.IsNullOrEmpty(id))
            id = item.displayName ?? "";
        if (string.IsNullOrEmpty(id))
            return false;
        return id.StartsWith("raft_raft_", StringComparison.Ordinal);
    }

    public static bool TryGetFloorRect(FloorDto floor,
        out float cx, out float cz, out float halfW, out float halfD)
    {
        cx = 0f;
        cz = 0f;
        halfW = 0f;
        halfD = 0f;
        if (floor == null)
            return false;
        cx = floor.worldPosition != null ? floor.worldPosition.x
            : (floor.localPosition != null ? floor.localPosition.x : 0f);
        cz = floor.worldPosition != null ? floor.worldPosition.z
            : (floor.localPosition != null ? floor.localPosition.z : 0f);
        float w = floor.widthUnits > 0f ? floor.widthUnits
            : (floor.widthCells > 0 ? floor.widthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
        float d = floor.depthUnits > 0f ? floor.depthUnits
            : (floor.depthCells > 0 ? floor.depthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
        halfW = w * 0.5f;
        halfD = d * 0.5f;
        return true;
    }

    public static bool IsRaftFloorMember(string floorInstanceId, LayoutDocumentDto document)
    {
        if (string.IsNullOrEmpty(floorInstanceId) || document == null || document.floors == null)
            return false;
        for (int i = 0; i < document.floors.Length; i++)
        {
            var f = document.floors[i];
            if (f != null && f.instanceId == floorInstanceId && f.surfaceKind == "raft")
                return true;
        }
        return false;
    }

    /// <summary>把组内木筏地板矩形覆盖范围内的拼板 GO 追加到 members（去重）。</summary>
    public static void AppendRaftPlankMembers(AnimGroupDto group, LayoutDocumentDto document,
        Dictionary<string, GameObject> createdObjects, List<GameObject> members)
    {
        if (group == null || document == null || members == null)
            return;
        if (group.floorInstanceIds == null || group.floorInstanceIds.Length == 0)
            return;
        if (document.floors == null || document.items == null)
            return;

        var memberSet = new HashSet<GameObject>();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null)
                memberSet.Add(members[i]);
        }

        var floorById = new Dictionary<string, FloorDto>();
        for (int i = 0; i < document.floors.Length; i++)
        {
            var f = document.floors[i];
            if (f == null || string.IsNullOrEmpty(f.instanceId))
                continue;
            floorById[f.instanceId] = f;
        }

        const float pad = 0.05f;
        for (int fi = 0; fi < group.floorInstanceIds.Length; fi++)
        {
            var floorId = group.floorInstanceIds[fi];
            if (string.IsNullOrEmpty(floorId))
                continue;
            FloorDto floor;
            if (!floorById.TryGetValue(floorId, out floor))
                continue;
            if (floor.surfaceKind != "raft")
                continue;

            float cx, cz, halfW, halfD;
            if (!TryGetFloorRect(floor, out cx, out cz, out halfW, out halfD))
                continue;

            for (int ii = 0; ii < document.items.Length; ii++)
            {
                var item = document.items[ii];
                if (!IsRaftPlankItem(item))
                    continue;
                float ix = item.worldPosition != null ? item.worldPosition.x
                    : (item.localPosition != null ? item.localPosition.x : 0f);
                float iz = item.worldPosition != null ? item.worldPosition.z
                    : (item.localPosition != null ? item.localPosition.z : 0f);
                if (Mathf.Abs(ix - cx) > halfW + pad || Mathf.Abs(iz - cz) > halfD + pad)
                    continue;

                GameObject go = null;
                if (createdObjects != null && !string.IsNullOrEmpty(item.instanceId))
                    createdObjects.TryGetValue(item.instanceId, out go);
                if (go == null || memberSet.Contains(go))
                    continue;
                memberSet.Add(go);
                members.Add(go);
            }
        }
    }
}
