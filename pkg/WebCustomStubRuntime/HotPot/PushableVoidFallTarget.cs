using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 可移动火锅载具标记（由 CustomStub.PushablePot 挂到载具上）。
    /// CustomStub.PushableVoidFall 只处理带此标记的对象，避免误伤其他 PushableObject。
    /// （接替原 LayoutPushableVoidFallTarget。）
    /// </summary>
    public class PushableVoidFallTarget : MonoBehaviour
    {
    }
}
