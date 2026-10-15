using UnityEngine;

namespace CustomStub
{
    /// <summary>子背包 → WebBackpackStorage 的运行时标记。
    /// 背包被背起后 child 由 PhysicalAttachment 重新挂到厨师身上（不再位于
    /// wrapper 之下），GetComponentInParent 寻址必然落空——一切前缀/兜底一律
    /// 先查本标记（与层级无关），marker 在容器装配时挂到 child 上。</summary>
    public class WebBackpackChild : MonoBehaviour
    {
        public WebBackpackStorage storage;
    }
}
