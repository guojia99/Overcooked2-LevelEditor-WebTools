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

        [Serializable]
        public class CustomRecipeCategoryEntry
        {
            public string id;
            public string zh;
            public string en;
        }
    }
}
