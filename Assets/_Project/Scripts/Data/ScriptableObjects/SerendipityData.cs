using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Serendipity_New", menuName = "问道/世界/SerendipityData")]
    public class SerendipityData : ScriptableObject
    {
        public string Id;
        public string MapId;
        public string TriggerId;
        public bool OnceOnly = true;
        public string RequiredQuestId;
        public RealmType RequiredRealm = RealmType.Mortal;
        public QuestReward Rewards = new QuestReward();
        public string DialogueId;
        public string WorldFlag;
    }
}
