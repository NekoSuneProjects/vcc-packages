using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    /// <summary>
    /// User preset asset. Create via the Lip Sync window's "Save preset" button.
    /// Lives in its own file named after the class: Unity can only resolve a ScriptableObject's
    /// script reference when the file and class names match.
    /// </summary>
    internal class NekoLipSyncPreset : ScriptableObject
    {
        public NekoLipSyncSettings settings = new NekoLipSyncSettings();
    }
}
