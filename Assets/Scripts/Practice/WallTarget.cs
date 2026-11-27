using Stickman.Core;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>Glowing target ring mounted flush on a wall face for Straight Drill practice.</summary>
    public sealed class WallTarget : MonoBehaviour
    {
        MeshRenderer _outerRenderer;
        MeshRenderer _innerRenderer;
        Material _outerMat;
        Material _innerMat;
        float _flashTimer;

        void Awake()
        {
            BuildVisual();
        }

        void BuildVisual()
        {
            if (_outerRenderer != null) return;

            _outerMat = RuntimeMat.TintedUnlit(new Color(0.15f, 0.92f, 1.0f, 0.55f), true);
            _innerMat = RuntimeMat.TintedUnlit(new Color(1.0f, 0.85f, 0.20f, 0.92f), true);

            var outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outer.name = "OuterRing";
            DestroyCollider(outer);
            outer.transform.SetParent(transform, false);
            outer.transform.localScale = new Vector3(2.8f, 0.06f, 2.8f);
            _outerRenderer = outer.GetComponent<MeshRenderer>();
            _outerRenderer.sharedMaterial = _outerMat;

            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            inner.name = "Bullseye";
            DestroyCollider(inner);
            inner.transform.SetParent(transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            inner.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
            _innerRenderer = inner.GetComponent<MeshRenderer>();
            _innerRenderer.sharedMaterial = _innerMat;
        }

        public void SetTarget(Vector3 wallPoint, float radius)
        {
            BuildVisual();
            gameObject.SetActive(true);
            transform.position = new Vector3(wallPoint.x, 1.0f, wallPoint.z);
            if (_outerRenderer != null)
            {
                float d = radius * 2f;
                _outerRenderer.transform.localScale = new Vector3(d, 0.06f, d);
            }
            _outerMat.SetColor("_BaseColor", new Color(0.15f, 0.92f, 1.0f, 0.55f));
            _outerMat.color = new Color(0.15f, 0.92f, 1.0f, 0.55f);
        }

        public void FlashResult(bool hit)
        {
            _flashTimer = 0.35f;
            if (_outerMat != null)
            {
                Color c = hit ? new Color(0.22f, 1.0f, 0.45f, 0.85f) : new Color(1.0f, 0.28f, 0.22f, 0.85f);
                _outerMat.SetColor("_BaseColor", c);
                _outerMat.color = c;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            transform.Rotate(0f, 90f * Time.unscaledDeltaTime, 0f, Space.Self);
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                if (_flashTimer <= 0f && _outerMat != null)
                {
                    Color c = new Color(0.15f, 0.92f, 1.0f, 0.55f);
                    _outerMat.SetColor("_BaseColor", c);
                    _outerMat.color = c;
                }
            }
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
