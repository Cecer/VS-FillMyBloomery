using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FillMyBloomery;

public class FillMyBloomeryModSystem : ModSystem
{
    private const string HarmonyId = "fillmybloomery";

    private HarmonyLib.Harmony? _harmony;

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void Start(ICoreAPI api)
    {
        _harmony = new HarmonyLib.Harmony(HarmonyId);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _harmony!.PatchCategory("client");
        _harmony!.PatchCategory("common");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _harmony!.PatchCategory("server");
        _harmony!.PatchCategory("common");
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
    }
}