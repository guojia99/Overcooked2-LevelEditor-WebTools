using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace LevelEditorStub
{
    [CreateAssetMenu(menuName = "LevelEditor/CustomRecipeConfigSO")]
    public class CustomRecipeConfigSO : ScriptableObject
    {
        [SerializeField] public int uidPrefix;
        [SerializeField] public int nextSequence = 1;
        [SerializeField] public CustomRecipeCategoryEntry[] categories = new CustomRecipeCategoryEntry[0];
        /// <summary>菜谱模型变换（modelScale/modelRotationY）。存放在插件自己的配置中，
        ///  避免修改宿主项目 CustomRecipeSO 类定义。</summary>
        [SerializeField] public CustomRecipeTransformEntry[] modelTransforms = new CustomRecipeTransformEntry[0];

        [Serializable]
        public class CustomRecipeCategoryEntry
        {
            public string id;
            public string zh;
            public string en;
        }

        [Serializable]
        public class CustomRecipeTransformEntry
        {
            public string assetPath;
            public float scale = 1f;
            public float rotationY;
            public float rotationX;
            public float rotationZ;
            public float positionX;
            public float positionY;
            public float positionZ;
        }
    }
}
