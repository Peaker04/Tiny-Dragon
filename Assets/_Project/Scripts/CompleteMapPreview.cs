using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[ExecuteAlways]
public class CompleteMapPreview : MonoBehaviour
{
    [Header("Real Source Data")]
    [SerializeField] private int mapId = 0;
    [SerializeField] private int tileId = 1;
    [SerializeField] private int zoomLevel = 1;
    [SerializeField] private string clientResourceRoot = "Assets/_Project/ImportedDragonBall/res";
    [SerializeField] private string serverResourceRoot = "Assets/_Project/ImportedDragonBall/server";
    [SerializeField] private string sqlPath = "Assets/_Project/ImportedDragonBall/hashirama.sql";

    [Header("Preview")]
    [SerializeField] private float pixelsPerUnit = 24f;
    [SerializeField] private bool rebuildInEditMode = true;
    [SerializeField] private bool showSqlObjects = true;
    [SerializeField] private bool showLabels = true;
    [SerializeField] private bool showSourceUi = true;
    [SerializeField] private int referenceScreenWidthPx = 720;
    [SerializeField] private int referenceScreenHeightPx = 320;
    [SerializeField] private int referenceCameraX = 0;
    [SerializeField] private int referenceCameraY = -1;
    [SerializeField] private Camera targetCamera;
    [SerializeField, HideInInspector] private int builtMapId = -1;
    [SerializeField, HideInInspector] private int builtSourceRevision = -1;

    private const int TilePixelSize = 24;
    private const int SourceBuilderRevision = 13;
    private const int TTree = 0x10;
    private const int TWaterfall = 0x20;
    private const int TTopWaterfall = 0x80;
    private const int TOutside = 0x100;
    private const int TDownOnePixel = 0x200;
    private readonly List<Texture2D> loadedTextures = new();
    private Transform generatedRoot;
    private string status = string.Empty;
    private RealMapData loadedMap;
    private SqlMapTemplate loadedTemplate;

    private enum Layer
    {
        Background = -300,
        Tile = 0,
        Waypoint = 35,
        Mob = 60,
        Npc = 70,
        Effect = 90,
        SourceUi = 1000,
        Label = 1100
    }

    private void Awake()
    {
        EnsurePreview();
    }

    private void Start()
    {
        EnsurePreview();
        SetupCamera();
    }

    private void OnEnable()
    {
        EnsurePreview();
    }

    private void OnValidate()
    {
        mapId = Mathf.Max(0, mapId);
        tileId = Mathf.Max(1, tileId);
        zoomLevel = Mathf.Clamp(zoomLevel, 1, 4);
        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);

        if (!Application.isPlaying && rebuildInEditMode)
        {
            EnsurePreview();
        }
    }

    [ContextMenu("Rebuild From Real Source")]
    public void Rebuild()
    {
        ClearGenerated();
        ClearLoadedTextures();

        generatedRoot = new GameObject(GetGeneratedRootName()).transform;
        generatedRoot.SetParent(transform, false);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string resourceRoot = ResolveProjectPath(projectRoot, clientResourceRoot);
        string serverRoot = ResolveProjectPath(projectRoot, serverResourceRoot);
        string clientMapPath = Path.Combine(resourceRoot, "mymap", $"{mapId}.bytes");
        string serverMapPath = Path.Combine(serverRoot, "map", mapId.ToString());
        string mapPath = File.Exists(clientMapPath) ? clientMapPath : serverMapPath;

        if (!File.Exists(mapPath))
        {
            status = $"Missing real map file: {mapPath}";
            BuildStatusOnly();
            return;
        }

        loadedMap = ReadMapBytes(mapPath);
        loadedTemplate = LoadMapTemplate(projectRoot);
        ApplySourceTileId();

        BuildBackground(resourceRoot, serverRoot);
        BuildBackgroundItems(resourceRoot, serverRoot, 1);
        BuildTiles(resourceRoot, serverRoot);
        BuildBackgroundItems(resourceRoot, serverRoot, 2);

        if (showSqlObjects && loadedTemplate != null)
        {
            BuildWaypoints(resourceRoot);
            BuildMobs(resourceRoot);
            BuildNpcs(resourceRoot);
            BuildEffects(resourceRoot);
        }

        BuildBackgroundItems(resourceRoot, serverRoot, 3);
        BuildBackgroundItems(resourceRoot, serverRoot, 4);

        if (showSourceUi)
        {
            BuildSourceHud(resourceRoot);
        }

        BuildLabels(resourceRoot);
        SetupCamera();
        builtMapId = mapId;
        builtSourceRevision = SourceBuilderRevision;
    }

    private void EnsurePreview()
    {
        if (generatedRoot == null && transform.Find(GetGeneratedRootName()) != null)
        {
            generatedRoot = transform.Find(GetGeneratedRootName());
        }

        if (generatedRoot == null || generatedRoot.childCount == 0 || builtMapId != mapId || builtSourceRevision != SourceBuilderRevision)
        {
            Rebuild();
        }
    }

    private static string ResolveProjectPath(string projectRoot, string path)
    {
        string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(normalized) ? normalized : Path.GetFullPath(Path.Combine(projectRoot, normalized));
    }

    private RealMapData ReadMapBytes(string mapPath)
    {
        byte[] bytes = File.ReadAllBytes(mapPath);
        if (bytes.Length < 2)
        {
            throw new InvalidDataException($"Map file is too short: {mapPath}");
        }

        int width = bytes[0];
        int height = bytes[1];
        int expected = width * height;
        int[] tiles = new int[expected];

        for (int i = 0; i < expected && i + 2 < bytes.Length; i++)
        {
            tiles[i] = bytes[i + 2];
        }

        return new RealMapData(width, height, tiles);
    }

    private SqlMapTemplate LoadMapTemplate(string projectRoot)
    {
        string resolvedSqlPath = ResolveProjectPath(projectRoot, sqlPath);
        if (!File.Exists(resolvedSqlPath))
        {
            status = $"Loaded tiles only. Missing SQL: {resolvedSqlPath}";
            return null;
        }

        string sql = File.ReadAllText(resolvedSqlPath);
        Match insert = Regex.Match(sql, @"INSERT INTO `map_template`.*?VALUES\s*(?<values>.*?);", RegexOptions.Singleline);
        if (!insert.Success)
        {
            status = "Loaded tiles only. map_template insert not found in SQL.";
            return null;
        }

        List<string> fields = null;
        foreach (Match row in Regex.Matches(insert.Groups["values"].Value, @"\((?<body>.*?)\)(?:,|$)", RegexOptions.Singleline))
        {
            List<string> rowFields = SplitSqlTuple(row.Groups["body"].Value);
            if (rowFields.Count > 0 && int.TryParse(rowFields[0], out int rowId) && rowId == mapId)
            {
                fields = rowFields;
                break;
            }
        }

        if (fields == null)
        {
            status = $"Loaded tiles only. map_template id {mapId} not found in SQL.";
            return null;
        }

        if (fields.Count < 11)
        {
            status = $"Loaded tiles only. Could not parse map_template id {mapId}.";
            return null;
        }

        return new SqlMapTemplate
        {
            Id = mapId,
            Name = Unquote(fields[1]),
            Data = Unquote(fields[2]),
            Waypoints = ParseWaypoints(fields[5]),
            Mobs = ParseIntObjectList(fields[6], 5),
            Npcs = ParseIntObjectList(fields[7], 4),
            Effects = ParseEffects(fields[10]),
            BackgroundEffects = ParseBackgroundEffects(fields[10]),
            MobNames = LoadNameLookup(sql, "mob_template"),
            NpcNames = LoadNameLookup(sql, "npc_template")
        };
    }

    private static Dictionary<int, string> LoadNameLookup(string sql, string tableName)
    {
        Dictionary<int, string> lookup = new();
        Match insert = Regex.Match(sql, $@"INSERT INTO `{Regex.Escape(tableName)}`.*?VALUES\s*(?<values>.*?);", RegexOptions.Singleline);
        if (!insert.Success)
        {
            return lookup;
        }

        foreach (Match row in Regex.Matches(insert.Groups["values"].Value, @"\((?<body>.*?)\)(?:,|$)", RegexOptions.Singleline))
        {
            List<string> fields = SplitSqlTuple(row.Groups["body"].Value);
            if (fields.Count < 2 || !int.TryParse(fields[0].Trim(), out int id))
            {
                continue;
            }

            int nameIndex = tableName == "mob_template" ? 2 : 1;
            if (fields.Count > nameIndex)
            {
                lookup[id] = Unquote(fields[nameIndex]);
            }
        }

        return lookup;
    }

    private static List<string> SplitSqlTuple(string tuple)
    {
        List<string> fields = new();
        bool inQuote = false;
        bool escaped = false;
        int start = 0;

        for (int i = 0; i < tuple.Length; i++)
        {
            char c = tuple[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '\'')
            {
                inQuote = !inQuote;
                continue;
            }

            if (c == ',' && !inQuote)
            {
                fields.Add(tuple.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        fields.Add(tuple.Substring(start).Trim());
        return fields;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            value = value.Substring(1, value.Length - 2);
        }

        return value.Replace("\\'", "'").Replace("\\\"", "\"");
    }

    private static List<WaypointData> ParseWaypoints(string field)
    {
        string text = Unquote(field);
        List<WaypointData> result = new();
        foreach (Match match in Regex.Matches(text, @"\[(?<body>[^\[\]]+)\]"))
        {
            string body = match.Groups["body"].Value;
            List<int> numbers = ExtractInts(body);
            if (numbers.Count < 9)
            {
                continue;
            }

            string name = "Waypoint";
            Match nameMatch = Regex.Match(body, "\"(?<name>[^\"]+)\"");
            if (nameMatch.Success)
            {
                name = nameMatch.Groups["name"].Value;
            }

            int offset = numbers.Count - 9;
            result.Add(new WaypointData
            {
                Name = name,
                MinX = numbers[offset],
                MinY = numbers[offset + 1],
                MaxX = numbers[offset + 2],
                MaxY = numbers[offset + 3],
                GoMap = numbers[offset + 6],
                GoX = numbers[offset + 7],
                GoY = numbers[offset + 8]
            });
        }

        return result;
    }

    private static List<int> ParseBackgroundEffects(string field)
    {
        string text = Unquote(field);
        List<int> result = new();
        foreach (Match match in Regex.Matches(text, "\"value\"\\s*:\\s*\"(?<value>-?\\d+)\"\\s*,\\s*\"key\"\\s*:\\s*\"beff\""))
        {
            if (int.TryParse(match.Groups["value"].Value, out int id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static List<IntObjectData> ParseIntObjectList(string field, int minNumbers)
    {
        string text = Unquote(field);
        List<IntObjectData> result = new();
        foreach (Match match in Regex.Matches(text, @"\[(?<body>[^\[\]]+)\]"))
        {
            List<int> numbers = ExtractInts(match.Groups["body"].Value);
            if (numbers.Count >= minNumbers)
            {
                result.Add(new IntObjectData { Values = numbers.ToArray() });
            }
        }

        return result;
    }

    private static List<EffectData> ParseEffects(string field)
    {
        string text = Unquote(field);
        List<EffectData> result = new();
        foreach (Match match in Regex.Matches(text, "\"value\"\\s*:\\s*\"(?<value>[^\"]+)\""))
        {
            string[] parts = match.Groups["value"].Value.Split('.');
            if (parts.Length >= 4 && int.TryParse(parts[0], out int id) && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int y))
            {
                result.Add(new EffectData { Id = id, X = x, Y = y });
            }
        }

        return result;
    }

    private static List<int> ExtractInts(string text)
    {
        List<int> numbers = new();
        foreach (Match match in Regex.Matches(text, @"-?\d+"))
        {
            if (int.TryParse(match.Value, out int value))
            {
                numbers.Add(value);
            }
        }

        return numbers;
    }

    private void BuildBackground(string resourceRoot, string serverRoot)
    {
        int typeBg = 0;
        int bgType = 0;
        if (loadedTemplate != null)
        {
            List<int> data = ExtractInts(loadedTemplate.Data);
            // The server sends TileMap.bgID from the final value in map_template.data.
            // The first values describe planet/type metadata rather than the background image set.
            if (data.Count > 4)
            {
                typeBg = data[4];
            }

            if (data.Count > 2)
            {
                bgType = data[2];
            }
        }

        string bgFolder = Path.Combine(resourceRoot, $"x{zoomLevel}", "bg");
        List<BackgroundLayer> layers = LoadBackgroundLayers(bgFolder, serverRoot, typeBg, bgType);
        if (layers.Count == 0)
        {
            return;
        }

        int[] yb = CalculateBackgroundY(typeBg, layers, Mathf.Max(1, referenceScreenHeightPx * 2 / 3));
        int[] layerSpeed = GetLayerSpeeds(typeBg, layers.Count);
        int[] deltaY = GetLayerDeltaY(typeBg, layers.Count);
        int cameraY = GetClientCameraY();
        int repeatWidthPx = Mathf.Max(loadedMap.Width * TilePixelSize, referenceScreenWidthPx);

        for (int layerIndex = layers.Count - 1; layerIndex >= 0; layerIndex--)
        {
            BackgroundLayer layer = layers[layerIndex];
            int speed = layerSpeed[Mathf.Min(layerIndex, layerSpeed.Length - 1)];
            int delta = deltaY[Mathf.Min(layerIndex, deltaY.Length - 1)];
            int parallaxOffset = speed == 0 ? 0 : cameraY >> speed;
            int yPx = cameraY + yb[layerIndex] - ((delta > 0) ? (cameraY >> delta) : 0);
            int startX = -layer.Width - parallaxOffset;
            int endX = repeatWidthPx + layer.Width;

            for (int x = startX; x <= endX; x += layer.Width)
            {
                GameObject obj = CreateSpriteObject($"Source_BG_b{typeBg}{layerIndex}_{x}", layer.Sprite, ToWorld2(x, yPx), Layer.Background);
                SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = (int)Layer.Background + (layers.Count - layerIndex);
                }
            }
        }

        BuildMapSpecificBackground(resourceRoot, typeBg, layerSpeed, cameraY, layers.Count);
    }

    // The original client draws this repeating pillar only while rendering map 1.
    // Keep the same source asset and parallax formula from GameCanvas.paintBackground.
    private void BuildMapSpecificBackground(string resourceRoot, int typeBg, int[] layerSpeed, int cameraY, int layerCount)
    {
        if (mapId != 1 || typeBg != 0 || loadedMap == null)
        {
            return;
        }

        Sprite pillar = LoadSprite(
            Path.Combine(resourceRoot, $"x{zoomLevel}", "bg", "caycot.png"),
            new Vector2(0f, 0f)
        );
        if (pillar == null)
        {
            return;
        }

        int layerSpeedIndex = Mathf.Min(2, layerSpeed.Length - 1);
        int sourceX = GetClientCameraX() - (GetClientCameraX() >> layerSpeed[layerSpeedIndex]) + 300;
        int sourceY = cameraY - (cameraY >> 3);
        int pillarHeight = Mathf.Max(1, Mathf.RoundToInt(pillar.rect.height));

        for (int y = 0; y < loadedMap.Height * TilePixelSize; y += pillarHeight)
        {
            GameObject obj = CreateSpriteObject($"Source_Map1_Pillar_{y}", pillar, ToWorld2(sourceX, sourceY + y), Layer.Background);
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = (int)Layer.Background + Mathf.Max(1, layerCount - 2);
            }
        }
    }

    private List<BackgroundLayer> LoadBackgroundLayers(string bgFolder, string serverRoot, int typeBg, int bgType)
    {
        if (bgType == 100)
        {
            return LoadSpecialBackgroundLayers(bgFolder, serverRoot, typeBg, new[] { "b100.png", "b100.png", "b82-1.png", "b93.png" });
        }

        int layerCount = GetBackgroundLayerCount(typeBg);
        List<BackgroundLayer> layers = new();
        for (int i = 0; i < layerCount; i++)
        {
            string name = bgType != 0 ? $"b{typeBg}{i}-{bgType}.png" : $"b{typeBg}{i}.png";
            string path = Path.Combine(bgFolder, name);
            if (!File.Exists(path))
            {
                path = Path.Combine(bgFolder, $"b{typeBg}0.png");
            }

            Texture2D texture = File.Exists(path) ? LoadTexture(path) : null;
            if (texture == null)
            {
                continue;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 1f), pixelsPerUnit);
            sprite.name = Path.GetFileNameWithoutExtension(path);
            layers.Add(new BackgroundLayer { Sprite = sprite, Width = texture.width, Height = texture.height });
        }

        return layers;
    }

    private List<BackgroundLayer> LoadSpecialBackgroundLayers(string bgFolder, string serverRoot, int typeBg, string[] names)
    {
        List<BackgroundLayer> layers = new();
        foreach (string name in names)
        {
            string path = Path.Combine(bgFolder, name);
            if (!File.Exists(path))
            {
                string serverPath = Path.Combine(serverRoot, "normal", "data", zoomLevel.ToString(), "bg", name);
                path = File.Exists(serverPath) ? serverPath : Path.Combine(bgFolder, $"b{typeBg}0.png");
            }

            Texture2D texture = File.Exists(path) ? LoadTexture(path) : null;
            if (texture == null)
            {
                continue;
            }

            layers.Add(new BackgroundLayer
            {
                Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 1f), pixelsPerUnit),
                Width = texture.width,
                Height = texture.height
            });
        }

        return layers;
    }

    private static int GetBackgroundLayerCount(int typeBg)
    {
        return typeBg switch
        {
            2 or 6 => 5,
            3 or 11 or 12 => 3,
            10 or 13 or 15 => 2,
            _ => 4
        };
    }

    private static int[] GetLayerSpeeds(int typeBg, int layerCount)
    {
        int[] speeds = typeBg is 0 or 16
            ? new[] { 1, 3, 5, 7 }
            : new[] { 1, 2, 3, 7, 8 };
        return ExpandToCount(speeds, layerCount, 8);
    }

    private static int[] GetLayerDeltaY(int typeBg, int layerCount)
    {
        int[] deltas = typeBg switch
        {
            0 => new[] { 2, 3, 4, 6 },
            1 => new[] { 1, 2, 3, 6 },
            2 => new[] { 1, 2, 5, 8, 10 },
            4 => new[] { 1, 2, 3, 7 },
            5 => new[] { 2, 4, 10, 15 },
            6 => new[] { 2, 3, 4, 7, 10 },
            7 => new[] { 3, 4, 5, 6 },
            8 => new[] { 1, 2, 4, 8 },
            9 => new[] { 2, 3, 5, 8 },
            10 => new[] { 0, 2 },
            11 => new[] { 2, 3, 6 },
            12 => new[] { 2, 3, 4 },
            13 => new[] { 5, 5 },
            15 => new[] { 2, 3 },
            16 => new[] { 2, 3, 4, 6 },
            _ => new[] { 2, 3, 4, 6 }
        };
        return ExpandToCount(deltas, layerCount, 6);
    }

    private static int[] ExpandToCount(int[] values, int count, int fallback)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = i < values.Length ? values[i] : fallback;
        }

        return result;
    }

    private int[] CalculateBackgroundY(int typeBg, List<BackgroundLayer> layers, int gameH23)
    {
        int[] yb = new int[Mathf.Max(5, layers.Count)];
        int h0 = LayerHeight(layers, 0);
        int h1 = LayerHeight(layers, 1);
        int h2 = LayerHeight(layers, 2);
        int h3 = LayerHeight(layers, 3);

        switch (typeBg)
        {
            case 0:
                yb[0] = gameH23 - h0 + 70;
                yb[1] = yb[0] - h1 + 20;
                yb[2] = yb[1] - h2 + 30;
                yb[3] = yb[2] - h3 + 50;
                break;
            case 1:
                yb[0] = gameH23 - h0 + 120;
                yb[1] = yb[0] - h1 + 40;
                yb[2] = yb[1] - 90;
                yb[3] = yb[2] - 25;
                break;
            case 8:
                yb[0] = gameH23 - 103 + 150;
                if (mapId == 103)
                {
                    yb[0] -= 100;
                }

                yb[1] = yb[0] - h1 - 10;
                yb[2] = yb[1] - h2 + 40;
                yb[3] = yb[2] - h3 + 10;
                break;
            default:
                yb[0] = gameH23 - h0 + 75;
                yb[1] = yb[0] - h1 + 50;
                yb[2] = yb[1] - h2 + 50;
                yb[3] = yb[2] - h3 + 90;
                break;
        }

        return yb;
    }

    private static int LayerHeight(List<BackgroundLayer> layers, int index)
    {
        return index >= 0 && index < layers.Count ? layers[index].Height : 1;
    }

    private int GetClientCameraX()
    {
        if (referenceCameraX >= 0)
        {
            return Mathf.Clamp(referenceCameraX, 0, Mathf.Max(0, loadedMap.Width * TilePixelSize - referenceScreenWidthPx));
        }

        return 0;
    }

    private int GetClientCameraY()
    {
        if (referenceCameraY >= 0)
        {
            return Mathf.Clamp(referenceCameraY, 0, Mathf.Max(0, loadedMap.Height * TilePixelSize - referenceScreenHeightPx));
        }

        return EstimateClientCameraY();
    }

    private int EstimateClientCameraY()
    {
        int focusY = 432;
        if (loadedTemplate?.Npcs.Count > 0 && loadedTemplate.Npcs[0].Values.Length >= 3)
        {
            focusY = loadedTemplate.Npcs[0].Values[2];
        }

        int gameH23 = Mathf.Max(1, referenceScreenHeightPx * 2 / 3);
        return Mathf.Clamp(focusY - gameH23, 0, Mathf.Max(0, loadedMap.Height * TilePixelSize - referenceScreenHeightPx));
    }

    private void BuildBackgroundItems(string resourceRoot, string serverRoot, int targetLayer)
    {
        string templatePath = Path.Combine(serverRoot, "bg_data");
        string mapItemPath = Path.Combine(serverRoot, "data", "nro", "map", "item_bg_map_data", mapId.ToString());
        if (!File.Exists(templatePath) || !File.Exists(mapItemPath))
        {
            return;
        }

        List<BgItemTemplate> templates = ReadBgItemTemplates(templatePath);
        List<BgItemPlacement> placements = ReadBgItemPlacements(mapItemPath);
        if (templates.Count == 0 || placements.Count == 0)
        {
            return;
        }

        int rendered = 0;
        for (int i = 0; i < placements.Count; i++)
        {
            BgItemPlacement placement = placements[i];
            if (placement.Id < 0)
            {
                continue;
            }

            // Some newer maps reference background image ids that are present in the
            // resource pack but absent from this repository's older bg_data table.
            // Preserve the real placement and source image instead of silently losing it.
            if (placement.Id >= templates.Count)
            {
                BuildUnmappedBackgroundItem(resourceRoot, serverRoot, placement, targetLayer);
                continue;
            }

            BgItemTemplate template = templates[placement.Id];
            if (template.Layer != targetLayer)
            {
                continue;
            }

            string imagePath = ResolveBgItemImagePath(resourceRoot, serverRoot, template.ImageId);
            Sprite sprite = LoadSprite(imagePath, new Vector2(0f, 1f));
            if (sprite == null)
            {
                AddMissingBgItemLabel(placement, template);
                continue;
            }

            int transX = GetBgItemTransX(template);
            int transY = GetBgItemTransY(template);
            bool flipped = ShouldFlipDuplicateBgItem(placements, i, placement.Id);
            Vector2 pos = ToWorld2(placement.TileX * TilePixelSize + template.Dx + transX, placement.TileY * TilePixelSize + template.Dy + transY);
            GameObject obj = CreateSpriteObject($"Source_BgItem_{placement.Id}_img_{template.ImageId}_layer_{template.Layer}", sprite, pos, Layer.Background);
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = BgItemLayerToSort(template.Layer);
                renderer.flipX = flipped;
            }

            BuildDoubleMapMirrorBgItem(imagePath, template, placement, transX, transY);
            rendered++;
        }

        if (rendered > 0)
        {
            status = $"{status} BG layer {targetLayer}: {rendered} source items.";
        }
    }

    private void BuildUnmappedBackgroundItem(string resourceRoot, string serverRoot, BgItemPlacement placement, int targetLayer)
    {
        // The client renders item layers 1, 3, and 4 around the tile map. The missing
        // template metadata cannot supply that layer, so keep the source image behind
        // terrain once, rather than duplicating it in every source layer pass.
        if (targetLayer != 1)
        {
            return;
        }

        string imagePath = ResolveBgItemImagePath(resourceRoot, serverRoot, placement.Id);
        Sprite sprite = LoadSprite(imagePath, new Vector2(0f, 1f));
        if (sprite == null)
        {
            return;
        }

        Vector2 pos = ToWorld2(placement.TileX * TilePixelSize, placement.TileY * TilePixelSize);
        GameObject obj = CreateSpriteObject($"Source_UnmappedBgItem_{placement.Id}", sprite, pos, Layer.Background);
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = (int)Layer.Tile - 20;
        }
    }

    private static List<BgItemTemplate> ReadBgItemTemplates(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int offset = 0;
        int count = ReadUnsignedShort(data, ref offset);
        List<BgItemTemplate> result = new(count);
        for (int i = 0; i < count && offset < data.Length; i++)
        {
            BgItemTemplate template = new()
            {
                Id = i,
                ImageId = ReadShort(data, ref offset),
                Layer = ReadByte(data, ref offset),
                Dx = ReadShort(data, ref offset),
                Dy = ReadShort(data, ref offset)
            };

            int tileCount = ReadByte(data, ref offset);
            for (int t = 0; t < tileCount && offset + 1 < data.Length; t++)
            {
                offset += 2;
            }

            result.Add(template);
        }

        return result;
    }

    private static List<BgItemPlacement> ReadBgItemPlacements(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int offset = 0;
        int count = ReadUnsignedShort(data, ref offset);
        List<BgItemPlacement> result = new(count);
        for (int i = 0; i < count && offset + 5 < data.Length; i++)
        {
            result.Add(new BgItemPlacement
            {
                Id = ReadShort(data, ref offset),
                TileX = ReadShort(data, ref offset),
                TileY = ReadShort(data, ref offset)
            });
        }

        return result;
    }

    private string ResolveBgItemImagePath(string resourceRoot, string serverRoot, int imageId)
    {
        string[] candidates =
        {
            Path.Combine(resourceRoot, $"x{zoomLevel}", "mapBackGround"),
            Path.Combine(resourceRoot, $"x{zoomLevel}", "bg"),
            Path.Combine(serverRoot, "normal", "image", "pixel", "bg"),
            Path.Combine(serverRoot, "normal", "image", zoomLevel.ToString(), "bg"),
            Path.Combine(serverRoot, "normal", "image", "1", "bg"),
            Path.Combine(serverRoot, "normal", "image", "2", "bg"),
            Path.Combine(serverRoot, "normal", "image", "sieu net", "bg"),
            Path.Combine(serverRoot, "normal", "image", "anh dep", "bg"),
            Path.Combine(serverRoot, "data", "map", "bg")
        };

        foreach (string candidate in candidates)
        {
            string imagePath = Path.Combine(candidate, $"{imageId}.png");
            if (File.Exists(imagePath))
            {
                return imagePath;
            }
        }

        return string.Empty;
    }

    private int GetBgItemTransX(BgItemTemplate template)
    {
        int cmx = GetClientCameraX();
        if (template.Layer == 4)
        {
            return -cmx / 2 + 100;
        }

        if ((template.ImageId == 28 || template.ImageId is 67 or 68 or 69 or 70) && template.Layer == 3)
        {
            return -cmx / 3 + 200;
        }

        if (IsMiniBg(template.ImageId) && template.Layer < 4)
        {
            return -(cmx >> 4) + 50;
        }

        return 0;
    }

    private int GetBgItemTransY(BgItemTemplate template)
    {
        if (IsMiniBg(template.ImageId) && template.Layer < 4)
        {
            return (GetClientCameraY() >> 5) - 15;
        }

        return 0;
    }

    private bool ShouldFlipDuplicateBgItem(List<BgItemPlacement> placements, int index, int id)
    {
        if (mapId == 45 || id is 156 or 330 or 345 or 334 || mapId is 54 or 55 or 56 or 57 or 58 or 59 or 103)
        {
            return false;
        }

        int count = 0;
        foreach (BgItemPlacement placement in placements)
        {
            if (placement.Id == id)
            {
                count++;
            }
        }

        return count > 2 && index % 2 != 0;
    }

    private void BuildDoubleMapMirrorBgItem(string imagePath, BgItemTemplate template, BgItemPlacement placement, int transX, int transY)
    {
        if (!IsDoubleMap() || !ShouldMirrorDoubleMapBgItem(template.ImageId) || loadedMap == null)
        {
            return;
        }

        Sprite mirrorSprite = LoadSprite(imagePath, new Vector2(1f, 1f));
        if (mirrorSprite == null)
        {
            return;
        }

        int sourceX = placement.TileX * TilePixelSize + template.Dx + transX;
        int sourceY = placement.TileY * TilePixelSize + template.Dy + transY;
        int mirrorX = loadedMap.Width * TilePixelSize - sourceX;
        GameObject obj = CreateSpriteObject($"Source_BgItemMirror_{placement.Id}_img_{template.ImageId}_layer_{template.Layer}", mirrorSprite, ToWorld2(mirrorX, sourceY), Layer.Background);
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = BgItemLayerToSort(template.Layer);
            renderer.flipX = true;
        }
    }

    private static bool IsMiniBg(int imageId)
    {
        return imageId is 79 or 80 or 81 or 85 or 86 or 90 or 91 or 92 or 99 or 100
            or 101 or 102 or 103 or 104 or 105 or 106 or 107 or 108;
    }

    private bool IsDoubleMap()
    {
        return mapId is 45 or 46 or 48 or 51 or 52 or 103 or 112 or 113 or 115 or 117
            or 118 or 119 or 120 or 121 or 125 or 129 or 130;
    }

    private static bool ShouldMirrorDoubleMapBgItem(int imageId)
    {
        if (imageId <= 137 || imageId is 156 or 159 or 157 or 165 or 167 or 168 or 169 or 170 or 238)
        {
            return false;
        }

        return imageId < 241 || imageId >= 266;
    }

    private static int BgItemLayerToSort(int layer)
    {
        return layer switch
        {
            1 => (int)Layer.Tile - 20,
            2 => (int)Layer.Waypoint + 10,
            3 => (int)Layer.Effect + 30,
            4 => (int)Layer.Effect + 50,
            _ => (int)Layer.Tile - 10
        };
    }

    private void AddMissingBgItemLabel(BgItemPlacement placement, BgItemTemplate template)
    {
        if (!showLabels)
        {
            return;
        }

        GameObject obj = new GameObject($"Missing_BgItem_{placement.Id}_img_{template.ImageId}");
        obj.transform.SetParent(generatedRoot, false);
        obj.transform.localPosition = ToWorld(placement.TileX * TilePixelSize + template.Dx, placement.TileY * TilePixelSize + template.Dy);
        AddText(obj.transform, $"bg {template.ImageId}", Vector2.zero, Color.gray);
    }

    private void ApplySourceTileId()
    {
        if (loadedTemplate == null)
        {
            return;
        }

        List<int> data = ExtractInts(loadedTemplate.Data);
        if (data.Count > 3 && data[3] > 0)
        {
            tileId = data[3];
        }
    }

    private void BuildTiles(string resourceRoot, string serverRoot)
    {
        Dictionary<int, Sprite> tileSprites = LoadTileSprites(resourceRoot);
        Dictionary<int, int> tileTypes = LoadTileTypes(serverRoot);
        int missingTiles = 0;

        for (int y = 0; y < loadedMap.Height; y++)
        {
            for (int x = 0; x < loadedMap.Width; x++)
            {
                if (x == 0 || x == loadedMap.Width - 1)
                {
                    continue;
                }

                int tile = loadedMap.Tiles[y * loadedMap.Width + x];
                if (tile <= 0)
                {
                    continue;
                }

                int tileType = tileTypes.TryGetValue(tile, out int sourceType) ? sourceType : 0;
                if ((tileType & TOutside) == TOutside)
                {
                    continue;
                }

                if (tileId == 13)
                {
                    continue;
                }

                if (!tileSprites.TryGetValue(tile, out Sprite sprite))
                {
                    missingTiles++;
                    continue;
                }

                GameObject obj = new GameObject($"tile_{x}_{y}_{tile:00}");
                obj.transform.SetParent(generatedRoot, false);
                obj.transform.localPosition = ToWorld(x * TilePixelSize, y * TilePixelSize);
                SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = (int)Layer.Tile;

                if ((tileType & TTree) == TTree)
                {
                    renderer.sortingOrder = (int)Layer.Tile + 1;
                }
            }
        }

        status = missingTiles > 0
            ? $"Loaded real map {mapId}; {missingTiles} tile refs missing in tileID {tileId}."
            : $"Loaded real map {mapId} from source.";
    }

    private Dictionary<int, int> LoadTileTypes(string serverRoot)
    {
        Dictionary<int, int> result = new();
        string path = Path.Combine(serverRoot, "data", "nro", "map", "tile_set_info");
        if (!File.Exists(path))
        {
            return result;
        }

        byte[] data = File.ReadAllBytes(path);
        int offset = 0;
        int tileMapCount = ReadUByte(data, ref offset);
        int sourceTileIndex = Mathf.Max(0, tileId - 1);

        for (int i = 0; i < tileMapCount && offset < data.Length; i++)
        {
            int typeGroupCount = ReadUByte(data, ref offset);
            for (int group = 0; group < typeGroupCount && offset < data.Length; group++)
            {
                int type = ReadInt(data, ref offset);
                int indexCount = ReadUByte(data, ref offset);
                for (int k = 0; k < indexCount && offset < data.Length; k++)
                {
                    int tileValue = ReadUByte(data, ref offset);
                    if (i != sourceTileIndex)
                    {
                        continue;
                    }

                    result[tileValue] = result.TryGetValue(tileValue, out int existing) ? existing | type : type;
                }
            }
        }

        return result;
    }

    private Dictionary<int, Sprite> LoadTileSprites(string resourceRoot)
    {
        Dictionary<int, Sprite> sprites = new();
        string tileFolder = Path.Combine(resourceRoot, $"x{zoomLevel}", "t", tileId.ToString());

        if (!Directory.Exists(tileFolder))
        {
            status = $"Tile folder missing: {tileFolder}";
            return sprites;
        }

        foreach (string pngPath in Directory.GetFiles(tileFolder, "t_*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(pngPath);
            Match match = Regex.Match(name, @"t_(\d+)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int tileNumber))
            {
                continue;
            }

            Texture2D texture = LoadTexture(pngPath);
            if (texture == null)
            {
                continue;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 1f), pixelsPerUnit);
            sprite.name = name;
            sprites[tileNumber] = sprite;
        }

        return sprites;
    }

    private Texture2D LoadTexture(string path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = Path.GetFileNameWithoutExtension(path)
        };

        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
            return null;
        }

        loadedTextures.Add(texture);
        return texture;
    }

    private void BuildWaypoints(string resourceRoot)
    {
        Sprite zoneSprite = LoadSprite(Path.Combine(resourceRoot, $"x{zoomLevel}", "mainimage", "khuvuc.png"), new Vector2(0.5f, 0.5f));
        Sprite arrowSprite = LoadSprite(Path.Combine(resourceRoot, $"x{zoomLevel}", "mainimage", "myTexture2darrow.png"), new Vector2(0.5f, 0.5f));

        foreach (WaypointData waypoint in loadedTemplate.Waypoints)
        {
            float x = (waypoint.MinX + waypoint.MaxX) * 0.5f;
            float y = (waypoint.MinY + waypoint.MaxY) * 0.5f;
            GameObject obj = CreateSpriteObject($"Waypoint_to_{waypoint.GoMap}_{waypoint.Name}", zoneSprite ?? arrowSprite, ToWorld2(x, y), Layer.Waypoint);
            AddText(obj.transform, $"{waypoint.Name} -> map {waypoint.GoMap}", Vector2.up * 0.48f, Color.cyan);
        }
    }

    private void BuildMobs(string resourceRoot)
    {
        Sprite shadow = LoadSprite(Path.Combine(resourceRoot, $"x{zoomLevel}", "mainimage", "myTexture2dbong.png"), new Vector2(0.5f, 0.5f));
        Sprite hpBar = LoadSprite(Path.Combine(resourceRoot, $"x{zoomLevel}", "mainimage", "myTexture2dmobHP.png"), new Vector2(0.5f, 0.5f));
        int index = 0;
        foreach (IntObjectData mob in loadedTemplate.Mobs)
        {
            int[] v = mob.Values;
            if (v.Length < 5)
            {
                continue;
            }

            Vector2 pos = ToWorld2(v[3], v[4] - 8);
            GameObject obj = CreateSpriteObject($"Mob_{index}_id_{v[0]}", shadow, pos, Layer.Mob);
            if (hpBar != null)
            {
                GameObject hp = CreateSpriteObject("Source_Mob_HP", hpBar, pos + Vector2.up * 0.42f, Layer.Mob);
                hp.transform.SetParent(obj.transform, true);
            }

            string name = loadedTemplate.MobNames.TryGetValue(v[0], out string mobName) ? mobName : $"mob {v[0]}";
            AddText(obj.transform, name, Vector2.up * 0.62f, Color.white);
            index++;
        }
    }

    private void BuildNpcs(string resourceRoot)
    {
        Sprite shadow = LoadSprite(Path.Combine(resourceRoot, $"x{zoomLevel}", "mainimage", "shadowBig.png"), new Vector2(0.5f, 0.5f));
        int index = 0;
        foreach (IntObjectData npc in loadedTemplate.Npcs)
        {
            int[] v = npc.Values;
            if (v.Length < 3)
            {
                continue;
            }

            Vector2 pos = ToWorld2(v[1], v[2] - 8);
            Sprite avatar = v.Length >= 4
                ? LoadSprite(Path.Combine(resourceRoot, $"x{zoomLevel}", "smallimage", $"Small{v[3]}.png"), new Vector2(0.5f, 0f))
                : null;
            GameObject obj = CreateSpriteObject($"Npc_{index}_id_{v[0]}", avatar ?? shadow, pos, Layer.Npc);
            string name = loadedTemplate.NpcNames.TryGetValue(v[0], out string npcName) ? npcName : $"npc {v[0]}";
            AddText(obj.transform, name, Vector2.up * 0.7f, Color.yellow);
            index++;
        }
    }

    private void BuildEffects(string resourceRoot)
    {
        int index = 0;
        foreach (EffectData effect in loadedTemplate.Effects)
        {
            Vector2 pos = ToWorld2(effect.X, effect.Y);
            GameObject obj = new GameObject($"Effect_{index}_id_{effect.Id}");
            obj.transform.SetParent(generatedRoot, false);
            obj.transform.localPosition = new Vector3(pos.x, pos.y, 0f);

            bool rendered = BuildEffectFrameFromSource(resourceRoot, effect.Id, obj.transform);
            AddText(obj.transform, rendered ? $"eff {effect.Id}" : $"eff {effect.Id} (missing local effectdata)", Vector2.up * 0.42f, new Color(0.6f, 1f, 1f));
            index++;
        }

        if (loadedTemplate.BackgroundEffects.Count > 0 && showLabels)
        {
            float mapWidth = loadedMap.Width * TilePixelSize / pixelsPerUnit;
            GameObject obj = new GameObject("Background_Effects_From_SQL");
            obj.transform.SetParent(generatedRoot, false);
            obj.transform.localPosition = new Vector3(mapWidth - 0.35f, -0.45f, 0f);
            TextMesh text = obj.AddComponent<TextMesh>();
            text.text = $"beff: {string.Join(", ", loadedTemplate.BackgroundEffects)}";
            text.fontSize = 18;
            text.characterSize = 0.05f;
            text.anchor = TextAnchor.UpperRight;
            text.alignment = TextAlignment.Right;
            text.color = new Color(0.6f, 1f, 1f);
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = (int)Layer.Label;
            }
        }
    }

    private void BuildSourceHud(string resourceRoot)
    {
        string main = Path.Combine(resourceRoot, $"x{zoomLevel}", "mainimage");
        int cmx = GetClientCameraX();
        int cmy = GetClientCameraY();
        float viewportWidth = referenceScreenWidthPx / pixelsPerUnit;
        float viewportHeight = referenceScreenHeightPx / pixelsPerUnit;
        Vector2 viewportTopLeft = ToWorld2(cmx, cmy);
        Vector2 topLeft = viewportTopLeft + new Vector2(0.16f, -0.16f);
        Vector2 bottomLeft = viewportTopLeft + new Vector2(0.82f, -viewportHeight + 0.78f);
        Vector2 bottomRight = viewportTopLeft + new Vector2(viewportWidth - 0.75f, -viewportHeight + 0.78f);

        CreateSpriteObject("Source_UI_HP_Frame", LoadSprite(Path.Combine(main, "i_khung.png"), new Vector2(0f, 1f)), topLeft, Layer.SourceUi);
        CreateSpriteObject("Source_UI_HP_Icon", LoadSprite(Path.Combine(main, "i_hp.png"), new Vector2(0f, 1f)), topLeft + new Vector2(0.08f, -0.08f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_HP_Bar", LoadSprite(Path.Combine(main, "myTexture2dHP.png"), new Vector2(0f, 1f)), topLeft + new Vector2(1.0f, -0.12f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_MP_Bar", LoadSprite(Path.Combine(main, "myTexture2dMP.png"), new Vector2(0f, 1f)), topLeft + new Vector2(1.0f, -0.42f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Char_Life", LoadSprite(Path.Combine(main, "i_charlife.png"), new Vector2(0.5f, 0.5f)), topLeft + new Vector2(0.18f, -0.92f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Analog", LoadSprite(Path.Combine(main, "myTexture2danalog1.png"), new Vector2(0.5f, 0.5f)), bottomLeft, Layer.SourceUi);
        CreateSpriteObject("Source_UI_Chat", LoadSprite(Path.Combine(main, "myTexture2dchat.png"), new Vector2(0.5f, 0.5f)), bottomLeft + new Vector2(1.25f, 0f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Menu", LoadSprite(Path.Combine(main, "myTexture2dmenu.png"), new Vector2(0.5f, 0.5f)), bottomRight + new Vector2(-1.35f, 0f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Skill", LoadSprite(Path.Combine(main, "myTexture2dskill.png"), new Vector2(0.5f, 0.5f)), bottomRight + new Vector2(-0.15f, 0f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Fire", LoadSprite(Path.Combine(main, "myTexture2dfirebtn0.png"), new Vector2(0.5f, 0.5f)), bottomRight + new Vector2(-0.72f, 0.72f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Menu_Right", LoadSprite(Path.Combine(main, "myTexture2dmenuright.png"), new Vector2(0.5f, 0.5f)), bottomRight + new Vector2(-1.95f, 0f), Layer.SourceUi);
        CreateSpriteObject("Source_UI_Focus", LoadSprite(Path.Combine(main, "myTexture2dfocus.png"), new Vector2(0.5f, 0.5f)), bottomRight + new Vector2(-0.15f, 0f), Layer.SourceUi);

        Sprite shortcut = LoadSprite(Path.Combine(main, "myTexture2dbtnl.png"), new Vector2(0.5f, 0.5f));
        Sprite shortcutFocus = LoadSprite(Path.Combine(main, "myTexture2dbtnlf.png"), new Vector2(0.5f, 0.5f));
        Vector2 shortcutStart = viewportTopLeft + new Vector2(0.70f, -viewportHeight + 1.76f);
        for (int i = 0; i < 5; i++)
        {
            GameObject slot = CreateSpriteObject($"Source_UI_Shortcut_{i + 1}", i == 0 ? shortcutFocus ?? shortcut : shortcut, shortcutStart + new Vector2(i * 0.83f, 0f), Layer.SourceUi);
            AddText(slot.transform, (i + 1).ToString(), Vector2.down * 0.38f, Color.white);
        }

        if (loadedTemplate != null)
        {
            GameObject label = new GameObject("Source_UI_Map_Name");
            label.transform.SetParent(generatedRoot, false);
            label.transform.localPosition = new Vector3(viewportTopLeft.x + viewportWidth * 0.5f, viewportTopLeft.y - 0.35f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = loadedTemplate.Name;
            text.fontSize = 32;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = (int)Layer.Label;
            }
        }
    }

    private void BuildLabels(string resourceRoot)
    {
        if (!showLabels)
        {
            return;
        }

        string templateText = loadedTemplate == null
            ? "SQL objects: not loaded"
            : $"SQL: {loadedTemplate.Name} | waypoints {loadedTemplate.Waypoints.Count} | mobs {loadedTemplate.Mobs.Count} | npcs {loadedTemplate.Npcs.Count} | effects {loadedTemplate.Effects.Count} | beff {loadedTemplate.BackgroundEffects.Count}";

        string[] labels =
        {
            $"REAL SOURCE MAP {mapId} | tileID {tileId} | size {loadedMap.Width}x{loadedMap.Height}",
            $"mymap: {Path.Combine(resourceRoot, "mymap", $"{mapId}.bytes")}",
            $"tiles:  {Path.Combine(resourceRoot, $"x{zoomLevel}", "t", tileId.ToString())}",
            templateText
        };

        for (int i = 0; i < labels.Length; i++)
        {
            GameObject obj = new GameObject($"Source_Label_{i}");
            obj.transform.SetParent(generatedRoot, false);
            obj.transform.localPosition = new Vector3(0f, 0.9f + i * -0.28f, 0f);
            TextMesh text = obj.AddComponent<TextMesh>();
            text.text = labels[i];
            text.fontSize = i == 0 ? 28 : 18;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.UpperLeft;
            text.alignment = TextAlignment.Left;
            text.color = Color.white;
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = (int)Layer.Label;
            }
        }
    }

    private void BuildStatusOnly()
    {
        GameObject obj = new GameObject("Status");
        obj.transform.SetParent(generatedRoot, false);
        obj.transform.localPosition = new Vector3(-7.5f, 2f, 0f);
        TextMesh text = obj.AddComponent<TextMesh>();
        text.text = status;
        text.fontSize = 24;
        text.characterSize = 0.08f;
        text.color = Color.white;
    }

    private Vector3 ToWorld(int px, int py)
    {
        return new Vector3(px / pixelsPerUnit, -py / pixelsPerUnit, 0f);
    }

    private Vector2 ToWorld2(float px, float py)
    {
        return new Vector2(px / pixelsPerUnit, -py / pixelsPerUnit);
    }

    private GameObject CreateSpriteObject(string name, Sprite sprite, Vector2 position, Layer layer)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(generatedRoot, false);
        obj.transform.localPosition = new Vector3(position.x, position.y, 0f);
        if (sprite == null)
        {
            return obj;
        }

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = (int)layer;
        return obj;
    }

    private Sprite LoadSprite(string path, Vector2 pivot)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        Texture2D texture = LoadTexture(path);
        if (texture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit);
        sprite.name = Path.GetFileNameWithoutExtension(path);
        return sprite;
    }

    private bool BuildEffectFrameFromSource(string resourceRoot, int effectId, Transform parent)
    {
        string folder = Path.Combine(resourceRoot, $"x{zoomLevel}", "effectdata", effectId.ToString());
        string dataPath = Path.Combine(folder, "data.bytes");
        string imagePath = Path.Combine(folder, "img.png");
        if (!File.Exists(dataPath) || !File.Exists(imagePath))
        {
            Sprite fallback = LoadSprite(
                Path.Combine(resourceRoot, $"x{zoomLevel}", "e", $"e_{effectId}.png"),
                new Vector2(0.5f, 1f)
            );
            if (fallback == null)
            {
                return false;
            }

            GameObject fallbackObject = CreateSpriteObject($"fallback_{effectId}", fallback, Vector2.zero, Layer.Effect);
            fallbackObject.transform.SetParent(parent, false);
            return true;
        }

        Texture2D sheet = LoadTexture(imagePath);
        if (sheet == null)
        {
            return false;
        }

        try
        {
            byte[] data = File.ReadAllBytes(dataPath);
            int offset = 0;
            int imageCount = ReadUByte(data, ref offset);
            Dictionary<int, EffectImagePart> images = new();
            for (int i = 0; i < imageCount; i++)
            {
                int id = ReadUByte(data, ref offset);
                images[id] = new EffectImagePart
                {
                    X = ReadUByte(data, ref offset),
                    Y = ReadUByte(data, ref offset),
                    Width = ReadUByte(data, ref offset),
                    Height = ReadUByte(data, ref offset)
                };
            }

            int frameCount = ReadShort(data, ref offset);
            if (frameCount <= 0)
            {
                return false;
            }

            int partCount = ReadUByte(data, ref offset);
            bool rendered = false;
            for (int i = 0; i < partCount; i++)
            {
                short dx = ReadShort(data, ref offset);
                short dy = ReadShort(data, ref offset);
                int imageId = ReadUByte(data, ref offset);
                if (!images.TryGetValue(imageId, out EffectImagePart part) || part.Width <= 0 || part.Height <= 0)
                {
                    continue;
                }

                Rect rect = new Rect(part.X, sheet.height - part.Y - part.Height, part.Width, part.Height);
                Sprite sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
                sprite.name = $"effect_{effectId}_{imageId}";
                GameObject obj = CreateSpriteObject($"part_{imageId}", sprite, new Vector2(dx / pixelsPerUnit, -dy / pixelsPerUnit), Layer.Effect);
                obj.transform.SetParent(parent, false);
                rendered = true;
            }

            return rendered;
        }
        catch (Exception ex)
        {
            status = $"Loaded real map {mapId}, but effect {effectId} failed to parse: {ex.Message}";
            return false;
        }
    }

    private static int ReadUByte(byte[] data, ref int offset)
    {
        if (offset >= data.Length)
        {
            throw new EndOfStreamException();
        }

        return data[offset++];
    }

    private static byte ReadByte(byte[] data, ref int offset)
    {
        if (offset >= data.Length)
        {
            throw new EndOfStreamException();
        }

        return data[offset++];
    }

    private static int ReadUnsignedShort(byte[] data, ref int offset)
    {
        if (offset + 1 >= data.Length)
        {
            throw new EndOfStreamException();
        }

        int value = (data[offset] << 8) | data[offset + 1];
        offset += 2;
        return value;
    }

    private static short ReadShort(byte[] data, ref int offset)
    {
        if (offset + 1 >= data.Length)
        {
            throw new EndOfStreamException();
        }

        short value = (short)((data[offset] << 8) | data[offset + 1]);
        offset += 2;
        return value;
    }

    private static int ReadInt(byte[] data, ref int offset)
    {
        if (offset + 3 >= data.Length)
        {
            throw new EndOfStreamException();
        }

        int value = (data[offset] << 24) |
            (data[offset + 1] << 16) |
            (data[offset + 2] << 8) |
            data[offset + 3];
        offset += 4;
        return value;
    }

    private void AddText(Transform parent, string value, Vector2 offset, Color color)
    {
        if (!showLabels)
        {
            return;
        }

        GameObject obj = new GameObject("Label");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        obj.transform.localScale = Vector3.one;

        TextMesh text = obj.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 18;
        text.characterSize = 0.045f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = (int)Layer.Label;
        }
    }

    private void SetupCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            targetCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        targetCamera.orthographic = true;
        targetCamera.backgroundColor = new Color(0.46f, 0.77f, 0.95f);
        targetCamera.aspect = Mathf.Max(0.1f, referenceScreenWidthPx / (float)Mathf.Max(1, referenceScreenHeightPx));

        if (loadedMap != null)
        {
            int cmx = GetClientCameraX();
            int cmy = GetClientCameraY();
            float centerX = (cmx + referenceScreenWidthPx * 0.5f) / pixelsPerUnit;
            float centerY = -(cmy + referenceScreenHeightPx * 0.5f) / pixelsPerUnit;
            targetCamera.transform.position = new Vector3(centerX, centerY, -10f);
            targetCamera.orthographicSize = referenceScreenHeightPx * 0.5f / pixelsPerUnit;
        }
    }

    private void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("Generated_Real_Map", StringComparison.Ordinal) &&
                child.name != "Generated_Map_Complete")
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private string GetGeneratedRootName()
    {
        return $"Generated_Real_Map_{mapId}";
    }

    private void ClearLoadedTextures()
    {
        foreach (Texture2D texture in loadedTextures)
        {
            if (texture == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        loadedTextures.Clear();
    }

    [Serializable]
    private sealed class RealMapData
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int[] Tiles;

        public RealMapData(int width, int height, int[] tiles)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
        }
    }

    private sealed class SqlMapTemplate
    {
        public int Id;
        public string Name;
        public string Data;
        public List<WaypointData> Waypoints = new();
        public List<IntObjectData> Mobs = new();
        public List<IntObjectData> Npcs = new();
        public List<EffectData> Effects = new();
        public List<int> BackgroundEffects = new();
        public Dictionary<int, string> MobNames = new();
        public Dictionary<int, string> NpcNames = new();
    }

    private sealed class WaypointData
    {
        public string Name;
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;
        public int GoMap;
        public int GoX;
        public int GoY;
    }

    private sealed class IntObjectData
    {
        public int[] Values;
    }

    private sealed class EffectData
    {
        public int Id;
        public int X;
        public int Y;
    }

    private sealed class EffectImagePart
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    private sealed class BackgroundLayer
    {
        public Sprite Sprite;
        public int Width;
        public int Height;
    }

    private sealed class BgItemTemplate
    {
        public int Id;
        public int ImageId;
        public int Layer;
        public int Dx;
        public int Dy;
    }

    private sealed class BgItemPlacement
    {
        public int Id;
        public int TileX;
        public int TileY;
    }
}
