using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    public sealed class FAPlayerShellAuthoring : MonoBehaviour
    {
        public string shellId = "player";
        public string screenContractVersion = "frameangel_screen_contract_v1";
        public string defaultDisconnectStateId = "media_controls";
        public string surfaceTargetId = "player:screen";
    }
}
