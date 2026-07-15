using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Feedback;

namespace Wendao.Entities.Enemy
{
    public sealed class BossSkillTelegraphView : SafeBehaviour
    {
        public const string ObjectName = "BossSkillTelegraph_Greybox";
        private static readonly Color WarningColor =
            new Color(0.92f, 0.16f, 0.08f, 0.58f);

        private static Material _warningMaterial;
        private GameObject _circle;
        private GameObject _line;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private IVfxService _vfxService;
        private GameObject _vfxInstance;

        public bool IsVisible { get; private set; }
        public string SkillId { get; private set; } = string.Empty;
        public string VfxId { get; private set; } = string.Empty;
        public TelegraphShape Shape { get; private set; }
        public float Duration { get; private set; }
        public float Progress01 { get; private set; }

        private void Awake()
        {
            gameObject.name = ObjectName;
            CreateShapes();
            Hide();
        }

        public void Show(BossSkillTelegraph telegraph, Vector3 targetPosition)
        {
            if (telegraph == null)
            {
                Hide();
                return;
            }

            SkillId = telegraph.SkillId ?? string.Empty;
            VfxId = telegraph.VfxId ?? string.Empty;
            Shape = telegraph.Shape;
            Duration = Mathf.Max(0f, telegraph.Duration);
            Progress01 = 0f;

            float size = Mathf.Max(0.5f, telegraph.RadiusOrLength);
            bool useLine = telegraph.Shape == TelegraphShape.Line;
            _line.SetActive(useLine);
            _circle.SetActive(!useLine);

            transform.localPosition = Vector3.zero;
            if (useLine)
            {
                Vector3 direction = targetPosition - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = transform.parent != null
                        ? transform.parent.forward
                        : Vector3.forward;
                }

                transform.rotation = Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
                _line.transform.localPosition = new Vector3(0f, 0.04f, size * 0.5f);
                _line.transform.localScale = new Vector3(1.2f, 0.04f, size);
            }
            else
            {
                transform.localRotation = Quaternion.identity;
                float diameter = size * 2f;
                _circle.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                _circle.transform.localScale = new Vector3(diameter, 0.025f, diameter);
            }

            IsVisible = true;
            gameObject.SetActive(true);
            ApplyPulse(0f);
            if (_vfxInstance != null)
            {
                _vfxService?.Stop(_vfxInstance);
                _vfxInstance = null;
            }

            if (_vfxService == null)
            {
                ServiceLocator.TryGet(out _vfxService);
            }

            if (_vfxService != null && VfxContentIds.IsKnown(VfxId))
            {
                _vfxInstance = _vfxService.PlayAttached(
                    VfxId,
                    transform,
                    Mathf.Max(0.1f, Duration));
            }
        }

        public void SetProgress(float progress01)
        {
            Progress01 = Mathf.Clamp01(progress01);
            if (IsVisible)
            {
                ApplyPulse(Progress01);
            }
        }

        public void Hide()
        {
            if (_vfxInstance != null)
            {
                _vfxService?.Stop(_vfxInstance);
                _vfxInstance = null;
            }

            IsVisible = false;
            Progress01 = 0f;
            if (_circle != null)
            {
                _circle.SetActive(false);
            }

            if (_line != null)
            {
                _line.SetActive(false);
            }
        }

        private void CreateShapes()
        {
            _circle = CreatePrimitive(PrimitiveType.Cylinder, "Circle");
            _line = CreatePrimitive(PrimitiveType.Cube, "Line");
            _renderers = new[]
            {
                _circle.GetComponent<Renderer>(),
                _line.GetComponent<Renderer>()
            };
        }

        private GameObject CreatePrimitive(PrimitiveType type, string childName)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = childName;
            shape.transform.SetParent(transform, false);
            Collider collider = shape.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = shape.GetComponent<Renderer>();
            Material material = GetWarningMaterial();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return shape;
        }

        private void ApplyPulse(float progress01)
        {
            float pulse = 0.65f + Mathf.PingPong(progress01 * 4f, 0.35f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(
                WarningColor.r,
                WarningColor.g,
                WarningColor.b,
                WarningColor.a * pulse));
            for (int index = 0; index < _renderers.Length; index++)
            {
                if (_renderers[index] != null)
                {
                    _renderers[index].SetPropertyBlock(block);
                }
            }
        }

        private static Material GetWarningMaterial()
        {
            if (_warningMaterial != null)
            {
                return _warningMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return null;
            }

            _warningMaterial = new Material(shader)
            {
                name = "BossTelegraph_Warning_Runtime",
                color = WarningColor,
                hideFlags = HideFlags.DontSave
            };
            _warningMaterial.SetColor("_BaseColor", WarningColor);
            return _warningMaterial;
        }
    }
}
