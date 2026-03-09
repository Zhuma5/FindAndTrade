using Verse;

namespace MGAutoSell
{
    public class Settings : ModSettings
    {
        // TODO Icons in Menu (true)
        public bool scanEveryStack = true;
        public bool showAllMatchingItems = true;
        public bool showQuanityInsteadOfLabel = true;
        public override void ExposeData()
        {
            
        }
    }
}
