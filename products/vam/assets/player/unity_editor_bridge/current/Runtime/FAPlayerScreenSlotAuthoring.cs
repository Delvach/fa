using UnityEngine;

namespace FrameAngel.UnityEditorBridge
{
    public sealed class FAPlayerScreenSlotAuthoring : MonoBehaviour
    {
        public string slotId = "main";
        public string surfaceTargetId = "player:screen";
        public string disconnectStateId = "";
        public Transform screenSurface;
        public Transform screenGlass;
        public Transform screenAperture;
        public Transform disconnectSurface;
    }
}
