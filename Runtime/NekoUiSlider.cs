using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Makes a UI slider do something: change sound volume, set an Animator float, or hand the value
    /// to another Udon script. Slider.onValueChanged → SendCustomEvent("Changed").
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoUiSlider : UdonSharpBehaviour
    {
        public const int SoundVolume = 0;
        public const int AnimatorFloat = 1;
        public const int SendToUdon = 2;

        public int action;
        [Tooltip("On: everyone hears/sees the same value. Off: each player sets their own.")]
        public bool synced;
        public Slider slider;

        [Tooltip("Sounds whose volume follows the slider (0–1).")]
        public AudioSource[] sounds;

        public Animator animator;
        public string parameter;

        [Tooltip("Script that receives the value in a public float variable, then gets the event.")]
        public UdonBehaviour eventTarget;
        public string valueVariable = "value";
        public string eventName;

        [Tooltip("Optional text showing the value, e.g. 75%.")]
        public TextMeshProUGUI valueText;
        public bool showAsPercent = true;

        [UdonSynced] private float syncedValue;
        private bool applying;

        private void Start()
        {
            if (slider != null) Apply(slider.value);
        }

        /// <summary>Called by the slider when it moves.</summary>
        public void Changed()
        {
            if (applying || slider == null) return;
            float v = slider.value;
            if (synced)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                syncedValue = v;
                RequestSerialization();
            }
            Apply(v);
        }

        public override void OnDeserialization()
        {
            if (slider != null)
            {
                applying = true;
                slider.value = syncedValue;
                applying = false;
            }
            Apply(syncedValue);
        }

        private void Apply(float v)
        {
            if (action == SoundVolume && sounds != null)
            {
                for (int i = 0; i < sounds.Length; i++) if (sounds[i] != null) sounds[i].volume = v;
            }
            else if (action == AnimatorFloat && animator != null && !string.IsNullOrEmpty(parameter))
            {
                animator.SetFloat(parameter, v);
            }
            else if (action == SendToUdon && eventTarget != null)
            {
                if (!string.IsNullOrEmpty(valueVariable)) eventTarget.SetProgramVariable(valueVariable, v);
                if (!string.IsNullOrEmpty(eventName)) eventTarget.SendCustomEvent(eventName);
            }

            if (valueText != null)
            {
                if (showAsPercent && slider != null && slider.maxValue > slider.minValue)
                    valueText.text = Mathf.RoundToInt((v - slider.minValue) / (slider.maxValue - slider.minValue) * 100f) + "%";
                else
                    valueText.text = v.ToString("0.##");
            }
        }
    }
}
