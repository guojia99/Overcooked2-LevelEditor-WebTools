using System;
using LevelEditor;
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
        item.cannon = null;
        item.pseudoPrefabGuid = null;
        item.meshWithMaterial = null;
        item.soArray = null;
        item.timedSwitch = null;

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
            // 提前 return 的分支须自行导出外观皮肤 guid（否则回读丢失）。
            ExportPseudoPrefabGuidIfPresent(go, item);
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
            // 饮料机/酱料机的多选循环列表（PseudoPrefabSOArray）必须在此导出：
            // 本分支提前 return，走不到方法末尾的通用 soArray 导出——
            // 漏掉时 web 重新加载看不到机器配置的饮料/酱料。
            ExportSoArrayIfPresent(go, item);
            // 外观皮肤 guid 同理须在此导出（食材箱有多款皮肤可换）。
            ExportPseudoPrefabGuidIfPresent(go, item);
            return;
        }

        // 酱料机 / 饮料机：编辑器按食材箱处理（可绑定特定酱料/饮料）
        var dispenserPrefabId = !string.IsNullOrEmpty(item.prefabAssetPath)
            ? System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath)
            : "";
        if (IsSpecialDispenserPrefabId(dispenserPrefabId))
        {
            item.stubKind = "Dispenser";
            item.dispenser = new LayoutDispenserStubDto();
            ExportSoArrayIfPresent(go, item);
            ExportPseudoPrefabGuidIfPresent(go, item);
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
            ExportPseudoPrefabGuidIfPresent(go, item);
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
        // 喷雾喷罐被（历史/误配）标成锅具时不作为锅具导出：保持普通道具
        // （宿主 CookingUtensil.Setup 对无容器的 child 会 NRE）。
        if (utensil != null && !LayoutEditorCookingUtensilGuard.IsIngredientSpray(utensil.pseudoPrefabSO))
        {
            item.stubKind = "CookingUtensil";
            var udto = new LayoutCookingUtensilStubDto { capacity = utensil.capacity };
            if (utensil.allowedIngredientSOs != null && utensil.allowedIngredientSOs.Length > 0)
            {
                // 跳过 null 引用（历史迁移/引用断裂残留）：不导出为空 guid，否则前端
                // 原样回传后 LoadIngredientSOs 会把它写成 None，形成「三个 none」残留。
                var guids = new System.Collections.Generic.List<string>();
                for (int i = 0; i < utensil.allowedIngredientSOs.Length; i++)
                {
                    var so = utensil.allowedIngredientSOs[i];
                    if (so == null)
                        continue;
                    guids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so)));
                }

                udto.allowedIngredientGuids = guids.ToArray();
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
            ExportPseudoPrefabGuidIfPresent(go, item);
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
            // 大炮开关（Unity 里带五角星标志的发射按钮）与普通开关共用
            // PseudoPrefabSwitchStub，按 prefabName 区分导出类型（往返不丢类型）。
            var so = switchStub.pseudoPrefabSO;
            var prefabName = so != null ? so.prefabName : string.Empty;
            item.stubKind = (prefabName == "p_dlc08_button_cannon" || prefabName == "p_dlc09_button_cannon")
                ? "CannonSwitch" : "Switch";
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
            {
                // 反向映射：pilotable 已被重定向到伪 prefab child 时，上报其伪根
                // instanceId（与前端 item id 对齐，往返不丢绑定显示）。
                var idGo = terminal.pilotableObject;
                var parentT = idGo.transform.parent;
                if (parentT != null
                    && parentT.GetComponent<PilotMovement>() == null
                    && parentT.GetComponent<PseudoPrefab>() != null)
                    idGo = parentT.gameObject;
                tdto.pilotableObjectInstanceId = "u:" + idGo.GetInstanceID();
            }
            item.terminal = tdto;
            return;
        }

        // 大炮（dlc08/dlc09 cannon）：Cannon/PilotRotation 都在 bundle child 上，
        // 导出限位（±180 = 自由旋转）供前端往返。
        var cannonComp = go.GetComponentInChildren<Cannon>();
        if (cannonComp != null)
        {
            var pilotRot = cannonComp.GetComponent<PilotRotation>();
            item.stubKind = "Cannon";
            if (pilotRot != null)
            {
                item.cannon = new LayoutCannonStubDto
                {
                    freeRotation = pilotRot.m_maxLimitDegrees >= 180f || pilotRot.m_minLimitDegrees <= -180f
                };
            }
            return;
        }

        var soArray = go.GetComponent<PseudoPrefabSOArray>();
        if (soArray != null && soArray.pseudoPrefabSOs != null && soArray.pseudoPrefabSOs.Length > 0)
        {
            var guids = new string[soArray.pseudoPrefabSOs.Length];
            for (int i = 0; i < soArray.pseudoPrefabSOs.Length; i++)
                guids[i] = soArray.pseudoPrefabSOs[i] != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(soArray.pseudoPrefabSOs[i]))
                    : string.Empty;
            item.soArray = new LayoutSOArrayStubDto { pseudoPrefabGuids = guids };
        }

        var mmStub = go.GetComponent<PseudoPrefabMeshWithMaterialStub>();
        if (mmStub != null)
        {
            item.stubKind = "MeshWithMaterial";
            var mdto = new LayoutMeshWithMaterialStubDto();
            if (mmStub.pseudoPrefabSO != null)
                mdto.pseudoPrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mmStub.pseudoPrefabSO));
            if (mmStub.materialSO != null)
                mdto.materialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mmStub.materialSO));
            item.meshWithMaterial = mdto;
            return;
        }

        var pseudoStub = go.GetComponent<PseudoPrefabStub>();
        if (pseudoStub != null && pseudoStub.pseudoPrefabSO != null)
        {
            item.pseudoPrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pseudoStub.pseudoPrefabSO));
        }

        // 火锅灶台定时开关：运行时组件导出（无 stub 的火锅灶台会走到此末尾；
        // 带定时组件的其它物品理论上不存在，但此导出不依赖 stubKind）。
        var timedComp = go.GetComponent<LayoutRuntimeTimedCookingSwitch>();
        if (timedComp != null)
        {
            item.timedSwitch = new LayoutTimedSwitchDto
            {
                enabled = timedComp.m_enabled,
                onSeconds = timedComp.m_onSeconds,
                offSeconds = timedComp.m_offSeconds,
                startOn = timedComp.m_startOn
            };
        }
    }

    public static void ApplyStub(GameObject go, LayoutItemDto item)
    {
        if (go == null || item == null)
            return;

        if (!string.IsNullOrEmpty(item.pseudoPrefabGuid))
        {
            var pseudoStub = go.GetComponent<PseudoPrefabStub>();
            if (pseudoStub != null)
            {
                // 仅当 guid 能解析才写入；解析失败时保留 prefab 继承的 SO，
                // 避免把 pseudoPrefabSO 置空 → 宿主 PseudoPrefabManager 空引用抛异常。
                var so = LoadPseudoPrefabSO(item.pseudoPrefabGuid);
                if (so != null)
                {
                    Undo.RecordObject(pseudoStub, "Layout Editor PseudoPrefab");
                    pseudoStub.pseudoPrefabSO = so;
                }
            }
        }

        // 食材箱外观防御：静态网格箱皮肤（dlc03/07/08/09/13 箱）已从外观目录移除——
        // 宿主 PseudoPrefabDispenser.Setup 的箱面图标渲染只支持「盖子节点
        // SkinnedMeshRenderer / 根节点 MeshRenderer」，静态箱两者皆无会抛
        // MissingComponentException 并中断 ResetAllPseudoPrefabs 循环（其后所有
        // 伪预制件不初始化）。若食材箱 wrapper 的 SO 是悬挂引用（皮肤资产已删/
        // guid 失效），回退默认箱 Dispenser01SO，避免运行时空引用 NRE。
        if (go.GetComponent<PseudoPrefabDispenser>() != null)
        {
            var pseudoStub = go.GetComponent<PseudoPrefabStub>();
            if (pseudoStub != null && pseudoStub.pseudoPrefabSO == null)
            {
                var baseSo = LoadPseudoPrefabSO("3085f7abbd164904dbf7ff588c076f4b"); // Dispenser01SO
                if (baseSo != null)
                {
                    Undo.RecordObject(pseudoStub, "Layout Editor Dispenser Base Skin");
                    pseudoStub.pseudoPrefabSO = baseSo;
                }
            }
        }

        if (item.soArray != null && item.soArray.pseudoPrefabGuids != null)
        {
            var soArray = go.GetComponent<PseudoPrefabSOArray>();
            if (soArray == null && item.soArray.pseudoPrefabGuids.Length > 0)
            {
                // 酱料机/饮料机的多选列表（游戏内开关循环切换，见 LayoutEditorItemSwitcherPatch）：
                // 原 prefab 没有 PseudoPrefabSOArray 组件，写回时补加。
                var pid0 = !string.IsNullOrEmpty(item.prefabAssetPath)
                    ? System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath)
                    : "";
                if (IsSpecialDispenserPrefabId(pid0))
                    soArray = Undo.AddComponent<PseudoPrefabSOArray>(go);
            }
            if (soArray != null)
            {
                Undo.RecordObject(soArray, "Layout Editor SOArray");
                soArray.pseudoPrefabSOs = LoadPseudoPrefabSOs(item.soArray.pseudoPrefabGuids);
            }
        }

        // 火锅灶台定时开关：伪根烘焙运行时循环组件（游戏程序集，随场景保存）。
        // 必须在空 stubKind 提前 return 之前处理——火锅灶台无任何 stub 组件。
        // enabled=false 也保留组件（配置随场景往返不丢，运行时不生效）。
        {
            var timedComp = go.GetComponent<LayoutRuntimeTimedCookingSwitch>();
            if (item.timedSwitch != null)
            {
                if (timedComp == null)
                    timedComp = Undo.AddComponent<LayoutRuntimeTimedCookingSwitch>(go);
                else
                    Undo.RecordObject(timedComp, "Layout Editor Timed Cooking Switch");
                timedComp.m_enabled = item.timedSwitch.enabled;
                timedComp.m_onSeconds = Mathf.Max(3f, item.timedSwitch.onSeconds);
                timedComp.m_offSeconds = Mathf.Max(3f, item.timedSwitch.offSeconds);
                timedComp.m_startOn = item.timedSwitch.startOn;
            }
            else if (timedComp != null)
            {
                Undo.DestroyObjectImmediate(timedComp);
            }
        }

        if (string.IsNullOrEmpty(item.stubKind))
        {
            // 可移动火锅：stubKind 为空（不挂 CookingUtensil stub），但锅具管理的
            //  allowedIngredientGuids 仍要落到载体组件上（运行时重建许可表）。
            var pushablePid = !string.IsNullOrEmpty(item.prefabAssetPath)
                ? System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath)
                : "";
            if (pushablePid == "utensil_large_pot_01_pushable" &&
                item.cookingUtensil != null &&
                item.cookingUtensil.allowedIngredientGuids != null &&
                item.cookingUtensil.allowedIngredientGuids.Length > 0)
            {
                var pushable = go.GetComponent<LevelEditor.LayoutRuntimePushablePot>();
                if (pushable != null)
                {
                    var bundles = new System.Collections.Generic.List<string>();
                    var paths = new System.Collections.Generic.List<string>();
                    foreach (var g in item.cookingUtensil.allowedIngredientGuids)
                    {
                        var so = LoadPseudoPrefabSO(g);
                        if (so == null || string.IsNullOrEmpty(so.assetPath))
                            continue;
                        bundles.Add(so.bundleName ?? "");
                        paths.Add(so.assetPath);
                    }
                    Undo.RecordObject(pushable, "Layout Editor Pushable Pot Ingredients");
                    pushable.m_allowedIngredientBundles = bundles.ToArray();
                    pushable.m_allowedIngredientPaths = paths.ToArray();
                }
            }
            return;
        }

        if (item.stubKind == "Dispenser" && item.dispenser != null)
        {
            var pid = !string.IsNullOrEmpty(item.prefabAssetPath)
                ? System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath)
                : "";
            var isSpecialMachine = IsSpecialDispenserPrefabId(pid);
            var dispenser = go.GetComponent<PseudoPrefabDispenserStub>();
            if (dispenser == null)
            {
                // 酱料机/饮料机原 prefab 无 PseudoPrefabDispenserStub：写回时补加（并复用基础 stub 的 pseudoPrefabSO）
                if (!isSpecialMachine)
                {
                    Debug.LogWarning("[LayoutEditor] Apply Dispenser: 场景对象缺少 PseudoPrefabDispenserStub: " + go.name);
                    return;
                }
                dispenser = Undo.AddComponent<PseudoPrefabDispenserStub>(go);
                var baseStub = go.GetComponent<PseudoPrefabStub>();
                if (baseStub != null && dispenser.pseudoPrefabSO == null)
                    dispenser.pseudoPrefabSO = baseStub.pseudoPrefabSO;
            }

            Undo.RecordObject(dispenser, "Layout Editor Dispenser");
            var so = LoadPseudoPrefabSO(item.dispenser.spawnerItemPrefabGuid);
            // 普通食材箱未设置生成食材：宿主 PseudoPrefabDispenser.Setup 的 LoadAsset(null)
            // 会 NRE。降级为普通道具（保留 base stub/runtime），等用户选好食材再配置。
            if (so == null && !isSpecialMachine)
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] Apply Dispenser: 食材箱未设置生成食材，" +
                    "已降级为普通道具（请先选择食材）: " + go.name);
                LayoutEditorCookingUtensilGuard.DowngradeToBase(go, dispenser.pseudoPrefabSO);
                return;
            }
            // 食材真实 prefab 无 ISpawnableItem（如 TurkeySO 原始火鸡）：宿主
            // PseudoPrefabDispenser.Setup 的 RequireInterface 得 null → GetSubTexture NRE。
            if (!isSpecialMachine && so != null && !LayoutEditorCookingUtensilGuard.RealPrefabIsSpawnableIngredient(so))
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] Apply Dispenser: 食材不可从食材箱产出（真实 prefab" +
                    " 无 ISpawnableItem），已降级为普通道具: " + go.name + "（" + so.name + "）");
                LayoutEditorCookingUtensilGuard.DowngradeToBase(go, dispenser.pseudoPrefabSO);
                return;
            }
            // node 型食材（assetPath 指 .asset 匹配节点，无实体 prefab）不能进【普通食材箱】：
            // 运行时 PseudoPrefabDispenser.Setup 按 GameObject 加载得 null 而 NRE。
            // 饮料机/酱料机豁免——其输出（饮料/酱料）本身就是 node 型（机器内置列表即如此）。
            // 已知映射自动替换为整食材（沙拉洋葱节点 → 整个沙拉洋葱，切 8 刀得到匹配形态）；
            // 未知 node 型则告警并跳过赋值（保留原值，避免写回必然崩溃的配置）。
            if (!isSpecialMachine && so != null && !string.IsNullOrEmpty(so.assetPath) &&
                !so.assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var mapped = so.name == "dlc11onion_salad"
                    ? AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>("Assets/common03/Ingredients/dlc11/dlc11_onion_salad.asset")
                    : null;
                if (mapped != null)
                {
                    Debug.LogWarning("[LayoutEditor] Apply Dispenser: node 型食材 " + so.name
                        + " 不能放入食材箱，已自动替换为整食材 " + mapped.name + "（加工后得到匹配形态）");
                    so = mapped;
                }
                else
                {
                    Debug.LogWarning("[LayoutEditor] Apply Dispenser: node 型食材 " + so.name
                        + " 无实体 prefab，放入食材箱运行时会崩溃，已跳过（请在 Web 编辑器改选 prefab 型食材）");
                    return;
                }
            }
            Debug.Log("[LayoutEditor] Apply Dispenser: guid=" + (item.dispenser.spawnerItemPrefabGuid ?? "<empty>")
                + " -> " + (so != null ? so.name : "NULL"));
            dispenser.spawnerItemPrefabSO = so;
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

        if (item.stubKind == "Teleportal")
        {
            // 传送门必须有出口（exitPortal）：裸传送门宿主 PseudoPrefabTeleportal.LateSetup
            // 对 null exitPortal 调用 GetComponent 抛 NRE。无出口时降级为普通道具。
            if (item.teleportal == null || string.IsNullOrEmpty(item.teleportal.exitPortalInstanceId))
            {
                var ts = go.GetComponent<PseudoPrefabTeleportalStub>();
                LayoutEditorLog.LogWarning("[LayoutEditor] Apply Teleportal: 传送门未配置出口（exitPortal），" +
                    "已降级为普通道具: " + go.name);
                LayoutEditorCookingUtensilGuard.DowngradeToBase(go, ts != null ? ts.pseudoPrefabSO : null);
                return;
            }
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
            // 防御：真实 prefab 不是锅具容器（无 IngredientContainer，如喷雾/搅拌工作站被
            // 误配成锅具）时，宿主 PseudoPrefabCookingUtensil.Setup 会 NRE。跳过锅具参数，
            // 保持基础道具（与 CookingUtensilGuard 的降级逻辑一致）。喷雾喷罐按 prefabName
            // 直接排除（编辑模式 bundle 未加载时按 SO 判定，不依赖容器检查）。
            var probeSo = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            var probeBase = FindExactBaseStub(go);
            var cuSo = probeSo != null && probeSo.pseudoPrefabSO != null
                ? probeSo.pseudoPrefabSO
                : (probeBase != null ? probeBase.pseudoPrefabSO : null);
            if (cuSo != null &&
                (LayoutEditorCookingUtensilGuard.IsIngredientSpray(cuSo) ||
                 !LayoutEditorCookingUtensilGuard.RealPrefabHasIngredientContainer(cuSo)))
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] 跳过锅具参数：该道具的真实 prefab 不是锅具容器" +
                    "（无 IngredientContainer）: " + go.name + "（" + cuSo.name + "）。如需多食材容器请换用锅/搅拌碗/烤盘等。");
                return;
            }

            // common03 厨具变体 wrapper（火锅大锅/烤盘/DLC 锅具）只带基础 PseudoPrefabStub
            // 与基类 PseudoPrefab（Setup 空操作）：补挂派生 stub（拷贝 SO）并换派生运行时
            // 组件，否则「锅具参数/额外食材」被静默丢弃、运行时不生效——与容器堆
            // （CleanPlateStack 分支）同模式的修复。
            var utensil = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            var baseStub = FindExactBaseStub(go);
            if (utensil == null)
            {
                utensil = Undo.AddComponent<PseudoPrefabCookingUtensilStub>(go);
                if (baseStub != null && baseStub.pseudoPrefabSO != null)
                    utensil.pseudoPrefabSO = baseStub.pseudoPrefabSO;
            }

            Undo.RecordObject(utensil, "Layout Editor Cooking Utensil");
            // capacity<=0（前端漏传/旧文档）不直接覆盖：写 0 会让锅具一个食材都放不进
            // （stub Setup 会把它写进 IngredientContainer.m_capacity）。
            // 已设置食材列表但容量无效时，按原版默认表兜底（bundle 实测：
            // 汤锅=3、搅拌碗/搅拌杯/烤盘=4、烤串=3、其余=1）。
            if (item.cookingUtensil.capacity > 0)
                utensil.capacity = item.cookingUtensil.capacity;
            else if (utensil.capacity <= 0 &&
                     item.cookingUtensil.allowedIngredientGuids != null &&
                     item.cookingUtensil.allowedIngredientGuids.Length > 0)
            {
                utensil.capacity = NativeUtensilCapacity(go);
            }
            if (item.cookingUtensil.allowedIngredientGuids != null)
            {
                var sos = LoadIngredientSOs(item.cookingUtensil.allowedIngredientGuids);
                // 必须走 SerializedObject 写数组：直接字段赋值在 prefab 实例上收缩数组时，
                // Array.size 的 property mod 会残留旧长度（Unity 2017），尾部槽位落回默认
                // null —— 搅拌碗出现「食材变 None」且无论写回多少次都无法自愈的根因。
                // SerializedObject 显式设置 arraySize 会正确更新 size mod 并清理孤儿条目。
                var serialized = new SerializedObject(utensil);
                var prop = serialized.FindProperty("allowedIngredientSOs");
                prop.arraySize = sos.Length;
                for (int i = 0; i < sos.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = sos[i];
                serialized.ApplyModifiedProperties();
            }

            // 容量最终兜底：新补挂的 stub capacity 为 0，而派生 Setup() 会把它
            // 无条件写进 IngredientContainer.m_capacity——0 = 锅具什么都放不进。
            if (utensil.capacity <= 0)
                utensil.capacity = NativeUtensilCapacity(go);

            // 基础 stub 的 SO 已被派生 stub 接管：移除基础组件，保证
            // PseudoPrefab.Awake 的 GetComponent<PseudoPrefabStub>() 命中派生 stub。
            if (baseStub != null && baseStub != utensil)
                Undo.DestroyObjectImmediate(baseStub);

            // 运行时组件：基类 PseudoPrefab（Setup 空操作）换成派生 PseudoPrefabCookingUtensil
            // （Setup 把容量/额外食材写进运行时容器）。
            var runtimeUtensil = go.GetComponent<LevelEditor.PseudoPrefabCookingUtensil>();
            var baseRuntime = FindExactBaseRuntime(go);
            if (baseRuntime != null && baseRuntime != runtimeUtensil)
                Undo.DestroyObjectImmediate(baseRuntime);
            if (runtimeUtensil == null)
                Undo.AddComponent<LevelEditor.PseudoPrefabCookingUtensil>(go);
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

        if (item.stubKind == "ServingStation")
        {
            // 上菜台变体（dlc13_workstation_plate_station 等）wrapper 无专属 stub：补挂
            // （未绑定回收台时不走二阶段，须在此保证组件存在，否则运行时不工作）
            if (go.GetComponent<PseudoPrefabServingStationStub>() == null)
            {
                var servingStub = Undo.AddComponent<PseudoPrefabServingStationStub>(go);
                var baseStub = go.GetComponent<PseudoPrefabStub>();
                if (baseStub != null && baseStub.pseudoPrefabSO != null)
                    servingStub.pseudoPrefabSO = baseStub.pseudoPrefabSO;
            }
            return;
        }

        if ((item.stubKind == "PlateReturn" || item.stubKind == "GlassReturn") && item.plateReturn != null)
        {
            var returnStation = go.GetComponent<PseudoPrefabPlateReturnStub>();
            if (returnStation == null)
            {
                // 回收台 DLC 变体（workstation_mug_return / dlc13 plate_return 等）的
                // common03 wrapper 只有基础 PseudoPrefabStub——缺组件时补挂，否则
                // 上菜台绑定二阶段找不到 stub，绑定被静默丢弃。
                returnStation = Undo.AddComponent<PseudoPrefabPlateReturnStub>(go);
                var baseStub = go.GetComponent<PseudoPrefabStub>();
                if (baseStub != null && baseStub.pseudoPrefabSO != null)
                    returnStation.pseudoPrefabSO = baseStub.pseudoPrefabSO;
            }

            Undo.RecordObject(returnStation, "Layout Editor Plate Return");
            returnStation.returnClean = item.plateReturn.returnClean;
            return;
        }

        if (item.stubKind == "CleanPlateStack")
        {
            // 容器堆变体（餐盘堆/马克杯堆/玻璃杯堆 wrapper）只带基础 PseudoPrefabStub +
            // 基类 PseudoPrefab：补齐核心堆同款的「派生 stub + 派生运行时组件」组合。
            // 堆类游戏 prefab 本身无网格（dump 实测无 .obj），可见的盘/杯完全由
            // PseudoPrefabCleanPlateStack.Setup() 逐个实例化——基类组件的 Setup() 是
            // 空操作，缺派生运行时组件 = 写回后场景里空无一物（餐盘/马克杯不显示的根因）。
            // dto 缺失（autofill 放置未配参数）也进入：数量默认 5、容器 SO 按堆类型推断。
            var plateStack = go.GetComponent<PseudoPrefabCleanPlateStackStub>();
            var baseStub = FindExactBaseStub(go);
            if (plateStack == null)
            {
                plateStack = Undo.AddComponent<PseudoPrefabCleanPlateStackStub>(go);
                if (baseStub != null && baseStub.pseudoPrefabSO != null)
                    plateStack.pseudoPrefabSO = baseStub.pseudoPrefabSO;
            }

            Undo.RecordObject(plateStack, "Layout Editor Clean Plate Stack");
            var count = item.cleanPlateStack != null ? item.cleanPlateStack.plateCount : 0;
            plateStack.plateCount = count > 0 ? count : 5;
            if (item.cleanPlateStack != null && !string.IsNullOrEmpty(item.cleanPlateStack.platePrefabGuid))
            {
                plateStack.platePseudoPrefabSO = LoadPseudoPrefabSO(item.cleanPlateStack.platePrefabGuid);
            }
            else if (plateStack.platePseudoPrefabSO == null)
            {
                // 未指定容器 SO 时按堆类型推断（堆 → 对应容器本体 SO）
                var so = DefaultPlateSOForStack(go);
                if (so != null)
                    plateStack.platePseudoPrefabSO = so;
            }

            // 防御：盘子 SO 缺失或真实 prefab 无 EditorGridSnap 时，宿主
            // PseudoPrefabCleanPlateStack.Setup 逐盘实例化会 NRE（如脏堆的 mesh 占位）。
            // 降级为普通道具（脏堆本身是自包含 DirtyPlateStack，无需走此流程）。
            if (plateStack.platePseudoPrefabSO == null ||
                !LayoutEditorCookingUtensilGuard.PlatePrefabHasGridSnap(plateStack.platePseudoPrefabSO))
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] Apply CleanPlateStack: 容器 SO 缺失或盘子" +
                    " prefab 无 EditorGridSnap，已降级为普通道具: " + go.name);
                LayoutEditorCookingUtensilGuard.DowngradeToBase(go, plateStack.pseudoPrefabSO);
                return;
            }

            // 基础 stub 的 SO 已被派生 stub 接管：移除基础组件（含旧写法残留的双 stub），
            // 保证 PseudoPrefab.Awake 的 GetComponent<PseudoPrefabStub>() 命中派生 stub，
            // 派生 Setup() 里的强转不再失败。
            if (baseStub != null && baseStub != plateStack)
                Undo.DestroyObjectImmediate(baseStub);

            // 运行时组件：基类 PseudoPrefab（Setup 空操作）换成派生 PseudoPrefabCleanPlateStack。
            var runtimeStack = go.GetComponent<LevelEditor.PseudoPrefabCleanPlateStack>();
            var baseRuntime = FindExactBaseRuntime(go);
            if (baseRuntime != null && baseRuntime != runtimeStack)
                Undo.DestroyObjectImmediate(baseRuntime);
            if (runtimeStack == null)
                Undo.AddComponent<LevelEditor.PseudoPrefabCleanPlateStack>(go);
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

        if ((item.stubKind == "Switch" || item.stubKind == "CannonSwitch") && item.switchStub != null)
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

        if (item.stubKind == "Terminal")
        {
            // 终端未配置 pilotableObject：宿主 PseudoPrefabTerminal.Setup 抛
            // UnassignedReferenceException。降级为普通道具。
            if (item.terminal == null)
            {
                var ts = go.GetComponent<PseudoPrefabTerminalStub>();
                LayoutEditorLog.LogWarning("[LayoutEditor] Apply Terminal: 终端未配置可操控对象，" +
                    "已降级为普通道具: " + go.name);
                LayoutEditorCookingUtensilGuard.DowngradeToBase(go, ts != null ? ts.pseudoPrefabSO : null);
                return;
            }
            var terminal = go.GetComponent<PseudoPrefabTerminalStub>();
            if (terminal == null)
                return;

            // 文档 id（"new:..."）指向本趟写回新建的对象：第一趟尚未创建，
            // 交给第二趟 ApplyTerminalPilotable 解析，这里不降级。
            var rid0 = item.terminal.pilotableObjectInstanceId ?? "";
            if (rid0.StartsWith("new:", StringComparison.Ordinal))
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
            // 大炮等伪 prefab：PilotMovement 在 bundle child 上，而宿主
            // PseudoPrefabTerminal.Setup 用同对象 GetComponent<PilotMovement>()，
            // 伪根拿不到 → 重定向到携带 PilotMovement 的 child。
            RedirectPilotableToChild(terminal);
            if (terminal.pilotableObject == null)
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] Apply Terminal: 终端可操控对象无法解析，" +
                    "已降级为普通道具: " + go.name);
                LayoutEditorCookingUtensilGuard.DowngradeToBase(go, terminal.pseudoPrefabSO);
                return;
            }
        }

        if (item.stubKind == "Cannon" && item.cannon != null)
        {
            // 大炮：Cannon/PilotRotation 在 bundle child 上；freeRotation=true 写 ±180°
            //（360° 自由旋转），false 恢复 prefab 默认 ±45°（固定小角度）。
            var cannonComp = go.GetComponentInChildren<Cannon>();
            var pilotRot = cannonComp != null ? cannonComp.GetComponent<PilotRotation>() : null;
            if (pilotRot == null)
                return;
            Undo.RecordObject(pilotRot, "Layout Editor Cannon");
            if (item.cannon.freeRotation)
            {
                pilotRot.m_minLimitDegrees = -180f;
                pilotRot.m_maxLimitDegrees = 180f;
            }
            else
            {
                pilotRot.m_minLimitDegrees = -45f;
                pilotRot.m_maxLimitDegrees = 45f;
            }
            return;
        }

        if (item.stubKind == "MeshWithMaterial" && item.meshWithMaterial != null)
        {
            var mm = go.GetComponent<PseudoPrefabMeshWithMaterialStub>();
            if (mm == null)
                return;

            Undo.RecordObject(mm, "Layout Editor MeshWithMaterial");
            if (!string.IsNullOrEmpty(item.meshWithMaterial.pseudoPrefabGuid))
                mm.pseudoPrefabSO = LoadPseudoPrefabSO(item.meshWithMaterial.pseudoPrefabGuid);
            if (!string.IsNullOrEmpty(item.meshWithMaterial.materialGuid))
                mm.materialSO = LoadPseudoPrefabSO(item.meshWithMaterial.materialGuid);
            return;
        }

    }

    /// <summary>大炮等伪 prefab 的 PilotMovement 在 bundle child 上，而宿主
    /// PseudoPrefabTerminal.Setup 用同对象 GetComponent&lt;PilotMovement&gt;()：
    /// pilotable 伪根拿不到组件时重定向到携带 PilotMovement 的 child。</summary>
    private static void RedirectPilotableToChild(PseudoPrefabTerminalStub terminal)
    {
        if (terminal.pilotableObject == null
            || terminal.pilotableObject.GetComponent<PilotMovement>() != null)
            return;
        var pp = terminal.pilotableObject.GetComponent<PseudoPrefab>();
        if (pp != null && pp.childGameObject != null
            && pp.childGameObject.GetComponent<PilotMovement>() != null)
            terminal.pilotableObject = pp.childGameObject;
    }

    /// <summary>Second pass: resolve a Terminal's pilotable object. The id is either
    /// "u:&lt;instanceID&gt;" (existing scene object) or a document instanceId
    /// ("new:...") mapped through createdObjects (created this write pass).
    /// Unresolvable targets downgrade the terminal to a plain prop (host Setup
    /// would otherwise throw).</summary>
    public static void ApplyTerminalPilotable(GameObject go, string pilotableInstanceId, System.Collections.Generic.Dictionary<string, GameObject> createdObjects)
    {
        if (go == null || string.IsNullOrEmpty(pilotableInstanceId))
            return;

        var terminal = go.GetComponent<PseudoPrefabTerminalStub>();
        if (terminal == null)
            return;

        GameObject target = null;
        if (pilotableInstanceId.StartsWith("u:", StringComparison.Ordinal))
        {
            int id;
            if (int.TryParse(pilotableInstanceId.Substring(2), out id))
                target = EditorUtility.InstanceIDToObject(id) as GameObject;
        }
        else if (createdObjects != null)
        {
            createdObjects.TryGetValue(pilotableInstanceId, out target);
        }

        Undo.RecordObject(terminal, "Layout Editor Terminal");
        terminal.pilotableObject = target;
        RedirectPilotableToChild(terminal);
        if (terminal.pilotableObject == null)
        {
            LayoutEditorLog.LogWarning("[LayoutEditor] Apply Terminal: 终端可操控对象无法解析，" +
                "已降级为普通道具: " + go.name);
            LayoutEditorCookingUtensilGuard.DowngradeToBase(go, terminal.pseudoPrefabSO);
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
        {
            // 上菜台变体（dlc13_workstation_plate_station 等）wrapper 无专属 stub：补挂
            serving = Undo.AddComponent<PseudoPrefabServingStationStub>(go);
            var baseStub = go.GetComponent<PseudoPrefabStub>();
            if (baseStub != null && baseStub.pseudoPrefabSO != null)
                serving.pseudoPrefabSO = baseStub.pseudoPrefabSO;
        }

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

                if (target == null)
                {
                    LayoutEditorLog.LogWarning("[LayoutEditor] 上菜台绑定丢弃：目标不在场景中 " + rid);
                    continue;
                }
                var stub = target.GetComponent<PseudoPrefabPlateReturnStub>();
                if (stub == null)
                {
                    // 回收台变体 wrapper 无专属 stub（首次写回未走过 ApplyStub 分支）：
                    // 有基础 PseudoPrefabStub 即补挂，绑定不再因缺组件被丢弃。
                    var targetBase = target.GetComponent<PseudoPrefabStub>();
                    if (targetBase == null)
                    {
                        LayoutEditorLog.LogWarning("[LayoutEditor] 上菜台绑定丢弃：目标不是伪预制件 " + target.name);
                        continue;
                    }
                    stub = Undo.AddComponent<PseudoPrefabPlateReturnStub>(target);
                    if (targetBase.pseudoPrefabSO != null)
                        stub.pseudoPrefabSO = targetBase.pseudoPrefabSO;
                }
                if (!resolved.Contains(stub))
                    resolved.Add(stub);
            }
        }

        Undo.RecordObject(serving, "Layout Editor ServingStation Returns");
        // 数组必须走 SerializedObject 写：直接字段赋值在 prefab 实例上收缩时
        // Array.size mod 残留旧长度，尾部槽位落回 null（同锅具食材 None 问题）。
        var serialized = new SerializedObject(serving);
        var prop = serialized.FindProperty("plateReturns");
        prop.arraySize = resolved.Count;
        for (int i = 0; i < resolved.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = resolved[i];
        serialized.ApplyModifiedProperties();
        // Mirror the first binding to the legacy single field for older runtime paths.
        serving.plateReturn = resolved.Count > 0 ? resolved[0] : null;
    }

    /// <summary>
    /// Second pass: apply document-level switch links (断头台/饮料机/酱料机按钮触发).
    /// Both ends resolve like Teleportal ids: "u:<instanceID>" for existing scene
    /// objects, otherwise a document instanceId mapped through createdObjects.
    /// If the switch object lacks PseudoPrefabSwitchStub (e.g. bundle-backed button
    /// prefabs), the component is added so the link has somewhere to live.
    ///
    /// 数组字段必须走 SerializedObject 写：直接字段赋值在 prefab 实例上收缩时
    /// Array.size mod 残留旧长度，尾部槽位落回 null/旧值（同锅具食材 None、
    /// 上菜台回收台问题）——开关加多个断头台时第二个目标丢失的根因之一。
    ///
    /// 配了按钮事件组（buttonEvents）的开关抑制直发广播（objectToTrigger 置空，
    /// 按压只走事件组 helper），避免同一目标一次按压收到两次消息（饮料机跳档）；
    /// LayoutRuntimeSwitchLink.m_targetRoots 仍写全量目标——事件路径依赖它完成
    /// 伪根→child 转发与监听字段接线。仅 Setup 路径（包装带 PseudoPrefabSwitch）
    /// 抑制：非 Setup 按钮的直发由 linker 的 relay 承担且无法从场景数据侧关闭。
    /// </summary>
    public static void ApplySwitchLinks(LayoutSwitchLinkDto[] links,
        System.Collections.Generic.Dictionary<string, GameObject> createdObjects,
        LayoutButtonEventDataDto buttonEvents)
    {
        if (links == null || links.Length == 0)
            return;

        // Group targets per switch so multi-target buttons end up with one array write.
        var perSwitch = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<GameObject>>(StringComparer.Ordinal);
        var triggerBySwitch = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        var order = new System.Collections.Generic.List<string>();

        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.switchId) || string.IsNullOrEmpty(link.targetId))
                continue;

            var target = ResolveLinkedObject(link.targetId, createdObjects);
            if (target == null)
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] 开关联动丢弃：目标不在场景中 " +
                    link.targetId + "（开关 " + link.switchId + "）");
                continue;
            }

            System.Collections.Generic.List<GameObject> targets;
            if (!perSwitch.TryGetValue(link.switchId, out targets))
            {
                targets = new System.Collections.Generic.List<GameObject>();
                perSwitch[link.switchId] = targets;
                order.Add(link.switchId);
            }
            if (!targets.Contains(target))
                targets.Add(target);
            if (!string.IsNullOrEmpty(link.trigger))
                triggerBySwitch[link.switchId] = link.trigger;
        }

        // 配了事件组（组非空，与 ButtonEventBakery.BakeLink 的跳过条件一致）的触发源。
        // 全量写回以文档为准；作用域写回不带 buttonEvents，回退按场景里上次的
        // 烘焙痕迹判定（BEP_ 前缀是事件组按压触发名；移动组联动用 BLP_，不会误判）。
        var eventSources = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        bool docCarriesEvents = buttonEvents != null && buttonEvents.links != null;
        if (docCarriesEvents)
        {
            foreach (var l in buttonEvents.links)
            {
                if (l == null || string.IsNullOrEmpty(l.sourceId))
                    continue;
                if (l.groups == null || l.groups.Length == 0)
                    continue;
                eventSources.Add(l.sourceId);
            }
        }

        foreach (var switchId in order)
        {
            var switchGo = ResolveLinkedObject(switchId, createdObjects);
            if (switchGo == null)
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] 开关联动丢弃：开关不在场景中 " + switchId);
                continue;
            }

            var sw = switchGo.GetComponent<PseudoPrefabSwitchStub>();
            if (sw == null)
                sw = switchGo.AddComponent<PseudoPrefabSwitchStub>();

            string trigger;
            string resolvedTrigger = triggerBySwitch.TryGetValue(switchId, out trigger) && !string.IsNullOrEmpty(trigger)
                ? trigger
                : "Switch";

            bool hasEvents = eventSources.Contains(switchId);
            if (!docCarriesEvents)
            {
                var wiredPress = sw.triggerOnAnimator ?? "";
                hasEvents = wiredPress.StartsWith("BEP_", StringComparison.Ordinal);
            }
            bool setupPath = switchGo.GetComponent<LevelEditor.PseudoPrefabSwitch>() != null;
            GameObject[] directTargets = hasEvents && setupPath
                ? new GameObject[0]
                : perSwitch[switchId].ToArray();

            Undo.RecordObject(sw, "Layout Editor Switch Link");
            var swSo = new SerializedObject(sw);
            var trigProp = swSo.FindProperty("triggerOnObject");
            if (trigProp != null)
                trigProp.stringValue = resolvedTrigger;
            var arrProp = swSo.FindProperty("objectToTrigger");
            if (arrProp != null)
            {
                arrProp.arraySize = directTargets.Length;
                for (int i = 0; i < directTargets.Length; i++)
                    arrProp.GetArrayElementAtIndex(i).objectReferenceValue = directTargets[i];
                swSo.ApplyModifiedProperties();
            }
            else
            {
                // 兜底：字段改名时不至于整体丢失
                sw.triggerOnObject = resolvedTrigger;
                sw.objectToTrigger = directTargets;
            }

            // 游戏编译的运行时接线组件（随场景保存，游戏包内也生效）：完成伪根→child
            // 触发转发、机器监听字段、大炮 Cannon.m_button / 瞄准 / 发射按钮门控。
            // 类在游戏程序集（Assembly-CSharp，经 Assembly-CSharp-Patch 编译）。
            var linker = switchGo.GetComponent<LayoutRuntimeSwitchLink>();
            if (linker == null)
                linker = switchGo.AddComponent<LayoutRuntimeSwitchLink>();
            Undo.RecordObject(linker, "Layout Editor Switch Link");
            var lkSo = new SerializedObject(linker);
            var lkTrig = lkSo.FindProperty("m_trigger");
            if (lkTrig != null)
                lkTrig.stringValue = resolvedTrigger;
            var rootsProp = lkSo.FindProperty("m_targetRoots");
            if (rootsProp != null)
            {
                GameObject[] roots = perSwitch[switchId].ToArray();
                rootsProp.arraySize = roots.Length;
                for (int i = 0; i < roots.Length; i++)
                    rootsProp.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
                lkSo.ApplyModifiedProperties();
            }
            else
            {
                linker.m_trigger = resolvedTrigger;
                linker.m_targetRoots = perSwitch[switchId].ToArray();
            }
        }
    }

    /// <summary>
    /// 世界地图装饰展开组件烘焙（随场景保存，游戏包内生效）。
    ///
    /// 背景：dlc08 map_* 装饰（bundle 内 dressing assets/map/ 家族，如
    /// p_dlc08_map_rope_fence_* 绳栏）的 prefab 带 WorldMapSceneryOptimizer：
    /// 其 Awake() 把整个可视 Mesh 子树 SetActive(false)，只有世界地图场景的
    /// 展开流程会重新激活；关卡场景里展开永不触发——编辑器可见、Play/游戏包内
    /// 整件消失。这里按伪 prefab SO 的 bundle assetPath 含 "/map/" 识别该家族，
    /// 给伪根烘焙 LayoutRuntimeWorldMapDressing（游戏编译），运行时 child 就绪后
    /// 强制 End(Unfold)。map 家族里的纯网格件（如 p_dlc08_cloud_clump）会被一并
    /// 烘焙但运行时自然空转，无副作用。编辑器 Play 另有
    /// LayoutEditorWorldMapDressingPatch 兜底未重新烘焙的旧场景。
    /// </summary>
    public static void BakeWorldMapDressing()
    {
        int added = 0;
        foreach (var stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub == null)
                continue;
            var so = stub.pseudoPrefabSO;
            if (so == null || string.IsNullOrEmpty(so.assetPath))
                continue;
            if (!so.assetPath.Contains("/map/"))
                continue;
            var go = stub.gameObject;
            if (go == null || go.GetComponent<LayoutRuntimeWorldMapDressing>() != null)
                continue;
            go.AddComponent<LayoutRuntimeWorldMapDressing>();
            added++;
        }
        if (added > 0)
            LayoutEditorLog.Log("[LayoutEditor] 世界地图装饰展开组件烘焙：" + added + " 个伪根");
    }

    private static GameObject ResolveLinkedObject(string id, System.Collections.Generic.Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (id.StartsWith("u:", StringComparison.Ordinal))
        {
            int instanceId;
            if (int.TryParse(id.Substring(2), out instanceId))
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            return null;
        }
        GameObject go = null;
        if (createdObjects != null)
            createdObjects.TryGetValue(id, out go);
        return go;
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

    /// <summary>酱料机 / 饮料机：编辑器按食材箱处理，可绑定特定酱料/饮料。</summary>
    private static bool IsSpecialDispenserPrefabId(string prefabId)
    {
        return prefabId == "dlc08_drink_machine" || prefabId == "dlc11_drink_dispenser"
            || prefabId == "dlc08_condiment_dispenser" || prefabId == "dlc11_condiment_dispenser";
    }

    /// <summary>导出 PseudoPrefabSOArray（饮料机/酱料机的多选循环列表）为 dto；
    ///  无组件或列表为空时不写。供 ExportStub 各提前 return 的分支复用。</summary>
    private static void ExportSoArrayIfPresent(GameObject go, LayoutItemDto item)
    {
        var soArray = go.GetComponent<PseudoPrefabSOArray>();
        if (soArray == null || soArray.pseudoPrefabSOs == null || soArray.pseudoPrefabSOs.Length == 0)
            return;
        var guids = new string[soArray.pseudoPrefabSOs.Length];
        for (int i = 0; i < soArray.pseudoPrefabSOs.Length; i++)
            guids[i] = soArray.pseudoPrefabSOs[i] != null
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(soArray.pseudoPrefabSOs[i]))
                : string.Empty;
        item.soArray = new LayoutSOArrayStubDto { pseudoPrefabGuids = guids };
    }

    /// <summary>导出基础 PseudoPrefabStub.pseudoPrefabSO 的 guid（外观皮肤）。
    /// 本分支提前 return 的 stub 类型（Dispenser/ServingStation/PlateReturn/Conveyor 等）
    /// 走不到方法末尾的通用导出——漏掉时 web 重新加载丢失已设置的外观皮肤
    /// （写回侧 ApplyStub 的外观应用位于所有分支之前，不受影响）。</summary>
    private static void ExportPseudoPrefabGuidIfPresent(GameObject go, LayoutItemDto item)
    {
        var pseudoStub = go.GetComponent<PseudoPrefabStub>();
        if (pseudoStub != null && pseudoStub.pseudoPrefabSO != null)
            item.pseudoPrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pseudoStub.pseudoPrefabSO));
    }

    private static PseudoPrefabSO[] LoadPseudoPrefabSOs(string[] guids)
    {
        var sos = new PseudoPrefabSO[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            sos[i] = LoadPseudoPrefabSO(guids[i]);
        return sos;
    }

    /// <summary>查找「恰好为基类类型」的 PseudoPrefabStub（派生类实例不算）：
    ///  common03 wrapper prefab 自带基础 stub，补挂派生 stub 后需移除基础组件，
    ///  而 GetComponent&lt;PseudoPrefabStub&gt;() 会多态命中派生实例，无法直接定位。</summary>
    private static PseudoPrefabStub FindExactBaseStub(GameObject go)
    {
        foreach (var s in go.GetComponents<PseudoPrefabStub>())
            if (s != null && s.GetType() == typeof(PseudoPrefabStub))
                return s;
        return null;
    }

    /// <summary>查找「恰好为基类类型」的 PseudoPrefab（派生类实例不算），用途同上。</summary>
    private static LevelEditor.PseudoPrefab FindExactBaseRuntime(GameObject go)
    {
        foreach (var p in go.GetComponents<LevelEditor.PseudoPrefab>())
            if (p != null && p.GetType() == typeof(LevelEditor.PseudoPrefab))
                return p;
        return null;
    }

    /// <summary>容器堆 id → 容器本体 SO id（均在 common03/pseudo_prefab_so/{dlc|core}/utensils 下）。
    ///  堆道具 wrapper 补挂派生 stub 后 platePseudoPrefabSO 按堆类型推断：
    ///  同 DLC 皮肤优先（dlc09 马克杯堆 → dlc09 马克杯），脏堆 → 脏容器本体；
    ///  common03 无 dlc02 单脏杯，脏玻璃杯堆借用 dlc11 脏玻璃杯（同形网格）。</summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> StackPlateSoIds =
        new System.Collections.Generic.Dictionary<string, string>
        {
            // 干净堆 → 同皮肤干净容器
            { "cleanmugstack", "equipment_mug_01" },
            { "dlc09_cleanmugstack", "dlc09_equipment_mug_01" },
            { "dlc08_cleantraystack", "dlc08_equipment_tray" },
            { "cleanglassstack", "equipment_glass_01" },
            { "dlc11_cleanglassstack", "dlc11_equipment_glass_01" },
            // 脏堆 → 脏容器
            { "dirtymugstack", "dirtymug" },
            { "dlc09_dirtymugstack", "dlc09_dirtymug" },
            { "dlc08_dirtytraystack", "dlc08_dirtytray" },
            { "dirtyglassstack", "dlc11_dirtyglass" },
            { "dlc11_dirtyglassstack", "dlc11_dirtyglass" }
        };

    private static PseudoPrefabSO LoadUtensilSo(string soId)
    {
        if (string.IsNullOrEmpty(soId))
            return null;
        // prefab/pseudo 已按 dlc 分目录（prefabs/{dlc|core}/{category}/），此处全库递归查找。
        var absRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Assets/common03/pseudo_prefab_so"));
        if (!System.IO.Directory.Exists(absRoot))
            return null;
        var files = System.IO.Directory.GetFiles(absRoot, soId + ".asset", System.IO.SearchOption.AllDirectories);
        if (files.Length == 0)
            return null;
        var rel = "Assets/common03/pseudo_prefab_so" + files[0].Substring(absRoot.Length).Replace('\\', '/');
        return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(rel);
    }

    /// <summary>容器堆 → 对应容器本体 SO（common03/pseudo_prefab_so）。
    ///  精确表（StackPlateSoIds）优先，未知堆 id 走子串兜底（脏堆先匹配脏容器，
    ///  避免「脏杯堆里摆干净杯」）。</summary>
    private static PseudoPrefabSO DefaultPlateSOForStack(GameObject go)
    {
        var pid = !string.IsNullOrEmpty(go.name) ? go.name.Replace("(Clone)", "") : "";
        if (string.IsNullOrEmpty(pid))
            return null;

        string soId;
        if (StackPlateSoIds.TryGetValue(pid, out soId))
            return LoadUtensilSo(soId);

        if (pid.IndexOf("dirtymug", StringComparison.OrdinalIgnoreCase) >= 0)
            return LoadUtensilSo("dirtymug");
        if (pid.IndexOf("dirtytray", StringComparison.OrdinalIgnoreCase) >= 0)
            return LoadUtensilSo("dlc08_dirtytray");
        if (pid.IndexOf("dirtyglass", StringComparison.OrdinalIgnoreCase) >= 0)
            return LoadUtensilSo("dlc11_dirtyglass");
        if (pid.IndexOf("tray", StringComparison.OrdinalIgnoreCase) >= 0)
            return LoadUtensilSo("dlc08_equipment_tray");
        if (pid.IndexOf("mug", StringComparison.OrdinalIgnoreCase) >= 0)
            return LoadUtensilSo("equipment_mug_01");
        if (pid.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0)
            return LoadUtensilSo("equipment_glass_01");
        return null;
    }

    /// <summary>锅具原版默认容量（bundle 实测 IngredientContainer.m_capacity）：
    ///  汤锅 Pot=3、搅拌碗 MixerBowl/搅拌杯 BlenderCup/烤盘 GriddlePan=4、烤串 Skewer=3、
    ///  火锅大锅（utensil_large_pot_01，bundle226）=4、烤菜烤盘（utensil_roasting_tray，
    ///  bundle297）=4、其余（煎锅/炸篮/蒸锅等）=1。用于前端漏传 capacity 时的兜底。
    ///  注意 large_pot / roasting_tray 必须先于 pot 子串判断。</summary>
    private static int NativeUtensilCapacity(GameObject go)
    {
        var pid = !string.IsNullOrEmpty(go.name) ? go.name.Replace("(Clone)", "") : "";
        return NativeUtensilCapacityForId(pid);
    }

    /// <summary>按 prefab id 查原版默认容量（utensil guard 等复用）。</summary>
    public static int NativeUtensilCapacityForId(string prefabId)
    {
        var pid = prefabId ?? "";
        if (pid.IndexOf("large_pot", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
        if (pid.IndexOf("roasting_tray", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
        if (pid.IndexOf("pot", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        if (pid.IndexOf("mixer", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pid.IndexOf("blender", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pid.IndexOf("griddle", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
        if (pid.IndexOf("skewer", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        return 1;
    }

    /// <summary>allowedIngredientSOs is ScriptableObject[] and may hold CustomRecipeSO
    ///  (not a PseudoPrefabSO) — load with the base type so custom recipes survive.</summary>
    private static ScriptableObject LoadIngredientSO(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
    }

    private static ScriptableObject[] LoadIngredientSOs(string[] guids)
    {
        if (guids == null)
            return null;
        // 空/失效 guid（前端残留、迁移断裂等）在 LoadIngredientSO 里解不出 SO：
        // 丢弃而不是写成 null，避免 allowedIngredientSOs 出现一排 None。
        var sos = new System.Collections.Generic.List<ScriptableObject>();
        for (int i = 0; i < guids.Length; i++)
        {
            var so = LoadIngredientSO(guids[i]);
            if (so == null)
            {
                if (!string.IsNullOrEmpty(guids[i]))
                    LayoutEditorLog.LogWarning("[LayoutEditor] 锅具额外食材 guid 无法解析为 ScriptableObject（已忽略）: " + guids[i]);
                continue;
            }
            sos.Add(so);
        }
        return sos.ToArray();
    }
}
