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
        /// <summary>二级分类（分类目录下再分一层子目录，如 Burger大全的
        ///  assembly/classic/deluxe/mega/breakfast/seafood/veggie/filling）。
        ///  归属以**目录**为准（ScanCustomRecipes 取 custom_recipes/ 下第二段目录名），
        ///  本表只提供显示名与排序，缺条目时前端回退显示目录名。</summary>
        [SerializeField] public CustomRecipeSubcategoryEntry[] subcategories = new CustomRecipeSubcategoryEntry[0];
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

        /// <summary>二级分类显示元数据。parent = 一级分类 id（如 "burger"）；
        ///  order 越小越靠前，相同 order 按 id 字典序。</summary>
        [Serializable]
        public class CustomRecipeSubcategoryEntry
        {
            public string id;
            public string parent;
            public string zh;
            public string en;
            public int order;
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
            /** 模型原点偏移（模型节点 localPosition，Unity 单位）：旋转/缩放绕偏移后的原点。 */
            public float pivotX;
            public float pivotY;
            public float pivotZ;
        }
    }
}
