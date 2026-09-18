using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VariaWeight
{
    /// <summary>
    /// Registers a custom Weightlifting skill without Jotunn (stable SkillType from identifier hash).
    /// </summary>
    internal static class WeightSkill
    {
        public static Skills.SkillType SkillType { get; private set; }

        public static string LocalizationKey { get; private set; }

        private static Skills.SkillDef _def;
        private static Sprite _icon;

        public static void Initialize()
        {
            int hash = VariaWeightPlugin.SkillIdentifier.GetStableHashCode();
            // Math.Abs(int.MinValue) throws; keep a stable fallback.
            if (hash == int.MinValue)
            {
                hash = 190_001;
            }
            else
            {
                hash = Math.Abs(hash);
            }

            if (hash == 0 || hash == (int)Skills.SkillType.All)
            {
                hash = 190_001;
            }

            SkillType = (Skills.SkillType)hash;
            // SkillsDialog / RaiseSkill localize "$skill_" + SkillType.ToString().ToLower().
            LocalizationKey = "skill_" + SkillType.ToString().ToLowerInvariant();
            _icon = CreateIcon();
            _def = new Skills.SkillDef
            {
                m_skill = SkillType,
                m_description =
                    "Increase carry weight.",
                m_increseStep = 1f,
                m_icon = _icon
            };
        }

        public static void EnsureRegistered(Skills skills)
        {
            if (skills?.m_skills == null || _def == null)
            {
                return;
            }

            for (int i = 0, count = skills.m_skills.Count; i < count; i++)
            {
                Skills.SkillDef existing = skills.m_skills[i];
                if (existing != null && existing.m_skill == SkillType)
                {
                    return;
                }
            }

            skills.m_skills.Add(_def);
        }

        /// <summary>Ensure the skill exists on the player at level 0 so it appears in the skills UI.</summary>
        public static void EnsurePlayerSkill(Skills skills)
        {
            EnsureRegistered(skills);
            if (skills != null)
            {
                skills.GetSkill(SkillType);
            }
        }

        public static void RegisterLocalization()
        {
            if (Localization.instance == null || string.IsNullOrEmpty(LocalizationKey))
            {
                return;
            }

            // Re-run on language change (SetupLanguage postfix).
            Localization.instance.AddWord(LocalizationKey, "Weightlifting");
        }

        private const string IconResourceName = "VariaWeight.Assets.weightlifting_icon.png";

        private static Sprite CreateIcon()
        {
            Assembly asm = typeof(WeightSkill).Assembly;
            using (Stream stream = asm.GetManifestResourceStream(IconResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Missing embedded icon: " + IconResourceName);
                }

                var bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException("Truncated embedded icon: " + IconResourceName);
                    }

                    offset += read;
                }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!TryLoadImage(tex, bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    throw new InvalidOperationException("Failed to decode PNG icon: " + IconResourceName);
                }

                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.name = "VariaWeight_Icon";

                return Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    64f);
            }
        }

        /// <summary>
        /// Unity's ImageConversionModule targets netstandard 2.1 / Span overloads that conflict with
        /// net48 compile refs, so call LoadImage via reflection at runtime.
        /// </summary>
        private static bool TryLoadImage(Texture2D texture, byte[] data)
        {
            Type conversion = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                throwOnError: false);
            if (conversion == null)
            {
                return false;
            }

            MethodInfo loadImage = conversion.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                modifiers: null);
            if (loadImage == null)
            {
                loadImage = conversion.GetMethod(
                    "LoadImage",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Texture2D), typeof(byte[]) },
                    modifiers: null);
            }

            if (loadImage == null)
            {
                return false;
            }

            object result = loadImage.GetParameters().Length == 3
                ? loadImage.Invoke(null, new object[] { texture, data, true })
                : loadImage.Invoke(null, new object[] { texture, data });
            return result is bool ok && ok;
        }
    }

    [HarmonyPatch(typeof(Skills), "Awake")]
    internal static class SkillsAwakePatch
    {
        private static void Postfix(Skills __instance)
        {
            WeightSkill.EnsurePlayerSkill(__instance);
        }
    }

    [HarmonyPatch(typeof(Skills), nameof(Skills.Load))]
    internal static class SkillsLoadPatch
    {
        private static void Postfix(Skills __instance)
        {
            WeightSkill.EnsurePlayerSkill(__instance);
        }
    }

    [HarmonyPatch(typeof(Skills), nameof(Skills.IsSkillValid))]
    internal static class SkillsIsSkillValidPatch
    {
        private static void Postfix(Skills.SkillType type, ref bool __result)
        {
            if (type == WeightSkill.SkillType)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Skills), nameof(Skills.GetSkillDef))]
    internal static class SkillsGetSkillDefPatch
    {
        private static void Postfix(Skills __instance, Skills.SkillType type, ref Skills.SkillDef __result)
        {
            if (__result != null || type != WeightSkill.SkillType)
            {
                return;
            }

            WeightSkill.EnsureRegistered(__instance);
            for (int i = 0, count = __instance.m_skills.Count; i < count; i++)
            {
                Skills.SkillDef def = __instance.m_skills[i];
                if (def != null && def.m_skill == WeightSkill.SkillType)
                {
                    __result = def;
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    internal static class LocalizationSetupLanguagePatch
    {
        private static void Postfix()
        {
            WeightSkill.RegisterLocalization();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
    internal static class PlayerGetMaxCarryWeightPatch
    {
        private static void Postfix(Player __instance, ref float __result)
        {
            WeightConfigSnapshot cfg = VariaWeightPlugin.ConfigSnapshot;
            if (!cfg.Enabled || cfg.ApproximatelyVanillaCarry())
            {
                return;
            }

            float bonus = WeightCurve.GetCarryBonus(__instance, cfg);
            if (bonus > 0f)
            {
                __result += bonus;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSkillLevelup))]
    internal static class PlayerOnSkillLevelupPatch
    {
        private static void Postfix(Skills.SkillType skill)
        {
            if (skill == WeightSkill.SkillType)
            {
                WeightCurve.InvalidateCarryBonusCache();
            }
        }
    }
}
