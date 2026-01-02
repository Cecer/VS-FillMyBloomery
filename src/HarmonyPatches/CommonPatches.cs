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
[HarmonyPatchCategory("common")]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public static class CommonPatches
{
    const string InteractionLangKey = "fillmybloomery:blockhelp-bloomery-addmany";

    /// <remarks>
    /// Before:
    ///   <code>
    ///     int quantity = byPlayer.Entity.Controls.CtrlKey ? 5 : 1;
    ///   </code>
    ///
    /// After:
    ///   <code>
    ///     int quantity = byPlayer.Entity.Controls.CtrlKey ? (byPlayer.Entity.Controls.ShiftKey ? MaxAddSize : 5) : 1;
    ///   </code>
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BlockBloomery), nameof(BlockBloomery.OnBlockInteractStart))]
    private static List<CodeInstruction> AddMaxAddLogic(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
            IL_0109: ldarg.2      // byPlayer
            IL_010a: ldarg.2      // byPlayer
            IL_010b: callvirt     instance class [VintagestoryAPI]Vintagestory.API.Common.EntityPlayer [VintagestoryAPI]Vintagestory.API.Common.IPlayer::get_Entity()
            IL_0110: callvirt     instance class [VintagestoryAPI]Vintagestory.API.Common.EntityControls [VintagestoryAPI]Vintagestory.API.Common.EntityAgent::get_Controls()
            IL_0115: callvirt     instance bool [VintagestoryAPI]Vintagestory.API.Common.EntityControls::get_CtrlKey()
            IL_011a: brtrue.s     IL_011f
            IL_011c: ldc.i4.1
            IL_011d: br.s         IL_0120

            // [162 7 - 162 87]
            IL_011f: ldc.i4.5
            IL_0120: callvirt     instance bool Vintagestory.GameContent.BlockEntityBloomery::TryAdd(class [VintagestoryAPI]Vintagestory.API.Common.IPlayer, int32)
         */

        var matcher = new CodeMatcher(instructions, generator);

        matcher.Start();
        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_2),
            new CodeMatch(OpCodes.Ldarg_2),
            new CodeMatch(OpCodes.Callvirt, typeof(IPlayer).PropertyGetter(nameof(IPlayer.Entity))),
            new CodeMatch(OpCodes.Callvirt, typeof(EntityAgent).PropertyGetter(nameof(EntityAgent.Controls))),
            new CodeMatch(OpCodes.Callvirt, typeof(EntityControls).PropertyGetter(nameof(EntityControls.CtrlKey))),
            new CodeMatch(OpCodes.Brtrue_S),
            new CodeMatch(OpCodes.Ldc_I4_1),
            new CodeMatch(OpCodes.Br_S),
            new CodeMatch(OpCodes.Ldc_I4_5),
            new CodeMatch(OpCodes.Callvirt, typeof(BlockEntityBloomery).Method(nameof(BlockEntityBloomery.TryAdd), [ typeof(IPlayer), typeof(int) ]))
        );
        matcher.ThrowIfNotMatch("Failed to match expected opcodes in BlockBloomery.OnBlockInteractStart");
        matcher.Advance(8); // Advance to IL_011f (Reached only when CtrlKey is true. Just before pushing the 5 on to the stack)

        var labelTryAdd = matcher.InstructionAt(1).labels[0]; // IL_0120 (Always reached - Just after pushing the amount to the stack.)
        matcher.DefineLabel(out var labelAddMax);

        matcher.InsertAfter(
            new CodeInstruction(OpCodes.Ldarg_2) // byPlayer
                .MoveLabelsFrom(matcher.Instruction), // Move the label from the Ldc_I4_5 instruction. This is done so that the CtrlKey check branches to our ShiftKey check instead of skipping over it.
            new CodeInstruction(OpCodes.Callvirt, typeof(IPlayer).PropertyGetter(nameof(IPlayer.Entity))),
            new CodeInstruction(OpCodes.Callvirt, typeof(EntityAgent).PropertyGetter(nameof(EntityAgent.Controls))),
            new CodeInstruction(OpCodes.Callvirt, typeof(EntityControls).PropertyGetter(nameof(EntityControls.ShiftKey))),
            new CodeInstruction(OpCodes.Brtrue_S, labelAddMax), // If ShiftKey is true, branch to labelAddMax.
            new CodeInstruction(OpCodes.Ldc_I4_5), // Push 5 on to the stack (Only reached if ShfitKey is false).
            new CodeInstruction(OpCodes.Br_S, labelTryAdd),
            new CodeInstruction(OpCodes.Ldc_I4, int.MaxValue).WithLabels(labelAddMax) // Push int.MaxValue on to the stack
        );
        // The next instruction after our inserted instructions is the call to TryAdd.

        matcher.RemoveInstruction(); // Remove the original Ldc_I4_5 instruction as we want to replace with the inserts above.
        return matcher.Instructions();
    }

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