using System.Collections.Generic;
using AutoBattler.Core;
using UnityEngine;

namespace AutoBattler.Game
{
    /// <summary>A sprite sheet cut into equal square frames.</summary>
    public sealed class SpriteStrip
    {
        public readonly Sprite[] Frames;
        public SpriteStrip(Sprite[] frames) => Frames = frames;
        public int Length => Frames.Length;
    }

    /// <summary>A texture rebuilt from a sheet of separated 9-slice (or 3-slice) pieces, with its border sizes in pixels.</summary>
    public sealed class SlicedTexture
    {
        public Texture2D Texture;
        public int Left, Right, Top, Bottom;

        public Sprite ToSprite(float pixelsPerUnit) => Sprite.Create(Texture,
            new Rect(0, 0, Texture.width, Texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
            SpriteMeshType.FullRect, new Vector4(Left, Bottom, Right, Top));
    }

    /// <summary>Animations for one unit type in one faction colour.</summary>
    public sealed class UnitArt
    {
        public SpriteStrip Idle, Run, Action;
        /// <summary>Lancer only: attack sheets by direction (Up, UpRight, Right, DownRight, Down).</summary>
        public SpriteStrip[] DirectionalActions;
        public Sprite Portrait;
    }

    /// <summary>
    /// Loads the Tiny Swords pack (Pixel Frog) from Resources and slices it at runtime. Unit sheets are horizontal
    /// strips of square frames, so the frame size is the texture height.
    /// </summary>
    public static class Art
    {
        public const float PixelsPerUnit = 64f;
        private const string Root = "Art/TinySwords/";
        private const int Tile = 64;

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, SpriteStrip> Strips = new Dictionary<string, SpriteStrip>();
        private static readonly Dictionary<(string, Team), UnitArt> Units = new Dictionary<(string, Team), UnitArt>();
        private static readonly Dictionary<string, SlicedTexture> Sliced = new Dictionary<string, SlicedTexture>();
        private static Sprite[,] _grassTiles;
        private static Sprite[] _benchTiles;

        public static Texture2D Texture(string path)
        {
            if (Textures.TryGetValue(path, out var tex)) return tex;
            tex = Resources.Load<Texture2D>(Root + path);
            if (tex == null) throw new MissingReferenceException($"Missing art: Resources/{Root}{path}");
            tex.filterMode = FilterMode.Point;
            Textures[path] = tex;
            return tex;
        }

        public static SpriteStrip Strip(string path)
        {
            if (Strips.TryGetValue(path, out var strip)) return strip;
            var tex = Texture(path);
            int size = tex.height;
            var frames = new Sprite[tex.width / size];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f),
                    PixelsPerUnit, 0, SpriteMeshType.FullRect);
            return Strips[path] = new SpriteStrip(frames);
        }

        private static string UnitFolder(Team team, string unit) =>
            $"Units/{(team == Team.Player ? "Blue" : "Red")} Units/{unit}/";

        public static UnitArt Unit(UnitDefinition def, Team team)
        {
            if (Units.TryGetValue((def.Id, team), out var art)) return art;
            switch (def.Id)
            {
                case "warrior":
                    art = Load(team, "Warrior", "Warrior_Idle", "Warrior_Run", "Warrior_Attack1");
                    break;
                case "archer":
                    art = Load(team, "Archer", "Archer_Idle", "Archer_Run", "Archer_Shoot");
                    break;
                case "lancer":
                    art = Load(team, "Lancer", "Lancer_Idle", "Lancer_Run", "Lancer_Right_Attack");
                    string dir = UnitFolder(team, "Lancer");
                    art.DirectionalActions = new[]
                    {
                        Strip(dir + "Lancer_Up_Attack"), Strip(dir + "Lancer_UpRight_Attack"),
                        Strip(dir + "Lancer_Right_Attack"), Strip(dir + "Lancer_DownRight_Attack"),
                        Strip(dir + "Lancer_Down_Attack"),
                    };
                    break;
                case "monk":
                    art = Load(team, "Monk", "Idle", "Run", "Heal");
                    break;
                default:
                    throw new KeyNotFoundException($"No art mapped for unit '{def.Id}'");
            }
            return Units[(def.Id, team)] = art;
        }

        private static UnitArt Load(Team team, string unit, string idle, string run, string action)
        {
            string dir = UnitFolder(team, unit);
            var art = new UnitArt { Idle = Strip(dir + idle), Run = Strip(dir + run), Action = Strip(dir + action) };
            // Shop portrait: the middle of the first idle frame, where the character stands.
            var first = art.Idle.Frames[0];
            const int crop = 96;
            var r = first.rect;
            art.Portrait = Sprite.Create(first.texture,
                new Rect(r.x + (r.width - crop) / 2f, r.y + (r.height - crop) / 2f, crop, crop),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
            return art;
        }

        public static Sprite Arrow(Team team) => Single(UnitFolder(team, "Archer") + "Arrow");

        public static SpriteStrip HealEffect(Team team) => Strip(UnitFolder(team, "Monk") + "Heal_Effect");

        public static Sprite Single(string path)
        {
            var strip = Strip(path);
            return strip.Frames[0];
        }

        // ---- Terrain -----------------------------------------------------------------------------------------

        private const string Tileset = "Terrain/Tileset/Tilemap_color1";

        /// <summary>Flat-grass 9-slice tile; col/row 0..2 from the top-left of the tileset.</summary>
        public static Sprite GrassTile(int col, int row)
        {
            if (_grassTiles == null)
            {
                _grassTiles = new Sprite[3, 3];
                for (int c = 0; c < 3; c++)
                for (int r = 0; r < 3; r++)
                    _grassTiles[c, r] = TileSprite(c, r);
            }
            return _grassTiles[col, row];
        }

        /// <summary>One-tile-high grass strip: 0 = left end, 1 = middle, 2 = right end.</summary>
        public static Sprite BenchTile(int index)
        {
            if (_benchTiles == null) _benchTiles = new[] { TileSprite(0, 3), TileSprite(1, 3), TileSprite(2, 3) };
            return _benchTiles[index];
        }

        private static Sprite TileSprite(int col, int rowFromTop)
        {
            var tex = Texture(Tileset);
            return Sprite.Create(tex, new Rect(col * Tile, tex.height - (rowFromTop + 1) * Tile, Tile, Tile),
                new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        public static Color WaterColor()
        {
            var tex = Texture("Terrain/Tileset/Water Background color");
            return tex.GetPixel(tex.width / 2, tex.height / 2);
        }

        // ---- UI ----------------------------------------------------------------------------------------------

        private const string Ui = "UI Elements/UI Elements/";

        public static Texture2D Icon(int number) => Texture($"{Ui}Icons/Icon_{number:00}");

        // Pack icons: 03 coin, 04 meat (health), 05 sword, 06 shield, 07 green gem (speed).
        public static Texture2D IconFor(ItemDefinition item) => Icon(StatIcon(item.Bonus));

        public static Texture2D IconFor(AugmentDefinition augment) =>
            augment.GoldPerRound > 0 ? Icon(3) : Icon(StatIcon(augment.TeamBonus));

        public static Texture2D IconFor(RewardOption option) =>
            option.Kind == RewardKind.Item ? IconFor(option.Item) : IconFor(option.Augment);

        private static int StatIcon(StatBonus bonus) =>
            bonus.HpPct > 0 ? 4 : bonus.AttackSpeedPct > 0 ? 7 : bonus.ArmorPct > 0 ? 6 : 5;

        private static readonly Dictionary<Texture2D, Sprite> IconSprites = new Dictionary<Texture2D, Sprite>();

        /// <summary>World-space sprite for an icon (e.g. the item badge on a unit).</summary>
        public static Sprite IconSprite(Texture2D icon)
        {
            if (!IconSprites.TryGetValue(icon, out var sprite))
                IconSprites[icon] = sprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height),
                    new Vector2(0.5f, 0.5f), PixelsPerUnit);
            return sprite;
        }

        public static SlicedTexture WoodPanel() => Stitch(Ui + "Wood Table/WoodTable");
        public static SlicedTexture Paper() => Stitch(Ui + "Papers/RegularPaper");
        public static SlicedTexture BigButton(bool red, bool pressed) =>
            Stitch($"{Ui}Buttons/Big{(red ? "Red" : "Blue")}Button_{(pressed ? "Pressed" : "Regular")}");
        public static SlicedTexture BarBase() => Stitch(Ui + "Bars/SmallBar_Base");

        /// <summary>BigRibbons rows from the top: 0 blue, 1 red, 2 yellow, 3 purple, 4 black.</summary>
        public static SlicedTexture Ribbon(int row)
        {
            var tex = Texture(Ui + "Ribbons/BigRibbons");
            const int band = 128;
            return Stitch(Ui + "Ribbons/BigRibbons", new RectInt(0, tex.height - (row + 1) * band, tex.width, band));
        }

        private static SlicedTexture Stitch(string path) => Stitch(path, null);

        /// <summary>
        /// The pack ships 9-slice UI as pieces separated by transparent gaps. Find the pieces by their opaque
        /// column/row runs and pack them edge to edge so the result can be used with slice borders.
        /// </summary>
        private static SlicedTexture Stitch(string path, RectInt? area)
        {
            string key = area.HasValue ? $"{path}#{area.Value}" : path;
            if (Sliced.TryGetValue(key, out var cached)) return cached;

            var src = Texture(path);
            var region = area ?? new RectInt(0, 0, src.width, src.height);
            var pixels = src.GetPixels32();

            var xRuns = OpaqueRuns(region.xMin, region.xMax, x =>
            {
                for (int y = region.yMin; y < region.yMax; y++) if (pixels[y * src.width + x].a > 0) return true;
                return false;
            });
            var yRuns = OpaqueRuns(region.yMin, region.yMax, y =>
            {
                for (int x = region.xMin; x < region.xMax; x++) if (pixels[y * src.width + x].a > 0) return true;
                return false;
            });

            int width = 0, height = 0;
            foreach (var r in xRuns) width += r.y;
            foreach (var r in yRuns) height += r.y;

            var output = new Color32[width * height];
            int dy = 0;
            foreach (var row in yRuns)
            {
                int dx = 0;
                foreach (var col in xRuns)
                {
                    for (int y = 0; y < row.y; y++)
                    for (int x = 0; x < col.y; x++)
                        output[(dy + y) * width + dx + x] = pixels[(row.x + y) * src.width + col.x + x];
                    dx += col.y;
                }
                dy += row.y;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = key,
            };
            tex.SetPixels32(output);
            tex.Apply();

            var result = new SlicedTexture { Texture = tex };
            if (xRuns.Count == 3)
            {
                result.Left = xRuns[0].y;
                result.Right = xRuns[2].y;
            }
            if (yRuns.Count == 3)
            {
                result.Bottom = yRuns[0].y;
                result.Top = yRuns[2].y;
            }
            return Sliced[key] = result;
        }

        /// <summary>Runs of consecutive opaque lines as (start, length).</summary>
        private static List<Vector2Int> OpaqueRuns(int from, int to, System.Func<int, bool> isOpaque)
        {
            var runs = new List<Vector2Int>();
            int start = -1;
            for (int i = from; i <= to; i++)
            {
                bool opaque = i < to && isOpaque(i);
                if (opaque && start < 0) start = i;
                else if (!opaque && start >= 0)
                {
                    runs.Add(new Vector2Int(start, i - start));
                    start = -1;
                }
            }
            return runs;
        }
    }
}
