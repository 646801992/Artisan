using Artisan.Autocraft;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static Recipe? SelectedRecipe = null;
        internal static string Search = "";
        public unsafe static InventoryManager* invManager = InventoryManager.Instance();
        public static Dictionary<Recipe, bool> CraftableItems = new();
        internal static Dictionary<int, int> SelectedRecipeRawIngredients = new();
        internal static Dictionary<int, int> SelectedListMateralsNew = new();
        internal static Dictionary<int, bool> SelectedRecipesCraftable = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = String.Empty;
        internal static CraftingList selectedList = new();
        public static Dictionary<uint, Recipe> FilteredList = LuminaSheets.RecipeSheet.Values
                    .DistinctBy(x => x.RowId)
                    .OrderBy(x => x.RecipeLevelTable.Value.ClassJobLevel)
                    .ThenBy(x => x.ItemResult.Value.Name.RawString)
                    .ToDictionary(x => x.RowId, x => x);

        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static Dictionary<int, int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static Dictionary<int, int> listMaterialsNew = new();
        internal static uint selectedListItem;
        public static bool Processing = false;
        public static uint CurrentProcessedItem;
        private static bool renameMode = false;
        private static string? renameList;
        public static bool Minimized = false;
        private static int timesToAdd = 1;
        private static bool GatherBuddy => DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);

        private unsafe static void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);
        internal static void Draw()
        {
            string hoverText = "!! 悬停以获取信息 !!";
            var hoverLength = ImGui.CalcTextSize(hoverText);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (hoverLength.Length() / 2));
            ImGui.TextColored(ImGuiColors.DalamudYellow, hoverText);
            if (ImGui.IsItemHovered())
            {
                ImGui.TextWrapped($"您可以使用此选项卡查看您可以使用背包中的材料来制作哪些物品。您还可以使用它来创建Artisan将尝试完成的快速制作清单。");
                ImGui.TextWrapped($"请注意，由于繁重的计算需求，Artisan过滤了配方列表以仅显示您拥有其材料的配方，不会考虑任何制品的原材料。这可能会在未来得到解决。目前，它*仅*可查看给定配方的最终成分。");
                ImGui.TextWrapped($"制作清单将从上到下处理，因此请确保高优先级的制作品排在第一位。");
                ImGui.TextWrapped("请确保为每个需要用来制作物品的职业保存套装。此外，如果您需要使用食物和/或药水，请在开始制作清单之前这样做，这不会自动进行。");
            }
            ImGui.Separator();

            if (Minimized)
            {
                if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowRight, "最大化", new Vector2(80f, 0)))
                {
                    Minimized = false;
                }
                ImGui.Spacing();
            }

            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            if (Handler.Enable)
            {
                Processing = false;
                ImGui.Text("持续模式已启动...");
                return;
            }
            if (Processing)
            {
                ImGui.Text("当前处理制作清单...");
                return;
            }

            if (!Minimized)
            {
                if (ImGui.Button("新建清单"))
                {
                    keyboardFocus = true;
                    ImGui.OpenPopup("新的制作清单");
                }

                DrawNewListPopup();

                if (Service.Configuration.CraftingLists.Count > 0)
                {
                    float longestName = 0;
                    foreach (var list in Service.Configuration.CraftingLists)
                    {
                        if (ImGui.CalcTextSize($"{list.Name}").Length() > longestName)
                            longestName = ImGui.CalcTextSize($"{list.Name}").Length();
                    }

                    longestName = Math.Max(150, longestName);
                    if (ImGui.BeginChild("###craftListSelector", new Vector2(longestName + 40, 0), true))
                    {
                        if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowLeft, "最小化按钮", new Vector2(longestName + 20, 0)))
                        {
                            Minimized = true;
                        }

                        ImGui.Separator();
                        if (ImGui.BeginChild("###craftingLists", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 90)))
                        {
                            foreach (CraftingList l in Service.Configuration.CraftingLists)
                            {
                                var selected = ImGui.Selectable($"{l.Name}###list{l.ID}", l.ID == selectedList.ID);

                                if (selected)
                                {
                                    selectedList = l;
                                    SelectedListMateralsNew.Clear();
                                    listMaterialsNew.Clear();
                                    selectedListItem = 0;
                                }
                            }
                        }
                        ImGui.EndChild();
                        Teamcraft.DrawTeamCraftListButtons();
                    }
                    ImGui.EndChild();
                }
                else
                {
                    Teamcraft.DrawTeamCraftListButtons();
                }


            }

            if (selectedList.ID != 0)
            {
                if (!Minimized)
                    ImGui.SameLine();
                if (ImGui.BeginChild("###selectedList", new Vector2(0, ImGui.GetContentRegionAvail().Y), false))
                {
                    if (!renameMode)
                    {
                        ImGui.Text($"选择清单: {selectedList.Name}");
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                        {
                            renameMode = true;
                        }
                    }
                    else
                    {
                        renameList = selectedList.Name;
                        if (ImGui.InputText("", ref renameList, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            selectedList.Name = renameList;
                            Service.Configuration.Save();

                            renameMode = false;
                            renameList = String.Empty;
                        }
                    }
                    if (ImGui.Button("删除清单（按住Ctrl）") && ImGui.GetIO().KeyCtrl)
                    {
                        Service.Configuration.CraftingLists.Remove(selectedList);

                        Service.Configuration.Save();
                        selectedList = new();

                        SelectedListMateralsNew.Clear();
                        listMaterialsNew.Clear();
                    }

                    bool skipIfEnough = selectedList.SkipIfEnough;
                    if (ImGui.Checkbox("跳过您已有足够数量的物品", ref skipIfEnough))
                    {
                        selectedList.SkipIfEnough = skipIfEnough;
                        Service.Configuration.Save();
                    }

                    bool materia = selectedList.Materia;
                    if (ImGui.Checkbox("自动精制魔晶石", ref materia))
                    {
                        selectedList.Materia = materia;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("当身上的任意装备的精炼值达到100%之后将自动进行魔晶石精制。");

                    bool repair = selectedList.Repair;
                    if (ImGui.Checkbox("自动修理", ref repair))
                    {
                        selectedList.Repair = repair;
                        Service.Configuration.Save();
                    }

                    ImGuiComponents.HelpMarker("如果启用，Artisan将在任意装备达到设定的修复阈值时自动使用暗物质修理您的装备。");
                    if (selectedList.Repair)
                    {
                        ImGui.PushItemWidth(200);
                        if (ImGui.SliderInt("##repairp", ref selectedList.RepairPercent, 10, 100, $"%d%%"))
                        {
                            Service.Configuration.Save();
                        }
                    }
                    if (ImGui.Checkbox("将添加到清单的新物品设置为简易制作", ref selectedList.AddAsQuickSynth))
                    {
                        Service.Configuration.Save();
                    }

                    if (selectedList.Items.Count > 0)
                    {
                        if (ImGui.CollapsingHeader("清单物品"))
                        {

                            ImGui.Columns(2, null, false);
                            ImGui.Text("当前物品");
                            ImGui.Indent();
                            var loop = 1;
                            foreach (var item in CollectionsMarshal.AsSpan(selectedList.Items.Distinct().ToList()))
                            {
                                var selected = ImGui.Selectable($"{loop}. {FilteredList[item].ItemResult.Value.Name.RawString} x{selectedList.Items.Count(x => x == item)} {(FilteredList[item].AmountResult > 1 ? $"(共 {FilteredList[item].AmountResult * selectedList.Items.Count(x => x == item)} 个)" : $"")}", selectedListItem == item);

                                if (selected)
                                {
                                    selectedListItem = item;
                                }

                                loop++;
                            }
                            ImGui.Unindent();
                            ImGui.NextColumn();
                            if (selectedListItem != 0)
                            {
                                var recipe = FilteredList[selectedListItem];
                                ImGui.Text("选项");
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                                {
                                    selectedList.Items.RemoveAll(x => x == selectedListItem);
                                    selectedListItem = 0;
                                    Service.Configuration.Save();

                                    SelectedListMateralsNew.Clear();
                                    listMaterialsNew.Clear();
                                }
                                ImGui.SameLine();
                                var count = selectedList.Items.Count(x => x == selectedListItem);

                                ImGui.PushItemWidth(150);
                                if (ImGui.InputInt("调整数量", ref count))
                                {
                                    if (count > 0)
                                    {
                                        var oldCount = selectedList.Items.Count(x => x == selectedListItem);
                                        if (oldCount < count)
                                        {
                                            var diff = count - oldCount;
                                            for (int i = 1; i <= diff; i++)
                                            {
                                                selectedList.Items.Insert(selectedList.Items.IndexOf(selectedListItem), selectedListItem);
                                            }
                                            Service.Configuration.Save();

                                            SelectedListMateralsNew.Clear();
                                            listMaterialsNew.Clear();
                                        }
                                        if (count < oldCount)
                                        {
                                            var diff = oldCount - count;
                                            for (int i = 1; i <= diff; i++)
                                            {
                                                selectedList.Items.Remove(selectedListItem);
                                            }
                                            Service.Configuration.Save();

                                            SelectedListMateralsNew.Clear();
                                            listMaterialsNew.Clear();
                                        }
                                    }
                                }


                                if (!selectedList.ListItemOptions.ContainsKey(selectedListItem))
                                {
                                    selectedList.ListItemOptions.TryAdd(selectedListItem, new ListItemOptions());
                                    if (selectedList.AddAsQuickSynth && recipe.CanQuickSynth)
                                        selectedList.ListItemOptions[selectedListItem].NQOnly = true;
                                }
                                selectedList.ListItemOptions.TryGetValue(selectedListItem, out var options);

                                if (recipe.CanQuickSynth)
                                {
                                    bool NQOnly = options.NQOnly;
                                    if (ImGui.Checkbox("简易制作该物品", ref NQOnly))
                                    {
                                        options.NQOnly = NQOnly;
                                        Service.Configuration.Save();
                                    }
                                }
                                else
                                {
                                    ImGui.TextWrapped("该物品无法被简易制作。");
                                }

                                string? preview = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var prevMacro) && prevMacro != null ? Service.Configuration.IndividualMacros[(uint)selectedListItem].Name : "";
                                if (prevMacro is not null && !Service.Configuration.UserMacros.Where(x => x.ID == prevMacro.ID).Any())
                                {
                                    preview = "";
                                    Service.Configuration.IndividualMacros[(uint)selectedListItem] = null;
                                    Service.Configuration.Save();
                                }

                                if (true)
                                {
                                    ImGui.Spacing();
                                    ImGui.TextWrapped($"在这个配方中使用食物");
                                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == options.Food, out var item) ? $"{(options.FoodHQ ? " " : "")}{item.Name}" : $"{(options.Food == 0 ? "禁用" : $"{(options.FoodHQ ? " " : "")}{options.Food}")}"))
                                    {
                                        if (ImGui.Selectable("禁用"))
                                        {
                                            options.Food = 0;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var x in ConsumableChecker.GetFood(true))
                                        {
                                            if (ImGui.Selectable($"{x.Name}"))
                                            {
                                                options.Food = x.Id;
                                                options.FoodHQ = false;
                                                Service.Configuration.Save();
                                            }
                                        }
                                        foreach (var x in ConsumableChecker.GetFood(true, true))
                                        {
                                            if (ImGui.Selectable($" {x.Name}"))
                                            {
                                                options.Food = x.Id;
                                                options.FoodHQ = true;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }

                                if (true)
                                {
                                    ImGui.Spacing();
                                    ImGuiEx.SetNextItemFullWidth();
                                    ImGui.TextWrapped($"在这个配方中使用药水");
                                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == options.Potion, out var item) ? $"{(options.PotHQ ? " " : "")}{item.Name}" : $"{(options.Potion == 0 ? "禁用" : $"{(options.PotHQ ? " " : "")}{options.Potion}")}"))
                                    {
                                        if (ImGui.Selectable("禁用"))
                                        {
                                            options.Potion = 0;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var x in ConsumableChecker.GetPots(true))
                                        {
                                            if (ImGui.Selectable($"{x.Name}"))
                                            {
                                                options.Potion = x.Id;
                                                options.PotHQ = false;
                                                Service.Configuration.Save();
                                            }
                                        }
                                        foreach (var x in ConsumableChecker.GetPots(true, true))
                                        {
                                            if (ImGui.Selectable($" {x.Name}"))
                                            {
                                                options.Potion = x.Id;
                                                options.PotHQ = true;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }


                                if (Service.Configuration.UserMacros.Count > 0)
                                {
                                    ImGui.Spacing();
                                    ImGui.TextWrapped($"用宏制作此配方（仅当启用宏模式时）");
                                    if (ImGui.BeginCombo("", preview))
                                    {
                                        if (ImGui.Selectable(""))
                                        {
                                            Service.Configuration.IndividualMacros[selectedListItem] = null;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var macro in Service.Configuration.UserMacros)
                                        {
                                            bool selected = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var selectedMacro) && selectedMacro != null;
                                            if (ImGui.Selectable(macro.Name, selected))
                                            {
                                                Service.Configuration.IndividualMacros[(uint)selectedListItem] = macro;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }
                                ImGui.Spacing();


                                if (selectedList.Items.Distinct().Count() > 1)
                                {
                                    ImGui.Text("清单排序");
                                    ImGui.SameLine();

                                    bool isFirstItem = selectedList.Items.IndexOf(selectedListItem) == 0;
                                    bool isLastItem = selectedList.Items.LastIndexOf(selectedListItem) == selectedList.Items.Count - 1;

                                    if (!isFirstItem)
                                    {
                                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                                        {
                                            var loops = selectedList.Items.Count(x => x == selectedListItem);
                                            var previousNum = selectedList.Items[selectedList.Items.IndexOf(selectedListItem) - 1];
                                            var insertionIndex = selectedList.Items.IndexOf(previousNum);

                                            selectedList.Items.RemoveAll(x => x == selectedListItem);
                                            for (int i = 1; i <= loops; i++)
                                            {
                                                selectedList.Items.Insert(insertionIndex, selectedListItem);
                                            }

                                        }
                                        if (!isLastItem) ImGui.SameLine();
                                    }

                                    if (!isLastItem)
                                    {
                                        if (isFirstItem)
                                        {
                                            ImGui.Dummy(new Vector2(22));
                                            ImGui.SameLine();
                                        }

                                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                                        {
                                            var nextNum = selectedList.Items[selectedList.Items.LastIndexOf(selectedListItem) + 1];
                                            var loops = selectedList.Items.Count(x => x == nextNum);
                                            var insertionIndex = selectedList.Items.IndexOf(selectedListItem);

                                            selectedList.Items.RemoveAll(x => x == nextNum);
                                            for (int i = 1; i <= loops; i++)
                                            {
                                                selectedList.Items.Insert(insertionIndex, nextNum);
                                            }

                                        }
                                    }
                                }

                            }
                        }
                        ImGui.Columns(1, null, false);
                        if (ImGui.CollapsingHeader("总材料"))
                        {
                            if (GatherBuddy)
                            {
                                ImGui.TextWrapped($"单击物品名称以复制到剪贴板。\n按住Shift单击物品名称以对该物品执行GatherBuddy采集指令。\n按住Ctrl单击物品名称以对该物品执行道具检索指令。");
                            }
                            else
                            {
                                ImGui.TextWrapped($"单击物品名称以复制到剪贴板。\n按住Ctrl单击物品名称以对该物品执行道具检索指令。\n安装GatherBuddy（主库）以获取更多功能。");
                            }

                            ImGui.Spacing();
                            ImGui.Separator();
                            DrawTotalIngredientsTable();
                        }
                        if (ImGui.Button("开始制作清单", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            CraftingListFunctions.CurrentIndex = 0;
                            if (CraftingListFunctions.RecipeWindowOpen())
                                CraftingListFunctions.CloseCraftingMenu();

                            Processing = true;
                            Handler.Enable = false;
                        }
                        if (RetainerInfo.ATools)
                        {
                            if (RetainerInfo.TM.IsBusy)
                            {
                                if (ImGui.Button("阻止从雇员获取材料", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                                {
                                    RetainerInfo.TM.Abort();
                                }
                            }
                            else
                            {
                                if (ImGui.Button("从雇员那里补充材料", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                                {
                                    RetainerInfo.RestockFromRetainers(selectedList);
                                }
                            }
                        }

                    }
                    ImGui.Spacing();
                    ImGui.Separator();

                    DrawRecipeData();


                }

                ImGui.EndChild();
            }


        }



        private async static void DrawTotalIngredientsTable()
        {
            int colCount = RetainerInfo.ATools ? 4 : 3;
            try
            {
                if (ImGui.BeginTable("###ListMaterialTableRaw", colCount, ImGuiTableFlags.Borders))
                {
                    ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("背包", ImGuiTableColumnFlags.WidthFixed);
                    if (RetainerInfo.ATools)
                        ImGui.TableSetupColumn("雇员", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();

                    if (SelectedListMateralsNew.Count == 0)
                    {
                        foreach (var item in selectedList.Items.Distinct())
                        {
                            Recipe r = FilteredList[item];
                            AddRecipeIngredientsToList(r, ref SelectedListMateralsNew, false, selectedList);
                        }
                    }

                    if (listMaterialsNew.Count == 0)
                        listMaterialsNew = SelectedListMateralsNew;

                    try
                    {
                        foreach (var item in listMaterialsNew.OrderByDescending(x => x.Key))
                        {
                            if (LuminaSheets.ItemSheet.TryGetValue((uint)item.Key, out var sheetItem))
                            {
                                if (SelectedRecipesCraftable[item.Key]) continue;
                                ImGui.PushID(item.Key);
                                var name = sheetItem.Name.RawString;
                                var count = item.Value;
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{name}");
                                if (GatherBuddy && ImGui.IsItemClicked() && ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl)
                                {
                                    Chat.Instance.SendMessage($"/gather {name}");
                                }
                                if (ImGui.IsItemClicked() && ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift)
                                {
                                    SearchItem((uint)item.Key);
                                }
                                if (ImGui.IsItemClicked() && !ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl)
                                {
                                    ImGui.SetClipboardText(name);
                                    Notify.Success("名称已复制到剪贴板。");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{count}");
                                ImGui.TableNextColumn();
                                var invCount = NumberOfIngredient((uint)item.Key);
                                if (invCount >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                                ImGui.Text($"{invCount}");
                                if (RetainerInfo.ATools)
                                {
                                    ImGui.TableNextColumn();

                                    if (RetainerInfo.CacheBuilt)
                                    {
                                        uint retainerCount = RetainerInfo.GetRetainerItemCount(sheetItem.RowId);
                                        ImGui.Text($"{(retainerCount)}");

                                        if (invCount >= count)
                                        {
                                            var color = ImGuiColors.HealerGreen;
                                            color.W -= 0.3f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                        else if (retainerCount >= count)
                                        {
                                            var color = ImGuiColors.DalamudOrange;
                                            color.W -= 0.6f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Text($"构建缓存中，请稍等。");
                                    }
                                }

                                ImGui.PopID();
                            }
                        }
                    }
                    catch
                    {

                    }

                    ImGui.EndTable();
                }
            }
            catch(Exception ex)
            {
                PluginLog.Debug(ex, "总材料表");
            }


            if (ImGui.BeginTable("###ListMaterialTableSub", colCount, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("背包", ImGuiTableColumnFlags.WidthFixed);
                if (RetainerInfo.ATools)
                    ImGui.TableSetupColumn("雇员", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (SelectedListMateralsNew.Count == 0)
                {
                    foreach (var item in selectedList.Items)
                    {
                        Recipe r = FilteredList[item];
                        AddRecipeIngredientsToList(r, ref SelectedListMateralsNew, false, selectedList);
                    }
                }

                if (listMaterialsNew.Count == 0)
                    listMaterialsNew = SelectedListMateralsNew;

                try
                {
                    foreach (var item in listMaterialsNew)
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item.Key, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item.Key])
                            {
                                ImGui.PushID(item.Key);
                                var name = sheetItem.Name.RawString;
                                var count = item.Value;
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{name}");
                                if (ImGui.IsItemClicked() && ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift)
                                {
                                    SearchItem((uint)item.Key);
                                }
                                if (ImGui.IsItemClicked() && !ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl)
                                {
                                    ImGui.SetClipboardText(name);
                                    Notify.Success("名称已复制到剪贴板。");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{count}");
                                ImGui.TableNextColumn();
                                var invCount = NumberOfIngredient((uint)item.Key);
                                if (invCount >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                                ImGui.Text($"{invCount}");
                                if (RetainerInfo.ATools)
                                {
                                    ImGui.TableNextColumn();
                                    if (RetainerInfo.CacheBuilt)
                                    {
                                        uint retainerCount = RetainerInfo.GetRetainerItemCount(sheetItem.RowId);
                                        ImGui.Text($"{(retainerCount)}");

                                        if (invCount >= count)
                                        {
                                            var color = ImGuiColors.HealerGreen;
                                            color.W -= 0.3f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                        else if (retainerCount >= count)
                                        {
                                            var color = ImGuiColors.DalamudOrange;
                                            color.W -= 0.6f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Text($"构建缓存中，请稍等。");
                                    }

                                }
                                ImGui.PopID();
                            }
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }
        }

        private static void DrawNewListPopup()
        {
            if (ImGui.BeginPopup("新的制作清单"))
            {
                if (keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    keyboardFocus = false;
                }

                if (ImGui.InputText("清单名称###listName", ref newListName, 100, ImGuiInputTextFlags.EnterReturnsTrue) && newListName.Any())
                {
                    CraftingList newList = new();
                    newList.Name = newListName;
                    newList.SetID();
                    newList.Save(true);

                    newListName = String.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private async static void DrawRecipeData()
        {
            bool showOnlyCraftable = Service.Configuration.ShowOnlyCraftable;

            if (ImGui.Checkbox("###ShowcraftableCheckbox", ref showOnlyCraftable))
            {
                Service.Configuration.ShowOnlyCraftable = showOnlyCraftable;
                Service.Configuration.Save();

                if (showOnlyCraftable)
                {
                    RetainerInfo.TM.Abort();
                    RetainerInfo.TM.Enqueue(() => RetainerInfo.LoadCache());
                }
            }
            ImGui.SameLine();
            ImGui.TextWrapped($"只显示你有其材料的配方（切换刷新）");

            if (Service.Configuration.ShowOnlyCraftable && RetainerInfo.ATools)
            {
                bool showOnlyCraftableRetainers = Service.Configuration.ShowOnlyCraftableRetainers;
                if (ImGui.Checkbox($"###ShowCraftableRetainersCheckbox", ref showOnlyCraftableRetainers))
                {
                    Service.Configuration.ShowOnlyCraftableRetainers = showOnlyCraftableRetainers;
                    Service.Configuration.Save();

                    CraftableItems.Clear();
                    RetainerInfo.TM.Abort();
                    RetainerInfo.TM.Enqueue(() => RetainerInfo.LoadCache());
                }

                ImGui.SameLine();
                ImGui.TextWrapped("包含雇员");
            }

            string preview = SelectedRecipe is null ? "" : $"{SelectedRecipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation.RawString})";
            if (ImGui.BeginCombo("选择配方", preview))
            {
                DrawRecipes();

                ImGui.EndCombo();
            }

            if (SelectedRecipe != null)
            {
                if (ImGui.CollapsingHeader("配方信息"))
                {
                    DrawRecipeOptions();
                }
                if (SelectedRecipeRawIngredients.Count == 0)
                    AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

                if (ImGui.CollapsingHeader("原材料"))
                {
                    ImGui.Text($"所需原材料");
                    DrawRecipeSubTable();

                }

                ImGui.Spacing();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().Length() / 2f);
                ImGui.TextWrapped("添加次数");
                ImGui.SameLine();
                ImGui.InputInt("###TimesToAdd", ref timesToAdd, 1, 5);
                ImGui.PushItemWidth(-1f);

                if (ImGui.Button("添加到清单", new Vector2(ImGui.GetContentRegionAvail().X / 2, 30)))
                {
                    SelectedListMateralsNew.Clear();
                    listMaterialsNew.Clear();

                    for (int i = 0; i < timesToAdd; i++)
                    {
                        if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(SelectedRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                        }
                    }
                    Service.Configuration.Save();
                    if (Service.Configuration.ResetTimesToAdd)
                        timesToAdd = 1;
                }
                ImGui.SameLine();
                if (ImGui.Button("添加到清单（包括所有子制作）", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    SelectedListMateralsNew.Clear();
                    listMaterialsNew.Clear();

                    AddAllSubcrafts(SelectedRecipe, selectedList, 1, timesToAdd);

                    PluginLog.Debug($"添加: {SelectedRecipe.ItemResult.Value.Name.RawString} {timesToAdd} 次");
                    for (int i = 1; i <= timesToAdd; i++)
                    {
                        if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(SelectedRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                        }
                    }

                    Service.Configuration.Save();
                    if (Service.Configuration.ResetTimesToAdd)
                        timesToAdd = 1;
                }
            }
        }

        public static void AddAllSubcrafts(Recipe selectedRecipe, CraftingList selectedList, int amounts = 1, int loops = 1)
        {
            PluginLog.Debug($"加工: {selectedRecipe.ItemResult.Value.Name.RawString}");
            foreach (var subItem in selectedRecipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                PluginLog.Debug($"子项目: {LuminaSheets.ItemSheet[(uint)subItem.ItemIngredient].Name.RawString} * {subItem.AmountIngredient}");
                var subRecipe = GetIngredientRecipe(subItem.ItemIngredient);
                if (subRecipe != null)
                {
                    AddAllSubcrafts(subRecipe, selectedList, subItem.AmountIngredient * amounts, loops);

                    PluginLog.Debug($"添加: {subRecipe.ItemResult.Value.Name.RawString} {Math.Ceiling((double)subItem.AmountIngredient / (double)subRecipe.AmountResult * (double)loops * amounts)} 次");

                    for (int i = 1; i <= Math.Ceiling((double)subItem.AmountIngredient / (double)subRecipe.AmountResult * (double)loops * amounts); i++)
                    {
                        if (selectedList.Items.IndexOf(subRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(subRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(subRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, subRecipe.RowId);
                        }
                    }

                    PluginLog.Debug($"现在有 {selectedList.Items.Count} 个物品在清单。");
                }
            }
        }

        private static void DrawRecipeSubTable()
        {
            int colCount = RetainerInfo.ATools ? 4 : 3;
            if (ImGui.BeginTable("###SubTableRecipeData", colCount, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("背包", ImGuiTableColumnFlags.WidthFixed);
                if (RetainerInfo.ATools)
                    ImGui.TableSetupColumn("雇员", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (subtableList.Count == 0)
                    subtableList = SelectedRecipeRawIngredients;

                try
                {
                    foreach (var item in subtableList)
                    {
                        if (LuminaSheets.ItemSheet.ContainsKey((uint)item.Key))
                        {
                            if (SelectedRecipesCraftable[item.Key]) continue;
                            ImGui.PushID($"###SubTableItem{item}");
                            var sheetItem = LuminaSheets.ItemSheet[(uint)item.Key];
                            var name = sheetItem.Name.RawString;
                            var count = item.Value;

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            var invcount = NumberOfIngredient((uint)item.Key);
                            if (invcount >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.Text($"{invcount}");
                            
                            if (RetainerInfo.ATools && RetainerInfo.CacheBuilt)
                            {
                                ImGui.TableNextColumn();
                                uint retainerCount = 0;
                                retainerCount = RetainerInfo.GetRetainerItemCount(sheetItem.RowId);

                                ImGuiEx.Text($"{retainerCount}");

                                if (invcount + retainerCount >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }

                            }
                            ImGui.PopID();

                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "子表渲染");
                }

                ImGui.EndTable();
            }
        }

        public static void AddRecipeIngredientsToList(Recipe? recipe, ref Dictionary<int, int> ingredientList, bool addSublist = true, CraftingList selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                if (selectedList != null)
                {
                    foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                    {
                        if (ingredientList.ContainsKey(ing.ItemIngredient))
                        {
                            ingredientList[ing.ItemIngredient] += ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId);
                        }
                        else
                        {
                            ingredientList.TryAdd(ing.ItemIngredient, ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId));
                        }

                        var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                        SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                        if (GetIngredientRecipe(ing.ItemIngredient) != null && addSublist)
                        {
                            AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                        }

                    }
                }
                else
                {
                    foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                    {
                        if (ingredientList.ContainsKey(ing.ItemIngredient))
                        {
                            ingredientList[ing.ItemIngredient] += ing.AmountIngredient;
                        }
                        else
                        {
                            ingredientList.TryAdd(ing.ItemIngredient, ing.AmountIngredient);
                        }

                        var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                        SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                        if (GetIngredientRecipe(ing.ItemIngredient) != null && addSublist)
                        {
                            AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "错误");
            }
        }
        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList, bool addSubList = true, CraftingList selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                        if (GetIngredientRecipe(ing.ItemIngredient).RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "错误");
            }
        }

        private static void DrawRecipes()
        {
            if (Service.Configuration.ShowOnlyCraftable && !RetainerInfo.CacheBuilt)
            {
                if (RetainerInfo.ATools)
                ImGui.TextWrapped($"构建雇员缓存: {(RetainerInfo.RetainerData.Values.Any() ? RetainerInfo.RetainerData.FirstOrDefault().Value.Count : "0")}/{FilteredList.Select(x => x.Value).SelectMany(x => x.UnkData5).Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0).DistinctBy(x => x.ItemIngredient).Count()}");
                ImGui.TextWrapped($"构建可制作物品清单: {CraftableItems.Count}/{FilteredList.Count}");
                ImGui.Spacing();
            }
            ImGui.Text("搜索");
            ImGui.SameLine();
            ImGui.InputText("###RecipeSearch", ref Search, 100);
            if (ImGui.Selectable("", SelectedRecipe == null))
            {
                SelectedRecipe = null;
            }

            if (Service.Configuration.ShowOnlyCraftable && RetainerInfo.CacheBuilt)
            {
                foreach (var recipe in CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    ImGui.PushID((int)recipe.RowId);
                    var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }
                    ImGui.PopID();
                }
            }
            else if (!Service.Configuration.ShowOnlyCraftable)
            {
                foreach (var recipe in CollectionsMarshal.AsSpan(FilteredList.Values.ToList()))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(recipe.ItemResult.Value.Name.RawString)) continue;
                        if (!recipe.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)) continue;
                        rawIngredientsList.Clear();
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            subtableList.Clear();
                            SelectedRecipeRawIngredients.Clear();
                            SelectedRecipe = recipe;
                        }

                    }
                    catch (Exception ex)
                    {
                        Dalamud.Logging.PluginLog.Error(ex, "绘制配方清单");
                    }
                }
            }
        }

        public async unsafe static Task<bool> CheckForIngredients(Recipe recipe, bool fetchFromCache = true, bool checkRetainer = false)
        {
            if (fetchFromCache)
                if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.UnkData5.Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient);
                    int? invNumberHQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient, true);

                    if (!checkRetainer)
                    {
                        if (value.AmountIngredient > (invNumberNQ + invNumberHQ))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }
                    else
                    {
                        uint retainerCount = RetainerInfo.GetRetainerItemCount((uint)value.ItemIngredient);
                        if (value.AmountIngredient > (invNumberNQ + invNumberHQ + retainerCount))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }

                    invNumberHQ = null;
                    invNumberNQ = null;
                }
                catch
                {

                }

            }

            CraftableItems[recipe] = true;
            return true;
        }

        private static bool HasRawIngredients(int itemIngredient, byte amountIngredient)
        {
            if (GetIngredientRecipe(itemIngredient).RowId == 0) return false;

            return CheckForIngredients(GetIngredientRecipe(itemIngredient)).Result;

        }

        public unsafe static int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true);

                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }
        private unsafe static void DrawRecipeOptions()
        {
            {
                List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == SelectedRecipe.ItemResult.Value.Name.RawString).Select(x => x.CraftType.Value.RowId + 8).ToList();
                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                ImGui.Text($"制作职业: {String.Join(", ", jobstrings)}");
            }
            var ItemsRequired = SelectedRecipe.UnkData5;

            int numRows = RetainerInfo.ATools ? 6 : 5;
            if (ImGui.BeginTable("###RecipeTable", numRows, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("需求", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("背包", ImGuiTableColumnFlags.WidthFixed);
                if (RetainerInfo.ATools)
                    ImGui.TableSetupColumn("雇员", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("方案", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("来源", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();
                try
                {
                    foreach (var value in ItemsRequired.Where(x => x.AmountIngredient > 0))
                    {
                        jobs.Clear();
                        string ingredient = LuminaSheets.ItemSheet[(uint)value.ItemIngredient].Name.RawString;
                        Recipe? ingredientRecipe = GetIngredientRecipe(value.ItemIngredient);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{ingredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{value.AmountIngredient}");
                        ImGui.TableNextColumn();
                        var invCount = NumberOfIngredient((uint)value.ItemIngredient);
                        ImGuiEx.Text($"{invCount}");
                        if (invCount >= value.AmountIngredient)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }
                        ImGui.TableNextColumn();
                        if (RetainerInfo.ATools && RetainerInfo.CacheBuilt)
                        {
                            uint retainerCount = 0;
                            retainerCount = RetainerInfo.GetRetainerItemCount((uint)value.ItemIngredient);

                            ImGuiEx.Text($"{retainerCount}");

                            if (invCount + retainerCount >= value.AmountIngredient)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.TableNextColumn();
                        }

                        if (ingredientRecipe is not null)
                        {
                            if (ImGui.Button($"已制作###search{ingredientRecipe.RowId}"))
                            {
                                SelectedRecipe = ingredientRecipe;
                            }
                        }
                        else
                        {
                            ImGui.Text("已采集");
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            try
                            {
                                jobs.AddRange(FilteredList.Values.Where(x => x.ItemResult == ingredientRecipe.ItemResult).Select(x => x.CraftType.Row + 8));
                                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                                ImGui.Text(String.Join(", ", jobstrings));
                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "职业字符串");
                            }

                        }
                        else
                        {
                            try
                            {
                                var gatheringItem = LuminaSheets.GatheringItemSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (gatheringItem != null)
                                {
                                    var jobs = LuminaSheets.GatheringPointBaseSheet?.Values.Where(x => x.Item.Any(y => y == gatheringItem.RowId)).Select(x => x.GatheringType).ToList();
                                    List<string> tempArray = new();
                                    if (jobs!.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.RawString);
                                    if (jobs!.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.RawString);
                                    if (jobs!.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.RawString);
                                    ImGui.Text($"{string.Join(", ", tempArray)}");
                                    continue;
                                }

                                var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.ItemIngredient).FirstOrDefault().Value;
                                if (spearfish != null && spearfish.Item.Value.Name.RawString == ingredient)
                                {
                                    ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    continue;
                                }

                                var fishSpot = LuminaSheets.FishParameterSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (fishSpot != null)
                                {
                                    ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    continue;
                                }


                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "职业字符串");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dalamud.Logging.PluginLog.Error(ex, "配方成分");
                }
                ImGui.EndTable();
            }

        }



        public static Recipe? GetIngredientRecipe(string ingredient)
        {
            return FilteredList.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? FilteredList.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }

        public static Recipe? GetIngredientRecipe(int ingredient)
        {
            if (FilteredList.Values.Any(x => x.ItemResult.Value.RowId == ingredient))
                return FilteredList.Values.First(x => x.ItemResult.Value.RowId == ingredient);

            return null;
        }
    }
}
