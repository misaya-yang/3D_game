using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Entities.Visuals;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Inventory;
using Wendao.Systems.Quest;
using Wendao.Systems.World;
using Wendao.UI.Common;
using Wendao.UI.Crafting;
using Wendao.UI.Cultivation;
using Wendao.UI.Quest;

namespace Wendao.UI.SceneFlow
{
    /// <summary>
    /// Opt-in release-player audit for G09-03. It uses an isolated save root
    /// and public gameplay services; normal launches are unchanged.
    /// </summary>
    public sealed class G0903JourneyAuditLauncher : MonoBehaviour
    {
        private const string EnableArgument = "-wendaoJourneyAudit";
        private const string StorageArgument = "-wendaoJourneyStorage";
        private const string ReportArgument = "-wendaoJourneyReport";
        private const string CaptureDirectoryArgument =
            "-wendaoJourneyCaptureDir";
        private const string RunIdArgument = "-wendaoJourneyRunId";
        private const string CleanArgument = "-wendaoJourneyClean";

        private static bool _installed;
        private readonly JourneyAuditReport _report = new JourneyAuditReport();
        private string _storageRoot = string.Empty;
        private string _reportPath = string.Empty;
        private string _captureDirectory = string.Empty;
        private bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFromCommandLine()
        {
            if (_installed || !HasArgument(EnableArgument))
            {
                return;
            }

            _installed = true;
            Application.runInBackground = true;
            var host = new GameObject("[G09-03 Journey Audit]");
            DontDestroyOnLoad(host);
            host.AddComponent<G0903JourneyAuditLauncher>();
        }

        private IEnumerator Start()
        {
            _storageRoot = ResolveStorageRoot();
            _reportPath = ResolvePath(ReadArgument(ReportArgument));
            _captureDirectory = ResolvePath(
                ReadArgument(CaptureDirectoryArgument));
            _report.runId = ReadArgument(RunIdArgument);
            if (string.IsNullOrWhiteSpace(_report.runId))
            {
                _report.runId = Guid.NewGuid().ToString("N");
            }
            _report.unityVersion = Application.unityVersion;
            _report.platform = Application.platform.ToString();
            _report.result = "Running";

            Screen.SetResolution(1280, 720, false);
            yield return WaitForScene(SceneLoader.MainMenuSceneName, 5f);
            if (!Require(
                    SceneManager.GetActiveScene().name
                        == SceneLoader.MainMenuSceneName,
                    "main_menu",
                    "Boot reached MainMenu."))
            {
                yield break;
            }

            if (!PrepareCleanSave())
            {
                yield break;
            }

            SceneLoader loader = SceneLoader.Instance;
            if (!Require(
                    loader != null
                    && loader.LoadMap(
                        SceneLoader.DefaultMapId,
                        MapContentIds.QingshiTownTeleport),
                    "qingshi_load_requested",
                    loader?.LastError ?? "SceneLoader unavailable."))
            {
                yield break;
            }

            yield return WaitForScene(SceneLoader.DefaultMapSceneName, 8f);
            if (!Require(
                    SceneManager.GetActiveScene().name
                        == SceneLoader.DefaultMapSceneName,
                    "qingshi_loaded",
                    "Qingshi loaded through SceneLoader."))
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.4f);

            SpiritRootSelectionView rootView =
                FindAnyObjectByType<SpiritRootSelectionView>();
            if (!Require(
                    rootView != null
                    && rootView.IsOpen
                    && rootView.SelectRoot(SpiritRootType.Wood),
                    "root_preview",
                    "Wood root can be previewed in the clean-save modal."))
            {
                yield break;
            }
            rootView.ConfirmSelection();
            if (!Require(
                    ServiceLocator.Get<ISpiritRootService>().Root
                        == SpiritRootType.Wood
                    && !rootView.IsOpen,
                    "root_committed",
                    "Root selection committed and returned gameplay input."))
            {
                yield break;
            }

            QuestTrackerView tracker = FindAnyObjectByType<QuestTrackerView>();
            QuestWorldMarkerView marker =
                FindAnyObjectByType<QuestWorldMarkerView>();
            tracker?.Refresh();
            marker?.RefreshNow();
            if (!Require(
                    tracker != null
                    && tracker.IsVisible
                    && tracker.CurrentQuestId
                        == QuestContentIds.MainRootAwakening
                    && marker != null
                    && marker.IsVisible
                    && marker.TargetId == QuestContentIds.YaoLaoNpc,
                    "first_objective_visible",
                    "Fresh save names the first main quest and points to Yao Lao."))
            {
                yield break;
            }
            yield return Capture("01-qingshi-guidance-furnace.png");

            IQuestService quests = ServiceLocator.Get<IQuestService>();
            IInventoryService inventory =
                ServiceLocator.Get<IInventoryService>();
            if (!CompleteQuest(
                    quests,
                    QuestContentIds.MainRootAwakening,
                    () => quests.NotifyTalk(QuestContentIds.YaoLaoNpc)))
            {
                yield break;
            }
            if (!CompleteQuest(
                    quests,
                    QuestContentIds.MainHuntWolves,
                    () => NotifyKills(
                        quests,
                        QuestContentIds.GreyWolfEnemy,
                        3)))
            {
                yield break;
            }

            if (!Require(
                    quests.Accept(QuestContentIds.MainGatherQingxin),
                    "quest_03_accept",
                    "Main quest 03 accepted."))
            {
                yield break;
            }
            if (!Require(
                    inventory.AddItem(
                        InventoryContentIds.QingxinGrass,
                        5,
                        AcquireSource.Gather),
                    "quest_03_gather",
                    "Five Qingxin grass entered inventory through Gather source."))
            {
                yield break;
            }
            quests.NotifyCollect(
                InventoryContentIds.QingxinGrass,
                inventory.CountItem(InventoryContentIds.QingxinGrass));
            NotifyKills(quests, QuestContentIds.EliteWolfEnemy, 1);
            if (!Require(
                    quests.TurnIn(QuestContentIds.MainGatherQingxin),
                    "quest_03_turn_in",
                    "Main quest 03 completed and turned in."))
            {
                yield break;
            }

            if (!Require(
                    quests.Accept(QuestContentIds.MainCraftManaPotion),
                    "quest_04_accept",
                    "Main quest 04 accepted."))
            {
                yield break;
            }
            if (!Require(
                    inventory.AddItem(
                        InventoryContentIds.SpiritDust,
                        2,
                        AcquireSource.Gather),
                    "quest_04_materials",
                    "Mana-potion materials are available."))
            {
                yield break;
            }

            PlayerController player = FindAnyObjectByType<PlayerController>();
            AlchemyFurnaceInteractable furnace =
                FindAnyObjectByType<AlchemyFurnaceInteractable>();
            AlchemyPanelView alchemyPanel =
                FindAnyObjectByType<AlchemyPanelView>();
            AlchemySystem alchemy = FindAnyObjectByType<AlchemySystem>();
            if (!Require(
                    player != null
                    && furnace != null
                    && alchemyPanel != null
                    && alchemy != null,
                    "furnace_runtime",
                    "Player, real furnace, alchemy UI and service exist."))
            {
                yield break;
            }
            alchemy.SetRandomValueProvider(() => 0f);
            player.TeleportTo(
                furnace.transform.position + Vector3.forward,
                Quaternion.identity);
            if (!Require(
                    furnace.TryInteract()
                    && alchemyPanel.IsOpen
                    && !ServiceLocator.Get<
                        Wendao.Systems.Input.IPlayerInputSource>().IsEnabled,
                    "furnace_opened",
                    "Furnace interaction opened the modal and locked gameplay."))
            {
                yield break;
            }
            yield return Capture("02-alchemy-furnace.png");
            alchemyPanel.SelectRecipe(AlchemyContentIds.ManaRecipe);
            if (!Require(
                    alchemyPanel.TryCraftSelected()
                    && quests.GetStatus(
                        QuestContentIds.MainCraftManaPotion)
                        == QuestStatus.Completed,
                    "quest_04_crafted",
                    "Runtime furnace craft completed main quest 04."))
            {
                yield break;
            }
            ServiceLocator.Get<IUIManager>().HidePanel(UiPanelIds.Alchemy);
            if (!Require(
                    ServiceLocator.Get<
                        Wendao.Systems.Input.IPlayerInputSource>().IsEnabled
                    && quests.TurnIn(
                        QuestContentIds.MainCraftManaPotion),
                    "quest_04_turn_in",
                    "Closing alchemy restored input and quest 04 turned in."))
            {
                yield break;
            }

            if (!CompleteQuest(
                    quests,
                    QuestContentIds.MainOpenCangwuPath,
                    () => quests.NotifyReach(
                        QuestContentIds.QingshiSecretPath)))
            {
                yield break;
            }
            CangwuPathGate cangwuGate =
                FindAnyObjectByType<CangwuPathGate>();
            if (!Require(
                    cangwuGate != null
                    && cangwuGate.IsOpen
                    && cangwuGate.TryEnter(player.gameObject),
                    "cangwu_gate",
                    "Quest flag opened the real Qingshi path gate."))
            {
                yield break;
            }

            yield return WaitForScene(SceneLoader.CangwuMapSceneName, 8f);
            if (!Require(
                    SceneManager.GetActiveScene().name
                        == SceneLoader.CangwuMapSceneName,
                    "cangwu_loaded",
                    "Cangwu loaded through the real gate."))
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            player = FindAnyObjectByType<PlayerController>();
            TeleportPoint cangwuTeleport =
                FindTeleport(MapContentIds.CangwuGateTeleport);
            if (!Require(
                    player != null
                    && cangwuTeleport != null
                    && (cangwuTeleport.TryDiscover(player.gameObject)
                        || ServiceLocator.Get<IMapTravelService>()
                            .IsTeleportUnlocked(
                                MapContentIds.CangwuGateTeleport)),
                    "cangwu_teleport",
                    "Cangwu teleport point is discovered."))
            {
                yield break;
            }

            quests = ServiceLocator.Get<IQuestService>();
            if (!CompleteQuest(
                    quests,
                    QuestContentIds.MainCangwuTrial,
                    () => NotifyKills(
                        quests,
                        QuestContentIds.GreyWolfEnemy,
                        3)))
            {
                yield break;
            }
            if (!CompleteQuest(
                    quests,
                    QuestContentIds.MainFoundationClue,
                    () => quests.NotifyTalk(
                        QuestContentIds.CangwuGuardNpc)))
            {
                yield break;
            }

            IMapTravelService travel =
                ServiceLocator.Get<IMapTravelService>();
            if (!Require(
                    travel.Travel(MapContentIds.QingshiTownTeleport),
                    "return_qingshi",
                    "Unlocked Qingshi teleport requested."))
            {
                yield break;
            }
            yield return WaitForScene(SceneLoader.DefaultMapSceneName, 8f);
            if (!Require(
                    SceneManager.GetActiveScene().name
                        == SceneLoader.DefaultMapSceneName,
                    "qingshi_returned",
                    "Returned to Qingshi for breakthrough quests."))
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.3f);

            quests = ServiceLocator.Get<IQuestService>();
            CultivationManager cultivation =
                FindAnyObjectByType<CultivationManager>();
            if (!Require(
                    quests.Accept(
                        QuestContentIds.MainFoundationBreakthrough)
                    && cultivation != null,
                    "quest_08_accept",
                    "Foundation quest and cultivation runtime are ready."))
            {
                yield break;
            }
            cultivation.SetRandomValueProvider(() => 0f);
            cultivation.AddXp(100000f, XpSourceType.Quest);
            if (!Require(
                    cultivation.TryBreakthrough(),
                    "foundation_start",
                    "Foundation breakthrough ceremony started."))
            {
                yield break;
            }
            yield return WaitForBreakthrough(cultivation, 15f);
            if (!Require(
                    cultivation.Realm == RealmType.Foundation
                    && quests.GetStatus(
                        QuestContentIds.MainFoundationBreakthrough)
                        == QuestStatus.Completed
                    && quests.TurnIn(
                        QuestContentIds.MainFoundationBreakthrough),
                    "foundation_complete",
                    "Foundation reached and quest 08 turned in."))
            {
                yield break;
            }

            if (!Require(
                    quests.Accept(
                        QuestContentIds.MainGoldenCoreBreakthrough),
                    "quest_09_accept",
                    "Golden Core quest accepted and pill latch initialized."))
            {
                yield break;
            }
            cultivation.AddXp(200000f, XpSourceType.Quest);
            cultivation.SetRandomValueProvider(() => 0f);
            if (!Require(
                    cultivation.TryBreakthrough(),
                    "golden_core_start",
                    "Golden Core breakthrough ceremony started."))
            {
                yield break;
            }
            yield return WaitForBreakthrough(cultivation, 15f);
            if (!Require(
                    cultivation.Realm == RealmType.GoldenCore,
                    "golden_core_complete",
                    "Golden Core realm reached."))
            {
                yield break;
            }

            travel = ServiceLocator.Get<IMapTravelService>();
            if (!Require(
                    travel.Travel(MapContentIds.CangwuGateTeleport),
                    "travel_cangwu_for_blackwind",
                    "Cangwu teleport requested for Blackwind entrance."))
            {
                yield break;
            }
            yield return WaitForScene(SceneLoader.CangwuMapSceneName, 8f);
            if (!Require(
                    SceneManager.GetActiveScene().name
                        == SceneLoader.CangwuMapSceneName,
                    "cangwu_returned",
                    "Returned to Cangwu at Golden Core."))
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            player = FindAnyObjectByType<PlayerController>();
            BlackwindDungeonGate blackwindGate =
                FindAnyObjectByType<BlackwindDungeonGate>();
            if (!Require(
                    player != null
                    && blackwindGate != null
                    && blackwindGate.MeetsRealmRequirement
                    && blackwindGate.TryEnter(player.gameObject),
                    "blackwind_gate",
                    "Golden Core passed the real Blackwind gate."))
            {
                yield break;
            }

            yield return WaitForScene(
                SceneLoader.BlackwindDungeonSceneName,
                8f);
            if (!Require(
                    SceneManager.GetActiveScene().name
                        == SceneLoader.BlackwindDungeonSceneName,
                    "blackwind_loaded",
                    "Blackwind dungeon loaded."))
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.3f);

            quests = ServiceLocator.Get<IQuestService>();
            if (!Require(
                    quests.GetStatus(
                        QuestContentIds.MainGoldenCoreBreakthrough)
                        == QuestStatus.Completed
                    && quests.TurnIn(
                        QuestContentIds.MainGoldenCoreBreakthrough)
                    && quests.Accept(
                        QuestContentIds.MainDefeatStoneGeneral),
                    "blackwind_quests",
                    "Quest 09 turned in and quest 10 accepted at Blackwind."))
            {
                yield break;
            }

            IBlackwindDungeonService dungeon =
                ServiceLocator.Get<IBlackwindDungeonService>();
            if (!dungeon.IsRunActive)
            {
                dungeon.BeginRun();
            }
            bool floorsCompleted =
                dungeon.EnterFloor(1)
                && dungeon.NotifyCombatObjectiveCleared(1)
                && dungeon.ActivatePressurePlate()
                && dungeon.EnterFloor(2)
                && dungeon.NotifyCombatObjectiveCleared(2)
                && dungeon.EnterFloor(3)
                && dungeon.CompleteExplorationFloor(3)
                && dungeon.EnterFloor(4)
                && dungeon.TryUseHealingSpring()
                && dungeon.NotifyCombatObjectiveCleared(4)
                && dungeon.EnterFloor(5);
            dungeon.NotifyBossDefeated();
            quests.NotifyKill(QuestContentIds.StoneGeneralEnemy);
            if (!Require(
                    floorsCompleted
                    && dungeon.IsRunCompleted
                    && dungeon.Checkpoint
                        == BlackwindDungeonSystem.MaximumCheckpoint
                    && quests.GetStatus(
                        QuestContentIds.MainDefeatStoneGeneral)
                        == QuestStatus.Completed
                    && quests.TurnIn(
                        QuestContentIds.MainDefeatStoneGeneral),
                    "blackwind_complete",
                    "Five floors, checkpoint and chapter boss completed."))
            {
                yield break;
            }
            yield return Capture("03-blackwind-complete.png");

            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (!Require(
                    stats != null,
                    "death_runtime",
                    "PlayerStats exists for death/respawn audit."))
            {
                yield break;
            }
            CombatSystem combat = FindAnyObjectByType<CombatSystem>();
            if (!Require(
                    combat != null,
                    "death_combat_service",
                    "CombatSystem exists for lethal damage resolution."))
            {
                yield break;
            }
            combat.DealDamage(
                stats,
                new DamageRequest
                {
                    Source = null,
                    BaseDamage = stats.MaxHp + 1000f,
                    Type = DamageType.True,
                    Element = ElementType.None,
                    Multiplier = 1f,
                    CanCrit = false,
                    IgnoreAttackScaling = true
                });
            if (!Require(
                    stats.IsDead
                    && GameManager.Instance.State == GameState.Dead,
                    "death_entered",
                    $"isDead={stats.IsDead}; state={GameManager.Instance.State}."))
            {
                yield break;
            }
            if (!Require(
                    stats.CanRespawn,
                    "death_respawn_available",
                    $"nearest={stats.NearestRespawnPointId}."))
            {
                yield break;
            }
            bool respawned = stats.TryRespawnAtNearestPoint();
            if (!Require(
                    respawned
                    && !stats.IsDead
                    && GameManager.Instance.State == GameState.Playing,
                    "death_respawn",
                    $"respawned={respawned}; isDead={stats.IsDead}; "
                    + $"state={GameManager.Instance.State}."))
            {
                yield break;
            }

            SaveManager save = SaveManager.Instance;
            inventory = ServiceLocator.Get<IInventoryService>();
            int expectedPotionCount =
                inventory.CountItem(InventoryContentIds.ManaPotion01);
            if (!Require(
                    save.SaveGame(0),
                    "save_final",
                    save.LastError ?? "Final save succeeded."))
            {
                yield break;
            }
            save.Profile.Realm = (int)RealmType.QiCondensation;
            save.World.UnlockedMaps.Clear();
            while (inventory.RemoveItem(
                       InventoryContentIds.ManaPotion01,
                       1))
            {
            }
            FindAnyObjectByType<QuestManager>()
                ?.RestoreSaveData(new QuestSaveData());
            if (!Require(
                    save.LoadGame(0)
                    && save.Profile.Realm == (int)RealmType.GoldenCore
                    && ServiceLocator.Get<IQuestService>()
                        .GetStatus(
                            QuestContentIds.MainDefeatStoneGeneral)
                        == QuestStatus.TurnedIn
                    && ServiceLocator.Get<IInventoryService>()
                        .CountItem(InventoryContentIds.ManaPotion01)
                        == expectedPotionCount
                    && save.World.DungeonCheckpoint[
                        MapContentIds.BlackwindMap]
                        == BlackwindDungeonSystem.MaximumCheckpoint,
                    "load_round_trip",
                    save.LastError ?? "Critical journey state restored."))
            {
                yield break;
            }

            _report.result = "Passed";
            _report.completedAtUtc = DateTime.UtcNow.ToString("O");
            WriteReport();
            _finished = true;
            Debug.Log(
                $"G09-03 journey audit passed: {_report.runId}; "
                + $"steps={_report.steps.Count}.");
            Application.Quit(0);
        }

        private bool PrepareCleanSave()
        {
            if (HasArgument(CleanArgument) && Directory.Exists(_storageRoot))
            {
                if (!IsSafeAuditPath(_storageRoot))
                {
                    return Require(
                        false,
                        "clean_save_root",
                        $"Refused unsafe journey storage path: {_storageRoot}");
                }

                Directory.Delete(_storageRoot, true);
            }

            Directory.CreateDirectory(_storageRoot);
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return Require(
                    false,
                    "save_service",
                    "SaveManager is unavailable.");
            }

            save.ConfigureStorageRoot(_storageRoot);
            return Require(
                save.SaveGame(0),
                "clean_save_created",
                save.LastError ?? "Clean slot 0 created.");
        }

        private bool CompleteQuest(
            IQuestService quests,
            string questId,
            Action progress)
        {
            if (!Require(
                    quests.Accept(questId),
                    questId + "_accept",
                    "Quest accepted."))
            {
                return false;
            }
            progress();
            if (!Require(
                    quests.GetStatus(questId) == QuestStatus.Completed,
                    questId + "_complete",
                    "Quest objectives completed."))
            {
                return false;
            }
            return Require(
                quests.TurnIn(questId),
                questId + "_turn_in",
                "Quest turned in.");
        }

        private static void NotifyKills(
            IQuestService quests,
            string enemyId,
            int count)
        {
            for (int index = 0; index < count; index++)
            {
                quests.NotifyKill(enemyId);
            }
        }

        private IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != sceneName
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator WaitForBreakthrough(
            CultivationManager cultivation,
            float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (cultivation != null
                && cultivation.IsBreakthroughActive
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private IEnumerator Capture(string fileName)
        {
            if (string.IsNullOrWhiteSpace(_captureDirectory))
            {
                yield break;
            }

            Directory.CreateDirectory(_captureDirectory);
            string path = Path.Combine(_captureDirectory, fileName);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        private static TeleportPoint FindTeleport(string teleportId)
        {
            TeleportPoint[] points = FindObjectsByType<TeleportPoint>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < points.Length; index++)
            {
                if (points[index] != null
                    && points[index].TeleportId == teleportId)
                {
                    return points[index];
                }
            }

            return null;
        }

        private bool Require(bool condition, string step, string detail)
        {
            _report.steps.Add(
                new JourneyAuditStep
                {
                    name = step,
                    passed = condition,
                    detail = detail ?? string.Empty,
                    scene = SceneManager.GetActiveScene().name
                });
            if (condition)
            {
                return true;
            }

            _report.result = "Failed";
            _report.error = step + ": " + detail;
            _report.completedAtUtc = DateTime.UtcNow.ToString("O");
            WriteReport();
            _finished = true;
            Debug.LogError("G09-03 journey audit failed: " + _report.error);
            Application.Quit(1);
            return false;
        }

        private void OnDestroy()
        {
            if (!_finished && !string.IsNullOrWhiteSpace(_reportPath))
            {
                _report.result = "Aborted";
                _report.error = "Journey audit host was destroyed before completion.";
                _report.completedAtUtc = DateTime.UtcNow.ToString("O");
                WriteReport();
            }
        }

        private void WriteReport()
        {
            if (string.IsNullOrWhiteSpace(_reportPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(_reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(
                _reportPath,
                JsonUtility.ToJson(_report, true));
        }

        private static string ResolveStorageRoot()
        {
            string configured = ResolvePath(ReadArgument(StorageArgument));
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Path.GetTempPath(),
                    "WendaoG0903Journey")
                : configured;
        }

        private static string ResolvePath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : Path.GetFullPath(value);
        }

        private static bool IsSafeAuditPath(string path)
        {
            string full = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return full.StartsWith(temp, StringComparison.Ordinal);
        }

        private static string ReadArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(
                        arguments[index],
                        name,
                        StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        private static bool HasArgument(string name)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class JourneyAuditReport
        {
            public string runId = string.Empty;
            public string result = string.Empty;
            public string unityVersion = string.Empty;
            public string platform = string.Empty;
            public string completedAtUtc = string.Empty;
            public string error = string.Empty;
            public List<JourneyAuditStep> steps =
                new List<JourneyAuditStep>();
        }

        [Serializable]
        private sealed class JourneyAuditStep
        {
            public string name = string.Empty;
            public bool passed;
            public string detail = string.Empty;
            public string scene = string.Empty;
        }
    }
}
