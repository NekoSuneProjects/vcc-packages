using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// One tab in the Motion Converter window (Fortnite, MMD, ...).
    ///
    /// To add a new source: create a [Serializable] class with a parameterless constructor that derives from
    /// this (or from <see cref="ClipPipelineModule"/> if your source can be turned into a Generic AnimationClip).
    /// The window finds it automatically; nothing else needs registering.
    /// </summary>
    [Serializable]
    public abstract class MotionSourceModule
    {
        [NonSerialized] private ConversionLog log;

        /// <summary>Stable id, used to remember the selected tab.</summary>
        public abstract string Id { get; }

        /// <summary>Tab label.</summary>
        public abstract string TabName { get; }

        /// <summary>Tab position, lowest first.</summary>
        public virtual int Order => 100;

        /// <summary>One line under the window title, e.g. "MMD / VMD → Humanoid".</summary>
        public abstract string Subtitle { get; }

        /// <summary>Short explanation under the tab bar.</summary>
        public abstract string Description { get; }

        protected ConversionLog Log => log ?? (log = new ConversionLog());

        /// <summary>Draw everything below the tab bar.</summary>
        public abstract void DrawGUI();

        /// <summary>Window closed or scripts reloading: free in-memory assets here.</summary>
        public virtual void OnDisable() { }
    }

    /// <summary>Finds every module type in the project and keeps one instance of each.</summary>
    public static class MotionSourceModules
    {
        public static void Sync(List<MotionSourceModule> modules)
        {
            modules.RemoveAll(m => m == null);
            var have = new HashSet<Type>(modules.Select(m => m.GetType()));

            foreach (var type in TypeCache.GetTypesDerivedFrom<MotionSourceModule>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition || have.Contains(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                try { modules.Add((MotionSourceModule)Activator.CreateInstance(type)); }
                catch (Exception e) { Debug.LogException(e); }
            }

            modules.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.TabName, b.TabName));
        }
    }
}
