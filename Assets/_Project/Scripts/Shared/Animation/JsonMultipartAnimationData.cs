using System;

namespace TinyDragon.Shared.Animation
{
    [Serializable]
    public class SpritePartInfo
    {
        public int ID;
        public int x0;
        public int y0;
        public int w;
        public int h;
    }

    [Serializable]
    public class FramePartData
    {
        public int[] dx;
        public int[] dy;
        public int[] idImg;
    }

    [Serializable]
    public class JsonAnimationActionData
    {
        public int index;
        public string name;
        public int[] frameIndices;
    }

    [Serializable]
    public class JsonMultipartAnimationData
    {
        public int monsterId;
        public int type;
        public int typeData;
        public SpritePartInfo[] imageInfos;
        public FramePartData[] frames;
        public JsonAnimationActionData[] actions;
    }

    [Serializable]
    public struct JsonAnimatorActionMap
    {
        public string animatorStateName;
        public string jsonActionName;
    }
}
