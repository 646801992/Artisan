using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Interface.Colors;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Artisan.FCWorkshops
{
    internal static class FCWorkshopUI
    {
        internal static uint SelectedProject = 0;
        internal static CompanyCraftSequence CurrentProject => LuminaSheets.WorkshopSequenceSheet[SelectedProject];
        private static string Search = string.Empty;
        private static int NumberOfLoops = 1;

        internal static void Draw()
        {
            if (NumberOfLoops <= 0)
            {
                NumberOfLoops = 1;
            }

            ImGui.TextWrapped($"在此标签页中，你可以浏览游戏中的所有部队工坊项目。" +
                $"它分为三个主要部分。第一部分是整个项目的总览。" +
                $"第二部分将项目按部件划分。" +
                $"最后一部分是各个部件内各阶段的详细内容。" +
                $"在每个部分中，你都可以点击按钮，" +
                $"创建一个包含该部分所需所有材料的制作清单。");


            ImGui.Separator();
            string preview = SelectedProject != 0 ? LuminaSheets.ItemSheet[LuminaSheets.WorkshopSequenceSheet[SelectedProject].ResultItem.Row].Name.RawString : "";
            if (ImGui.BeginCombo("###Workshop Project", preview))
            {
                ImGui.Text("搜索");
                ImGui.SameLine();
                ImGui.InputText("###ProjectSearch", ref Search, 100);

                if (ImGui.Selectable("", SelectedProject == 0))
                {
                    SelectedProject = 0;
                }

                foreach (var project in LuminaSheets.WorkshopSequenceSheet.Values.Where(x => x.RowId > 0).Where(x => x.ResultItem.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    bool selected = ImGui.Selectable($"{project.ResultItem.Value.Name.RawString}", project.RowId == SelectedProject);

                    if (selected)
                    {
                        SelectedProject = project.RowId;
                    }
                }

                ImGui.EndCombo();
            }

            if (SelectedProject != 0)
            {
                var project = LuminaSheets.WorkshopSequenceSheet[SelectedProject];

                if (ImGui.CollapsingHeader("项目信息"))
                {
                    if (ImGui.BeginTable($"FCWorkshopProjectContainer", 2, ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn($"###Description", ImGuiTableColumnFlags.WidthFixed);

                        ImGui.TableNextColumn();

                        ImGuiEx.Text($"选择的项目:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.ResultItem.Value.Name.RawString}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"部件数:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.CompanyCraftPart.Where(x => x.Row > 0).Count()}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"总阶段数:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.CompanyCraftPart.Where(x => x.Row > 0).SelectMany(x => x.Value.CompanyCraftProcess).Where(x => x.Row > 0).Count()}");

                        ImGui.EndTable();
                    }
                    if (ImGui.BeginTable($"###FCWorkshopProjectItemsContainer", RetainerInfo.ATools ? 4 : 3, ImGuiTableFlags.Borders))
                    {
                        ImGui.TableSetupColumn($"物品", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn($"总需", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn($"库存", ImGuiTableColumnFlags.WidthFixed);
                        if (RetainerInfo.ATools) ImGui.TableSetupColumn($"雇员", ImGuiTableColumnFlags.WidthFixed);

                        ImGui.TableHeadersRow();

                        Dictionary<uint, int> TotalItems = new Dictionary<uint, int>();
                        foreach (var item in project.CompanyCraftPart.Where(x => x.Row > 0).SelectMany(x => x.Value.CompanyCraftProcess).Where(x => x.Row > 0).SelectMany(x => x.Value.UnkData0).Where(x => x.SupplyItem > 0).GroupBy(x => x.SupplyItem))
                        {
                            if (TotalItems.ContainsKey(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row))
                                TotalItems[LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row] += item.Sum(x => x.SetQuantity * x.SetsRequired);
                            else
                                TotalItems.TryAdd(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row, item.Sum(x => x.SetQuantity * x.SetsRequired));
                        }

                        foreach (var item in TotalItems)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{LuminaSheets.ItemSheet[item.Key].Name.RawString}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{item.Value * NumberOfLoops}");
                            ImGui.TableNextColumn();
                            int invCount = CraftingListUI.NumberOfIngredient(LuminaSheets.ItemSheet[item.Key].RowId);
                            ImGui.Text($"{invCount}");
                            bool hasEnoughInInv = invCount >= item.Value * NumberOfLoops;
                            if (hasEnoughInInv)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            if (RetainerInfo.ATools)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text($"{RetainerInfo.GetRetainerItemCount(LuminaSheets.ItemSheet[item.Key].RowId)}");

                                bool hasEnoughWithRetainer = (invCount + RetainerInfo.GetRetainerItemCount(LuminaSheets.ItemSheet[item.Key].RowId) >= item.Value * NumberOfLoops);
                                if (!hasEnoughInInv && hasEnoughWithRetainer)
                                {
                                    var color = ImGuiColors.DalamudOrange;
                                    color.W -= 0.6f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                            }

                        }

                        ImGui.EndTable();
                    }

                    ImGui.InputInt("次数###LoopProject", ref NumberOfLoops);

                    if (ImGui.Button($"为此项目创建制作清单", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                    {
                        Notify.Info($"创建清单中，请稍后。");
                        Task.Run(() => CreateProjectList(project, false)).ContinueWith((_) => Notify.Success("部队工坊清单已创建。"));
                    }

                    if (ImGui.Button($"为此项目创建制作清单 (包含半成品)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                    {
                        Notify.Info($"创建清单中，请稍后。");
                        Task.Run(() => CreateProjectList(project, true)).ContinueWith((_) => Notify.Success("部队工坊清单已创建。"));
                    }
                }
                if (ImGui.CollapsingHeader("项目部件"))
                {
                    ImGui.Indent();
                    string partNum = "";
                    foreach (var part in project.CompanyCraftPart.Where(x => x.Row > 0).Select(x => x.Value))
                    {
                        partNum = part.CompanyCraftType.Value.Name.RawString;
                        if (ImGui.CollapsingHeader($"{partNum}"))
                        {
                            if (ImGui.BeginTable($"FCWorkshopPartsContainer###{part.RowId}", 2, ImGuiTableFlags.None))
                            {
                                ImGui.TableSetupColumn($"###PartType{part.RowId}", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"###Phases{part.RowId}", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableNextColumn();

                                ImGuiEx.Text($"部件类型:");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{part.CompanyCraftType.Value.Name.RawString}");
                                ImGui.TableNextColumn();
                                ImGuiEx.Text($"阶段数:");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{part.CompanyCraftProcess.Where(x => x.Row > 0).Count()}");
                                ImGui.TableNextColumn();

                                ImGui.EndTable();
                            }
                            if (ImGui.BeginTable($"###FCWorkshopPartItemsContainer{part.RowId}", RetainerInfo.ATools ? 4 : 3, ImGuiTableFlags.Borders))
                            {
                                ImGui.TableSetupColumn($"物品", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"总需", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"库存", ImGuiTableColumnFlags.WidthFixed);
                                if (RetainerInfo.ATools) ImGui.TableSetupColumn($"雇员", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableHeadersRow();

                                Dictionary<uint, int> TotalItems = new Dictionary<uint, int>();
                                foreach (var item in part.CompanyCraftProcess.Where(x => x.Row > 0).SelectMany(x => x.Value.UnkData0).Where(x => x.SupplyItem > 0).GroupBy(x => x.SupplyItem))
                                {
                                    if (TotalItems.ContainsKey(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row))
                                        TotalItems[LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row] += item.Sum(x => x.SetQuantity * x.SetsRequired);
                                    else
                                        TotalItems.TryAdd(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row, item.Sum(x => x.SetQuantity * x.SetsRequired));

                                }

                                foreach (var item in TotalItems)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{LuminaSheets.ItemSheet[item.Key].Name.RawString}");
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{item.Value * NumberOfLoops}");
                                    ImGui.TableNextColumn();
                                    int invCount = CraftingListUI.NumberOfIngredient(item.Key);
                                    ImGui.Text($"{invCount}");
                                    bool hasEnoughInInv = invCount >= item.Value * NumberOfLoops;
                                    if (hasEnoughInInv)
                                    {
                                        var color = ImGuiColors.HealerGreen;
                                        color.W -= 0.3f;
                                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                    }
                                    if (RetainerInfo.ATools)
                                    {
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{RetainerInfo.GetRetainerItemCount(item.Key)}");

                                        bool hasEnoughWithRetainer = (invCount + RetainerInfo.GetRetainerItemCount(item.Key)) >= item.Value * NumberOfLoops;
                                        if (!hasEnoughInInv && hasEnoughWithRetainer)
                                        {
                                            var color = ImGuiColors.DalamudOrange;
                                            color.W -= 0.6f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                    }
                                }

                                ImGui.EndTable();
                            }

                            ImGui.InputInt("次数###LoopPart", ref NumberOfLoops);


                            if (ImGui.Button($"为此部件创建制作清单", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                            {
                                Notify.Info($"创建清单中，请稍后。");
                                Task.Run(() => CreatePartList(part, partNum, false)).ContinueWith((_) => Notify.Success("部队工坊清单已创建。"));
                            }

                            if (ImGui.Button($"为此部件创建制作清单 (包含半成品)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                            {
                                Notify.Info($"创建清单中，请稍后。");
                                Task.Run(() => CreatePartList(part, partNum, true)).ContinueWith((_) => Notify.Success("部队工坊清单已创建。"));
                            }
                        }
                    }
                    ImGui.Unindent();
                }

                if (ImGui.CollapsingHeader("项目阶段"))
                {
                    string pNum = "";
                    foreach (var part in project.CompanyCraftPart.Where(x => x.Row > 0).Select(x => x.Value))
                    {
                        ImGui.Indent();
                        int phaseNum = 1;
                        pNum = part.CompanyCraftType.Value.Name.RawString;
                        foreach (var phase in part.CompanyCraftProcess.Where(x => x.Row > 0))
                        {
                            if (ImGui.CollapsingHeader($"{pNum} - 阶段 {phaseNum}"))
                            {
                                if (ImGui.BeginTable($"###FCWorkshopPhaseContainer{phase.Row}", RetainerInfo.ATools ? 6 : 5, ImGuiTableFlags.Borders))
                                {
                                    ImGui.TableSetupColumn($"物品", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"设置数量", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"设置所需", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"总需", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"库存", ImGuiTableColumnFlags.WidthFixed);
                                    if (RetainerInfo.ATools) ImGui.TableSetupColumn($"雇员", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableHeadersRow();

                                    foreach (var item in phase.Value.UnkData0.Where(x => x.SupplyItem > 0))
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Value.Name.RawString}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetQuantity}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetsRequired * NumberOfLoops}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetsRequired * item.SetQuantity * NumberOfLoops}");
                                        ImGui.TableNextColumn();
                                        int invCount = CraftingListUI.NumberOfIngredient(LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Row);
                                        ImGui.Text($"{invCount}");
                                        bool hasEnoughInInv = invCount >= (item.SetQuantity * item.SetsRequired);
                                        if (hasEnoughInInv)
                                        {
                                            var color = ImGuiColors.HealerGreen;
                                            color.W -= 0.3f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                        if (RetainerInfo.ATools)
                                        {
                                            ImGui.TableNextColumn();
                                            ImGui.Text($"{RetainerInfo.GetRetainerItemCount(LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Row)}");

                                            bool hasEnoughWithRetainer = (invCount + RetainerInfo.GetRetainerItemCount(LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Row)) >= (item.SetQuantity * item.SetsRequired);
                                            if (!hasEnoughInInv && hasEnoughWithRetainer)
                                            {
                                                var color = ImGuiColors.DalamudOrange;
                                                color.W -= 0.6f;
                                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                            }
                                        }

                                    }

                                    ImGui.EndTable();
                                }

                                ImGui.InputInt("次数###LoopPhase", ref NumberOfLoops);

                                if (ImGui.Button($"为此阶段创建制作清单###PhaseButton{phaseNum}", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                                {
                                    Notify.Info($"创建清单中，请稍后。");
                                    Task.Run(() => CreatePhaseList(phase.Value!, pNum, phaseNum, false)).ContinueWith((_) => Notify.Success("部队工坊清单已创建。"));
                                }

                                if (ImGui.Button($"为此阶段创建制作清单 (包含半成品)###PhaseButtonPC{phaseNum}", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                                {
                                    Notify.Info($"创建清单中，请稍后。");
                                    Task.Run(() => CreatePhaseList(phase.Value!, pNum, phaseNum, true)).ContinueWith((_) => Notify.Success("部队工坊清单已创建。"));
                                }

                            }
                            phaseNum++;
                        }
                        ImGui.Unindent();
                    }

                }
            }
        }

        private static void CreatePartList(CompanyCraftPart value, string partNum, bool includePrecraft, NewCraftingList? existingList = null)
        {
            if (existingList == null)
            {
                existingList = new NewCraftingList();
                existingList.Name = $"{CurrentProject.ResultItem.Value.Name.RawString} - Part {partNum} x{NumberOfLoops}";
                existingList.SetID();
                existingList.Save(true);
            }

            var phaseNum = 1;
            foreach (var phase in value.CompanyCraftProcess.Where(x => x.Row > 0))
            {
                CreatePhaseList(phase.Value!, partNum, phaseNum, includePrecraft, existingList);
                phaseNum++;
            }
        }

        private static void CreateProjectList(CompanyCraftSequence value, bool includePrecraft)
        {
            NewCraftingList existingList = new NewCraftingList();
            existingList.Name = $"{CurrentProject.ResultItem.Value.Name.RawString} x{NumberOfLoops}";
            existingList.SetID();
            existingList.Save(true);

            foreach (var part in value.CompanyCraftPart.Where(x => x.Row > 0))
            {
                string partNum = part.Value.CompanyCraftType.Value.Name.RawString;
                var phaseNum = 1;
                foreach (var phase in part.Value.CompanyCraftProcess.Where(x => x.Row > 0))
                {
                    CreatePhaseList(phase.Value!, partNum, phaseNum, includePrecraft, existingList);
                    phaseNum++;
                }

            }
        }

        public static void CreatePhaseList(CompanyCraftProcess value, string partNum, int phaseNum, bool includePrecraft, NewCraftingList? existingList = null, CompanyCraftSequence? projectOverride = null)
        {
            if (existingList == null)
            {
                existingList = new NewCraftingList();
                if (projectOverride != null)
                {
                    existingList.Name = $"{projectOverride.ResultItem.Value.Name.RawString} - {partNum}, 阶段 {phaseNum} x{NumberOfLoops}";
                }
                else
                {
                    existingList.Name = $"{CurrentProject.ResultItem.Value.Name.RawString} - {partNum}, 阶段 {phaseNum} x{NumberOfLoops}";
                }
                existingList.SetID();
                existingList.Save(true);
            }


                foreach (var item in value.UnkData0.Where(x => x.SupplyItem > 0))
                {
                    var timesToAdd = item.SetsRequired * item.SetQuantity * NumberOfLoops;
                    var supplyItemID = LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Value.RowId;
                    if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == supplyItemID))
                    {
                        var recipeID = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == supplyItemID);
                        if (includePrecraft)
                        {
                            Svc.Log.Debug($"我想添加 {recipeID.ItemResult.Value.Name.RawString} {timesToAdd} 次");
                            CraftingListUI.AddAllSubcrafts(recipeID, existingList, timesToAdd);
                        }

                        if (existingList.Recipes.Any(x => x.ID == recipeID.RowId))
                        {
                            var addition = timesToAdd / recipeID.AmountResult;
                            existingList.Recipes.First(x => x.ID == recipeID.RowId).Quantity += addition;
                        }
                        else
                        {
                            ListItem listItem = new ListItem()
                            {
                                ID = recipeID.RowId,
                                Quantity = timesToAdd / recipeID.AmountResult,
                                ListItemOptions = new ListItemOptions()
                            };

                            existingList.Recipes.Add(listItem);
                        }
                    }
                }
            
            P.Config.Save();

        }
    }
}
