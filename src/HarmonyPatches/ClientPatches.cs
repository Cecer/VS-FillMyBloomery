using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FillMyBloomery.HarmonyPatches;

[HarmonyPatch]
[HarmonyPatchCategory("client")]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public static class ClientPatches
{
    const string InteractionLangKey = "fillmybloomery:blockhelp-bloomery-addmany";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlockBloomery), nameof(BlockBloomery.OnLoaded))]
    private static void AddMaxAddInteractionText(ref WorldInteraction[] ___interactions, ICoreAPI api)
    {
        if (api.Side == EnumAppSide.Server)
        {
            // Do nothing on the server.
            return;
        }
        
        var add5InteractionIndex = ___interactions.IndexOf(i => i.ActionLangCode == "blockhelp-bloomery-heatablex4");
        var add5Interaction = ___interactions[add5InteractionIndex];

        var interaction = new WorldInteraction
        {
            ActionLangCode = InteractionLangKey,
            HotKeyCodes = [ "ctrl", "shift" ],
            MouseButton = EnumMouseButton.Right,
            Itemstacks = add5Interaction.Itemstacks,
            GetMatchingStacks = add5Interaction.GetMatchingStacks
        };

        var mutableInteractions = ___interactions.ToList();
        mutableInteractions.Insert(add5InteractionIndex + 1, interaction);
        ___interactions = mutableInteractions.ToArray();
    }
}