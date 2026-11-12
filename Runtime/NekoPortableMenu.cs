using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// A menu that opens near the player instead of being fixed in the world. Local: each player opens
    /// their own. Opens with a desktop key or a VR gesture; can stay put, follow the head or sit on a hand.
    /// Uses GetTrackingData to find the player's head and hands.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoPortableMenu : UdonSharpBehaviour
    {
        public const int StayWhereOpened = 0;
        public const int FollowHead = 1;
        public const int LeftHand = 2;
        public const int RightHand = 3;

        public const int GestureNone = 0;
        public const int GestureDoubleTapLeftTrigger = 1;
        public const int GestureDoubleJump = 2;

        [Tooltip("The panel (canvas) to show and move.")]
        public Transform menu;
        public int followMode = StayWhereOpened;
        [Tooltip("Distance in front of the eyes, in metres.")]
        public float distance = 0.6f;
        [Tooltip("Up/down offset from eye height, in metres.")]
        public float heightOffset = -0.12f;
        [Tooltip("Size multiplier.")]
        public float scale = 1f;
        [Tooltip("Close automatically after this many seconds (0 = never).")]
        public float autoHideSeconds = 0f;
        [Tooltip("How quickly it follows the head (FollowHead mode).")]
        public float followSpeed = 4f;

        public KeyCode desktopKey = KeyCode.M;
        public int vrGesture = GestureDoubleTapLeftTrigger;

        private bool open;
        private float openedAt;
        private float lastTap = -10f;
        private Vector3 baseScale;

        private void Start()
        {
            if (menu == null) return;
            baseScale = menu.localScale;
            menu.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public void Open()
        {
            if (menu == null || Networking.LocalPlayer == null) return;
            open = true;
            openedAt = Time.time;
            menu.localScale = baseScale * scale;
            Place(true);
            menu.gameObject.SetActive(true);
        }

        public void Close()
        {
            open = false;
            if (menu != null) menu.gameObject.SetActive(false);
        }

        private void Update()
        {
            var me = Networking.LocalPlayer;
            if (me == null) return;
            if (!me.IsUserInVR() && Input.GetKeyDown(desktopKey)) Toggle();
            if (!open) return;
            if (autoHideSeconds > 0f && Time.time - openedAt > autoHideSeconds) { Close(); return; }
            if (followMode != StayWhereOpened) Place(false);
        }

        private void Place(bool instant)
        {
            var me = Networking.LocalPlayer;
            var head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 target;
            if (followMode == LeftHand || followMode == RightHand)
            {
                var hand = me.GetTrackingData(followMode == LeftHand ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
                target = hand.position + hand.rotation * new Vector3(0f, 0.08f, 0.05f);
            }
            else
            {
                var forward = head.rotation * Vector3.forward;
                target = head.position + forward * distance + Vector3.up * heightOffset;
            }
            // The canvas faces away from the viewer so its front is readable.
            var look = Quaternion.LookRotation(target - head.position, Vector3.up);
            if (instant || followMode == LeftHand || followMode == RightHand)
            {
                menu.SetPositionAndRotation(target, look);
            }
            else
            {
                float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
                menu.SetPositionAndRotation(Vector3.Lerp(menu.position, target, t), Quaternion.Slerp(menu.rotation, look, t));
            }
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (vrGesture == GestureDoubleTapLeftTrigger && value && args.handType == HandType.LEFT) DoubleTap();
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (vrGesture == GestureDoubleJump && value && Networking.LocalPlayer != null && Networking.LocalPlayer.IsUserInVR()) DoubleTap();
        }

        private void DoubleTap()
        {
            if (Time.time - lastTap < 0.4f) { Toggle(); lastTap = -10f; }
            else lastTap = Time.time;
        }
    }
}
