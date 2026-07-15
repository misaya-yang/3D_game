using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Wendao.Systems.Diagnostics
{
    public static class MvpBoundaryCatalog
    {
        public const string InventoryFullWorldDrop = "inventory_full_world_drop";
        public const string SkillManaAndCooldown = "skill_mana_and_cooldown";
        public const string EquipmentRealmGate = "equipment_realm_gate";
        public const string BreakthroughInvincibility =
            "breakthrough_invincibility";
        public const string QuestDuplicateAccept = "quest_duplicate_accept";
        public const string DialogueSceneChange = "dialogue_scene_change";
        public const string EnemyDeadTargetReturn = "enemy_dead_target_return";
        public const string SaveFailureRecovery = "save_failure_recovery";

        public static readonly string[] All =
        {
            InventoryFullWorldDrop,
            SkillManaAndCooldown,
            EquipmentRealmGate,
            BreakthroughInvincibility,
            QuestDuplicateAccept,
            DialogueSceneChange,
            EnemyDeadTargetReturn,
            SaveFailureRecovery
        };
    }

    [Serializable]
    public sealed class MvpRuntimeDiagnosticsReport
    {
        public int TargetWidth;
        public int TargetHeight;
        public string QualityName = string.Empty;
        public float PlannedSimulationSeconds;
        public float SimulatedSeconds;
        public int SampledFrames;
        public float SampledWallSeconds;
        public float AverageFramesPerSecond;
        public float WorstFrameMilliseconds;
        public int FramesOverBudget;
        public long PeakAllocatedMemoryBytes;
        public int ErrorCount;
        public int ExceptionCount;

        public bool DurationComplete =>
            SimulatedSeconds + 0.001f >= PlannedSimulationSeconds;
        public bool MeetsFrameBudget =>
            SampledFrames > 0
            && AverageFramesPerSecond >= MvpRuntimeDiagnostics.MinimumFramesPerSecond;
        public bool MeetsMemoryBudget =>
            PeakAllocatedMemoryBytes <= MvpRuntimeDiagnostics.MaximumAllocatedBytes;
        public bool IsStable => DurationComplete
            && MeetsFrameBudget
            && MeetsMemoryBudget
            && ErrorCount == 0
            && ExceptionCount == 0;
    }

    public sealed class MvpRuntimeDiagnostics : IDisposable
    {
        public const int TargetWidth = 1920;
        public const int TargetHeight = 1080;
        public const string TargetQualityName = "Medium";
        public const float MinimumFramesPerSecond = 30f;
        public const float FrameBudgetSeconds = 1f / MinimumFramesPerSecond;
        public const long MaximumAllocatedBytes = 6L * 1024L * 1024L * 1024L;
        public const float StabilityDurationSeconds = 60f * 60f;

        private readonly MvpRuntimeDiagnosticsReport _report =
            new MvpRuntimeDiagnosticsReport
            {
                TargetWidth = TargetWidth,
                TargetHeight = TargetHeight,
                QualityName = TargetQualityName
            };
        private bool _capturingLogs;

        public MvpRuntimeDiagnosticsReport Report => _report;

        public void Begin(float plannedSimulationSeconds)
        {
            _report.PlannedSimulationSeconds = Mathf.Max(
                0f,
                plannedSimulationSeconds);
            _report.SimulatedSeconds = 0f;
            _report.SampledFrames = 0;
            _report.SampledWallSeconds = 0f;
            _report.AverageFramesPerSecond = 0f;
            _report.WorstFrameMilliseconds = 0f;
            _report.FramesOverBudget = 0;
            _report.PeakAllocatedMemoryBytes = 0L;
            _report.ErrorCount = 0;
            _report.ExceptionCount = 0;
            StartLogCapture();
            SampleMemory();
        }

        public void AdvanceSimulation(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds)
                || float.IsInfinity(deltaSeconds)
                || deltaSeconds <= 0f)
            {
                return;
            }

            _report.SimulatedSeconds += deltaSeconds;
            SampleMemory();
        }

        public void SampleFrame(float unscaledDeltaSeconds)
        {
            if (float.IsNaN(unscaledDeltaSeconds)
                || float.IsInfinity(unscaledDeltaSeconds)
                || unscaledDeltaSeconds <= 0f)
            {
                return;
            }

            _report.SampledFrames++;
            _report.SampledWallSeconds += unscaledDeltaSeconds;
            _report.WorstFrameMilliseconds = Mathf.Max(
                _report.WorstFrameMilliseconds,
                unscaledDeltaSeconds * 1000f);
            if (unscaledDeltaSeconds > FrameBudgetSeconds)
            {
                _report.FramesOverBudget++;
            }

            _report.AverageFramesPerSecond = _report.SampledWallSeconds > 0f
                ? _report.SampledFrames / _report.SampledWallSeconds
                : 0f;
            SampleMemory();
        }

        public void Dispose()
        {
            if (_capturingLogs)
            {
                Application.logMessageReceived -= HandleLog;
                _capturingLogs = false;
            }
        }

        private void StartLogCapture()
        {
            if (_capturingLogs)
            {
                return;
            }

            Application.logMessageReceived += HandleLog;
            _capturingLogs = true;
        }

        private void HandleLog(
            string condition,
            string stackTrace,
            LogType type)
        {
            switch (type)
            {
                case LogType.Exception:
                    _report.ExceptionCount++;
                    break;
                case LogType.Error:
                case LogType.Assert:
                    _report.ErrorCount++;
                    break;
            }
        }

        private void SampleMemory()
        {
            _report.PeakAllocatedMemoryBytes = Math.Max(
                _report.PeakAllocatedMemoryBytes,
                Profiler.GetTotalAllocatedMemoryLong());
        }
    }
}
