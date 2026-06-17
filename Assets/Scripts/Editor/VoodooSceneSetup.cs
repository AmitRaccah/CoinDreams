#if UNITY_EDITOR
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// Deprecated. The Canvas placeholder doll (yellow square + needles + pedestal text)
    /// is no longer used; the voodoo doll will be a 3D scene object, and stab clicks are
    /// now mediated by <c>DrawButtonRouter</c> on the Draw button instead of a dedicated
    /// UI button on the doll. This menu is kept only so old muscle memory surfaces the
    /// warning rather than silently doing nothing.
    /// </summary>
    internal static class VoodooSceneSetup
    {
        [MenuItem("CoinDreams/Voodoo/Setup Voodoo Scene Hierarchy")]
        private static void SetupVoodooScene()
        {
            EditorUtility.DisplayDialog(
                "Voodoo Setup Deprecated",
                "This script no longer creates anything.\n\n" +
                "The Canvas placeholder doll (yellow square, needle squares, pedestal text)\n" +
                "is no longer used — the voodoo doll will be a 3D scene object, not UI.\n\n" +
                "Stab clicks are now mediated by DrawButtonRouter on the Draw button,\n" +
                "so no dedicated stab button is created.\n\n" +
                "Run CoinDreams/Steal/Clean Steal Scene instead — it sets up\n" +
                "VoodooStealCoordinator + DrawButtonRouter on a logic-only GameObject.",
                "OK");
        }
    }
}
#endif
