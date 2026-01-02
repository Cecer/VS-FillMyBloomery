using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace FillMyBloomery.HarmonyPatches;

[HarmonyPatch]
[HarmonyPatchCategory("server")]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public static class ServerPatches
{
    /// <remarks>
    /// Before:
    ///   <code>
    ///     OreCapacity - OreSlot.StackSize
    ///   </code>
    ///
    /// After:
    ///   <code>
    ///     (combustibleProps.SmeltedRatio * 6) - OreSlot.StackSize
    ///
    ///     (activeHotbarSlot.Itemstack.ItemAttributes?["bloomeryFuelRatio"].AsInt(combustibleProps.SmeltedRatio) * 6) - OreSlot.StackSize
    ///   </code>
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.TryAdd))]
    private static List<CodeInstruction> FixMaxAddIntoEmptySlot(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
            IL_007d: ldarg.0      // this
            IL_007e: call         instance int32 Vintagestory.GameContent.BlockEntityBloomery::get_OreCapacity()
            IL_0083: ldarg.0      // this
            IL_0084: call         instance class [VintagestoryAPI]Vintagestory.API.Common.ItemSlot Vintagestory.GameContent.BlockEntityBloomery::get_OreSlot()
            IL_0089: callvirt     instance int32 [VintagestoryAPI]Vintagestory.API.Common.ItemSlot::get_StackSize()
            IL_008e: sub
            IL_008f: ldarg.2      // quantity
            IL_0090: call         int32 [System.Runtime]System.Math::Min(int32, int32)
         */

        var matcher = new CodeMatcher(instructions, generator);

        matcher.Start();
        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Call, typeof(BlockEntityBloomery).PropertyGetter("OreCapacity")),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Call, typeof(BlockEntityBloomery).PropertyGetter("OreSlot")),
            new CodeMatch(OpCodes.Callvirt, typeof(IPlayer).PropertyGetter(nameof(ItemSlot.StackSize))),
            new CodeMatch(OpCodes.Sub),
            new CodeMatch(OpCodes.Ldarg_2),
            new CodeMatch(OpCodes.Call, typeof(Math).Method(nameof(Math.Min), [ typeof(int), typeof(int) ]))
        );
        matcher.ThrowIfNotMatch("Failed to match expected opcodes in BlockEntityBloomery.TryAdd");

        matcher.DefineLabel(out var nullItemAttributesLabel);
        matcher.DefineLabel(out var multiplyBySixLabel);

        matcher.RemoveInstructions(2);

        matcher.Insert(
            // activeHotbarSlot.Itemstack.ItemAttributes
            new CodeInstruction(OpCodes.Ldloc_0), // activeHotbarSlot
            new CodeInstruction(OpCodes.Callvirt, typeof(ItemSlot).PropertyGetter(nameof(ItemSlot.Itemstack))),
            new CodeInstruction(OpCodes.Callvirt, typeof(ItemStack).PropertyGetter(nameof(ItemStack.ItemAttributes))),

            // Branch if ItemAttributes is null
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brfalse_S, nullItemAttributesLabel),



            // PATH: When ItemAttributes is not null
            // {activeHotbarSlot.Itemstack.ItemAttributes}["bloomeryFuelRatio"]
            new CodeInstruction(OpCodes.Ldstr, "bloomeryFuelRatio"),
            new CodeInstruction(OpCodes.Callvirt, typeof(JsonObject).IndexerGetter([ typeof(string) ])),

            // combustibleProps.SmeltedRatio
            new CodeInstruction(OpCodes.Ldloc_1), // combustibleProps
            new CodeInstruction(OpCodes.Ldfld, typeof(CombustibleProperties).Field(nameof(CombustibleProperties.SmeltedRatio))),

            // {activeHotbarSlot.Itemstack.ItemAttributes["bloomeryFuelRatio"]}.AsInt({combustibleProps.SmeltedRatio})
            new CodeInstruction(OpCodes.Callvirt, typeof(JsonObject).Method(nameof(JsonObject.AsInt), [ typeof(int) ])),

            // {activeHotbarSlot.Itemstack.ItemAttributes["bloomeryFuelRatio"].AsInt({combustibleProps.SmeltedRatio})}
            new CodeInstruction(OpCodes.Br_S, multiplyBySixLabel),



            // PATH: When ItemAttributes is null
            new CodeInstruction(OpCodes.Pop).WithLabels(nullItemAttributesLabel),

            // combustibleProps.SmeltedRatio * 6
            new CodeInstruction(OpCodes.Ldloc_1), // combustibleProps
            new CodeInstruction(OpCodes.Ldfld, typeof(CombustibleProperties).Field(nameof(CombustibleProperties.SmeltedRatio))),



            // PATH: Always
            // Multiply the result by 6
            new CodeInstruction(OpCodes.Ldc_I4_6).WithLabels(multiplyBySixLabel),
            new CodeInstruction(OpCodes.Mul)
        );

        return matcher.Instructions();
    }
}