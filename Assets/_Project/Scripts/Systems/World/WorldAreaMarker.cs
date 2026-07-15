using UnityEngine;
using Wendao.Core;
using Wendao.Systems.Quest;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WorldAreaMarker : MonoBehaviour
    {
        public const string PlateName = "AreaPlate";
        public const string LabelName = "AreaLabel";

        public string AreaId { get; private set; } = string.Empty;
        public string NameLocalizationKey { get; private set; } = string.Empty;
        public string DefaultName { get; private set; } = string.Empty;
        public Vector2 Footprint { get; private set; }

        public void Configure(
            string areaId,
            string localizationKey,
            string defaultName,
            Vector2 footprint,
            Color color)
        {
            AreaId = areaId ?? string.Empty;
            NameLocalizationKey = localizationKey ?? string.Empty;
            DefaultName = defaultName ?? string.Empty;
            Footprint = new Vector2(
                Mathf.Max(1f, footprint.x),
                Mathf.Max(1f, footprint.y));
            EnsureTrigger();
            EnsurePlate(color);
            EnsureLabel(color);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!string.IsNullOrEmpty(AreaId)
                && WorldActorUtility.IsPlayer(
                    other != null ? other.gameObject : null)
                && ServiceLocator.TryGet<IQuestService>(
                    out IQuestService quests))
            {
                quests.NotifyReach(AreaId);
            }
        }

        private void EnsureTrigger()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(Footprint.x, 3f, Footprint.y);
        }

        private void EnsurePlate(Color color)
        {
            Transform existing = transform.Find(PlateName);
            GameObject plate;
            if (existing == null)
            {
                plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = PlateName;
                plate.transform.SetParent(transform, false);
            }
            else
            {
                plate = existing.gameObject;
            }

            plate.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            plate.transform.localScale = new Vector3(
                Footprint.x,
                0.03f,
                Footprint.y);
            Collider collider = plate.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            ApplyMaterial(plate.GetComponent<Renderer>(), color);
        }

        private void EnsureLabel(Color color)
        {
            Transform existing = transform.Find(LabelName);
            TextMesh label;
            if (existing == null)
            {
                var labelObject = new GameObject(LabelName);
                labelObject.transform.SetParent(transform, false);
                label = labelObject.AddComponent<TextMesh>();
            }
            else
            {
                label = existing.GetComponent<TextMesh>()
                    ?? existing.gameObject.AddComponent<TextMesh>();
            }

            label.transform.localPosition = new Vector3(
                0f,
                1.8f,
                -Footprint.y * 0.35f);
            label.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            label.text = DefaultName;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = 0.12f;
            label.color = Color.Lerp(color, Color.white, 0.65f);
        }

        private void ApplyMaterial(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            string materialName = AreaId + "_GreyboxMaterial";
            Material current = renderer.sharedMaterial;
            if (current != null && current.name == materialName)
            {
                current.color = color;
                return;
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = material;
        }
    }
}
