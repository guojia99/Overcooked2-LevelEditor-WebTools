using System;

[Serializable]
public class CustomContentTemplateDto
{
    public string id;
    public string kind;
    public string labelZh;
    public string labelEn;
    public string assetPath;
    public string nodePath;
}

[Serializable]
public class CustomContentTemplateListDto
{
    public CustomContentTemplateDto[] templates;
}

[Serializable]
public class CustomContentItemDto
{
    public string id;
    public string kind;
    public string nameZh;
    public string nameEn;
    public string templateId;
    public string assetPath;
    public string guid;
    public string nodePath;
    public string iconUrl;
    public string updatedAt;
    public string bundleName;
    public string[] warnings;
    public int orderUid;
    public int capacity;
    public int splitNum;
    public string[] allowedIngredientGuids;
    public string[] stationTypes;
    public string[] modelGuids;
    public string[] modelSoGuids;
    public string platingStepId;
    public string restrictionsGuid;
    public string cosmeticsPrefabGuid;
    public string nextPrefabGuid;
    public string progressUiGuid;
    public string fireEffectGuid;
    public string chopPfxGuid;
    public string tutorialIconGuid;
    public string ingredientContentUiGuid;
    public string sludgeGuid;
    public string spawnEffectGuid;
    public string washingPartPath;
    public string dryingPartPath;
    public string cleanPlateStackGuid;
    public string traySlotGuid;
    public string inputRecipeGuid;
    public string outputPrefabGuid;
    public string splitAudioTag;

    // 模型变换回读（WebCustomModel 子物体）
    public bool hasModel;
    public float modelScale;
    public float modelPositionX;
    public float modelPositionY;
    public float modelPositionZ;
    public float modelRotationX;
    public float modelRotationY;
    public float modelRotationZ;
    public float modelPivotX;
    public float modelPivotY;
    public float modelPivotZ;
}

[Serializable]
public class CustomContentListDto
{
    public string setName;
    public CustomContentItemDto[] items;
}

[Serializable]
public class CustomContentListRequest
{
    public string setName;
    public string kind;
}

[Serializable]
public class CustomContentEditDto
{
    public string setName;
    public string kind;
    public string id;
    public string assetPath;
    public string nameZh;
    public string nameEn;
    public string templateId;

    // 食材 / 备餐容器
    public int orderUid;
    public string platingStepId;
    public string restrictionsGuid;
    public string cosmeticsPrefabGuid;
    public string nextPrefabGuid;

    // 工作台 / 锅具 / 水槽 / 托盘
    public int capacity;
    public string[] allowedIngredientGuids;
    public string[] stationTypes;
    public string[] modelGuids;
    public string[] modelSoGuids;
    public string washingPartPath;
    public string dryingPartPath;
    public string cleanPlateStackGuid;
    public string traySlotGuid;
    public string progressUiGuid;
    public string fireEffectGuid;
    public string chopPfxGuid;
    public string tutorialIconGuid;
    public string sludgeGuid;
    public string spawnEffectGuid;
    public string ingredientContentUiGuid;

    // 分料机
    public string inputRecipeGuid;
    public string outputPrefabGuid;
    public int splitNum;
    public string splitAudioTag;

    // 引用字段显式清除名单（仅 update 消费；空 guid = 保持原值的历史语义不变）
    public string[] clearFields;

    // 资产上传（base64）
    public string iconBase64;
    public CustomContentUploadFileDto[] modelFiles;
    public float modelScale;
    public float modelPositionX;
    public float modelPositionY;
    public float modelPositionZ;
    public float modelRotationX;
    public float modelRotationY;
    public float modelRotationZ;
    public float modelPivotX;
    public float modelPivotY;
    public float modelPivotZ;
}

[Serializable]
public class CustomContentUploadFileDto
{
    public string fileName;
    public string base64;
}

[Serializable]
public class CustomContentUploadResultDto
{
    public bool ok;
    public string error;
    public string assetPath;
    public string guid;
}

[Serializable]
public class CustomContentCatalogDto
{
    public string setName;
    public string generatedAt;
    public CustomContentCatalogItemDto[] items;
}

[Serializable]
public class CustomContentCatalogItemDto
{
    public string id;
    public string guid;
    public string assetPath;
    public string category;
    public string nameZh;
    public string nameEn;
    public string defaultParent;
    public LayoutFootprint footprint;
    public bool needsStub;
    public string customKind;
    public string customStubKind;
}

[Serializable]
public class CustomContentStationTypeDto
{
    public string name;
    public string labelZh;
    public string labelEn;
}

[Serializable]
public class CustomContentMetaDto
{
    public CustomContentStationTypeDto[] stationTypes;
}

[Serializable]
public class CustomContentRefDto
{
    public string id;
    public string nameZh;
    public string nameEn;
    public string guid;
    public string assetPath;
    public string kind;
}

[Serializable]
public class CustomContentOptionsDto
{
    public string setName;
    public CustomContentRefDto[] prefabs;
    public CustomContentRefDto[] soRefs;
    public CustomContentRefDto[] modelPrefabs;
    public CustomContentRefDto[] modelSOs;
    public CustomContentRefDto[] orderLists;
    public CustomContentRefDto[] lookups;
    public CustomContentRefDto[] recipes;
    public CustomContentRefDto[] plating;
    public string[] audioTags;
}

[Serializable]
public class CustomContentHierarchyDto
{
    public string assetPath;
    public string[] paths;
}
