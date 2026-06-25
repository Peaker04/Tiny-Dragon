using UnityEngine;

namespace TinyDragon.Config
{
    public static class TinyDragonRuntimeConfigProvider
    {
        private const string ConfigResourcePath = "Config/TinyDragonRuntimeConfig";
        private static TinyDragonRuntimeConfig cachedConfig;

        public static TinyDragonRuntimeConfig Resolve(TinyDragonRuntimeConfig overrideConfig = null)
        {
            if (overrideConfig != null)
            {
                return overrideConfig;
            }

            if (cachedConfig == null)
            {
                cachedConfig = Resources.Load<TinyDragonRuntimeConfig>(ConfigResourcePath);
                if (cachedConfig == null)
                {
                    cachedConfig = ScriptableObject.CreateInstance<TinyDragonRuntimeConfig>();
                    cachedConfig.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            return cachedConfig;
        }
    }
}
