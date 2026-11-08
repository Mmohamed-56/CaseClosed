using System;

namespace CaseClosed.Voice
{
    /// <summary>
    /// Mix notes for Magdy's low-poly people pack (David Jalbert).
    /// Slots are body / hair / top / bottoms plus skin, color, presentation, and accessories.
    /// Do not treat this as a single prefab path. Body is the pack build
    /// (normal, stout, strong). Gender presentation picks a man or woman family
    /// only as a starting point; androgynous means either mesh is acceptable.
    /// Hair, top, bottoms, and accessories are what Bezi matches across variants.
    /// </summary>
    [Serializable]
    public sealed class SuspectLook
    {
        public string body;
        public string hair;
        public string skinTone;
        public string top;
        public string bottoms;
        public string outfitColor;
        public string genderPresentation;
        public string accessories;
        public string summary;
    }
}
