using System.Collections.Generic;
using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    public static class JsonMultipartAnimationParser
    {
        private class RawSpritePart
        {
            public int id;
            public int x;
            public int y;
            public int w;
            public int h;
        }

        private class RawJsonAnimationData
        {
            public int id;
            public int type;
            public int type_data;
            public RawSpritePart[] sprites;
            public FramePartData[] frames;
            public int[][] data;
            public int[] animations;
        }

        public static JsonMultipartAnimationData Parse(string json)
        {
            JsonMultipartAnimationData converted = JsonUtility.FromJson<JsonMultipartAnimationData>(json);
            if (converted != null && converted.imageInfos != null)
            {
                return converted;
            }

            return TryParseRawData(json);
        }

        private static JsonMultipartAnimationData TryParseRawData(string json)
        {
            RawJsonAnimationData raw = TryParseRaw(json);
            if (raw == null || raw.sprites == null || raw.frames == null)
            {
                return null;
            }

            SpritePartInfo[] imageInfos = new SpritePartInfo[raw.sprites.Length];
            for (int i = 0; i < raw.sprites.Length; i++)
            {
                RawSpritePart sprite = raw.sprites[i];
                imageInfos[i] = new SpritePartInfo
                {
                    ID = sprite.id,
                    x0 = sprite.x,
                    y0 = sprite.y,
                    w = NormalizeSourceSize(sprite.w),
                    h = NormalizeSourceSize(sprite.h)
                };
            }

            return new JsonMultipartAnimationData
            {
                monsterId = raw.id,
                type = raw.type,
                typeData = raw.type_data,
                imageInfos = imageInfos,
                frames = raw.frames,
                actions = BuildActionsFromRawData(raw.data, raw.frames.Length) ?? BuildDefaultActions(raw.frames.Length)
            };
        }

        private static RawJsonAnimationData TryParseRaw(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"sprites\"") || !json.Contains("\"frames\""))
            {
                return null;
            }

            string spritesSection = ExtractArraySection(json, "\"sprites\"");
            string framesSection = ExtractArraySection(json, "\"frames\"");
            if (spritesSection == null || framesSection == null)
            {
                return null;
            }

            return new RawJsonAnimationData
            {
                id = ExtractInt(json, "\"id\"", 0),
                type = ExtractInt(json, "\"type\"", 0),
                type_data = ExtractInt(json, "\"type_data\"", 0),
                sprites = ParseSprites(spritesSection),
                frames = ParseFrames(framesSection),
                data = ParseActionData(ExtractArraySection(json, "\"data\"")),
                animations = ParseIntArray(ExtractArraySection(json, "\"animations\""))
            };
        }

        private static int NormalizeSourceSize(int size)
        {
            return size < 0 ? size + 256 : size;
        }

        private static JsonAnimationActionData[] BuildDefaultActions(int frameCount)
        {
            if (frameCount >= 16)
            {
                return new[]
                {
                    new JsonAnimationActionData { index = 0, name = "Stand", frameIndices = BuildRange(0, 2) },
                    new JsonAnimationActionData { index = 1, name = "Move", frameIndices = BuildRange(2, 6) },
                    new JsonAnimationActionData { index = 2, name = "Attack1", frameIndices = BuildRange(6, 10) },
                    new JsonAnimationActionData { index = 3, name = "Attack2", frameIndices = BuildRange(frameCount - 3, frameCount - 1) },
                    new JsonAnimationActionData { index = 4, name = "Attack3", frameIndices = BuildRange(10, frameCount - 1) },
                    new JsonAnimationActionData { index = 5, name = "Hurt", frameIndices = BuildRange(frameCount - 1, frameCount) },
                    new JsonAnimationActionData { index = 6, name = "Die", frameIndices = BuildRange(frameCount - 1, frameCount) }
                };
            }

            int[] moveFrames = BuildRange(0, Mathf.Min(frameCount, 6));
            return new[]
            {
                new JsonAnimationActionData { index = 0, name = "Stand", frameIndices = BuildRange(0, Mathf.Min(frameCount, 2)) },
                new JsonAnimationActionData { index = 1, name = "Move", frameIndices = moveFrames },
                new JsonAnimationActionData { index = 2, name = "Attack1", frameIndices = BuildRange(6, Mathf.Min(frameCount, 10)) },
                new JsonAnimationActionData { index = 3, name = "Attack2", frameIndices = BuildRange(10, Mathf.Min(frameCount, 14)) },
                new JsonAnimationActionData { index = 4, name = "Attack3", frameIndices = BuildRange(12, Mathf.Min(frameCount, 16)) },
                new JsonAnimationActionData { index = 5, name = "Hurt", frameIndices = BuildRange(Mathf.Max(0, frameCount - 1), frameCount) },
                new JsonAnimationActionData { index = 6, name = "Die", frameIndices = BuildRange(Mathf.Max(0, frameCount - 1), frameCount) }
            };
        }

        private static JsonAnimationActionData[] BuildActionsFromRawData(int[][] actionFrames, int frameCount)
        {
            if (actionFrames == null || actionFrames.Length == 0)
            {
                return null;
            }

            string[] actionNames =
            {
                "Stand", "Move", "Attack1", "Attack2", "Attack3", "Attack4", "Attack5", "Attack6", "Attack7",
                "Attack8", "Attack9", "Attack10", "Hurt", "Die", "Fly", "AddDameTick", "EffectType"
            };

            List<JsonAnimationActionData> actions = new List<JsonAnimationActionData>();
            for (int i = 0; i < actionFrames.Length; i++)
            {
                int[] frames = actionFrames[i];
                if (frames == null || frames.Length == 0)
                {
                    continue;
                }

                actions.Add(new JsonAnimationActionData
                {
                    index = i,
                    name = i < actionNames.Length ? actionNames[i] : $"Action{i}",
                    frameIndices = ClampFrameIndices(frames, frameCount)
                });
            }

            return actions.Count > 0 ? actions.ToArray() : null;
        }

        private static int[] BuildRange(int startInclusive, int endExclusive)
        {
            if (endExclusive <= startInclusive)
            {
                return new[] { 0 };
            }

            int[] frames = new int[endExclusive - startInclusive];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = startInclusive + i;
            }

            return frames;
        }

        private static int[] ClampFrameIndices(int[] sourceFrames, int frameCount)
        {
            int[] frames = new int[sourceFrames.Length];
            for (int i = 0; i < sourceFrames.Length; i++)
            {
                int frame = sourceFrames[i];
                frames[i] = frame < 0 ? frame : Mathf.Clamp(frame, 0, Mathf.Max(0, frameCount - 1));
            }

            return frames;
        }

        private static RawSpritePart[] ParseSprites(string section)
        {
            List<RawSpritePart> sprites = new List<RawSpritePart>();
            foreach (string objectText in ExtractObjects(section))
            {
                sprites.Add(new RawSpritePart
                {
                    id = ExtractInt(objectText, "\"id\"", 0),
                    x = ExtractInt(objectText, "\"x\"", 0),
                    y = ExtractInt(objectText, "\"y\"", 0),
                    w = ExtractInt(objectText, "\"w\"", 0),
                    h = ExtractInt(objectText, "\"h\"", 0)
                });
            }

            return sprites.ToArray();
        }

        private static FramePartData[] ParseFrames(string section)
        {
            List<FramePartData> frames = new List<FramePartData>();
            foreach (string frameText in ExtractArrayItems(section))
            {
                List<int> dx = new List<int>();
                List<int> dy = new List<int>();
                List<int> idImg = new List<int>();
                foreach (string partText in ExtractObjects(frameText))
                {
                    dx.Add(ExtractInt(partText, "\"dx\"", 0));
                    dy.Add(ExtractInt(partText, "\"dy\"", 0));
                    idImg.Add(ExtractInt(partText, "\"sprite_id\"", 0));
                }

                frames.Add(new FramePartData
                {
                    dx = dx.ToArray(),
                    dy = dy.ToArray(),
                    idImg = idImg.ToArray()
                });
            }

            return frames.ToArray();
        }

        private static int[][] ParseActionData(string section)
        {
            if (section == null)
            {
                return null;
            }

            List<int[]> actions = new List<int[]>();
            foreach (string actionText in ExtractArrayItems(section))
            {
                actions.Add(ParseIntArray(actionText));
            }

            return actions.Count > 0 ? actions.ToArray() : null;
        }

        private static int[] ParseIntArray(string section)
        {
            if (section == null)
            {
                return new int[0];
            }

            List<int> values = new List<int>();
            foreach (string piece in section.Split(','))
            {
                if (int.TryParse(piece.Trim(), out int value))
                {
                    values.Add(value);
                }
            }

            return values.ToArray();
        }

        private static IEnumerable<string> ExtractObjects(string section)
        {
            int depth = 0;
            int start = -1;
            for (int i = 0; i < section.Length; i++)
            {
                char c = section[i];
                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        yield return section.Substring(start, i - start + 1);
                        start = -1;
                    }
                }
            }
        }

        private static IEnumerable<string> ExtractArrayItems(string section)
        {
            int depth = 0;
            int start = -1;
            for (int i = 0; i < section.Length; i++)
            {
                char c = section[i];
                if (c == '[')
                {
                    if (depth == 0)
                    {
                        start = i + 1;
                    }

                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        yield return section.Substring(start, i - start);
                        start = -1;
                    }
                }
            }
        }

        private static string ExtractArraySection(string json, string key)
        {
            int keyIndex = json.IndexOf(key, System.StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            int start = json.IndexOf('[', keyIndex);
            if (start < 0)
            {
                return null;
            }

            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[')
                {
                    depth++;
                }
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(start + 1, i - start - 1);
                    }
                }
            }

            return null;
        }

        private static int ExtractInt(string text, string key, int fallback)
        {
            int keyIndex = text.IndexOf(key, System.StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return fallback;
            }

            int colon = text.IndexOf(':', keyIndex);
            if (colon < 0)
            {
                return fallback;
            }

            int valueStart = colon + 1;
            while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
            {
                valueStart++;
            }

            int valueEnd = valueStart;
            if (valueEnd < text.Length && text[valueEnd] == '-')
            {
                valueEnd++;
            }

            while (valueEnd < text.Length && char.IsDigit(text[valueEnd]))
            {
                valueEnd++;
            }

            return int.TryParse(text.Substring(valueStart, valueEnd - valueStart), out int value)
                ? value
                : fallback;
        }
    }
}
