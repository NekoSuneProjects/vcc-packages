using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// "You are here" map: moves a marker over a map picture to match the local player's position
    /// (top-down X/Z) and turns it with their view. Teleport buttons on the map use NekoUiAction.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoMap : UdonSharpBehaviour
    {
        [Tooltip("The map picture's RectTransform.")]
        public RectTransform mapArea;
        [Tooltip("The marker inside the map area (anchored at its bottom-left).")]
        public RectTransform marker;
        [Tooltip("World corner shown at the map's bottom-left (X, Z).")]
        public Vector2 worldMin = new Vector2(-50, -50);
        [Tooltip("World corner shown at the map's top-right (X, Z).")]
        public Vector2 worldMax = new Vector2(50, 50);
        public bool rotateWithView = true;

        private void Update()
        {
            var me = Networking.LocalPlayer;
            if (me == null || mapArea == null || marker == null) return;
            var pos = me.GetPosition();
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, pos.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, pos.z);
            var size = mapArea.rect.size;
            marker.anchorMin = marker.anchorMax = Vector2.zero;
            marker.anchoredPosition = new Vector2(u * size.x, v * size.y);
            if (rotateWithView)
            {
                float yaw = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
                marker.localRotation = Quaternion.Euler(0, 0, -yaw);
            }
        }
    }
}
