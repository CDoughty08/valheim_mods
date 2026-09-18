using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace VariaChestFocus
{
    internal struct ChestFocusConfigSnapshot
    {
        public bool Enabled;
        public bool ProtectHotbar;
        public float SortRange;
        public int MaxChests;
        public int MaxMoves;
        public long MaxPaintTextureBytes;
        public KeyCode SortKey;
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuExtendedPlayerInventory", BepInDependency.DependencyFlags.SoftDependency)]
    public class VariaChestFocusPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.varia.chestfocus";
        public const string PluginName = "VariaChestFocus";
        public const string PluginVersion = "0.1.16";

        private static readonly ConfigSync ConfigSync = new(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            ModRequired = false
        };

        internal static VariaChestFocusPlugin Instance { get; private set; }
        internal static BepInEx.Logging.ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ProtectHotbar;
        internal static ConfigEntry<float> SortRange;
        internal static ConfigEntry<int> MaxChests;
        internal static ConfigEntry<int> MaxMoves;
        internal static ConfigEntry<int> MaxPaintTextureMiB;
        internal static ConfigEntry<KeyboardShortcut> SortKeybind;

        private Harmony _harmony;
        private static ChestFocusConfigSnapshot _snapshot;
        private static int _snapshotFrame = -1;

        internal static bool IsModEnabled => ConfigSnapshot.Enabled;

        internal static ChestFocusConfigSnapshot ConfigSnapshot
        {
            get
            {
                if (_snapshotFrame != Time.frameCount)
                {
                    _snapshotFrame = Time.frameCount;
                    _snapshot = BuildSnapshot();
                }

                return _snapshot;
            }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            LockConfiguration = ConfigBind(
                "General",
                "LockConfiguration",
                false,
                "If enabled on a server that also runs this mod, force clients to use the server's config.");
            _ = ConfigSync.AddLockingConfigEntry(LockConfiguration);

            Enabled = ConfigBind(
                "General",
                "Enabled",
                true,
                "Master toggle for VariaChestFocus.");

            SortRange = ConfigBind(
                "QuickSort",
                "SortRange",
                16f,
                new ConfigDescription(
                    "Radius in meters for nearby chests when quick-sorting.",
                    new AcceptableValueRange<float>(4f, 64f)));

            MaxChests = ConfigBind(
                "QuickSort",
                "MaxChests",
                40,
                new ConfigDescription(
                    "Max chests considered per quick-sort (nearest within priority).",
                    new AcceptableValueRange<int>(1, 200)));

            MaxMoves = ConfigBind(
                "QuickSort",
                "MaxMoves",
                60,
                new ConfigDescription(
                    "Max successful item moves per quick-sort press.",
                    new AcceptableValueRange<int>(1, 500)));

            MaxPaintTextureMiB = ConfigBind(
                "Appearance", "MaxPaintTextureMiB", 64,
                new ConfigDescription(
                    "Budget for color-specific paint textures, including mipmaps. New colors beyond this budget use shared dye textures. Existing paint is released as chests/debris unload; lowering the budget does not evict visible finishes.",
                    new AcceptableValueRange<int>(0, 512)), synchronizedSetting: false);

            ProtectHotbar = ConfigBind(
                "QuickSort",
                "ProtectHotbar",
                true,
                "Skip items in numbered hotbar slots 1-8 when quick-sorting. Disable to allow sorting those slots; equipped items, AzuEPI slots and favorites remain protected.",
                synchronizedSetting: false);

            SortKeybind = Config.Bind(
                "QuickSort",
                "SortKeybind",
                new KeyboardShortcut(KeyCode.H),
                "Key to quick-sort inventory into nearby focused chests. [Not Synced with Server]");

            var keyMigrated = Config.Bind("Migration", "SortKeyUpdated", false,
                "Tracks the one-time migration from the old G default to H. [Not Synced with Server]");
            if (!keyMigrated.Value)
            {
                if (SortKeybind.Value.MainKey == KeyCode.G && !SortKeybind.Value.Modifiers.Any())
                {
                    SortKeybind.Value = new KeyboardShortcut(KeyCode.H);
                    Logger.LogInfo("Quick-sort key changed from G to H to avoid Valheim's radial menu.");
                }
                keyMigrated.Value = true;
            }

            _harmony = new Harmony(PluginGuid);
            PatchOwnTypes(_harmony);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            ChestFocusConfigSnapshot cfg = ConfigSnapshot;
            if (!cfg.Enabled)
            {
                return;
            }

            if (SortKeybind == null || !SortKeybind.Value.IsDown())
            {
                return;
            }

            if (Console.IsVisible() || Chat.instance != null && Chat.instance.HasFocus())
            {
                return;
            }

            if (InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || StoreGui.IsVisible())
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || !player.TakeInput())
            {
                return;
            }

            int moves = QuickSort.Run(player, cfg);
            player.Message(
                MessageHud.MessageType.TopLeft,
                moves > 0
                    ? $"Chest Focus: stored {moves} stack(s)"
                    : "Chest Focus: nothing to store");
        }

        private void OnDestroy()
        {
            Gui.ChestSettingsUi.Destroy();
            ChestTint.RemoveAll();
            _harmony?.UnpatchSelf();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static ChestFocusConfigSnapshot BuildSnapshot()
        {
            return new ChestFocusConfigSnapshot
            {
                Enabled = Enabled == null || Enabled.Value,
                ProtectHotbar = ProtectHotbar?.Value ?? true,
                SortRange = SortRange?.Value ?? 16f,
                MaxChests = MaxChests?.Value ?? 40,
                MaxMoves = MaxMoves?.Value ?? 60,
                MaxPaintTextureBytes = (long)(MaxPaintTextureMiB?.Value ?? 64) * 1024 * 1024,
                SortKey = SortKeybind?.Value.MainKey ?? KeyCode.H
            };
        }

        private ConfigEntry<T> ConfigBind<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return ConfigBind(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private ConfigEntry<T> ConfigBind<T>(
            string group,
            string name,
            T value,
            ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extended = new(
                description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues,
                description.Tags);
            ConfigEntry<T> entry = Config.Bind(group, name, value, extended);
            if (synchronizedSetting)
            {
                SyncedConfigEntry<T> synced = ConfigSync.AddConfigEntry(entry);
                synced.SynchronizedConfig = true;
            }

            return entry;
        }

        private static void PatchOwnTypes(Harmony harmony)
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.Namespace != null && type.Namespace.StartsWith("ServerSync", StringComparison.Ordinal))
                {
                    continue;
                }

                harmony.CreateClassProcessor(type).Patch();
            }
        }
    }
}
