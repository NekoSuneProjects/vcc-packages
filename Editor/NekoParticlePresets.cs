#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.VRChatIdleAudio
{
    public enum IntroPreset
    {
        HeartsRising,
        DiamondAscend,
        StarBurst,
        SakuraFall,
        EmberRise,
        SnowFall,
        BubbleRise,
        DigitalBoot,
        AuraRing
    }

    public enum ParticleShapeKind
    {
        Heart,
        Diamond,
        Star,
        Sparkle,
        Petal,
        Snowflake,
        Bubble,
        Soft
    }

    public enum ParticleBlend
    {
        Additive,
        AlphaBlend
    }

    /// <summary>
    /// The particle looks offered by the intro installer.
    ///
    /// Sprites are generated procedurally rather than shipped as PNGs, so the package stays
    /// self-contained and every shape comes out crisp at whatever resolution is asked for.
    /// </summary>
    public static class NekoParticlePresets
    {
        public sealed class PresetInfo
        {
            public IntroPreset Id;
            public string Name;
            public string Description;
            public ParticleShapeKind Shape;
            public Color ColorA;
            public Color ColorB;
            public ParticleBlend Blend;
        }

        private const string ShaderName = "NekoSuneVR/Particle";
        private const int SpriteSize = 128;

        private static readonly Dictionary<ParticleShapeKind, Texture2D> PreviewCache =
            new Dictionary<ParticleShapeKind, Texture2D>();

        public static readonly PresetInfo[] All =
        {
            new PresetInfo
            {
                Id = IntroPreset.HeartsRising,
                Name = "Hearts Rising",
                Description = "Hearts bloom around your feet the moment you spawn and float up past your head into the sky. Soft pink, short and sweet.",
                Shape = ParticleShapeKind.Heart,
                ColorA = new Color(1f, 0.42f, 0.66f),
                ColorB = new Color(1f, 0.78f, 0.88f),
                Blend = ParticleBlend.AlphaBlend
            },
            new PresetInfo
            {
                Id = IntroPreset.DiamondAscend,
                Name = "Diamond Ascend",
                Description = "Cut gems lift off the floor and spiral upward, catching light as they tumble. Cool cyan into white.",
                Shape = ParticleShapeKind.Diamond,
                ColorA = new Color(0.36f, 0.90f, 1f),
                ColorB = new Color(0.86f, 0.98f, 1f),
                Blend = ParticleBlend.Additive
            },
            new PresetInfo
            {
                Id = IntroPreset.StarBurst,
                Name = "Star Burst",
                Description = "Stars explode outward from your chest, then drift and fade. Punchy and instant - good for a short intro.",
                Shape = ParticleShapeKind.Star,
                ColorA = new Color(1f, 0.85f, 0.32f),
                ColorB = new Color(1f, 0.98f, 0.80f),
                Blend = ParticleBlend.Additive
            },
            new PresetInfo
            {
                Id = IntroPreset.SakuraFall,
                Name = "Sakura Fall",
                Description = "Cherry petals drift down from above your head and settle past your feet, swaying as they go.",
                Shape = ParticleShapeKind.Petal,
                ColorA = new Color(1f, 0.70f, 0.82f),
                ColorB = new Color(1f, 0.92f, 0.95f),
                Blend = ParticleBlend.AlphaBlend
            },
            new PresetInfo
            {
                Id = IntroPreset.EmberRise,
                Name = "Ember Rise",
                Description = "Embers lift off the ground like a dying fire, flickering orange to deep red as they climb.",
                Shape = ParticleShapeKind.Soft,
                ColorA = new Color(1f, 0.55f, 0.12f),
                ColorB = new Color(0.85f, 0.14f, 0.05f),
                Blend = ParticleBlend.Additive
            },
            new PresetInfo
            {
                Id = IntroPreset.SnowFall,
                Name = "Snow Fall",
                Description = "Slow snowflakes falling around you, tumbling gently. Calm and quiet.",
                Shape = ParticleShapeKind.Snowflake,
                ColorA = new Color(0.86f, 0.94f, 1f),
                ColorB = Color.white,
                Blend = ParticleBlend.AlphaBlend
            },
            new PresetInfo
            {
                Id = IntroPreset.BubbleRise,
                Name = "Bubble Rise",
                Description = "Soap bubbles wobble up from the floor and pop out of sight overhead.",
                Shape = ParticleShapeKind.Bubble,
                ColorA = new Color(0.60f, 0.88f, 1f),
                ColorB = new Color(0.92f, 0.80f, 1f),
                Blend = ParticleBlend.AlphaBlend
            },
            new PresetInfo
            {
                Id = IntroPreset.DigitalBoot,
                Name = "Digital Boot",
                Description = "Sharp sparks swirling around you like a system powering on. Pairs with the generated boot chime.",
                Shape = ParticleShapeKind.Sparkle,
                ColorA = new Color(0.30f, 0.92f, 1f),
                ColorB = new Color(0.70f, 0.55f, 1f),
                Blend = ParticleBlend.Additive
            },
            new PresetInfo
            {
                Id = IntroPreset.AuraRing,
                Name = "Aura Ring",
                Description = "A ring of light sweeps outward across the floor and lifts into a column around you.",
                Shape = ParticleShapeKind.Soft,
                ColorA = new Color(0.72f, 0.40f, 1f),
                ColorB = new Color(1f, 0.45f, 0.85f),
                Blend = ParticleBlend.Additive
            }
        };

        public static PresetInfo Get(IntroPreset id)
        {
            foreach (var preset in All)
            {
                if (preset.Id == id)
                    return preset;
            }
            return All[0];
        }

        // ------------------------------------------------------------------
        // Sprite generation
        // ------------------------------------------------------------------

        /// <summary>Small in-memory sprite for the preset picker. Never written to disk.</summary>
        public static Texture2D PreviewTexture(ParticleShapeKind shape)
        {
            Texture2D texture;
            if (PreviewCache.TryGetValue(shape, out texture) && texture != null)
                return texture;

            texture = GenerateSprite(shape, 64);
            texture.hideFlags = HideFlags.HideAndDontSave;
            PreviewCache[shape] = texture;
            return texture;
        }

        /// <summary>
        /// Renders a shape to a texture with 4x4 supersampling, so edges stay smooth instead of
        /// showing the stair-stepping a plain inside/outside test would give.
        /// </summary>
        public static Texture2D GenerateSprite(ParticleShapeKind shape, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            const int Samples = 4;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = 0f;

                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / Samples) / size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / Samples) / size) * 2f - 1f;
                            coverage += Evaluate(shape, u, v);
                        }
                    }

                    coverage /= Samples * Samples;
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(coverage * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Returns 0..1 coverage for a point in the -1..1 sprite square.</summary>
        private static float Evaluate(ParticleShapeKind shape, float u, float v)
        {
            switch (shape)
            {
                case ParticleShapeKind.Heart:
                {
                    // Classic implicit heart: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    // v is already +1 at the top of the texture, and the formula's lobes sit on
                    // +y, so v is used directly. Negating it lands the point at the top instead.
                    float x = u * 1.28f;
                    float y = v * 1.28f;
                    float t = x * x + y * y - 1f;
                    return t * t * t - x * x * y * y * y <= 0f ? 1f : 0f;
                }

                case ParticleShapeKind.Diamond:
                {
                    float d = Mathf.Abs(u) / 0.62f + Mathf.Abs(v) / 0.92f;
                    if (d > 1f) return 0f;

                    // Facet shading: a brighter core so the gem does not read as a flat lozenge.
                    return Mathf.Lerp(0.55f, 1f, 1f - Mathf.Abs(u) / 0.62f);
                }

                case ParticleShapeKind.Star:
                    return StarCoverage(u, v, 5, 0.94f, 0.42f);

                case ParticleShapeKind.Sparkle:
                {
                    // Astroid - a four-point sparkle with concave sides. The exponent controls how
                    // pinched the arms are; below about 0.45 it thins into a bare cross.
                    float a = Mathf.Pow(Mathf.Abs(u), 0.5f) + Mathf.Pow(Mathf.Abs(v), 0.5f);
                    return a <= 1f ? 1f : 0f;
                }

                case ParticleShapeKind.Petal:
                {
                    // Teardrop: rounded and wide toward the top, tapering to a point at the stem.
                    // A plain sin() profile is symmetric and reads as a leaf, so it is weighted
                    // along y to push the widest part above centre.
                    float y = Mathf.Clamp01(v * 0.5f + 0.5f);
                    float width = Mathf.Sin(y * Mathf.PI) * (0.6f + 0.8f * y) * 0.62f;
                    if (width <= 0.001f) return 0f;
                    return Mathf.Abs(u) <= width ? 1f : 0f;
                }

                case ParticleShapeKind.Snowflake:
                {
                    float radius = Mathf.Sqrt(u * u + v * v);
                    if (radius > 0.98f) return 0f;

                    float angle = Mathf.Atan2(v, u);
                    float sector = Mathf.Abs(Mathf.Repeat(angle, Mathf.PI / 3f) - Mathf.PI / 6f);

                    // Six main arms plus a pair of shorter branches on each.
                    float arm = sector < 0.13f ? 1f : 0f;
                    float branch = (radius < 0.62f && sector < 0.30f && radius > 0.28f) ? 1f : 0f;
                    float core = radius < 0.16f ? 1f : 0f;

                    return Mathf.Max(core, Mathf.Max(arm, branch * 0.85f));
                }

                case ParticleShapeKind.Bubble:
                {
                    float radius = Mathf.Sqrt(u * u + v * v);
                    if (radius > 0.97f) return 0f;

                    // Hollow shell with a soft rim and a small specular highlight.
                    float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.95f, radius));
                    float highlight = Mathf.Clamp01(
                        1f - new Vector2(u + 0.38f, v - 0.38f).magnitude / 0.26f);

                    return Mathf.Clamp01(rim * 0.75f + highlight * 0.9f + 0.06f);
                }

                default:
                {
                    // Soft radial falloff, used for embers and the aura column.
                    float radius = Mathf.Sqrt(u * u + v * v);
                    return Mathf.Clamp01(1f - radius);
                }
            }
        }

        private static float StarCoverage(float u, float v, int points, float outer, float inner)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            if (radius > outer) return 0f;

            // Subtracting a quarter turn puts a tip straight up. Adding one instead lands a
            // valley at the top and the whole star comes out point-down.
            float angle = Mathf.Atan2(v, u) - Mathf.PI / 2f;
            float step = 2f * Mathf.PI / points;
            float local = Mathf.Repeat(angle, step) / step; // 0..1 across one point
            float blend = Mathf.Abs(local - 0.5f) * 2f;     // 1 at the tip, 0 between tips

            float limit = Mathf.Lerp(inner, outer, blend);
            return radius <= limit ? 1f : 0f;
        }

        // ------------------------------------------------------------------
        // Material
        // ------------------------------------------------------------------

        public static Material CreateMaterial(PresetInfo preset, string outputFolder)
        {
            var texture = GenerateSprite(preset.Shape, SpriteSize);

            string texturePath = outputFolder + "/NekoParticle_" + preset.Shape + ".png";
            File.WriteAllBytes(ToAbsolute(texturePath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var shader = Shader.Find(ShaderName)
                         ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
                         ?? Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = "NekoParticle_" + preset.Id };

            var sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (sprite != null)
                material.mainTexture = sprite;

            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);

            // Blend factors are driven from here so one shader covers both looks.
            if (material.HasProperty("_SrcBlend") && material.HasProperty("_DstBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", preset.Blend == ParticleBlend.Additive
                    ? (int)UnityEngine.Rendering.BlendMode.One
                    : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            string materialPath = outputFolder + "/NekoParticle_" + preset.Id + ".mat";
            AssetDatabase.CreateAsset(material, materialPath);

            return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        private static string ToAbsolute(string assetPath)
        {
            string absolute = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            return absolute;
        }

        // ------------------------------------------------------------------
        // Particle system configuration
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies a preset to a ParticleSystem. <paramref name="duration"/> is the intro length,
        /// used to size the emission window so the burst stops when the intro does.
        /// </summary>
        public static void Configure(
            ParticleSystem ps,
            PresetInfo preset,
            float duration,
            float intensity,
            float scale,
            bool worldSpace,
            bool loop = true)
        {
            var main = ps.main;
            main.duration = Mathf.Max(0.5f, duration);

            // A one-shot system emits for exactly `duration` and then stops on its own, so a 10 or
            // 30 second intro ends cleanly instead of relying on the object being switched off
            // mid-emission. Looping is still right for the AFK loading phase, which repeats.
            main.loop = loop;
            main.stopAction = ParticleSystemStopAction.None;
            main.playOnAwake = true;
            main.simulationSpace = worldSpace
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
            main.startColor = new ParticleSystem.MinMaxGradient(preset.ColorA, preset.ColorB);
            main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(120f, 420f, intensity));

            var emission = ps.emission;
            emission.enabled = true;

            var shape = ps.shape;
            shape.enabled = true;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeGradient());

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;

            switch (preset.Id)
            {
                case IntroPreset.HeartsRising:
                    // Ring of hearts on the floor around the feet, lifting past head height.
                    ConfigureFloorRise(ps, intensity, scale,
                        radius: 0.34f, sizeMin: 0.07f, sizeMax: 0.15f,
                        lifeMin: 2.2f, lifeMax: 3.2f, speedMin: 1.1f, speedMax: 2.3f);
                    ps.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
                    rotation.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
                    break;

                case IntroPreset.DiamondAscend:
                    ConfigureFloorRise(ps, intensity, scale,
                        radius: 0.40f, sizeMin: 0.05f, sizeMax: 0.12f,
                        lifeMin: 2.0f, lifeMax: 3.0f, speedMin: 1.3f, speedMax: 2.6f);
                    ps.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
                    rotation.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
                    break;

                case IntroPreset.StarBurst:
                {
                    ps.transform.localPosition = new Vector3(0f, 1.15f, 0f);

                    var burstShape = ps.shape;
                    burstShape.shapeType = ParticleSystemShapeType.Sphere;
                    burstShape.radius = 0.12f * scale;
                    burstShape.radiusThickness = 1f;

                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * scale, 4.2f * scale);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.13f * scale);
                    main.gravityModifier = 0.35f;

                    emission.rateOverTime = Mathf.Lerp(10f, 40f, intensity);
                    emission.SetBursts(new[]
                    {
                        new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(Mathf.Lerp(30f, 90f, intensity)))
                    });

                    velocity.enabled = false;
                    rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                    break;
                }

                case IntroPreset.SakuraFall:
                {
                    ps.transform.localPosition = new Vector3(0f, 2.1f, 0f);

                    var fallShape = ps.shape;
                    fallShape.shapeType = ParticleSystemShapeType.Box;
                    fallShape.scale = new Vector3(1.1f * scale, 0.05f, 1.1f * scale);

                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.11f * scale);
                    main.gravityModifier = 0.10f;

                    emission.rateOverTime = Mathf.Lerp(20f, 70f, intensity);

                    velocity.y = new ParticleSystem.MinMaxCurve(-0.55f, -0.25f);
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
                    rotation.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
                    break;
                }

                case IntroPreset.EmberRise:
                    ConfigureFloorRise(ps, intensity, scale,
                        radius: 0.45f, sizeMin: 0.02f, sizeMax: 0.06f,
                        lifeMin: 1.4f, lifeMax: 2.6f, speedMin: 0.8f, speedMax: 2.0f);
                    ps.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
                    break;

                case IntroPreset.SnowFall:
                {
                    ps.transform.localPosition = new Vector3(0f, 2.2f, 0f);

                    var snowShape = ps.shape;
                    snowShape.shapeType = ParticleSystemShapeType.Box;
                    snowShape.scale = new Vector3(1.3f * scale, 0.05f, 1.3f * scale);

                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 4.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f * scale, 0.06f * scale);

                    emission.rateOverTime = Mathf.Lerp(25f, 80f, intensity);

                    velocity.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.15f);
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
                    rotation.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
                    break;
                }

                case IntroPreset.BubbleRise:
                    ConfigureFloorRise(ps, intensity, scale,
                        radius: 0.36f, sizeMin: 0.04f, sizeMax: 0.14f,
                        lifeMin: 2.4f, lifeMax: 3.6f, speedMin: 0.7f, speedMax: 1.6f);
                    ps.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                    break;

                case IntroPreset.DigitalBoot:
                {
                    ps.transform.localPosition = new Vector3(0f, 1.05f, 0f);

                    var bootShape = ps.shape;
                    bootShape.shapeType = ParticleSystemShapeType.Sphere;
                    bootShape.radius = 0.42f * scale;
                    bootShape.radiusThickness = 0.18f;

                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.4f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.012f * scale, 0.055f * scale);

                    emission.rateOverTime = Mathf.Lerp(40f, 140f, intensity);

                    velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
                    velocity.radial = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
                    break;
                }

                case IntroPreset.AuraRing:
                {
                    ps.transform.localPosition = new Vector3(0f, 0.03f, 0f);

                    var ringShape = ps.shape;
                    ringShape.shapeType = ParticleSystemShapeType.Circle;
                    ringShape.radius = 0.5f * scale;
                    ringShape.radiusThickness = 0.05f; // hollow ring, not a filled disc
                    ringShape.arc = 360f;

                    main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f * scale, 0.16f * scale);

                    emission.rateOverTime = Mathf.Lerp(60f, 180f, intensity);

                    velocity.y = new ParticleSystem.MinMaxCurve(0.9f, 2.1f);
                    velocity.radial = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
                    break;
                }
            }

            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, PopCurve());

            ConfigureRenderer(ps);
        }

        /// <summary>
        /// The shared "rises off the floor around the feet" motion, which most of the spawn
        /// presets are variations on.
        /// </summary>
        private static void ConfigureFloorRise(
            ParticleSystem ps,
            float intensity,
            float scale,
            float radius,
            float sizeMin,
            float sizeMax,
            float lifeMin,
            float lifeMax,
            float speedMin,
            float speedMax)
        {
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin * scale, sizeMax * scale);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * scale;
            shape.radiusThickness = 0.85f; // mostly at the rim, so it reads as a ring at the feet
            shape.arc = 360f;

            var emission = ps.emission;
            emission.rateOverTime = Mathf.Lerp(18f, 70f, intensity);
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(Mathf.Lerp(6f, 22f, intensity)))
            });

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        }

        private static void ConfigureRenderer(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>Scale in quickly, hold, then shrink away.</summary>
        private static AnimationCurve PopCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.14f, 1f),
                new Keyframe(0.72f, 0.92f),
                new Keyframe(1f, 0f));
        }

        private static Gradient FadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.16f),
                    new GradientAlphaKey(0.9f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
#endif
