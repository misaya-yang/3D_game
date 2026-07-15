using System;

namespace Wendao.Data
{
    [Serializable]
    public struct BreakthroughBlocker
    {
        public string Code;
        public string MessageKey;
        public string RelatedItemId;
        public string[] AcquisitionHintKeys;
    }
}
