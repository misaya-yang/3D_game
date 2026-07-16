using System;
using Wendao.Systems.Enemy;

namespace Wendao.Entities.Visuals
{
    /// <summary>
    /// Stable semantic paths for the curated G08-03 art subset.
    /// Gameplay content ids never depend on third-party filenames directly.
    /// </summary>
    public static class BudgetArtCatalog
    {
        public const string ResourceRoot = "Art/Budget";
        public const string CharacterRoot = ResourceRoot + "/Characters";
        public const string CreatureRoot = ResourceRoot + "/Creatures";
        public const string NatureRoot = ResourceRoot + "/Nature";
        public const string DungeonRoot = ResourceRoot + "/Dungeon";
        public const string SurfaceRoot = ResourceRoot + "/Surfaces";

        public const string Player = CharacterRoot + "/Cultivator";
        public const string Wolf = CreatureRoot + "/Wolf";
        public const string StoneGeneral = CreatureRoot + "/StoneGeneral";
        public const string QingshiSurface =
            SurfaceRoot + "/grass_path_2_diff_1k";
        public const string CangwuSurface =
            SurfaceRoot + "/forest_ground_04_diff_1k";
        public const string BlackwindSurface =
            SurfaceRoot + "/rocky_terrain_diff_1k";
        public const string NpcHealer =
            CharacterRoot + "/NpcHealer_Modular";
        public const string NpcGuard =
            CharacterRoot + "/NpcGuard_Modular";
        public const string NpcHermit =
            CharacterRoot + "/NpcHermit_Modular";
        public const string HumanEnemy =
            CharacterRoot + "/Bandit_Modular";
        public const string AlchemyPot = NatureRoot + "/pot_large";
        public const string AlchemyHearth =
            NatureRoot + "/campfire_stones";

        public static readonly string[] RequiredCharacterResources =
        {
            Player,
            NpcHealer,
            NpcGuard,
            NpcHermit,
            HumanEnemy
        };

        public static readonly string[] RequiredCharacterTextureResources =
        {
            CharacterRoot + "/CultivatorTextures/Skin",
            CharacterRoot + "/CultivatorTextures/Eyes",
            CharacterRoot + "/CultivatorTextures/Hair",
            CharacterRoot + "/CultivatorTextures/Robe",
            CharacterRoot + "/CultivatorTextures/Ranger"
        };

        public static readonly string[] RequiredRefinementResources =
        {
            Wolf,
            StoneGeneral,
            QingshiSurface,
            CangwuSurface,
            BlackwindSurface
        };

        public static readonly string[] QingshiTreeResources =
        {
            Nature("tree_default"),
            Nature("tree_oak"),
            Nature("tree_tall"),
            Nature("tree_thin"),
            Nature("tree_small")
        };

        public static readonly string[] CangwuTreeResources =
        {
            Nature("tree_pineTallA"),
            Nature("tree_pineTallB"),
            Nature("tree_pineSmallA"),
            Nature("tree_thin")
        };

        public static readonly string[] RockResources =
        {
            Nature("rock_largeA"),
            Nature("rock_largeC"),
            Nature("rock_largeE"),
            Nature("rock_smallA"),
            Nature("rock_smallC"),
            Nature("rock_smallF"),
            Nature("rock_tallA"),
            Nature("rock_tallC"),
            Nature("stone_largeB"),
            Nature("stone_smallB")
        };

        public static readonly string[] DungeonRoomResources =
        {
            Dungeon("room-small"),
            Dungeon("room-small-variation"),
            Dungeon("room-wide"),
            Dungeon("room-large")
        };

        public static string Nature(string modelName)
        {
            return NatureRoot + "/" + modelName;
        }

        public static string Dungeon(string modelName)
        {
            return DungeonRoot + "/" + modelName;
        }

        public static bool IsModularCharacter(string resourcePath)
        {
            return string.Equals(
                    resourcePath,
                    NpcGuard,
                    StringComparison.Ordinal)
                || string.Equals(
                    resourcePath,
                    NpcHealer,
                    StringComparison.Ordinal)
                || string.Equals(
                    resourcePath,
                    NpcHermit,
                    StringComparison.Ordinal)
                || string.Equals(
                    resourcePath,
                    HumanEnemy,
                    StringComparison.Ordinal);
        }

        public static string GetNpcResource(string objectName)
        {
            string value = objectName ?? string.Empty;
            if (value.IndexOf("YaoLao", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Danding", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return NpcHealer;
            }

            if (value.IndexOf("Hermit", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Echo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return NpcHermit;
            }

            return NpcGuard;
        }

        public static string GetEnemyResource(string enemyId)
        {
            switch (enemyId)
            {
                case EnemyContentIds.Bandit:
                case EnemyContentIds.BlackwindSpawn:
                    return HumanEnemy;
                default:
                    return string.Empty;
            }
        }
    }
}
