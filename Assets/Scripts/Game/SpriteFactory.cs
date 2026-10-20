using UnityEngine;

namespace AutoBattler.Game
{
    // white squares/diamonds for overlays and star pips, tint with SpriteRenderer.color
    public static class SpriteFactory
    {
        private const int Size = 64;
        private static Sprite _square;
        private static Sprite _diamond;

        public static Sprite Square => _square != null ? _square : _square = Create(diamond: false);
        public static Sprite Diamond => _diamond != null ? _diamond : _diamond = Create(diamond: true);

        private static Sprite Create(bool diamond)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Size * Size];
            float r = Size / 2f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                byte a = 255;
                if (diamond && Mathf.Abs(x + 0.5f - r) + Mathf.Abs(y + 0.5f - r) > r) a = 0;
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        public static SpriteRenderer AddRenderer(GameObject go, Sprite sprite, Color color, int order)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer CreateChild(Transform parent, string name, Sprite sprite, Color color, int order,
            Vector3 localPos, Vector2 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            return AddRenderer(go, sprite, color, order);
        }

        public static Color FromRgb(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
