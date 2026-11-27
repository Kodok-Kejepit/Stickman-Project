using UnityEngine;

namespace Stickman.Input
{
    /// <summary>Cached procedural anti-aliased UI sprites created once on demand.</summary>
    public static class UiSprites
    {
        static Sprite _circle;
        static Sprite _ring;
        static Sprite _rounded;

        public static Sprite Circle => _circle != null ? _circle : (_circle = BuildCircle(128, 0f));
        public static Sprite Ring => _ring != null ? _ring : (_ring = BuildCircle(128, 0.84f));
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = BuildRoundedRect(64, 14f));

        static Sprite BuildCircle(int size, float innerRadiusRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            float outer = half - 1.5f;
            float inner = outer * innerRadiusRatio;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((outer - r) * 0.9f);
                    if (innerRadiusRatio > 0.01f)
                        a *= Mathf.Clamp01((r - inner) * 0.9f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite BuildRoundedRect(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float inner = size * 0.5f - radius - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x - (size - 1) * 0.5f) - inner;
                    float py = Mathf.Abs(y - (size - 1) * 0.5f) - inner;
                    float dx = Mathf.Max(px, 0f);
                    float dy = Mathf.Max(py, 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float a = Mathf.Clamp01(-d * 0.9f + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
