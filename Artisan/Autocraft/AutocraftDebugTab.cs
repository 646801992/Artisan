using Artisan.CraftingLogic;
using Artisan.CustomDeliveries;
using Artisan.IPC;
using Artisan.QuestSync;
using Artisan.RawInformation;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe static class AutocraftDebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;

        public static int DebugValue = 0;

        internal static void Draw()
        {
            ImGui.Checkbox("调试日志", ref Debug);
            if (ImGui.CollapsingHeader("所有能工巧匠食物"))
            {
                foreach (var x in ConsumableChecker.GetFood())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的工匠食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("背包内的HQ工匠食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }
            if (ImGui.CollapsingHeader("所有能工巧匠药水"))
            {
                foreach (var x in ConsumableChecker.GetPots())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的工匠药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("背包内的HQ工匠药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }
            if (ImGui.CollapsingHeader("工程学指南"))
            {
                foreach (var x in ConsumableChecker.GetManuals())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的工程学指南"))
            {
                foreach (var x in ConsumableChecker.GetManuals(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("军用工程学指南"))
            {
                foreach (var x in ConsumableChecker.GetSquadronManuals())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的军用工程学指南"))
            {
                foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }

            if (ImGui.CollapsingHeader("制作状态"))
            {
                CurrentCraft.BestSynthesis(out var act, true);
                ImGui.Text($"加工精度: {CharacterInfo.Control()}");
                ImGui.Text($"作业精度: {CharacterInfo.Craftsmanship()}");
                ImGui.Text($"当前耐久: {CurrentCraft.CurrentDurability}");
                ImGui.Text($"最大耐久: {CurrentCraft.MaxDurability}");
                ImGui.Text($"当前进展: {CurrentCraft.CurrentProgress}");
                ImGui.Text($"最大进展: {CurrentCraft.MaxProgress}");
                ImGui.Text($"当前品质: {CurrentCraft.CurrentQuality}");
                ImGui.Text($"最大品质: {CurrentCraft.MaxQuality}");
                ImGui.Text($"物品名称e: {CurrentCraft.ItemName}");
                ImGui.Text($"当前状态: {CurrentCraft.CurrentCondition}");
                ImGui.Text($"当前步骤: {CurrentCraft.CurrentStep}");
                ImGui.Text($"当前简易制作步骤: {CurrentCraft.QuickSynthCurrent}");
                ImGui.Text($"最大简易制作步骤: {CurrentCraft.QuickSynthMax}");
                ImGui.Text($"阔步+比尔格: {CurrentCraft.GreatStridesByregotCombo()}");
                ImGui.Text($"初期品质: {CurrentCraft.BaseQuality()}");
                ImGui.Text($"预期品质: {CurrentCraft.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
                ImGui.Text($"预期进展: {CurrentCraft.CalculateNewProgress(CurrentCraft.CurrentRecommendation)}");
                ImGui.Text($"当前宏步骤: {CurrentCraft.MacroStep}");
                ImGui.Text($"低收藏价值: {CurrentCraft.CollectabilityLow}");
                ImGui.Text($"中收藏价值: {CurrentCraft.CollectabilityMid}");
                ImGui.Text($"高收藏价值: {CurrentCraft.CollectabilityHigh}");
                ImGui.Text($"制作状态: {CurrentCraft.State}");
                ImGui.Text($"可以完成: {CurrentCraft.CanFinishCraft(act)}");
                ImGui.Text($"当前记录: {CurrentCraft.RecommendationName}");
                ImGui.Text($"上一步技能: {CurrentCraft.PreviousAction.NameOfAction()}");
                ImGui.Text($"任务？: {Artisan.Tasks.Count}");
            }

            if (ImGui.CollapsingHeader("魔晶石精制"))
            {
                ImGui.Text($"主手 精炼度: {Spiritbond.Weapon}");
                ImGui.Text($"副手 精炼度: {Spiritbond.Offhand}");
                ImGui.Text($"头部 精炼度: {Spiritbond.Helm}");
                ImGui.Text($"身体 精炼度: {Spiritbond.Body}");
                ImGui.Text($"手臂 精炼度: {Spiritbond.Hands}");
                ImGui.Text($"腿部 精炼度: {Spiritbond.Legs}");
                ImGui.Text($"脚部 精炼度: {Spiritbond.Feet}");
                ImGui.Text($"耳部 精炼度: {Spiritbond.Earring}");
                ImGui.Text($"颈部 精炼度: {Spiritbond.Neck}");
                ImGui.Text($"腕部 精炼度: {Spiritbond.Wrist}");
                ImGui.Text($"右指 精炼度: {Spiritbond.Ring1}");
                ImGui.Text($"左指 精炼度: {Spiritbond.Ring2}");

                ImGui.Text($"已经满精炼的装备: {Spiritbond.IsSpiritbondReadyAny()}");

            }

            if (ImGui.CollapsingHeader("任务"))
            {
                QuestManager* qm = QuestManager.Instance();
                foreach (var quest in qm->DailyQuestsSpan)
                {
                    ImGui.TextWrapped($"任务ID: {quest.QuestId}, 序列: {QuestManager.GetQuestSequence(quest.QuestId)}, 名称: {quest.QuestId.NameOfQuest()}, Flags: {quest.Flags}");
                }

            }

            if (ImGui.CollapsingHeader("IPC"))
            {
                ImGui.Text($"AutoRetainer: {AutoRetainer.IsEnabled()}");
                if (ImGui.Button("Suppress"))
                {
                    AutoRetainer.Suppress();
                }
                if (ImGui.Button("Unsuppress"))
                {
                    AutoRetainer.Unsuppress();
                }

                ImGui.Text($"Endurance IPC: {Svc.PluginInterface.GetIpcSubscriber<bool>("Artisan.GetEnduranceStatus").InvokeFunc()}");
                if (ImGui.Button("Enable"))
                {
                    Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(true);
                }
                if (ImGui.Button("Disable"))
                {
                    Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(false);
                }

                if (ImGui.Button("Send Stop Request (true)"))
                {
                    Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(true);   
                }

                if (ImGui.Button("Send Stop Request (false)"))
                {
                    Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(false);
                }

                foreach (var retainer in Service.Configuration.RetainerIDs.Where(x => x.Value == Svc.ClientState.LocalContentId))
                {
                    ImGui.Text($"ATools IPC: {RetainerInfo.ATools} {RetainerInfo.GetRetainerInventoryItem(5111, retainer.Key)}");
                }
                ImGui.Text($"ATools IPC: {RetainerInfo.ATools} {RetainerInfo.GetRetainerItemCount(5111, false)}");
            }
            ImGui.Separator();

            if (ImGui.Button("修复所有装备"))
            {
                RepairManager.ProcessRepair();
            }
            ImGuiEx.Text($"装备耐久: {RepairManager.GetMinEquippedPercent()}");

            ImGui.Text($"持续制作项目: {Handler.RecipeID} {Handler.RecipeName}");
            if (ImGui.Button($"打开持续制作项目"))
            {
                CraftingLists.CraftingListFunctions.OpenRecipeByID((uint)Handler.RecipeID);
            }

            ImGui.InputInt("调试值", ref DebugValue);

            if (ImGui.Button("接受等级（使用调试值作为 ID）"))
            {
                if (Svc.GameGui.GetAddonByName("GuildLeve") != IntPtr.Zero)
                {
                    var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("GuildLeve");
                    Callback(addon, 13, 13, DebugValue);
                }
            }

            if (ImGui.Button($"打开简易制作"))
            {
                CurrentCraft.QuickSynthItem(DebugValue);
            }
            if (ImGui.Button($"关闭简易制作窗口"))
            {
                CurrentCraft.CloseQuickSynthWindow();
            }
            if (ImGui.Button($"打开魔晶石窗口"))
            {
                Spiritbond.OpenMateriaMenu();
            }
            if (ImGui.Button($"精制第一个魔晶石"))
            {
                Spiritbond.ExtractFirstMateria();
            }


            /*ImGui.InputInt("id", ref SelRecId);
            if (ImGui.Button("OpenRecipeByRecipeId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId((uint)SelRecId);
            }
            if (ImGui.Button("OpenRecipeByItemId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByItemId((uint)SelRecId);
            }*/
            //ImGuiEx.Text($"Selected recipe id: {*(int*)(((IntPtr)AgentRecipeNote.Instance()) + 528)}");




        }
    }
}
