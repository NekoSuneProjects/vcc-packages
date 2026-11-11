using System;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    internal enum NekoLipSyncTarget
    {
        Auto = 0,
        VrcVisemes = 1,
        JawBone = 2,
        SingleMouthOpen = 3
    }

    internal enum NekoJawAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    /// <summary>All tunables for one lip-sync bake. Serializable so it can live in a preset asset.</summary>
    [Serializable]
    internal class NekoLipSyncSettings
    {
        // --- Feel -------------------------------------------------------------------------
        [Range(0f, 1f)]   public float volumeToMouth   = 0.7f;
        [Range(0.5f, 6f)] public float clarity         = 2f;
        [Range(0f, 1f)]   public float consonantClose  = 0.6f;
        [Range(0f, 2f)]   public float strength        = 1f;
        [Range(-250f, 250f)] public float offsetMs     = 0f;
        [Range(0f, 200f)] public float attackMs        = 20f;
        [Range(0f, 400f)] public float releaseMs       = 40f;
        [Range(0f, 0.5f)] public float silenceThreshold = 0.05f;
        [Range(0f, 1f)]   public float liveliness      = 0f;

        // --- Bake -------------------------------------------------------------------------
        public int  fps        = 30;
        [Range(1, 10)] public int quality = 6;
        public bool cleanVocal = false;
        public bool loopTime   = false;
        public bool normalize  = true;
        public bool writeSil   = true;
        public bool keyReduction = true;
        [Range(0.05f, 5f)] public float keyTolerance = 0.5f;

        // --- Range (seconds; end <= 0 means "to the end") ----------------------------------
        public float startSec = 0f;
        public float endSec   = 0f;

        // --- Targets ----------------------------------------------------------------------
        public NekoLipSyncTarget target = NekoLipSyncTarget.Auto;

        public bool  driveJaw      = false;
        public string jawPath      = "";
        public NekoJawAxis jawAxis = NekoJawAxis.X;
        public float jawMaxAngle   = 18f;
        public bool  jawInvert     = false;

        public bool   driveSingleShape   = false;
        public string singleShapePath    = "";   // renderer path
        public string singleShapeName    = "";   // blendshape name

        public NekoLipSyncSettings Clone()
        {
            return (NekoLipSyncSettings)MemberwiseClone();
        }

        public void CopyFrom(NekoLipSyncSettings o)
        {
            if (o == null) return;
            volumeToMouth = o.volumeToMouth;
            clarity = o.clarity;
            consonantClose = o.consonantClose;
            strength = o.strength;
            offsetMs = o.offsetMs;
            attackMs = o.attackMs;
            releaseMs = o.releaseMs;
            silenceThreshold = o.silenceThreshold;
            liveliness = o.liveliness;
            fps = o.fps;
            quality = o.quality;
            cleanVocal = o.cleanVocal;
            loopTime = o.loopTime;
            normalize = o.normalize;
            writeSil = o.writeSil;
            keyReduction = o.keyReduction;
            keyTolerance = o.keyTolerance;
            startSec = o.startSec;
            endSec = o.endSec;
            target = o.target;
            driveJaw = o.driveJaw;
            jawPath = o.jawPath;
            jawAxis = o.jawAxis;
            jawMaxAngle = o.jawMaxAngle;
            jawInvert = o.jawInvert;
            driveSingleShape = o.driveSingleShape;
            singleShapePath = o.singleShapePath;
            singleShapeName = o.singleShapeName;
        }

        public void ResetToDefaults()
        {
            CopyFrom(new NekoLipSyncSettings());
        }

        public static readonly int[] FpsOptions = { 10, 12, 15, 20, 24, 25, 30, 50, 60, 90, 120 };

        // --- Built-in presets ---------------------------------------------------------------

        public struct Builtin
        {
            public string NameKey;
            public Func<NekoLipSyncSettings> Make;
        }

        public static readonly Builtin[] Builtins =
        {
            new Builtin { NameKey = "preset.default", Make = () => new NekoLipSyncSettings() },

            new Builtin { NameKey = "preset.song", Make = () => new NekoLipSyncSettings {
                volumeToMouth = 0.85f, clarity = 1.6f, consonantClose = 0.45f, strength = 1.1f,
                attackMs = 25f, releaseMs = 70f, silenceThreshold = 0.06f, quality = 7 } },

            new Builtin { NameKey = "preset.speech", Make = () => new NekoLipSyncSettings {
                volumeToMouth = 0.6f, clarity = 2.6f, consonantClose = 0.7f, strength = 1f,
                attackMs = 14f, releaseMs = 32f, silenceThreshold = 0.04f, quality = 6 } },

            new Builtin { NameKey = "preset.anime", Make = () => new NekoLipSyncSettings {
                volumeToMouth = 0.5f, clarity = 3.5f, consonantClose = 0.85f, strength = 1.35f,
                attackMs = 8f, releaseMs = 24f, silenceThreshold = 0.05f, quality = 6, liveliness = 0.15f } },

            new Builtin { NameKey = "preset.subtle", Make = () => new NekoLipSyncSettings {
                volumeToMouth = 0.9f, clarity = 1.3f, consonantClose = 0.3f, strength = 0.65f,
                attackMs = 35f, releaseMs = 110f, silenceThreshold = 0.07f, quality = 6 } },

            new Builtin { NameKey = "preset.noisy", Make = () => new NekoLipSyncSettings {
                volumeToMouth = 0.75f, clarity = 2f, consonantClose = 0.5f, strength = 1f,
                attackMs = 25f, releaseMs = 80f, silenceThreshold = 0.12f, quality = 8 } },
        };
    }
}
