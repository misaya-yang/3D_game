using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.Feedback
{
    public sealed class VFXManager : Singleton<VFXManager>, IVfxService
    {
        private readonly List<GameObject> _activeInstances =
            new List<GameObject>();
        private readonly Dictionary<string, Material> _materials =
            new Dictionary<string, Material>();

        public string LastPlayedVfxId { get; private set; } = string.Empty;
        public int PlayCount { get; private set; }
        public int ActiveCount
        {
            get
            {
                PruneDestroyedInstances();
                return _activeInstances.Count;
            }
        }

        protected override void OnSingletonAwake()
        {
            EnsureRegistered();
        }

        private void Update()
        {
            EnsureRegistered();
            PruneDestroyedInstances();
        }

        public void EnsureRegistered()
        {
            if (ServiceLocator.TryGet<IVfxService>(out IVfxService current))
            {
                return;
            }

            ServiceLocator.Register<IVfxService>(this);
        }

        protected override void OnSingletonDestroyed()
        {
            if (ServiceLocator.TryGet<IVfxService>(out IVfxService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IVfxService>();
            }

            foreach (Material material in _materials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _materials.Clear();
            _activeInstances.Clear();
        }

        public GameObject Play(
            string vfxId,
            Vector3 position,
            Quaternion rotation,
            float duration = 2f)
        {
            if (!VfxContentIds.IsKnown(vfxId))
            {
                return null;
            }

            float lifetime = Mathf.Max(0.05f, duration);
            var instance = new GameObject(vfxId + "_Placeholder");
            instance.transform.SetPositionAndRotation(position, rotation);

            ParticleSystem particles = instance.AddComponent<ParticleSystem>();
            ConfigureParticles(particles, vfxId, lifetime);
            var runtime = instance.AddComponent<RuntimeVfxInstance>();
            runtime.Initialize(this, lifetime);

            _activeInstances.Add(instance);
            LastPlayedVfxId = vfxId;
            PlayCount++;
            particles.Play(true);
            return instance;
        }

        public GameObject PlayAttached(
            string vfxId,
            Transform parent,
            float duration)
        {
            if (parent == null)
            {
                return Play(vfxId, Vector3.zero, Quaternion.identity, duration);
            }

            GameObject instance = Play(
                vfxId,
                parent.position,
                parent.rotation,
                duration);
            if (instance != null)
            {
                instance.transform.SetParent(parent, true);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            return instance;
        }

        public void Stop(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            _activeInstances.Remove(instance);
            ParticleSystem particles = instance.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(instance);
        }

        internal void NotifyDestroyed(GameObject instance)
        {
            _activeInstances.Remove(instance);
        }

        private void ConfigureParticles(
            ParticleSystem particles,
            string vfxId,
            float duration)
        {
            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            Color color = ResolveColor(vfxId);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = duration;
            main.startLifetime = Mathf.Min(0.6f, duration);
            main.startSpeed = IsWarning(vfxId) ? 0.2f : 1.2f;
            main.startSize = IsWarning(vfxId) ? 0.36f : 0.22f;
            main.startColor = color;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(
                new[]
                {
                    new ParticleSystem.Burst(
                        0f,
                        (short)(IsWarning(vfxId) ? 20 : 12))
                });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = IsWarning(vfxId)
                ? ParticleSystemShapeType.Circle
                : ParticleSystemShapeType.Sphere;
            shape.radius = IsWarning(vfxId) ? 1.1f : 0.18f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(color, 0f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(color.a, 0f),
                        new GradientAlphaKey(0f, 1f)
                    }
                });

            ParticleSystemRenderer renderer =
                particles.GetComponent<ParticleSystemRenderer>();
            Material material = GetOrCreateMaterial(vfxId, color);
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private Material GetOrCreateMaterial(string vfxId, Color color)
        {
            string materialKey = ResolveMaterialKey(vfxId);
            if (_materials.TryGetValue(materialKey, out Material existing)
                && existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "VFX_Placeholder_" + materialKey,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            _materials[materialKey] = material;
            return material;
        }

        private void PruneDestroyedInstances()
        {
            for (int index = _activeInstances.Count - 1; index >= 0; index--)
            {
                if (_activeInstances[index] == null)
                {
                    _activeInstances.RemoveAt(index);
                }
            }
        }

        private static bool IsWarning(string id)
        {
            return id == VfxContentIds.BossSlamWarning
                || id == VfxContentIds.BossChargeWarning
                || id == VfxContentIds.BossSummonWarning;
        }

        private static string ResolveMaterialKey(string id)
        {
            if (id == VfxContentIds.HitFire
                || id == VfxContentIds.SkillEmber)
            {
                return "Fire";
            }

            if (id == VfxContentIds.HitIce)
            {
                return "Ice";
            }

            if (IsWarning(id))
            {
                return "Warning";
            }

            if (id == VfxContentIds.Heal)
            {
                return "Heal";
            }

            if (id == VfxContentIds.RealmBreakthrough)
            {
                return "Realm";
            }

            return "Qi";
        }

        private static Color ResolveColor(string id)
        {
            string key = ResolveMaterialKey(id);
            switch (key)
            {
                case "Fire":
                    return new Color(1f, 0.26f, 0.05f, 0.9f);
                case "Ice":
                    return new Color(0.25f, 0.78f, 1f, 0.9f);
                case "Warning":
                    return new Color(0.95f, 0.08f, 0.02f, 0.82f);
                case "Heal":
                    return new Color(0.25f, 1f, 0.48f, 0.9f);
                case "Realm":
                    return new Color(0.95f, 0.76f, 0.2f, 0.95f);
                default:
                    return new Color(0.42f, 0.95f, 0.78f, 0.9f);
            }
        }
    }
}
