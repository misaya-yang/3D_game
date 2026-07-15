#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Globalization;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Enemy;
using Wendao.Systems.Inventory;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;

namespace Wendao.Systems.Debugging
{
    public sealed class DebugConsoleService : MonoBehaviour, IDebugConsoleService
    {
        public const string HelpLocalizationKey = "debug_console_help";
        public const string GodSuccessLocalizationKey = "debug_console_success_god";
        public const string KillAllSuccessLocalizationKey =
            "debug_console_success_killall";
        public const string SetRealmSuccessLocalizationKey =
            "debug_console_success_setrealm";
        public const string GiveXpSuccessLocalizationKey =
            "debug_console_success_givexp";
        public const string GiveSuccessLocalizationKey =
            "debug_console_success_give";
        public const string SpawnSuccessLocalizationKey =
            "debug_console_success_spawn";
        public const string TeleportSuccessLocalizationKey =
            "debug_console_success_tp";
        public const string SaveSuccessLocalizationKey =
            "debug_console_success_save";
        public const string TimeScaleSuccessLocalizationKey =
            "debug_console_success_timescale";
        public const string TutorialSkipSuccessLocalizationKey =
            "debug_console_success_tutorial_skip";
        public const string UsageErrorLocalizationKey =
            "debug_console_error_usage";
        public const string UnknownCommandLocalizationKey =
            "debug_console_error_unknown";
        public const string InvalidArgumentLocalizationKey =
            "debug_console_error_invalid_argument";
        public const string ServiceUnavailableLocalizationKey =
            "debug_console_error_service_unavailable";
        public const string OperationFailedLocalizationKey =
            "debug_console_error_operation_failed";

        public const string HelpDefaultValue =
            "可用命令：/god [on|off]、/killall、/setrealm <境界> <层>、"
            + "/givexp <数值>、/give <物品ID> <数量>、/spawn <敌人ID> [数量]、"
            + "/tp <地图ID> <出生点ID>、/save、/timescale <0-10>、"
            + "/tutorial_skip。";

        private const int MaximumGiveCount = 9999;
        private const int MaximumSpawnCount = 20;
        private const float MaximumXpGrant = 1000000000f;
        private const float MaximumTimeScale = 10f;

        private static readonly char[] TokenSeparators =
        {
            ' ', '\t', '\r', '\n'
        };

        private bool _registered;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IDebugConsoleService>(
                    out IDebugConsoleService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(this);
                return;
            }

            EnsureRegistered();
        }

        private void Update()
        {
            EnsureRegistered();
        }

        internal bool EnsureRegistered()
        {
            if (ServiceLocator.TryGet<IDebugConsoleService>(
                    out IDebugConsoleService current))
            {
                _registered = ReferenceEquals(current, this);
                return _registered;
            }

            ServiceLocator.Register<IDebugConsoleService>(this);
            _registered = true;
            return true;
        }

        private void OnDestroy()
        {
            if (_registered
                && ServiceLocator.TryGet<IDebugConsoleService>(
                    out IDebugConsoleService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IDebugConsoleService>();
            }

            _registered = false;
        }

        public DebugCommandResult Execute(string commandLine)
        {
            string[] tokens = Tokenize(commandLine);
            if (tokens.Length == 0)
            {
                return Usage("请输入调试命令；使用 /help 查看列表。");
            }

            switch (tokens[0].ToLowerInvariant())
            {
                case "/help":
                    return tokens.Length == 1
                        ? DebugCommandResult.Success(
                            HelpLocalizationKey,
                            HelpDefaultValue)
                        : Usage("用法：/help");
                case "/god":
                    return ExecuteGod(tokens);
                case "/killall":
                    return ExecuteKillAll(tokens);
                case "/setrealm":
                    return ExecuteSetRealm(tokens);
                case "/givexp":
                    return ExecuteGiveXp(tokens);
                case "/give":
                    return ExecuteGive(tokens);
                case "/spawn":
                    return ExecuteSpawn(tokens);
                case "/tp":
                    return ExecuteTeleport(tokens);
                case "/save":
                    return ExecuteSave(tokens);
                case "/timescale":
                    return ExecuteTimeScale(tokens);
                case "/tutorial_skip":
                    return ExecuteTutorialSkip(tokens);
                default:
                    return DebugCommandResult.Failure(
                        UnknownCommandLocalizationKey,
                        $"未知命令：{tokens[0]}。使用 /help 查看列表。");
            }
        }

        private static DebugCommandResult ExecuteGod(string[] tokens)
        {
            if (tokens.Length > 2)
            {
                return Usage("用法：/god [on|off]");
            }

            if (!ServiceLocator.TryGet<IDebugPlayerService>(
                    out IDebugPlayerService player))
            {
                return ServiceUnavailable("玩家调试服务尚未就绪。");
            }

            bool enabled = !player.GodModeEnabled;
            if (tokens.Length == 2
                && !TryParseToggle(tokens[1], out enabled))
            {
                return InvalidArgument("/god 只接受 on 或 off。");
            }

            if (!player.SetGodMode(enabled))
            {
                return OperationFailed("无法切换无敌状态。");
            }

            return DebugCommandResult.Success(
                GodSuccessLocalizationKey,
                enabled ? "无敌模式已开启。" : "无敌模式已关闭。");
        }

        private static DebugCommandResult ExecuteKillAll(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                return Usage("用法：/killall");
            }

            if (!ServiceLocator.TryGet<IEnemySpawnService>(
                    out IEnemySpawnService enemies))
            {
                return ServiceUnavailable("敌人调试服务尚未就绪。");
            }

            if (!enemies.TryDefeatAll(out int defeatedCount))
            {
                return OperationFailed("清除敌人失败。");
            }

            return DebugCommandResult.Success(
                KillAllSuccessLocalizationKey,
                $"已击败 {defeatedCount} 个敌人。");
        }

        private static DebugCommandResult ExecuteSetRealm(string[] tokens)
        {
            if (tokens.Length != 3)
            {
                return Usage("用法：/setrealm <境界编号或名称> <层数>");
            }

            if (!TryParseRealm(tokens[1], out RealmType realm)
                || !int.TryParse(
                    tokens[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int subStage))
            {
                return InvalidArgument("境界或层数格式不正确。");
            }

            RealmEntry entry = ConfigDatabase.Instance?.GetRealm((int)realm);
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            if (entry == null
                || profile == null
                || subStage < 1
                || subStage > Math.Max(1, entry.SubStages))
            {
                return InvalidArgument("境界不存在，或层数超出该境界范围。");
            }

            profile.Realm = (int)realm;
            profile.SubStage = subStage;
            profile.CultivationXp = 0f;
            if (saveManager.ActiveSlot >= 0
                && !saveManager.TrySaveModule("profile"))
            {
                return OperationFailed(
                    $"境界已写入内存，但存档失败：{saveManager.LastError}");
            }

            return DebugCommandResult.Success(
                SetRealmSuccessLocalizationKey,
                $"境界已设为 {entry.Name} 第{subStage}层。");
        }

        private static DebugCommandResult ExecuteGiveXp(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                return Usage("用法：/givexp <正数>");
            }

            if (!float.TryParse(
                    tokens[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float amount)
                || amount <= 0f
                || amount > MaximumXpGrant
                || float.IsNaN(amount)
                || float.IsInfinity(amount))
            {
                return InvalidArgument("修为必须是有效正数。");
            }

            if (!ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation))
            {
                return ServiceUnavailable("修为服务尚未就绪。");
            }

            cultivation.AddXp(amount, XpSourceType.Other);
            return DebugCommandResult.Success(
                GiveXpSuccessLocalizationKey,
                $"已增加 {amount:0.##} 点基础修为（灵根倍率照常生效）。");
        }

        private static DebugCommandResult ExecuteGive(string[] tokens)
        {
            if (tokens.Length != 3)
            {
                return Usage("用法：/give <物品ID> <数量>");
            }

            if (!TryParsePositiveCount(tokens[2], MaximumGiveCount, out int count))
            {
                return InvalidArgument("物品数量必须是有效正整数。");
            }

            if (ConfigDatabase.Instance?.GetItem(tokens[1]) == null)
            {
                return InvalidArgument($"未知物品 ID：{tokens[1]}。");
            }

            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return ServiceUnavailable("背包服务尚未就绪。");
            }

            if (!inventory.AddItem(tokens[1], count, AcquireSource.Cheat))
            {
                return OperationFailed("发放物品失败，请检查背包容量。");
            }

            return DebugCommandResult.Success(
                GiveSuccessLocalizationKey,
                $"已发放 {tokens[1]} ×{count}。");
        }

        private static DebugCommandResult ExecuteSpawn(string[] tokens)
        {
            if (tokens.Length < 2 || tokens.Length > 3)
            {
                return Usage("用法：/spawn <敌人ID> [数量]");
            }

            int count = 1;
            if (tokens.Length == 3
                && !TryParsePositiveCount(
                    tokens[2],
                    MaximumSpawnCount,
                    out count))
            {
                return InvalidArgument("刷怪数量必须是 1–20 的整数。");
            }

            if (ConfigDatabase.Instance?.GetEnemy(tokens[1]) == null)
            {
                return InvalidArgument($"未知敌人 ID：{tokens[1]}。");
            }

            if (!ServiceLocator.TryGet<IEnemySpawnService>(
                    out IEnemySpawnService enemies))
            {
                return ServiceUnavailable("敌人调试服务尚未就绪。");
            }

            if (!enemies.TrySpawn(tokens[1], count, out int spawnedCount)
                || spawnedCount != count)
            {
                return OperationFailed(
                    $"刷怪未完成，仅生成 {spawnedCount}/{count} 个敌人。");
            }

            return DebugCommandResult.Success(
                SpawnSuccessLocalizationKey,
                $"已生成 {tokens[1]} ×{spawnedCount}。");
        }

        private static DebugCommandResult ExecuteTeleport(string[] tokens)
        {
            if (tokens.Length != 3)
            {
                return Usage("用法：/tp <地图ID> <出生点ID>");
            }

            SceneLoader loader = SceneLoader.Instance;
            if (loader == null)
            {
                return ServiceUnavailable("场景加载服务尚未就绪。");
            }

            if (!loader.LoadMap(tokens[1], tokens[2]))
            {
                string error = loader.LastError ?? "未知原因";
                return OperationFailed(
                    $"传送失败：{error}");
            }

            return DebugCommandResult.Success(
                TeleportSuccessLocalizationKey,
                $"正在传送至 {tokens[1]} / {tokens[2]}。");
        }

        private static DebugCommandResult ExecuteSave(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                return Usage("用法：/save");
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                return ServiceUnavailable("存档服务尚未就绪。");
            }

            int slot = saveManager.ActiveSlot >= 0
                ? saveManager.ActiveSlot
                : 0;
            if (!saveManager.SaveGame(slot))
            {
                string error = saveManager.LastError ?? "未知原因";
                return OperationFailed(
                    $"存档失败：{error}");
            }

            return DebugCommandResult.Success(
                SaveSuccessLocalizationKey,
                $"已保存至存档位 {slot + 1}。");
        }

        private static DebugCommandResult ExecuteTimeScale(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                return Usage("用法：/timescale <0-10>");
            }

            if (!float.TryParse(
                    tokens[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value)
                || value < 0f
                || value > MaximumTimeScale
                || float.IsNaN(value)
                || float.IsInfinity(value))
            {
                return InvalidArgument("时间倍率必须在 0–10 之间。");
            }

            Time.timeScale = value;
            return DebugCommandResult.Success(
                TimeScaleSuccessLocalizationKey,
                $"时间倍率已设为 {value:0.##}。");
        }

        private static DebugCommandResult ExecuteTutorialSkip(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                return Usage("用法：/tutorial_skip");
            }

            if (!ServiceLocator.TryGet<ITutorialService>(
                    out ITutorialService tutorials))
            {
                return ServiceUnavailable("教程服务尚未就绪。");
            }

            if (tutorials.IsActive)
            {
                tutorials.Skip();
            }

            string[] requiredTutorials =
            {
                TutorialManager.MoveTutorialId,
                TutorialManager.CombatTutorialId
            };
            for (int index = 0; index < requiredTutorials.Length; index++)
            {
                string tutorialId = requiredTutorials[index];
                if (tutorials.HasCompleted(tutorialId))
                {
                    continue;
                }

                if (!tutorials.TryStart(tutorialId))
                {
                    return OperationFailed($"无法启动并跳过教程：{tutorialId}。");
                }

                tutorials.Skip();
                if (!tutorials.HasCompleted(tutorialId))
                {
                    return OperationFailed($"教程完成态写入失败：{tutorialId}。");
                }
            }

            return DebugCommandResult.Success(
                TutorialSkipSuccessLocalizationKey,
                "移动与战斗教程已跳过，完成态已写入存档。");
        }

        private static string[] Tokenize(string commandLine)
        {
            return string.IsNullOrWhiteSpace(commandLine)
                ? Array.Empty<string>()
                : commandLine.Trim().Split(
                    TokenSeparators,
                    StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool TryParsePositiveCount(
            string value,
            int maximum,
            out int count)
        {
            return int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out count)
                && count > 0
                && count <= maximum;
        }

        private static bool TryParseToggle(string value, out bool enabled)
        {
            switch (value?.ToLowerInvariant())
            {
                case "on":
                case "true":
                case "1":
                    enabled = true;
                    return true;
                case "off":
                case "false":
                case "0":
                    enabled = false;
                    return true;
                default:
                    enabled = false;
                    return false;
            }
        }

        private static bool TryParseRealm(string value, out RealmType realm)
        {
            if (int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int numeric))
            {
                realm = (RealmType)numeric;
                return ConfigDatabase.Instance?.GetRealm(numeric) != null;
            }

            return Enum.TryParse(value, true, out realm)
                && ConfigDatabase.Instance?.GetRealm((int)realm) != null;
        }

        private static DebugCommandResult Usage(string defaultValue)
        {
            return DebugCommandResult.Failure(
                UsageErrorLocalizationKey,
                defaultValue);
        }

        private static DebugCommandResult InvalidArgument(string defaultValue)
        {
            return DebugCommandResult.Failure(
                InvalidArgumentLocalizationKey,
                defaultValue);
        }

        private static DebugCommandResult ServiceUnavailable(string defaultValue)
        {
            return DebugCommandResult.Failure(
                ServiceUnavailableLocalizationKey,
                defaultValue);
        }

        private static DebugCommandResult OperationFailed(string defaultValue)
        {
            return DebugCommandResult.Failure(
                OperationFailedLocalizationKey,
                defaultValue);
        }
    }
}
#endif
