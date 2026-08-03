using System;
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
        item.conveyor = null;
        item.teleportal = null;
        item.cookingUtensil = null;
        item.travelator = null;
        item.flamethrower = null;
        item.cleanPlateStack = null;
        item.burner = null;
        item.player = null;
        item.servingStation = null;
        item.plateReturn = null;
        item.switchStub = null;
        item.pressureSwitch = null;
        item.terminal = null;

        var playerStub = go.GetComponent<PseudoPrefabPlayerStub>();
        if (playerStub != null)
        {
            item.stubKind = "Player";
            item.player = new LayoutPlayerStubDto { playerID = (int)playerStub.playerID };
            return;
        }

        var serving = go.GetComponent<PseudoPrefabServingStationStub>();
        if (serving != null)
        {
            item.stubKind = "ServingStation";
            var sdto = new LayoutServingStationStubDto();
            if (serving.plateReturn != null)
                sdto.plateReturnInstanceId = "u:" + serving.plateReturn.gameObject.GetInstanceID();

            // Collect all bound returns (array + legacy single), de-duplicated.
            var ids = new System.Collections.Generic.List<string>();
            if (serving.plateReturns != null)
            {
                foreach (var pr in serving.plateReturns)
                    if (pr != null)
                    {
                        var pid = "u:" + pr.gameObject.GetInstanceID();
                        if (!ids.Contains(pid)) ids.Add(pid);
                    }
            }
            if (!string.IsNullOrEmpty(sdto.plateReturnInstanceId) && !ids.Contains(sdto.plateReturnInstanceId))
                ids.Add(sdto.plateReturnInstanceId);
            sdto.plateReturnInstanceIds = ids.ToArray();

            item.servingStation = sdto;
            return;
        }

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
            return;
        }

        var conveyor = go.GetComponent<PseudoPrefabConveyorStub>();
        if (conveyor != null)
        {
            item.stubKind = "Conveyor";
            item.conveyor = new LayoutConveyorStubDto { conveySpeed = conveyor.conveySpeed };
            return;
        }

        var teleportal = go.GetComponent<PseudoPrefabTeleportalStub>();
        if (teleportal != null)
        {
            item.stubKind = "Teleportal";
            var tdto = new LayoutTeleportalStubDto
            {
                portalColor = (int)teleportal.portalColor,
                doubleSided = teleportal.doubleSided
            };
            if (teleportal.exitPortal != null)
                tdto.exitPortalInstanceId = "u:" + teleportal.exitPortal.gameObject.GetInstanceID();
            item.teleportal = tdto;
            return;
        }

        var utensil = go.GetComponent<PseudoPrefabCookingUtensilStub>();
        if (utensil != null)
        {
            item.stubKind = "CookingUtensil";
            var udto = new LayoutCookingUtensilStubDto { capacity = utensil.capacity };
            if (utensil.allowedIngredientSOs != null && utensil.allowedIngredientSOs.Length > 0)
            {
                var guids = new string[utensil.allowedIngredientSOs.Length];
                for (int i = 0; i < utensil.allowedIngredientSOs.Length; i++)
                {
                    var so = utensil.allowedIngredientSOs[i];
                    guids[i] = so != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so))
                        : string.Empty;
                }

                udto.allowedIngredientGuids = guids;
            }

            item.cookingUtensil = udto;
            return;
        }

        var travelator = go.GetComponent<PseudoPrefabTravelatorStub>();
        if (travelator != null)
        {
            item.stubKind = "Travelator";
            item.travelator = new LayoutTravelatorStubDto { speed = travelator.speed };
            return;
        }

        var flamethrower = go.GetComponent<PseudoPrefabFlamethrowerStub>();
        if (flamethrower != null)
        {
            item.stubKind = "Flamethrower";
            item.flamethrower = new LayoutFlamethrowerStubDto { cookingRate = flamethrower.cookingRate };
            return;
        }

        var returnStation = go.GetComponent<PseudoPrefabPlateReturnStub>();
        if (returnStation != null)
        {
            item.stubKind = !string.IsNullOrEmpty(item.prefabAssetPath) && item.prefabAssetPath.Contains("GlassReturn")
                ? "GlassReturn"
                : "PlateReturn";
            item.plateReturn = new LayoutPlateReturnStubDto { returnClean = returnStation.returnClean };
            return;
        }

        var plateStack = go.GetComponent<PseudoPrefabCleanPlateStackStub>();
        if (plateStack != null)
        {
            item.stubKind = "CleanPlateStack";
            var pdto = new LayoutCleanPlateStackStubDto { plateCount = plateStack.plateCount };
            if (plateStack.platePseudoPrefabSO != null)
            {
                var path = AssetDatabase.GetAssetPath(plateStack.platePseudoPrefabSO);
                pdto.platePrefabGuid = AssetDatabase.AssetPathToGUID(path);
            }

            item.cleanPlateStack = pdto;
            return;
        }

        var burner = go.GetComponent<PseudoPrefabBurnerStub>();
        if (burner != null)
        {
            item.stubKind = "Burner";
            item.burner = new LayoutBurnerStubDto
            {
                fireMode = (int)burner.fireMode,
                airTime = burner.airTime,
                randomTargetOrder = burner.randomTargetOrder,
                hideVisual = burner.hideVisual
            };
            return;
        }

        var switchStub = go.GetComponent<PseudoPrefabSwitchStub>();
        if (switchStub != null)
        {
            item.stubKind = "Switch";
            var sdto = new LayoutSwitchStubDto { startEnabled = switchStub.startEnabled };
            if (switchStub.activeMaterial != null)
                sdto.activeMaterialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(switchStub.activeMaterial));
            if (switchStub.inactiveMaterial != null)
                sdto.inactiveMaterialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(switchStub.inactiveMaterial));
            item.switchStub = sdto;
            return;
        }

        var pressureSwitch = go.GetComponent<PseudoPrefabPressureSwitchStub>();
        if (pressureSwitch != null)
        {
            item.stubKind = "PressureSwitch";
            var pdto = new LayoutPressureSwitchStubDto();
            if (pressureSwitch.occupiedMaterialSO != null)
                pdto.occupiedMaterialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pressureSwitch.occupiedMaterialSO));
            if (pressureSwitch.unoccupiedMaterialSO != null)
                pdto.unoccupiedMaterialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pressureSwitch.unoccupiedMaterialSO));
            item.pressureSwitch = pdto;
            return;
        }

        var terminal = go.GetComponent<PseudoPrefabTerminalStub>();
        if (terminal != null)
        {
            item.stubKind = "Terminal";
            var tdto = new LayoutTerminalStubDto();
            if (terminal.pilotableObject != null)
                tdto.pilotableObjectInstanceId = "u:" + terminal.pilotableObject.GetInstanceID();
            item.terminal = tdto;
            return;
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
            dispenser.spawnerItemPrefabSO = LoadPseudoPrefabSO(item.dispenser.spawnerItemPrefabGuid);
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
                spawner.attachmentPrefabSOs = LoadPseudoPrefabSOs(dto.attachmentPrefabGuids);

            if (dto.weights != null && dto.weights.Length > 0)
                spawner.weights = dto.weights;
            return;
        }

        if (item.stubKind == "Conveyor" && item.conveyor != null)
        {
            var conveyor = go.GetComponent<PseudoPrefabConveyorStub>();
            if (conveyor == null)
                return;

            Undo.RecordObject(conveyor, "Layout Editor Conveyor");
            conveyor.conveySpeed = item.conveyor.conveySpeed;
            return;
        }

        if (item.stubKind == "Teleportal" && item.teleportal != null)
        {
            var teleportal = go.GetComponent<PseudoPrefabTeleportalStub>();
            if (teleportal == null)
                return;

            Undo.RecordObject(teleportal, "Layout Editor Teleportal");
            teleportal.portalColor = (PseudoPrefabTeleportalStub.PortalColor)item.teleportal.portalColor;
            teleportal.doubleSided = item.teleportal.doubleSided;
            // exitPortal is resolved in a second pass by SceneLayoutApplier.
            return;
        }

        if (item.stubKind == "CookingUtensil" && item.cookingUtensil != null)
        {
            var utensil = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            if (utensil == null)
                return;

            Undo.RecordObject(utensil, "Layout Editor Cooking Utensil");
            utensil.capacity = item.cookingUtensil.capacity;
            if (item.cookingUtensil.allowedIngredientGuids != null)
                utensil.allowedIngredientSOs = LoadPseudoPrefabSOs(item.cookingUtensil.allowedIngredientGuids);
            return;
        }

        if (item.stubKind == "Travelator" && item.travelator != null)
        {
            var travelator = go.GetComponent<PseudoPrefabTravelatorStub>();
            if (travelator == null)
                return;

            Undo.RecordObject(travelator, "Layout Editor Travelator");
            travelator.speed = item.travelator.speed;
            return;
        }

        if (item.stubKind == "Flamethrower" && item.flamethrower != null)
        {
            var flamethrower = go.GetComponent<PseudoPrefabFlamethrowerStub>();
            if (flamethrower == null)
                return;

            Undo.RecordObject(flamethrower, "Layout Editor Flamethrower");
            flamethrower.cookingRate = item.flamethrower.cookingRate;
            return;
        }

        if ((item.stubKind == "PlateReturn" || item.stubKind == "GlassReturn") && item.plateReturn != null)
        {
            var returnStation = go.GetComponent<PseudoPrefabPlateReturnStub>();
            if (returnStation == null)
                return;

            Undo.RecordObject(returnStation, "Layout Editor Plate Return");
            returnStation.returnClean = item.plateReturn.returnClean;
            return;
        }

        if (item.stubKind == "CleanPlateStack" && item.cleanPlateStack != null)
        {
            var plateStack = go.GetComponent<PseudoPrefabCleanPlateStackStub>();
            if (plateStack == null)
                return;

            Undo.RecordObject(plateStack, "Layout Editor Clean Plate Stack");
            plateStack.plateCount = item.cleanPlateStack.plateCount;
            if (!string.IsNullOrEmpty(item.cleanPlateStack.platePrefabGuid))
                plateStack.platePseudoPrefabSO = LoadPseudoPrefabSO(item.cleanPlateStack.platePrefabGuid);
            return;
        }

        if (item.stubKind == "Player" && item.player != null)
        {
            var playerStub = go.GetComponent<PseudoPrefabPlayerStub>();
            if (playerStub == null)
                return;

            Undo.RecordObject(playerStub, "Layout Editor Player");
            playerStub.playerID = (PseudoPrefabPlayerStub.Player)item.player.playerID;
            return;
        }

        if (item.stubKind == "Burner" && item.burner != null)
        {
            var burner = go.GetComponent<PseudoPrefabBurnerStub>();
            if (burner == null)
                return;

            Undo.RecordObject(burner, "Layout Editor Burner");
            burner.fireMode = (PseudoPrefabBurnerStub.FireMode)item.burner.fireMode;
            burner.airTime = item.burner.airTime;
            burner.randomTargetOrder = item.burner.randomTargetOrder;
            burner.hideVisual = item.burner.hideVisual;
        }

        if (item.stubKind == "Switch" && item.switchStub != null)
        {
            var sw = go.GetComponent<PseudoPrefabSwitchStub>();
            if (sw == null)
                return;

            Undo.RecordObject(sw, "Layout Editor Switch");
            sw.startEnabled = item.switchStub.startEnabled;
            if (!string.IsNullOrEmpty(item.switchStub.activeMaterialGuid))
                sw.activeMaterial = LoadPseudoPrefabSO(item.switchStub.activeMaterialGuid);
            if (!string.IsNullOrEmpty(item.switchStub.inactiveMaterialGuid))
                sw.inactiveMaterial = LoadPseudoPrefabSO(item.switchStub.inactiveMaterialGuid);
            return;
        }

        if (item.stubKind == "PressureSwitch" && item.pressureSwitch != null)
        {
            var ps = go.GetComponent<PseudoPrefabPressureSwitchStub>();
            if (ps == null)
                return;

            Undo.RecordObject(ps, "Layout Editor Pressure Switch");
            if (!string.IsNullOrEmpty(item.pressureSwitch.occupiedMaterialGuid))
                ps.occupiedMaterialSO = LoadPseudoPrefabSO(item.pressureSwitch.occupiedMaterialGuid);
            if (!string.IsNullOrEmpty(item.pressureSwitch.unoccupiedMaterialGuid))
                ps.unoccupiedMaterialSO = LoadPseudoPrefabSO(item.pressureSwitch.unoccupiedMaterialGuid);
            return;
        }

        if (item.stubKind == "Terminal" && item.terminal != null)
        {
            var terminal = go.GetComponent<PseudoPrefabTerminalStub>();
            if (terminal == null)
                return;

            Undo.RecordObject(terminal, "Layout Editor Terminal");
            if (!string.IsNullOrEmpty(item.terminal.pilotableObjectInstanceId))
            {
                var rid = item.terminal.pilotableObjectInstanceId;
                if (rid.StartsWith("u:", StringComparison.Ordinal))
                {
                    int id;
                    if (int.TryParse(rid.Substring(2), out id))
                        terminal.pilotableObject = EditorUtility.InstanceIDToObject(id) as GameObject;
                }
            }
            else
            {
                terminal.pilotableObject = null;
            }
        }
    }

    /// <summary>
    /// Second pass: resolve Teleportal.exitPortal. exitPortalInstanceId is either
    /// "u:<instanceID>" (existing scene object) or a document instanceId (e.g. "new:...")
    /// mapped through createdObjects. Empty string clears the pairing.
    /// </summary>
    public static void ApplyTeleportalExit(GameObject go, string exitPortalInstanceId, System.Collections.Generic.Dictionary<string, GameObject> createdObjects)
    {
        if (go == null || exitPortalInstanceId == null)
            return;

        var teleportal = go.GetComponent<PseudoPrefabTeleportalStub>();
        if (teleportal == null)
            return;

        GameObject target = null;
        if (exitPortalInstanceId.StartsWith("u:", StringComparison.Ordinal))
        {
            int id;
            if (int.TryParse(exitPortalInstanceId.Substring(2), out id))
                target = EditorUtility.InstanceIDToObject(id) as GameObject;
        }
        else if (!string.IsNullOrEmpty(exitPortalInstanceId) && createdObjects != null)
        {
            createdObjects.TryGetValue(exitPortalInstanceId, out target);
        }

        var exitStub = target != null ? target.GetComponent<PseudoPrefabTeleportalStub>() : null;

        Undo.RecordObject(teleportal, "Layout Editor Teleportal Exit");
        teleportal.exitPortal = exitStub;
    }

    /// <summary>
    /// Second pass: resolve a ServingStation's bound return stations (one-to-many).
    /// Each id is either "u:<instanceID>" (existing scene object) or a document
    /// instanceId (e.g. "new:...") mapped through createdObjects. Resolved stubs
    /// (PlateReturn / GlassReturn share PseudoPrefabPlateReturnStub) are written to
    /// serving.plateReturns; the first also mirrors to the legacy single field.
    /// </summary>
    public static void ApplyServingStationPlateReturns(GameObject go, string[] plateReturnInstanceIds, System.Collections.Generic.Dictionary<string, GameObject> createdObjects)
    {
        if (go == null)
            return;

        var serving = go.GetComponent<PseudoPrefabServingStationStub>();
        if (serving == null)
            return;

        var resolved = new System.Collections.Generic.List<PseudoPrefabPlateReturnStub>();
        if (plateReturnInstanceIds != null)
        {
            foreach (var rid in plateReturnInstanceIds)
            {
                if (string.IsNullOrEmpty(rid))
                    continue;

                GameObject target = null;
                if (rid.StartsWith("u:", StringComparison.Ordinal))
                {
                    int id;
                    if (int.TryParse(rid.Substring(2), out id))
                        target = EditorUtility.InstanceIDToObject(id) as GameObject;
                }
                else if (createdObjects != null)
                {
                    createdObjects.TryGetValue(rid, out target);
                }

                var stub = target != null ? target.GetComponent<PseudoPrefabPlateReturnStub>() : null;
                if (stub != null && !resolved.Contains(stub))
                    resolved.Add(stub);
            }
        }

        Undo.RecordObject(serving, "Layout Editor ServingStation Returns");
        serving.plateReturns = resolved.ToArray();
        // Mirror the first binding to the legacy single field for older runtime paths.
        serving.plateReturn = resolved.Count > 0 ? resolved[0] : null;
    }

    private static PseudoPrefabSO LoadPseudoPrefabSO(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
    }

    private static PseudoPrefabSO[] LoadPseudoPrefabSOs(string[] guids)
    {
        var sos = new PseudoPrefabSO[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            sos[i] = LoadPseudoPrefabSO(guids[i]);
        return sos;
    }
}
