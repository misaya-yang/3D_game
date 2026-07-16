using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Enemy;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Shop;
using Wendao.Systems.World;

namespace Wendao.Entities.Visuals
{
    /// <summary>
    /// Opt-in command-line launcher used only to capture real Player evidence.
    /// Normal launches are unchanged unless -wendaoShowcaseScene is present.
    /// </summary>
    public sealed class BudgetArtShowcaseLauncher : MonoBehaviour
    {
        private const string SceneArgument = "-wendaoShowcaseScene";
        private const string CaptureArgument = "-wendaoCapturePath";
        private const string ExitArgument = "-wendaoExitAfterCapture";
        private const string HideUiArgument = "-wendaoHideUi";
        private const string UiStateArgument = "-wendaoShowcaseUiState";
        private const string CaptureWidthArgument = "-wendaoCaptureWidth";
        private const string CaptureHeightArgument = "-wendaoCaptureHeight";
        private const string ArtViewArgument = "-wendaoShowcaseArtView";

        private static bool _installed;
        private string _targetScene = string.Empty;
        private string _capturePath = string.Empty;
        private bool _exitAfterCapture;
        private bool _hideUi;
        private string _uiState = "hud";
        private int _captureWidth = 1280;
        private int _captureHeight = 720;
        private string _artView = "gameplay";
        private bool _loadingTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            InstallFromCommandLine();
        }

        public static void InstallFromCommandLine()
        {
            if (_installed)
            {
                return;
            }

            string target = ReadArgument(SceneArgument);
            if (!IsAllowedScene(target))
            {
                return;
            }

            _installed = true;
            // Player evidence capture is frequently started from a terminal and
            // may never become the frontmost macOS app. Keep the opt-in capture
            // flow advancing even while the window is unfocused.
            Application.runInBackground = true;
            var host = new GameObject("[BudgetArtShowcaseLauncher]");
            DontDestroyOnLoad(host);
            BudgetArtShowcaseLauncher launcher =
                host.AddComponent<BudgetArtShowcaseLauncher>();
            launcher._targetScene = target;
            launcher._capturePath = ReadArgument(CaptureArgument);
            launcher._exitAfterCapture = HasArgument(ExitArgument);
            launcher._hideUi = HasArgument(HideUiArgument);
            launcher._uiState = ReadArgument(UiStateArgument);
            if (string.IsNullOrWhiteSpace(launcher._uiState))
            {
                launcher._uiState = "hud";
            }
            launcher._captureWidth = ReadPositiveIntArgument(
                CaptureWidthArgument,
                1280);
            launcher._captureHeight = ReadPositiveIntArgument(
                CaptureHeightArgument,
                720);
            launcher._artView = ReadArgument(ArtViewArgument);
            if (string.IsNullOrWhiteSpace(launcher._artView))
            {
                launcher._artView = "gameplay";
            }
            Debug.Log($"G09-02 showcase launcher installed for {target}.");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name == _targetScene)
            {
                HandleSceneLoaded(active, LoadSceneMode.Single);
            }
            else
            {
                _loadingTarget = true;
                StartCoroutine(LoadTargetAfterStartup());
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == _targetScene)
            {
                StartCoroutine(CaptureAfterWorldSettles());
                return;
            }

        }

        private IEnumerator LoadTargetAfterStartup()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (SceneLoader.Instance != null
                && SceneLoader.Instance.IsLoading
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Debug.Log($"G09-02 showcase loading {_targetScene}.");
            AsyncOperation load = SceneManager.LoadSceneAsync(
                _targetScene,
                LoadSceneMode.Single);
            if (load != null)
            {
                yield return load;
            }
        }

        private IEnumerator CaptureAfterWorldSettles()
        {
            GameManager gameManager = GameManager.Instance;
            bool captureLoadingScene = SceneManager.GetActiveScene().name
                == SceneLoader.LoadingSceneName;
            if (!captureLoadingScene)
            {
                EnsurePlayingState(gameManager);
            }

            bool captureRootSelection = string.Equals(
                _uiState,
                "root",
                StringComparison.OrdinalIgnoreCase);
            if (!captureLoadingScene
                && !captureRootSelection
                && ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService spiritRoot)
                && !spiritRoot.HasChosenRoot)
            {
                spiritRoot.TryChooseRoot(SpiritRootType.Wood);
            }
            if (!captureLoadingScene
                && ServiceLocator.TryGet<IDayNightService>(
                    out IDayNightService dayNight))
            {
                dayNight.SetTimeOfDay(10f);
            }

            Screen.SetResolution(_captureWidth, _captureHeight, false);
            yield return new WaitForSecondsRealtime(1.5f);
            ApplyArtView(_artView);
            yield return null;
            ApplyUiState(_uiState);
            yield return new WaitForSecondsRealtime(0.5f);
            if (_hideUi)
            {
                HideAllCanvases();
                yield return null;
            }
            if (!string.IsNullOrWhiteSpace(_capturePath))
            {
                string fullPath = Path.GetFullPath(_capturePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                if (_hideUi)
                {
                    HideAllCanvases();
                }
                ScreenCapture.CaptureScreenshot(fullPath);
                Debug.Log($"G09-02 Player screenshot requested: {fullPath}");
                yield return new WaitForSecondsRealtime(1f);
            }

            if (_exitAfterCapture)
            {
                Application.Quit(0);
            }
        }

        private static void EnsurePlayingState(GameManager gameManager)
        {
            if (gameManager == null || gameManager.State == GameState.Playing)
            {
                return;
            }

            if (gameManager.State == GameState.Boot)
            {
                gameManager.TrySetState(GameState.MainMenu);
            }
            if (gameManager.State == GameState.MainMenu)
            {
                gameManager.TrySetState(GameState.Loading);
            }
            if (gameManager.State == GameState.Loading)
            {
                gameManager.TrySetState(GameState.Playing);
            }
        }

        private static void ApplyUiState(string state)
        {
            if (string.IsNullOrWhiteSpace(state)
                || string.Equals(state, "hud", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string normalizedState = state.Trim().ToLowerInvariant();
            switch (normalizedState)
            {
                case "combat":
                    BossArenaController arena = FindAnyObjectByType<
                        BossArenaController>();
                    PlayerController player = FindAnyObjectByType<
                        PlayerController>();
                    if (arena != null && player != null)
                    {
                        player.TeleportTo(
                            arena.ArenaCenter + Vector3.forward * 2f,
                            Quaternion.identity);
                        arena.TickArena();
                        GameManager.Instance?.SetCombatFlag(true);
                        return;
                    }

                    Debug.LogWarning("Cannot open showcase combat state.");
                    return;
                case "root":
                    GameObject rootSelection = GameObject.Find(
                        "SpiritRootSelectionView");
                    rootSelection?.SendMessage(
                        "SelectRoot",
                        SpiritRootType.Wood,
                        SendMessageOptions.DontRequireReceiver);
                    return;
                case "shop":
                    if (ServiceLocator.TryGet<IShopService>(out IShopService shop)
                        && shop.OpenVendor(ShopContentIds.ZhangguiNpc))
                    {
                        return;
                    }

                    Debug.LogWarning("Cannot open showcase shop state.");
                    return;
                case "dialogue":
                    if (ServiceLocator.TryGet<IDialogueService>(
                            out IDialogueService dialogue)
                        && dialogue.TryStartDialogue(
                            QuestContentIds.HuntStartDialogue,
                            QuestContentIds.YaoLaoNpc))
                    {
                        return;
                    }

                    Debug.LogWarning("Cannot open showcase dialogue state.");
                    return;
                case "death":
                    GameManager gameManager = GameManager.Instance;
                    if (gameManager != null
                        && gameManager.State == GameState.Playing
                        && gameManager.TrySetState(GameState.Dead))
                    {
                        return;
                    }

                    Debug.LogWarning("Cannot open showcase death state.");
                    return;
                case "loading":
                    return;
            }

            string panelId;
            switch (normalizedState)
            {
                case "inventory":
                    panelId = "panel_inventory";
                    break;
                case "character":
                    panelId = "panel_character";
                    break;
                case "skill":
                    panelId = "panel_skill";
                    break;
                case "quest":
                    panelId = "panel_quest";
                    break;
                case "map":
                    panelId = "panel_map";
                    break;
                case "alchemy":
                    panelId = "panel_alchemy";
                    break;
                case "pause":
                case "settings":
                    panelId = "panel_pause";
                    break;
                default:
                    Debug.LogWarning($"Unknown showcase UI state: {state}");
                    return;
            }

            GameObject manager = GameObject.Find("UIManager");
            if (manager == null)
            {
                Debug.LogWarning(
                    $"Cannot show {panelId}; scene UI manager is unavailable.");
                return;
            }

            manager.SendMessage(
                "ShowPanel",
                panelId,
                SendMessageOptions.DontRequireReceiver);

            if (normalizedState == "settings")
            {
                GameObject pause = GameObject.Find("PausePanelView");
                pause?.SendMessage(
                    "ShowSettingsSummary",
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        private static void ApplyArtView(string view)
        {
            if (string.IsNullOrWhiteSpace(view)
                || string.Equals(view, "gameplay", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Camera camera = Camera.main;
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (camera == null || player == null)
            {
                Debug.LogWarning($"Cannot apply showcase art view: {view}");
                return;
            }

            ThirdPersonCamera thirdPerson = camera.GetComponent<ThirdPersonCamera>();
            if (thirdPerson != null)
            {
                thirdPerson.enabled = false;
            }

            Vector3 origin = player.transform.position;
            Vector3 focus = origin + Vector3.up * 1.05f;
            string normalized = view.Trim().ToLowerInvariant();
            if (TryGetCharacterCandidate(
                    normalized,
                    out string candidateResource,
                    out Color candidateTint))
            {
                PrepareStudioView(camera, null);
                CreateSingleCharacterCandidate(
                    origin,
                    candidateResource,
                    candidateTint);
                camera.transform.position = origin
                    + Vector3.forward * 4.25f
                    + Vector3.up * 1.34f;
                camera.fieldOfView = 29f;
                camera.transform.LookAt(focus, Vector3.up);
                return;
            }

            switch (normalized)
            {
                case "player_front":
                    camera.transform.position = origin
                        + Vector3.forward * 4.6f
                        + Vector3.right * 2.2f
                        + Vector3.up * 1.38f;
                    camera.fieldOfView = 31f;
                    break;
                case "player_back":
                    camera.transform.position = origin
                        + Vector3.back * 3.15f
                        + Vector3.up * 1.42f;
                    camera.fieldOfView = 34f;
                    break;
                case "player_three_quarter":
                    camera.transform.position = origin
                        + new Vector3(2.35f, 1.45f, 2.55f);
                    camera.fieldOfView = 36f;
                    break;
                case "player_studio_front":
                    PrepareStudioView(camera, player.transform);
                    camera.transform.position = origin
                        + Vector3.forward * 4.15f
                        + Vector3.up * 1.32f;
                    camera.fieldOfView = 29f;
                    break;
                case "player_studio_back":
                    PrepareStudioView(camera, player.transform);
                    camera.transform.position = origin
                        + Vector3.back * 4.15f
                        + Vector3.up * 1.32f;
                    camera.fieldOfView = 29f;
                    break;
                case "player_studio_three_quarter":
                    PrepareStudioView(camera, player.transform);
                    camera.transform.position = origin
                        + new Vector3(2.8f, 1.38f, 3.35f);
                    camera.fieldOfView = 31f;
                    break;
                case "character_lineup":
                    PrepareStudioView(camera, null);
                    CreateCharacterLineup(origin);
                    camera.transform.position = origin
                        + new Vector3(0f, 1.45f, 10.8f);
                    focus = origin + Vector3.up * 1.05f;
                    camera.fieldOfView = 38f;
                    break;
                case "npc_lineup":
                    PrepareStudioView(camera, null);
                    camera.fieldOfView = 35f;
                    GameObject npcLineup = CreateNpcLineup(origin);
                    FrameStudioRoot(camera, npcLineup, 1.18f);
                    return;
                case "enemy_lineup":
                    PrepareStudioView(camera, null);
                    camera.fieldOfView = 39f;
                    GameObject enemyLineup = CreateEnemyLineup(origin);
                    FrameStudioRoot(camera, enemyLineup, 1.2f);
                    return;
                case "final_character_lineup":
                    PrepareStudioView(camera, null);
                    camera.fieldOfView = 33f;
                    CreateFinalCharacterLineup(origin);
                    camera.transform.position = origin
                        + new Vector3(0f, 2.75f, 12.2f);
                    camera.transform.LookAt(
                        origin + new Vector3(0f, 1.65f, 0f),
                        Vector3.up);
                    return;
                case "map_overview":
                    camera.transform.position = origin
                        + new Vector3(-8.5f, 10.5f, -11f);
                    focus = origin + Vector3.forward * 4f;
                    camera.fieldOfView = 52f;
                    break;
                default:
                    Debug.LogWarning($"Unknown showcase art view: {view}");
                    return;
            }

            camera.transform.LookAt(focus, Vector3.up);
        }

        private static bool TryGetCharacterCandidate(
            string view,
            out string resource,
            out Color tint)
        {
            tint = Color.white;
            switch (view)
            {
                case "candidate_monk":
                    resource = BudgetArtCatalog.CharacterRoot + "/Monk";
                    tint = new Color(0.82f, 0.92f, 0.88f, 1f);
                    return true;
                case "candidate_rogue":
                    resource = BudgetArtCatalog.CharacterRoot + "/Rogue";
                    tint = new Color(0.82f, 0.72f, 0.66f, 1f);
                    return true;
                case "candidate_ranger":
                    resource = BudgetArtCatalog.CharacterRoot + "/Ranger";
                    tint = new Color(0.72f, 0.82f, 0.75f, 1f);
                    return true;
                case "candidate_cleric":
                    resource = BudgetArtCatalog.CharacterRoot + "/Cleric";
                    tint = new Color(0.78f, 0.88f, 0.8f, 1f);
                    return true;
                case "candidate_wizard":
                    resource = BudgetArtCatalog.CharacterRoot + "/Wizard";
                    tint = new Color(0.7f, 0.82f, 0.9f, 1f);
                    return true;
                case "candidate_warrior":
                    resource = BudgetArtCatalog.CharacterRoot + "/Warrior";
                    tint = new Color(0.78f, 0.8f, 0.82f, 1f);
                    return true;
                default:
                    resource = string.Empty;
                    return false;
            }
        }

        private static void PrepareStudioView(
            Camera camera,
            Transform visibleRoot)
        {
            Renderer[] renderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = visibleRoot != null
                    && renderer.transform.IsChildOf(visibleRoot);
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.38f, 0.4f, 1f);
            RenderSettings.fog = false;
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.72f, 0.74f, 0.76f, 1f);
            CreateStudioLight(
                "Key",
                new Vector3(34f, -38f, 0f),
                new Color(1f, 0.92f, 0.8f, 1f),
                1.65f,
                LightShadows.Soft);
            CreateStudioLight(
                "Fill",
                new Vector3(22f, 142f, 0f),
                new Color(0.64f, 0.78f, 1f, 1f),
                0.95f,
                LightShadows.None);
            CreateStudioLight(
                "Rim",
                new Vector3(62f, 205f, 0f),
                new Color(0.72f, 0.9f, 1f, 1f),
                0.75f,
                LightShadows.None);
        }

        private static void CreateStudioLight(
            string suffix,
            Vector3 euler,
            Color color,
            float intensity,
            LightShadows shadows)
        {
            var lightObject = new GameObject("[G09-07 Studio " + suffix + "]");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            lightObject.transform.rotation = Quaternion.Euler(euler);
        }

        private static void CreateCharacterLineup(Vector3 origin)
        {
            var lineupRoot = new GameObject("[G09-07 Character Lineup]");
            lineupRoot.transform.position = origin;
            string[] resources =
            {
                BudgetArtCatalog.CharacterRoot + "/Monk",
                BudgetArtCatalog.CharacterRoot + "/Rogue",
                BudgetArtCatalog.CharacterRoot + "/Ranger",
                BudgetArtCatalog.CharacterRoot + "/Cleric",
                BudgetArtCatalog.CharacterRoot + "/Wizard",
                BudgetArtCatalog.CharacterRoot + "/Warrior"
            };
            Color[] tints =
            {
                new Color(0.82f, 0.92f, 0.88f, 1f),
                new Color(0.82f, 0.72f, 0.66f, 1f),
                new Color(0.72f, 0.82f, 0.75f, 1f),
                new Color(0.78f, 0.88f, 0.8f, 1f),
                new Color(0.7f, 0.82f, 0.9f, 1f),
                new Color(0.78f, 0.8f, 0.82f, 1f)
            };

            const float spacing = 1.62f;
            float left = -spacing * (resources.Length - 1) * 0.5f;
            for (int index = 0; index < resources.Length; index++)
            {
                BudgetVisualFactory.CreateResourceProp(
                    lineupRoot.transform,
                    resources[index],
                    "Candidate_" + index,
                    new Vector3(left + spacing * index, 0f, 0f),
                    1.76f,
                    false,
                    0f,
                    tints[index],
                    BudgetMaterialProfile.Character);
            }
        }

        private static void CreateSingleCharacterCandidate(
            Vector3 origin,
            string resource,
            Color tint)
        {
            var root = new GameObject("[G09-07 Character Candidate]");
            root.transform.position = origin;
            BudgetVisualFactory.CreateResourceProp(
                root.transform,
                resource,
                "Candidate",
                Vector3.zero,
                1.82f,
                false,
                0f,
                tint,
                BudgetMaterialProfile.Character);
        }

        private static GameObject CreateNpcLineup(Vector3 origin)
        {
            var root = new GameObject("[G09-07 NPC Lineup]");
            root.transform.position = origin;
            const float spacing = 1.82f;
            string[] names =
            {
                "Player",
                "YaoLao",
                "QingshiGuard",
                "Hermit",
                "Bandit"
            };
            for (int index = 0; index < names.Length; index++)
            {
                var actor = new GameObject("FinalRole_" + names[index]);
                actor.transform.SetParent(root.transform, false);
                actor.transform.localPosition = new Vector3(
                    (index - 2) * spacing,
                    0f,
                    0f);
                if (index == 0)
                {
                    BudgetVisualFactory.AttachPlayer(actor);
                }
                else if (index < 4)
                {
                    BudgetVisualFactory.AttachNpc(actor, names[index]);
                }
                else
                {
                    EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
                    data.Id = EnemyContentIds.Bandit;
                    BudgetVisualFactory.AttachEnemy(actor, data);
                    Destroy(data);
                }
            }
            return root;
        }

        private static GameObject CreateEnemyLineup(Vector3 origin)
        {
            var root = new GameObject("[G09-07 Enemy Lineup]");
            root.transform.position = origin;
            string[] ids =
            {
                EnemyContentIds.Bandit,
                EnemyContentIds.GreyWolf,
                EnemyContentIds.EliteWolf,
                EnemyContentIds.StoneGeneral
            };
            float[] xPositions = { -3.65f, -1.45f, 1.0f, 4.0f };
            for (int index = 0; index < ids.Length; index++)
            {
                var actor = new GameObject("FinalEnemy_" + ids[index]);
                actor.transform.SetParent(root.transform, false);
                actor.transform.localPosition = new Vector3(
                    xPositions[index],
                    0f,
                    0f);
                EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
                data.Id = ids[index];
                BudgetVisualFactory.AttachEnemy(actor, data);
                Destroy(data);
            }
            return root;
        }

        private static GameObject CreateFinalCharacterLineup(
            Vector3 origin)
        {
            var root = new GameObject(
                "[G09-07 Final Character Lineup]");
            root.transform.position = origin;
            string[] roleNames =
            {
                "Player",
                "YaoLao",
                "QingshiGuard",
                "Hermit",
                "Bandit"
            };
            float[] roleX = { -3.35f, -1.68f, 0f, 1.68f, 3.35f };
            for (int index = 0; index < roleNames.Length; index++)
            {
                var actor = new GameObject(
                    "FinalRole_" + roleNames[index]);
                actor.transform.SetParent(root.transform, false);
                actor.transform.localPosition = new Vector3(
                    roleX[index],
                    0f,
                    2.4f);
                if (index == 0)
                {
                    BudgetVisualFactory.AttachPlayer(actor);
                }
                else if (index < 4)
                {
                    BudgetVisualFactory.AttachNpc(
                        actor,
                        roleNames[index]);
                }
                else
                {
                    AttachLineupEnemy(
                        actor,
                        EnemyContentIds.Bandit);
                }
            }

            string[] enemyIds =
            {
                EnemyContentIds.GreyWolf,
                EnemyContentIds.EliteWolf,
                EnemyContentIds.StoneGeneral
            };
            float[] enemyX = { -4.35f, 4.35f, 0f };
            float[] enemyZ = { 0.75f, 0.75f, -2.6f };
            for (int index = 0; index < enemyIds.Length; index++)
            {
                var actor = new GameObject(
                    "FinalEnemy_" + enemyIds[index]);
                actor.transform.SetParent(root.transform, false);
                actor.transform.localPosition = new Vector3(
                    enemyX[index],
                    0f,
                    enemyZ[index]);
                AttachLineupEnemy(actor, enemyIds[index]);
            }

            return root;
        }

        private static void AttachLineupEnemy(
            GameObject actor,
            string enemyId)
        {
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            data.Id = enemyId;
            BudgetVisualFactory.AttachEnemy(actor, data);
            Destroy(data);
        }

        private static void FrameStudioRoot(
            Camera camera,
            GameObject root,
            float padding)
        {
            if (camera == null
                || root == null
                || !TryGetEnabledBounds(root, out Bounds bounds))
            {
                return;
            }

            float verticalHalfAngle = camera.fieldOfView
                * Mathf.Deg2Rad
                * 0.5f;
            float verticalDistance = bounds.extents.y
                / Mathf.Max(0.05f, Mathf.Tan(verticalHalfAngle));
            float horizontalHalfAngle = Mathf.Atan(
                Mathf.Tan(verticalHalfAngle) * camera.aspect);
            float horizontalDistance = bounds.extents.x
                / Mathf.Max(0.05f, Mathf.Tan(horizontalHalfAngle));
            float distance = Mathf.Max(verticalDistance, horizontalDistance)
                * Mathf.Max(1f, padding)
                + bounds.extents.z
                + 0.8f;
            Vector3 focus = bounds.center;
            camera.transform.position = focus
                + Vector3.forward * distance
                + Vector3.up * bounds.size.y * 0.03f;
            camera.transform.LookAt(focus, Vector3.up);
        }

        private static bool TryGetEnabledBounds(
            GameObject root,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in
                root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static void HideAllCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null)
                {
                    continue;
                }

                canvas.enabled = false;
                canvas.gameObject.SetActive(false);
            }
        }

        private static string ReadArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
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

        private static int ReadPositiveIntArgument(string name, int fallback)
        {
            string value = ReadArgument(name);
            return int.TryParse(value, out int parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static bool IsAllowedScene(string sceneName)
        {
            return sceneName == SceneLoader.LoadingSceneName
                || sceneName == SceneLoader.DefaultMapSceneName
                || sceneName == SceneLoader.CangwuMapSceneName
                || sceneName == SceneLoader.BlackwindDungeonSceneName;
        }
    }
}
