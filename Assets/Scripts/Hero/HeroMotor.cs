using Stickman.Practice;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Continuous swept-capsule motor in <c>Update</c>: sub-stepped <c>CapsuleCastNonAlloc</c> + wall sliding
    /// + overlap depenetration so 29 u/s dual-cable flight never tunnels through thin walls.
    /// Zero GC allocations per frame.
    /// </summary>
    public sealed class HeroMotor : MonoBehaviour
    {
        public const int WallLayer = 8;
        public const int DummyLayer = 9;
        public const int HeroLayer = 10;

        [SerializeField] HeroTuning tuning;

        readonly RaycastHit[] _castHits = new RaycastHit[8];
        readonly Collider[] _overlapHits = new Collider[8];

        int _wallMask;
        int _groundMask;
        Vector3 _facingDir = new Vector3(0.7071f, 0f, 0.7071f);

        public HeroTuning Tuning => tuning;
        public Vector3 Velocity { get; private set; }
        public Vector3 FacingDir => _facingDir;
        public bool IsBlockedThisFrame { get; private set; }
        public bool IsInBlueBase => Vector2.Distance(new Vector2(transform.position.x, transform.position.z), ArenaLayoutData.BlueNexus) <= ArenaLayoutData.BaseHealingRadius;

        public void SetTuning(HeroTuning value) => tuning = value;

        void Awake()
        {
            _wallMask = 1 << WallLayer;
            _groundMask = (1 << WallLayer) | 1; // Wall + Default (turrets/nexuses) when grounded.
            gameObject.layer = HeroLayer;
        }

        public void Teleport(Vector3 worldPos)
        {
            worldPos.y = 0f;
            float limit = ArenaLayoutData.HalfExtent - (tuning != null ? tuning.heroRadius + 0.5f : 1f);
            worldPos.x = Mathf.Clamp(worldPos.x, -limit, limit);
            worldPos.z = Mathf.Clamp(worldPos.z, -limit, limit);
            transform.position = worldPos;
            Velocity = Vector3.zero;
            IsBlockedThisFrame = false;
            Depenetrate(_wallMask);
        }

        public void FaceDirection(Vector3 dirXZ, float dt, bool instant = false)
        {
            dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            dirXZ.Normalize();
            _facingDir = dirXZ;
            Quaternion target = Quaternion.LookRotation(_facingDir, Vector3.up);
            if (instant || tuning == null)
                transform.rotation = target;
            else
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, tuning.turnSpeedDegPerSec * dt);
        }

        public void MoveSweep(Vector3 velocityXZ, bool airborne, float dt)
        {
            IsBlockedThisFrame = false;
            velocityXZ.y = 0f;
            if (dt <= 0f || tuning == null)
            {
                Velocity = Vector3.zero;
                return;
            }

            Vector3 startPos = transform.position;
            startPos.y = 0f;
            Vector3 totalDelta = velocityXZ * dt;
            float totalDist = totalDelta.magnitude;

            if (totalDist < 0.00001f)
            {
                Velocity = Vector3.zero;
                return;
            }

            int mask = airborne ? _wallMask : _groundMask;
            float radius = tuning.heroRadius;
            float maxStep = Mathf.Max(0.15f, radius * 0.5f);
            int subSteps = Mathf.Clamp(Mathf.CeilToInt(totalDist / maxStep), 1, 8);
            Vector3 subDelta = totalDelta / subSteps;

            Vector3 pos = startPos;
            for (int s = 0; s < subSteps; s++)
            {
                pos = SweepWithSlide(pos, subDelta, radius, mask);
            }

            float limit = ArenaLayoutData.HalfExtent - radius - 0.5f;
            pos.x = Mathf.Clamp(pos.x, -limit, limit);
            pos.z = Mathf.Clamp(pos.z, -limit, limit);
            pos.y = 0f;

            transform.position = pos;
            Depenetrate(mask);

            Vector3 actualMove = transform.position - startPos;
            actualMove.y = 0f;
            Velocity = actualMove / dt;
        }

        Vector3 SweepWithSlide(Vector3 pos, Vector3 delta, float radius, int mask)
        {
            const float skin = 0.02f;
            Vector3 remaining = delta;
            float height = tuning.heroHeight;

            for (int iter = 0; iter < 3; iter++)
            {
                float dist = remaining.magnitude;
                if (dist < 0.00005f) break;

                Vector3 dir = remaining / dist;
                Vector3 p1 = new Vector3(pos.x, radius + 0.05f, pos.z);
                Vector3 p2 = new Vector3(pos.x, Mathf.Max(radius + 0.1f, height - radius), pos.z);

                int hitCount = Physics.CapsuleCastNonAlloc(p1, p2, radius, dir, _castHits, dist + skin, mask, QueryTriggerInteraction.Ignore);
                int bestIdx = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < hitCount; i++)
                {
                    if (_castHits[i].distance < bestDist)
                    {
                        bestDist = _castHits[i].distance;
                        bestIdx = i;
                    }
                }

                if (bestIdx < 0)
                {
                    pos += remaining;
                    break;
                }

                IsBlockedThisFrame = true;
                float safeTravel = Mathf.Max(0f, bestDist - skin);
                pos += dir * safeTravel;

                Vector3 normal = _castHits[bestIdx].normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.0001f) break;
                normal.Normalize();

                float leftoverLen = Mathf.Max(0f, dist - safeTravel);
                Vector3 leftover = dir * leftoverLen;
                remaining = Vector3.ProjectOnPlane(leftover, normal);
                remaining.y = 0f;
            }

            return pos;
        }

        void Depenetrate(int mask)
        {
            if (tuning == null) return;
            float radius = tuning.heroRadius;
            float height = tuning.heroHeight;
            Vector3 pos = transform.position;
            Vector3 p1 = new Vector3(pos.x, radius + 0.05f, pos.z);
            Vector3 p2 = new Vector3(pos.x, Mathf.Max(radius + 0.1f, height - radius), pos.z);

            int count = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, _overlapHits, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlapHits[i];
                if (other == null) continue;
                Vector3 myCenter = pos + new Vector3(0f, height * 0.5f, 0f);
                // Simple horizontal pushout along closest point on static collider.
                Vector3 closest = other.ClosestPoint(myCenter);
                Vector3 away = pos - closest;
                away.y = 0f;
                float d = away.magnitude;
                if (d > 0.0001f && d < radius)
                {
                    pos += (away / d) * (radius - d + 0.01f);
                }
            }
            pos.y = 0f;
            transform.position = pos;
        }
    }
}
