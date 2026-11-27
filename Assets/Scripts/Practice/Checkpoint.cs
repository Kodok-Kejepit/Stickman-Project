using Stickman.Core;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>Glowing circular waypoint ring for Chain Course routes.</summary>
    public sealed class Checkpoint : MonoBehaviour
    {
        public const float TriggerRadius = 3.2f;

        MeshRenderer _ringRenderer;
        Material _ringMat;

        void Awake()
        {
            BuildVisual();
        }

        void BuildVisual()
        {
            if (_ringRenderer != null) return;
            _ringMat = RuntimeMat.TintedUnlit(new Color(1.0f, 0.82f, 0.18f, 0.72f), true);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "CheckpointRing";
            var col = ring.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ring.transform.localScale = new Vector3(TriggerRadius * 1.4f, 0.08f, TriggerRadius * 1.4f);
            _ringRenderer = ring.GetComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = _ringMat;
        }

        public void ShowAt(Vector2 xz, bool isNext)
        {
            BuildVisual();
            gameObject.SetActive(true);
            transform.position = new Vector3(xz.x, 0f, xz.y);
            Color c = isNext ? new Color(1.0f, 0.85f, 0.18f, 0.88f) : new Color(0.30f, 0.85f, 1.0f, 0.35f);
            _ringMat.SetColor("_BaseColor", c);
            _ringMat.color = c;
            float s = isNext ? TriggerRadius * 1.5f : TriggerRadius * 1.1f;
            _ringRenderer.transform.localScale = new Vector3(s, 0.08f, s);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
