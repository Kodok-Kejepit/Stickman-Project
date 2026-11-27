using Stickman.Practice;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Continuous swept-capsule motor in <c>Update</c>: sub-stepped <c>CapsuleCastNonAlloc</c> + wall sliding
    /// + analytical 2D depenetration so flight never tunnels or stalls in map corners/edges/obstacles.
    /// Zero GC allocations per frame.
    /// </summary>
    public sealed class HeroMotor : MonoBehaviour
    {
        public const int WallLayer = 8;
        public const int DummyLayer = 9;
        public const int HeroLayer = 10;

        [SerializeField] HeroTuning tuning;

        readonly RaycastHit[] _castHits = new RaycastHit[8];

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
            _groundMask = _wallMask; // Layer 8 includes all walls, outer borders, 18 turrets, and 2 nexuses.
            gameObject.layer = HeroLayer;
        }

        public void Teleport(Vector3 worldPos)
        {
            float radius = tuning != null ? tuning.heroRadius : 0.45f;
            Vector2 resolved = ArenaCollisionMath.ResolveDepenetrationXZ(new Vector2(worldPos.x, worldPos.z), radius);
            transform.position = new Vector3(resolved.x, 0f, resolved.y);
            Velocity = Vector3.zero;
            IsBlockedThisFrame = false;
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

            Depenetrate();
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

            Vector2 resolved = ArenaCollisionMath.ResolveDepenetrationXZ(new Vector2(pos.x, pos.z), radius);
            pos = new Vector3(resolved.x, 0f, resolved.y);
            transform.position = pos;

            Vector3 actualMove = pos - startPos;
            actualMove.y = 0f;
            float actualDist = actualMove.magnitude;
            IsBlockedThisFrame = totalDist > 0.01f && actualDist < totalDist * 0.35f;
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
                Vector3 bestNormalXZ = Vector3.zero;

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = _castHits[i];
                    if (hit.distance <= 0.0001f && hit.point == Vector3.zero) continue;
                    Vector3 nXZ = hit.normal;
                    nXZ.y = 0f;
                    if (nXZ.sqrMagnitude < 0.0001f) continue;
                    nXZ.Normalize();
                    if (Vector3.Dot(dir, nXZ) >= -0.01f) continue;

                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        bestIdx = i;
                        bestNormalXZ = nXZ;
                    }
                }

                if (bestIdx < 0)
                {
                    pos += remaining;
                    break;
                }

                float safeTravel = Mathf.Max(0f, bestDist - skin);
                pos += dir * safeTravel;

                float leftoverLen = Mathf.Max(0f, dist - safeTravel);
                Vector3 leftover = dir * leftoverLen;
                remaining = Vector3.ProjectOnPlane(leftover, bestNormalXZ);
                remaining.y = 0f;
            }

            return pos;
        }

        void Depenetrate()
        {
            if (tuning == null) return;
            Vector3 pos = transform.position;
            Vector2 resolved = ArenaCollisionMath.ResolveDepenetrationXZ(new Vector2(pos.x, pos.z), tuning.heroRadius);
            transform.position = new Vector3(resolved.x, 0f, resolved.y);
        }
    }
}
