using System;
using UnityEngine;

/// <summary>关卡集自定义内容索引（Assets/LevelSets/&lt;set&gt;/custom_content/Config.asset）。
/// 只存管理页需要的轻量元数据；真正的参数在 prefab/stub 上。Config 显式不赋 bundle 名，
/// 不随 custom_content bundle 分发。</summary>
public class CustomContentConfigSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string kind;
        public string nameZh;
        public string nameEn;
        public string templateId;
        public string prefabPath;
        public string nodePath;
        public string iconPath;
        public string createdAt;
        public string updatedAt;
    }

    public Entry[] entries = new Entry[0];
}
