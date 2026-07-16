using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wendao.Editor
{
    public static class CharacterAssetAudit
    {
        private const string CultivatorAssetPath =
            "Assets/_Project/Resources/Art/Budget/Characters/Cultivator.fbx";
        private const string CultivatorReportPath =
            "TestResults/G09-07-cultivator-bindings.json";
        private const string StoneGeneralAssetPath =
            "Assets/_Project/Resources/Art/Budget/Creatures/StoneGeneral.fbx";
        private const string StoneGeneralReportPath =
            "TestResults/G09-07-stone-general-bindings.json";

        [Serializable]
        private sealed class ClipRecord
        {
            public string name = string.Empty;
            public bool legacy;
            public float length;
            public float maximumFloatCurveDelta;
            public string[] paths = Array.Empty<string>();
        }

        [Serializable]
        private sealed class RendererRecord
        {
            public string path = string.Empty;
            public string[] materials = Array.Empty<string>();
            public string[] shaders = Array.Empty<string>();
            public string[] textures = Array.Empty<string>();
        }

        [Serializable]
        private sealed class CharacterReport
        {
            public string assetPath = string.Empty;
            public string animatorPath = string.Empty;
            public string avatarName = string.Empty;
            public bool avatarIsValid;
            public bool avatarIsHuman;
            public string[] transformPaths = Array.Empty<string>();
            public RendererRecord[] renderers = Array.Empty<RendererRecord>();
            public ClipRecord[] clips = Array.Empty<ClipRecord>();
        }

        public static void WriteCultivatorReport()
        {
            WriteReport(
                CultivatorAssetPath,
                CultivatorReportPath);
        }

        public static void WriteG0907Reports()
        {
            WriteReport(
                CultivatorAssetPath,
                CultivatorReportPath);
            WriteReport(
                StoneGeneralAssetPath,
                StoneGeneralReportPath);
        }

        public static void WriteStoneGeneralReport()
        {
            WriteReport(
                StoneGeneralAssetPath,
                StoneGeneralReportPath);
        }

        private static void WriteReport(
            string assetPath,
            string relativeReportPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                assetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException(assetPath);
            }

            GameObject instance =
                UnityEngine.Object.Instantiate(prefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Transform root = instance.transform;
                UnityEngine.Object[] assets =
                    AssetDatabase.LoadAllAssetsAtPath(assetPath);
                ClipRecord[] clips = assets
                    .OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith(
                        "__preview__",
                        StringComparison.Ordinal))
                    .Select(clip => new ClipRecord
                    {
                        name = clip.name,
                        legacy = clip.legacy,
                        length = clip.length,
                        maximumFloatCurveDelta =
                            MaximumFloatCurveDelta(clip),
                        paths = AnimationUtility.GetCurveBindings(clip)
                            .Select(binding => binding.path)
                            .Concat(
                                AnimationUtility.GetObjectReferenceCurveBindings(
                                        clip)
                                    .Select(binding => binding.path))
                            .Distinct()
                            .OrderBy(path => path, StringComparer.Ordinal)
                            .ToArray()
                    })
                    .OrderBy(clip => clip.name, StringComparer.Ordinal)
                    .ToArray();
                var report = new CharacterReport
                {
                    assetPath = assetPath,
                    animatorPath = animator != null
                        ? RelativePath(root, animator.transform)
                        : string.Empty,
                    avatarName = animator != null && animator.avatar != null
                        ? animator.avatar.name
                        : string.Empty,
                    avatarIsValid = animator != null
                        && animator.avatar != null
                        && animator.avatar.isValid,
                    avatarIsHuman = animator != null
                        && animator.avatar != null
                        && animator.avatar.isHuman,
                    transformPaths = instance
                        .GetComponentsInChildren<Transform>(true)
                        .Select(transform => RelativePath(root, transform))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray(),
                    renderers = instance
                        .GetComponentsInChildren<Renderer>(true)
                        .Select(renderer => new RendererRecord
                        {
                            path = RelativePath(root, renderer.transform),
                            materials = renderer.sharedMaterials
                                .Select(material => material != null
                                    ? material.name
                                    : string.Empty)
                                .ToArray(),
                            shaders = renderer.sharedMaterials
                                .Select(material =>
                                    material != null
                                    && material.shader != null
                                        ? material.shader.name
                                        : string.Empty)
                                .ToArray(),
                            textures = renderer.sharedMaterials
                                .Select(material =>
                                    material != null
                                    && material.mainTexture != null
                                        ? material.mainTexture.name
                                        : string.Empty)
                                .ToArray()
                        })
                        .OrderBy(renderer => renderer.path, StringComparer.Ordinal)
                        .ToArray(),
                    clips = clips
                };

                string projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                string reportPath = Path.Combine(
                    projectRoot,
                    relativeReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(
                    reportPath,
                    JsonUtility.ToJson(report, true));
                Debug.Log($"Character binding report written: {reportPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (root == target)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static float MaximumFloatCurveDelta(AnimationClip clip)
        {
            float maximum = 0f;
            foreach (EditorCurveBinding binding in
                AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve =
                    AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2)
                {
                    continue;
                }

                float minimum = float.PositiveInfinity;
                float maximumValue = float.NegativeInfinity;
                foreach (Keyframe key in curve.keys)
                {
                    minimum = Mathf.Min(minimum, key.value);
                    maximumValue = Mathf.Max(maximumValue, key.value);
                }
                maximum = Mathf.Max(
                    maximum,
                    maximumValue - minimum);
            }
            return maximum;
        }
    }
}
