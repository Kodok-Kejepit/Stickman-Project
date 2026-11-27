using System.Collections.Generic;
using Stickman.Core;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>Pure ghost trajectory recorder and time-interpolating sampler.</summary>
    public sealed class GhostTrack
    {
        readonly List<GhostSample> _samples = new List<GhostSample>(256);

        public int Count => _samples.Count;

        public void Clear() => _samples.Clear();

        public void AddSample(float time, Vector3 pos, float yawDeg)
        {
            _samples.Add(new GhostSample { time = time, position = pos, yawDeg = yawDeg });
        }

        public void Load(GhostSample[] data)
        {
            _samples.Clear();
            if (data == null) return;
            for (int i = 0; i < data.Length; i++) _samples.Add(data[i]);
        }

        public GhostSample[] ToArray() => _samples.ToArray();

        public bool SampleAt(float time, out Vector3 position, out float yawDeg)
        {
            int n = _samples.Count;
            if (n == 0)
            {
                position = Vector3.zero;
                yawDeg = 0f;
                return false;
            }
            if (n == 1 || time <= _samples[0].time)
            {
                position = _samples[0].position;
                yawDeg = _samples[0].yawDeg;
                return true;
            }
            if (time >= _samples[n - 1].time)
            {
                position = _samples[n - 1].position;
                yawDeg = _samples[n - 1].yawDeg;
                return true;
            }

            // Binary search for bracketing samples.
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_samples[mid].time <= time) lo = mid;
                else hi = mid;
            }

            GhostSample a = _samples[lo];
            GhostSample b = _samples[hi];
            float span = b.time - a.time;
            float t = span > 0.0001f ? Mathf.Clamp01((time - a.time) / span) : 0f;
            position = Vector3.Lerp(a.position, b.position, t);
            yawDeg = Mathf.LerpAngle(a.yawDeg, b.yawDeg, t);
            return true;
        }
    }
}
