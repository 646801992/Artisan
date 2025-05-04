using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using static Artisan.RawInformation.AddonExtensions;

namespace Artisan.CraftingLogic.Solvers;

public class ExpertSolverSettings
{
    public bool MaxIshgardRecipes;
    public bool UseReflectOpener;
    public bool MuMeIntensiveGood = true; // if true, we allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs
    public bool MuMeIntensiveMalleable = false; // if true and we have malleable during mume, use intensive rather than hoping for rapid
    public bool MuMeIntensiveLastResort = true; // if true and we're on last step of mume, use intensive (forcing via H&S if needed) rather than hoping for rapid (unless we have centered)
    public bool MuMePrimedManip = false; // if true, allow using primed manipulation after veneration is up on mume
    public bool MuMeAllowObserve = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability
    public int MuMeMinStepsForManip = 2; // if this or less rounds are remaining on mume, don't use manipulation under favourable conditions
    public int MuMeMinStepsForVene = 1; // if this or less rounds are remaining on mume, don't use veneration
    public int MidMinIQForHSPrecise = 10; // min iq stacks where we use h&s+precise; 10 to disable
    public bool MidBaitPliantWithObservePreQuality = true; // if true, when very low on durability and without manip active during pre-quality phase, we use observe rather than normal manip
    public bool MidBaitPliantWithObserveAfterIQ = true; // if true, when very low on durability and without manip active after iq has 10 stacks, we use observe rather than normal manip or inno+finnesse
    public bool MidPrimedManipPreQuality = true; // if true, allow using primed manipulation during pre-quality phase
    public bool MidPrimedManipAfterIQ = true; // if true, allow using primed manipulation during after iq has 10 stacks
    public bool MidKeepHighDuraUnbuffed = true; // if true, observe rather than use actions during unfavourable conditions to conserve durability when no buffs are active
    public bool MidKeepHighDuraVeneration = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability when veneration is active
    public bool MidAllowVenerationGoodOmen = true; // if true, we allow using veneration during iq phase if we lack a lot of progress on good omen
    public bool MidAllowVenerationAfterIQ = true; // if true, we allow using veneration after iq is fully stacked if we still lack a lot of progress
    public bool MidAllowIntensiveUnbuffed = false; // if true, we allow spending good condition on intensive if we still need progress when no buffs are active
    public bool MidAllowIntensiveVeneration = false; // if true, we allow spending good condition on intensive if we still need progress when veneration is active
    public bool MidAllowPrecise = true; // if true, we allow spending good condition on precise touch if we still need iq
    public bool MidAllowSturdyPreсise = false; // if true,we consider sturdy+h&s+precise touch a good move for building iq
    public bool MidAllowCenteredHasty = true; // if true, we consider centered hasty touch a good move for building iq (85% reliability)
    public bool MidAllowSturdyHasty = true; // if true, we consider sturdy hasty touch a good move for building iq (50% reliability), otherwise we use combo
    public bool MidAllowGoodPrep = true; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool MidAllowSturdyPrep = true; // if true, we consider prep touch a good move for finisher under sturdy+inno
    public bool MidGSBeforeInno = true; // if true, we start quality combos with gs+inno rather than just inno
    public bool MidFinishProgressBeforeQuality = true; // if true, at 10 iq we first finish progress before starting on quality
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp

    [NonSerialized]
    public IDalamudTextureWrap? expertIcon;

    public ExpertSolverSettings()
    {
        var tex = Svc.PluginInterface.UiBuilder.LoadUld("ui/uld/RecipeNoteBook.uld");
        expertIcon = tex.LoadTexturePart("ui/uld/RecipeNoteBook_hr1.tex", 14);
    }

    public bool Draw()
    {
        ImGui.TextWrapped($"专家配方求解器不是标准求解器的替代品，它仅用于专家配方。");
        if (expertIcon != null)
        {
            ImGui.TextWrapped($"此求解器仅适用于制作笔记中带有");
            ImGui.SameLine();
            ImGui.Image(expertIcon.ImGuiHandle, expertIcon.Size, new(0, 0), new(1, 1), new(0.94f, 0.57f, 0f, 1f));
            ImGui.SameLine();
            ImGui.TextWrapped($"图标的配方。");
        }
        bool changed = false;
        ImGui.Indent();
        if (ImGui.CollapsingHeader("开场设置"))
        {
            changed |= ImGui.Checkbox($"开场使用 {Skills.Reflect.NameOfAction()} 替代 {Skills.MuscleMemory.NameOfAction()} ", ref UseReflectOpener);
            changed |= ImGui.Checkbox($"如果是 {Condition.高品质.ToLocalizedString()} {ConditionString}，允许将 {Skills.MuscleMemory.NameOfAction()} 用于 {Skills.IntensiveSynthesis.NameOfAction()} (400%) 而不是 {Skills.RapidSynthesis.NameOfAction()} (500%)", ref MuMeIntensiveGood);
            changed |= ImGui.Checkbox($"如果在 {Skills.MuscleMemory.NameOfAction()} 触发 {Condition.大进展.ToLocalizedString()} {ConditionString}，使用 {Skills.HeartAndSoul.NameOfAction()} + {Skills.IntensiveSynthesis.NameOfAction()}", ref MuMeIntensiveMalleable);
            changed |= ImGui.Checkbox($"如果在 {Skills.MuscleMemory.NameOfAction()} 的最后一步且不处于 {Condition.安定.ToLocalizedString()} {ConditionString}，使用 {Skills.IntensiveSynthesis.NameOfAction()} (如有必要，通过 {Skills.HeartAndSoul.NameOfAction()} 强制使用)", ref MuMeIntensiveLastResort);
            changed |= ImGui.Checkbox($"如果 {Skills.Veneration.NameOfAction()} 已经激活，在 {Condition.长持续.ToLocalizedString()} {ConditionString} 上使用 {Skills.Manipulation.NameOfAction()}", ref MuMePrimedManip);
            changed |= ImGui.Checkbox($"在不利 {ConditionString} 下使用 {Skills.Observe.NameOfAction()}，而不是消耗 {DurabilityString} 来进行 {Skills.RapidSynthesis.NameOfAction()}", ref MuMeAllowObserve);
            ImGui.Text($"仅当 {Skills.MuscleMemory.NameOfAction()} 剩余步骤超过该数量时，允许使用 {Skills.Manipulation.NameOfAction()}");
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt("###MumeMinStepsForManip", ref MuMeMinStepsForManip, 0, 5);
            ImGui.Text($"仅当 {Skills.MuscleMemory.NameOfAction()} 剩余步骤超过该数量时，允许使用 {Skills.Veneration.NameOfAction()}");
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt("###MuMeMinStepsForVene", ref MuMeMinStepsForVene, 0, 5);
        }
        if (ImGui.CollapsingHeader("主要技能设置"))
        {
            ImGui.Text($"最少 {Buffs.InnerQuiet.NameOfBuff()} 层数以在 {Skills.PreciseTouch.NameOfAction()} 上使用 {Skills.HeartAndSoul.NameOfAction()} (10 为禁用)");
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt($"###MidMinIQForHSPrecise", ref MidMinIQForHSPrecise, 0, 10);
            changed |= ImGui.Checkbox($"低 {DurabilityString}时，在 {Buffs.InnerQuiet.NameOfBuff()} 层数达到 10 之前，优先使用 {Skills.Observe.NameOfAction()} 而不是非-{Condition.高效.ToLocalizedString()} 状态下的 {Skills.Manipulation.NameOfAction()}", ref MidBaitPliantWithObservePreQuality);
            changed |= ImGui.Checkbox($"低 {DurabilityString}时，在 {Buffs.InnerQuiet.NameOfBuff()} 层数达到 10 之后，优先使用 {Skills.Observe.NameOfAction()} 而不是非-{Condition.高效.ToLocalizedString()} 状态下的 {Skills.Manipulation.NameOfAction()} / {Skills.Innovation.NameOfAction()}+{Skills.TrainedFinesse.NameOfAction()}", ref MidBaitPliantWithObserveAfterIQ);
            changed |= ImGui.Checkbox($"在 {Buffs.InnerQuiet.NameOfBuff()} 层数达到 10 之前，在 {Condition.长持续.ToLocalizedString()} {ConditionString} 下使用 {Skills.Manipulation.NameOfAction()}", ref MidPrimedManipPreQuality);
            changed |= ImGui.Checkbox($"在 {Buffs.InnerQuiet.NameOfBuff()} 数达到 10 后，如果有足够的 制作力 来有效利用 {DurabilityString} ，则在 {Condition.长持续.ToLocalizedString()} {ConditionString} 下使用 {Skills.Manipulation.NameOfAction()}", ref MidPrimedManipAfterIQ);
            changed |= ImGui.Checkbox($"允许在没有增益的不利 {ConditionString} 下使用 {Skills.Observe.NameOfAction()}", ref MidKeepHighDuraUnbuffed);
            changed |= ImGui.Checkbox($"在 {Buffs.Veneration.NameOfBuff()} 激活状态下，允许在不利 {ConditionString} 下使用 {Skills.Observe.NameOfAction()}", ref MidKeepHighDuraVeneration);
            changed |= ImGui.Checkbox($"如果在 {Condition.好兆头.ToLocalizedString()} 状态下仍然存在较大的 {ProgressString} 缺口（超过 {Skills.IntensiveSynthesis.NameOfAction()} 可以完成的量），则允许使用 {Skills.Veneration.NameOfAction()}", ref MidAllowVenerationGoodOmen);
            changed |= ImGui.Checkbox($"如果在 {Buffs.InnerQuiet.NameOfBuff()} 层数达到 10 之后仍然存在较大的 {ProgressString} 缺口（超过 {Skills.RapidSynthesis.NameOfAction()} 可以完成的量），则允许使用 {Skills.Veneration.NameOfAction()}", ref MidAllowVenerationAfterIQ);
            changed |= ImGui.Checkbox($"如果在没有增益的情况下需要更多 {ProgressString}，则在 {Condition.高品质.ToLocalizedString()} {ConditionString} 下使用 {Skills.IntensiveSynthesis.NameOfAction()}", ref MidAllowIntensiveUnbuffed);
            changed |= ImGui.Checkbox($"如果在 {Skills.Veneration.NameOfAction()} 激活状态下需要更多 {ProgressString} ，则在 {Condition.高品质.ToLocalizedString()} {ConditionString} 下使用 {Skills.IntensiveSynthesis.NameOfAction()}", ref MidAllowIntensiveVeneration);
            changed |= ImGui.Checkbox($"如果需要更多 {Buffs.InnerQuiet.NameOfBuff()} 层数，则在 {Condition.高品质.ToLocalizedString()} {ConditionString} 下使用 {Skills.PreciseTouch.NameOfAction()}", ref MidAllowPrecise);
            changed |= ImGui.Checkbox($"将 {Condition.结实.ToLocalizedString()} {ConditionString} 下的 {Skills.HeartAndSoul.NameOfAction()} + {Skills.PreciseTouch.NameOfAction()} 视为积累 {Buffs.InnerQuiet.NameOfBuff()} 层数的良好选择", ref MidAllowSturdyPreсise);
            changed |= ImGui.Checkbox($"将 {Condition.安定.ToLocalizedString()} {ConditionString} 下的 {Skills.HastyTouch.NameOfAction()} 视为积累 {Buffs.InnerQuiet.NameOfBuff()} 层数的良好选择 (85% 成功率，消耗 10 {DurabilityString})", ref MidAllowCenteredHasty);
            changed |= ImGui.Checkbox($"将 {Condition.结实.ToLocalizedString()} {ConditionString} 下的 {Skills.HastyTouch.NameOfAction()} 视为积累 {Buffs.InnerQuiet.NameOfBuff()} 层数的良好选择 (50% 成功率，消耗 5 {DurabilityString})", ref MidAllowSturdyHasty);
            changed |= ImGui.Checkbox($"在 {Condition.高品质.ToLocalizedString()} {ConditionString} 下的 {Buffs.Innovation.NameOfBuff()} + {Buffs.GreatStrides.NameOfBuff()} 的组合中，将 {Skills.PreparatoryTouch.NameOfAction()} 视为一个良好的选择，前提是有足够的 {DurabilityString}", ref MidAllowGoodPrep);
            changed |= ImGui.Checkbox($"在 {Condition.结实.ToLocalizedString()} {ConditionString} + {Buffs.Innovation.NameOfBuff()} 的组合中，将 {Skills.PreparatoryTouch.NameOfAction()} 视为一个良好的选择，前提是有足够的 {DurabilityString}", ref MidAllowSturdyPrep);
            changed |= ImGui.Checkbox($"在 {Skills.Innovation.NameOfAction()} + {QualityString} 组合之前使用 {Skills.GreatStrides.NameOfAction()}", ref MidGSBeforeInno);
            changed |= ImGui.Checkbox($"在开始 {QualityString} 阶段之前，先完成 {ProgressString} 阶段", ref MidFinishProgressBeforeQuality);
            changed |= ImGui.Checkbox($"如果本来会在 {Condition.高品质.ToLocalizedString()} {ConditionString} 下使用 {Skills.TricksOfTrade.NameOfAction()}，则在 {Condition.好兆头.ToLocalizedString()} {ConditionString} 下使用 {Skills.Observe.NameOfAction()}", ref MidObserveGoodOmenForTricks);
        }
        ImGui.Unindent();
        changed |= ImGui.Checkbox("最大化重建伊修加德配方的品质，而不仅仅是达到最大收藏品价值阈值", ref MaxIshgardRecipes);
        ImGuiComponents.HelpMarker("这将尝试最大化品质，以赚取更多技巧点");
        changed |= ImGui.Checkbox($"收尾: 使用 {Skills.CarefulObservation.NameOfAction()} 尝试触发 {Condition.高品质.ToLocalizedString()} {ConditionString}，以便使用 {Skills.ByregotsBlessing.NameOfAction()}", ref FinisherBaitGoodByregot);
        changed |= ImGui.Checkbox($"紧急情况: 如果 制作力 极低，使用 {Skills.CarefulObservation.NameOfAction()} 尝试触发 {Condition.高品质.ToLocalizedString()} {ConditionString} ，以便使用 {Skills.TricksOfTrade.NameOfAction()}", ref EmergencyCPBaitGood);
        if (ImGuiEx.ButtonCtrl("将专家求解器设置重置为默认值"))
        {
            P.Config.ExpertSolverConfig = new();
            changed |= true;
        }
        return changed;
    }
}
