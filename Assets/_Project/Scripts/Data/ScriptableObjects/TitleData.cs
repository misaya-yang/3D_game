using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Title_New", menuName = "问道/称号/TitleData")]
    public class TitleData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public string DescriptionKey;
        public string Description;
        public StatBlock Bonus = new StatBlock { CritDamage = 0f };
        public bool ShowInNameplate = true;
    }
}
