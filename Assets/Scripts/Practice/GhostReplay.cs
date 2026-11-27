using Stickman.Core;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>Translucent cyan stickman silhouette replaying the player's personal best Chain Course run.</summary>
    public sealed class GhostReplay : MonoBehaviour
    {
        readonly GhostTrack _track = new GhostTrack();
        bool _playing;

        void Awake()
        {
            BuildVisual();
            gameObject.SetActive(false);
        }

        void BuildVisual()
        {
            if (transform.childCount > 0) return;
            var mat = RuntimeMat.TintedUnlit(new Color(0.28f, 0.90f, 1.0f, 0.38f), true);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "GhostBody";
            DestroyCollider(body);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.65f, 0.45f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "GhostHead";
            DestroyCollider(head);
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            head.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
            head.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public void StartReplay(GhostSample[] samples)
        {
            _track.Load(samples);
            _playing = _track.Count > 1;
            gameObject.SetActive(_playing);
        }

        public void StopReplay()
        {
            _playing = false;
            gameObject.SetActive(false);
        }

        public void UpdateTime(float elapsedSec)
        {
            if (!_playing) return;
            if (_track.SampleAt(elapsedSec, out Vector3 pos, out float yawDeg))
            {
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            }
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
