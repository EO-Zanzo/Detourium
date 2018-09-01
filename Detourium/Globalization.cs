namespace Detourium
{
    public static class Globalization<T> where T : DetouriumPlugin
    {
        public static string PluginDisabledMessage(T plugin) =>
            string.Format("[Runtime] Plugin '{0} v{1}' is disabled and will not be installed. You can disable this functionality in the configuration for the specified plugin.",
                plugin.PluginName, plugin.PluginVersion);

        public static string PluginVersionPriorityMessage(T plugin, T duplicate) =>
            string.Format("[Runtime] Plugin '{0} v{1}' is lower than the current version loaded (v{2}). " +
                $"The plugin will be skipped and not installed. You can disable this functionality in the configuration for the specified plugin.",
                plugin.PluginName, plugin.PluginVersion, duplicate.PluginVersion);

        public static string PluginUnloadedMessage(T duplicate) =>
            string.Format("[Runtime] Plugin '{0} v{1}' " + (duplicate.OnUninstall() ? "gracefully" : "forcibly") + "unloaded.", duplicate.PluginName, duplicate.PluginVersion);

        public static string PluginLoadedMessage(T plugin) =>
            string.Format("[Runtime] Plugin '{0} v{1}' successfully loaded.", plugin.PluginName, plugin.PluginVersion);

        public static string PluginInterfaceNotFound() =>
            string.Format($"[Runtime] An attempt to load a plugin failed as the assembly did not contain any {nameof(DetouriumPlugin)} interface.");
    }
}
