﻿namespace Artisan.UI;

using Autocraft;
using CraftingLists;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using global::Artisan.CraftingLogic;
using global::Artisan.CraftingLogic.Solvers;
using global::Artisan.GameInterop;
using global::Artisan.RawInformation.Character;
using global::Artisan.UI.Tables;
using ImGuiNET;
using IPC;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;
using PunishLib.ImGuiMethods;
using RawInformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

internal class ListEditor : Window, IDisposable
{
    public bool Minimized = false;

    private Task RegenerateTask = null;
    private CancellationTokenSource source = new CancellationTokenSource();
    private CancellationToken token;

    public bool Processing = false;

    internal List<uint> jobs = new();

    internal List<int> listMaterials = new();

    internal Dictionary<int, int> listMaterialsNew = new();

    internal string newListName = string.Empty;

    internal List<int> rawIngredientsList = new();

    internal Recipe? SelectedRecipe;

    internal Dictionary<uint, int> SelectedRecipeRawIngredients = new();

    internal Dictionary<uint, int> subtableList = new();

    private ListFolders ListsUI = new();

    private bool TidyAfter;

    private int timesToAdd = 1;

    public readonly RecipeSelector RecipeSelector;

    public readonly NewCraftingList SelectedList;

    private string newName = string.Empty;

    private bool RenameMode;

    internal string Search = string.Empty;

    public Dictionary<uint, int> SelectedListMateralsNew = new();

    public IngredientTable? Table;

    private bool ColourValidation = false;

    private bool HQSubcraftsOnly = false;

    private bool NeedsToRefreshTable = false;

    NewCraftingList? copyList;

    IngredientHelpers IngredientHelper = new();

    private bool hqSim = false;

    public ListEditor(int listId)
        : base($"清单编辑器###{listId}")
    {
        SelectedList = P.Config.NewCraftingLists.First(x => x.ID == listId);
        RecipeSelector = new RecipeSelector(SelectedList.ID);
        RecipeSelector.ItemAdded += RefreshTable;
        RecipeSelector.ItemDeleted += RefreshTable;
        RecipeSelector.ItemSkipTriggered += RefreshTable;
        IsOpen = true;
        P.ws.AddWindow(this);
        Size = new Vector2(1000, 600);
        SizeCondition = ImGuiCond.Appearing;
        ShowCloseButton = true;
        RespectCloseHotkey = false;
        NeedsToRefreshTable = true;

        if (P.Config.DefaultHQCrafts) HQSubcraftsOnly = true;
        if (P.Config.DefaultColourValidation) ColourValidation = true;
    }

    public async Task GenerateTableAsync(CancellationTokenSource source)
    {
        Table?.Dispose();
        var list = await IngredientHelper.GenerateList(SelectedList, source);
        if (list is null)
        {
            Svc.Log.Debug($"表格列表为空，正在中止。");
            return;
        }

        Table = new IngredientTable(list);
    }

    public void RefreshTable(object? sender, bool e)
    {
        token = source.Token;
        Table = null;
        P.UniversalsisClient.PlayerWorld = Svc.ClientState.LocalPlayer?.CurrentWorld.RowId;
        if (RegenerateTask == null || RegenerateTask.IsCompleted)
        {
            Svc.Log.Debug($"开始重新生成");
            RegenerateTask = Task.Run(() => GenerateTableAsync(source), token);
        }
        else
        {
            Svc.Log.Debug($"停止并重新生成");
            if (source != null)
                source.Cancel();

            source = new();
            token = source.Token;
            RegenerateTask = Task.Run(() => GenerateTableAsync(source), token);
        }
    }

    public override void PreDraw()
    {
        if (!P.Config.DisableTheme)
        {
            P.Style.Push();
            P.StylePushed = true;
        }
    }

    public override void PostDraw()
    {
        if (P.StylePushed)
        {
            P.Style.Pop();
            P.StylePushed = false;
        }
    }

    private static bool GatherBuddy =>
        DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);

    private static bool ItemVendor =>
        DalamudReflector.TryGetDalamudPlugin("物品商人位置", out var ivl, false, true);

    private static bool MonsterLookup =>
        DalamudReflector.TryGetDalamudPlugin("怪物掉落狩猎", out var mlh, false, true);

    private static unsafe void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);

    public class ListOrderCheck
    {
        public uint RecID;
        public int RecipeDepth = 0;
        public int RecipeDiff => Calculations.RecipeDifficulty(LuminaSheets.RecipeSheet[RecID]);
        public uint CraftType => LuminaSheets.RecipeSheet[RecID].CraftType.RowId;

        public int ListQuantity = 0;
        public ListItemOptions ops;
    }

    public async override void Draw()
    {
        var btn = ImGuiHelpers.GetButtonSize("开始制作清单");

        if (Endurance.Enable || CraftingListUI.Processing)
            ImGui.BeginDisabled();

        if (ImGui.Button("开始制作清单"))
        {
            CraftingListUI.selectedList = this.SelectedList;
            CraftingListUI.StartList();
            this.IsOpen = false;
        }

        if (Endurance.Enable || CraftingListUI.Processing)
            ImGui.EndDisabled();

        ImGui.SameLine();
        var export = ImGuiHelpers.GetButtonSize("导出清单");

        if (ImGui.Button("导出清单"))
        {
            ImGui.SetClipboardText(JsonConvert.SerializeObject(P.Config.NewCraftingLists.Where(x => x.ID == SelectedList.ID).First()));
            Notify.Success("清单已导出到剪贴板。");
        }

        var restock = ImGuiHelpers.GetButtonSize("从雇员背包补充库存");
        if (RetainerInfo.ATools)
        {
            ImGui.SameLine();

            if (Endurance.Enable || CraftingListUI.Processing)
                ImGui.BeginDisabled();

            if (ImGui.Button($"从雇员背包补充库存"))
            {
                Task.Run(() => RetainerInfo.RestockFromRetainers(SelectedList));
            }

            if (Endurance.Enable || CraftingListUI.Processing)
                ImGui.EndDisabled();
        }
        else
        {
            ImGui.SameLine();

            if (!RetainerInfo.AToolsInstalled)
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"请安装 Allagan Tools 以使用雇员功能。");

            if (RetainerInfo.AToolsInstalled && !RetainerInfo.AToolsEnabled)
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"请启用 Allagan Tools 以使用雇员功能。");

            if (RetainerInfo.AToolsEnabled)
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"已关闭 Allagan Tools 集成.");
        }

        if (ImGui.BeginTabBar("CraftingListEditor", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("配方"))
            {
                DrawRecipes();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("材料"))
            {
                if (NeedsToRefreshTable)
                {
                    RefreshTable(null, true);
                    NeedsToRefreshTable = false;
                }

                DrawIngredients();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("清单设置"))
            {
                DrawListSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("从其他清单复制"))
            {
                DrawCopyFromList();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }


    private void DrawCopyFromList()
    {
        if (P.Config.NewCraftingLists.Count > 1)
        {
            ImGuiEx.TextWrapped($"选择清单");
            ImGuiEx.SetNextItemFullWidth();
            if (ImGui.BeginCombo("###ListCopyCombo", copyList is null ? "" : copyList.Name))
            {
                if (ImGui.Selectable($""))
                {
                    copyList = null;
                }
                foreach (var list in P.Config.NewCraftingLists.Where(x => x.ID != SelectedList.ID))
                {
                    if (ImGui.Selectable($"{list.Name}###CopyList{list.ID}"))
                    {
                        copyList = list.JSONClone();
                    }
                }

                ImGui.EndCombo();
            }
        }
        else
        {
            ImGui.Text($"请添加其他清单以供复制");
        }

        if (copyList != null)
        {
            ImGui.Text($"这些将会被复制：");
            ImGui.Indent();
            if (ImGui.BeginListBox("###ItemList", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 30f)))
            {
                foreach (var rec in copyList.Recipes.Distinct())
                {
                    ImGui.Text($"- {LuminaSheets.RecipeSheet[rec.ID].ItemResult.Value.Name.ToDalamudString()} x{rec.Quantity}");
                }

                ImGui.EndListBox();
            }
            ImGui.Unindent();
            if (ImGui.Button($"复制物品"))
            {
                foreach (var recipe in copyList.Recipes)
                {
                    if (SelectedList.Recipes.Any(x => x.ID == recipe.ID))
                    {
                        SelectedList.Recipes.First(x => x.ID == recipe.ID).Quantity += recipe.Quantity;
                    }
                    else
                        SelectedList.Recipes.Add(new ListItem() { Quantity = recipe.Quantity, ID = recipe.ID });
                }
                Notify.Success($"所有物品已从 {copyList.Name} 复制到 {SelectedList.Name}。");
                RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();
                RefreshTable(null, true);
                P.Config.Save();
            }
        }
    }

    public void DrawRecipeSubTable()
    {
        int colCount = RetainerInfo.ATools ? 4 : 3;
        if (ImGui.BeginTable("###SubTableRecipeData", colCount, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("所需", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("库存", ImGuiTableColumnFlags.WidthFixed);
            if (RetainerInfo.ATools)
                ImGui.TableSetupColumn("雇员", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            if (subtableList.Count == 0)
                subtableList = SelectedRecipeRawIngredients;

            try
            {
                foreach (var item in subtableList)
                {
                    if (LuminaSheets.ItemSheet.ContainsKey(item.Key))
                    {
                        if (CraftingListHelpers.SelectedRecipesCraftable[item.Key]) continue;
                        ImGui.PushID($"###SubTableItem{item}");
                        var sheetItem = LuminaSheets.ItemSheet[item.Key];
                        var name = sheetItem.Name.ToString();
                        var count = item.Value;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text($"{name}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{count}");
                        ImGui.TableNextColumn();
                        var invcount = CraftingListUI.NumberOfIngredient(item.Key);
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
                            int retainerCount = 0;
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
                Svc.Log.Error(ex, "SubTableRender");
            }

            ImGui.EndTable();
        }
    }
    public void DrawRecipeData()
    {
        var showOnlyCraftable = P.Config.ShowOnlyCraftable;

        if (ImGui.Checkbox("###ShowCraftableCheckbox", ref showOnlyCraftable))
        {
            P.Config.ShowOnlyCraftable = showOnlyCraftable;
            P.Config.Save();

            if (showOnlyCraftable)
            {
                RetainerInfo.TM.Abort();
                RetainerInfo.TM.Enqueue(async () => await RetainerInfo.LoadCache());
            }
        }

        ImGui.SameLine();
        ImGui.TextWrapped("仅显示你有材料的配方（切换以刷新）");

        if (P.Config.ShowOnlyCraftable && RetainerInfo.ATools)
        {
            var showOnlyCraftableRetainers = P.Config.ShowOnlyCraftableRetainers;
            if (ImGui.Checkbox("###ShowCraftableRetainersCheckbox", ref showOnlyCraftableRetainers))
            {
                P.Config.ShowOnlyCraftableRetainers = showOnlyCraftableRetainers;
                P.Config.Save();

                CraftingListUI.CraftableItems.Clear();
                RetainerInfo.TM.Abort();
                RetainerInfo.TM.Enqueue(async () => await RetainerInfo.LoadCache());
            }

            ImGui.SameLine();
            ImGui.TextWrapped("包含雇员库存");
        }

        var preview = SelectedRecipe is null
                          ? string.Empty
                          : $"{SelectedRecipe.Value.ItemResult.Value.Name.ToDalamudString().ToString()} ({LuminaSheets.ClassJobSheet[SelectedRecipe.Value.CraftType.RowId + 8].Abbreviation.ToString()})";

        if (ImGui.BeginCombo("选择配方", preview))
        {
            DrawRecipeList();

            ImGui.EndCombo();
        }

        if (SelectedRecipe != null)
        {
            if (ImGui.CollapsingHeader("配方信息")) DrawRecipeOptions();
            if (SelectedRecipeRawIngredients.Count == 0)
                CraftingListHelpers.AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

            if (ImGui.CollapsingHeader("原材料"))
            {
                ImGui.Text("所需原材料");
                DrawRecipeSubTable();
            }

            ImGui.Spacing();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().Length() / 2f);
            ImGui.TextWrapped("添加数量");
            ImGui.SameLine();
            ImGui.InputInt("###TimesToAdd", ref timesToAdd, 1, 5);
            ImGui.PushItemWidth(-1f);

            if (timesToAdd < 1)
                ImGui.BeginDisabled();

            if (ImGui.Button("添加到清单", new Vector2(ImGui.GetContentRegionAvail().X / 2, 45f)))
            {
                SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                if (SelectedList.Recipes.Any(x => x.ID == SelectedRecipe.Value.RowId))
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).Quantity += checked(timesToAdd);
                }
                else
                {
                    SelectedList.Recipes.Add(new ListItem() { ID = SelectedRecipe.Value.RowId, Quantity = checked(timesToAdd) });
                }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(SelectedList);

                if (SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions is null)
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions = new ListItemOptions { NQOnly = SelectedList.AddAsQuickSynth };
                }
                else
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions.NQOnly = SelectedList.AddAsQuickSynth;
                }

                RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();

                NeedsToRefreshTable = true;

                P.Config.Save();
                if (P.Config.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            ImGui.SameLine();
            if (ImGui.Button("添加到清单 (包含所有半成品)", new Vector2(ImGui.GetContentRegionAvail().X, 45f)))
            {
                SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                CraftingListUI.AddAllSubcrafts(SelectedRecipe.Value, SelectedList, 1, timesToAdd);

                Svc.Log.Debug($"正在添加: {SelectedRecipe.Value.ItemResult.Value.Name.ToDalamudString().ToString()} {timesToAdd} 次");
                if (SelectedList.Recipes.Any(x => x.ID == SelectedRecipe.Value.RowId))
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).Quantity += timesToAdd;
                }
                else
                {
                    SelectedList.Recipes.Add(new ListItem() { ID = SelectedRecipe.Value.RowId, Quantity = timesToAdd });
                }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(SelectedList);

                if (SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions is null)
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions = new ListItemOptions { NQOnly = SelectedList.AddAsQuickSynth };
                }
                else
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions.NQOnly = SelectedList.AddAsQuickSynth;
                }

                RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();
                RefreshTable(null, true);
                P.Config.Save();
                if (P.Config.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            if (timesToAdd < 1)
                ImGui.EndDisabled();

            ImGui.Checkbox("添加后移除所有不必要的半成品", ref TidyAfter);
        }

        ImGui.Separator();

        if (ImGui.Button($"排序配方"))
        {
            List<ListItem> newList = new();
            List<ListOrderCheck> order = new();
            foreach (var item in SelectedList.Recipes.Distinct())
            {
                var orderCheck = new ListOrderCheck();
                var r = LuminaSheets.RecipeSheet[item.ID];
                orderCheck.RecID = r.RowId;
                int maxDepth = 0;
                foreach (var ing in r.Ingredients().Where(x => x.Amount > 0).Select(x => x.Item.RowId))
                {
                    CheckIngredientRecipe(ing, orderCheck);
                    if (orderCheck.RecipeDepth > maxDepth)
                    {
                        maxDepth = orderCheck.RecipeDepth;
                    }
                    orderCheck.RecipeDepth = 0;
                }
                orderCheck.RecipeDepth = maxDepth;
                orderCheck.ListQuantity = item.Quantity;
                orderCheck.ops = item.ListItemOptions ?? new ListItemOptions();
                order.Add(orderCheck);
            }

            foreach (var ord in order.OrderBy(x => x.RecipeDepth).ThenBy(x => x.RecipeDiff).ThenBy(x => x.CraftType))
            {
                newList.Add(new ListItem() { ID = ord.RecID, Quantity = ord.ListQuantity, ListItemOptions = ord.ops });
            }

            SelectedList.Recipes = newList;
            RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();
            P.Config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGuiEx.Tooltip($"这将按配方深度，然后按难度对你的清单进行排序。配方深度是指有多少材料依赖于清单上的其他配方。\n\n" +
                $"例如: {LuminaSheets.RecipeSheet[35508].ItemResult.Value.Name.ToDalamudString()} 需要 {LuminaSheets.ItemSheet[36186].Name} ，而后者又需要 {LuminaSheets.ItemSheet[36189].Name}，如果这些物品都在清单上，则这个配方的深度为 3。\n" +
                $"没有其他配方依赖的物品深度为 1，因此会排在清单顶部，例如 {LuminaSheets.RecipeSheet[5299].ItemResult.Value.Name.ToDalamudString()}\n\n" +
                $"最后，清单根据游戏内制作的难度进行排序，希望能将类似的制作分组在一起。");
        }

        Task.Run(() =>
        {
            listTime = CraftingListUI.GetListTimer(SelectedList);
        });
        string duration = listTime == TimeSpan.Zero ? "未知" : string.Format("{0:D2}d {1:D2}h {2:D2}m {3:D2}s", listTime.Days, listTime.Hours, listTime.Minutes, listTime.Seconds);
        ImGui.SameLine();
        ImGui.Text($"预计清单完成时间: {duration}");
    }

    TimeSpan listTime;

    private void CheckIngredientRecipe(uint ing, ListOrderCheck orderCheck)
    {
        foreach (var result in SelectedList.Recipes.Distinct().Select(x => LuminaSheets.RecipeSheet[x.ID]))
        {
            if (result.ItemResult.RowId == ing)
            {
                orderCheck.RecipeDepth += 1;
                foreach (var subIng in result.Ingredients().Where(x => x.Amount > 0).Select(x => x.Item.RowId))
                {
                    CheckIngredientRecipe(subIng, orderCheck);
                }
                return;
            }
        }
    }

    private Dictionary<uint, string> RecipeLabels = new Dictionary<uint, string>();
    private void DrawRecipeList()
    {
        if (P.Config.ShowOnlyCraftable && !RetainerInfo.CacheBuilt)
        {
            if (RetainerInfo.ATools)
                ImGui.TextWrapped($"正在构建雇员缓存: {(RetainerInfo.RetainerData.Values.Any() ? RetainerInfo.RetainerData.FirstOrDefault().Value.Count : "0")}/{LuminaSheets.RecipeSheet!.Select(x => x.Value).SelectMany(x => x.Ingredients()).Where(x => x.Item.RowId != 0 && x.Amount > 0).DistinctBy(x => x.Item.RowId).Count()}");
            ImGui.TextWrapped($"正在构建可制作物品清单: {CraftingListUI.CraftableItems.Count}/{LuminaSheets.RecipeSheet.Count}");
            ImGui.Spacing();
        }

        ImGui.Text("搜索");
        ImGui.SameLine();
        ImGui.InputText("###RecipeSearch", ref Search, 150);
        if (ImGui.Selectable(string.Empty, SelectedRecipe == null))
        {
            SelectedRecipe = null;
        }

        if (P.Config.ShowOnlyCraftable && RetainerInfo.CacheBuilt)
        {
            foreach (var recipe in CraftingListUI.CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => Regex.Match(x.ItemResult.Value.Name.GetText(true), Search, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Success))
            {
                if (recipe.Number == 0) continue;
                ImGui.PushID((int)recipe.RowId);
                if (!RecipeLabels.ContainsKey(recipe.RowId))
                {
                    RecipeLabels[recipe.RowId] = $"{recipe.ItemResult.Value.Name.ToDalamudString()} ({LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation} {recipe.RecipeLevelTable.Value.ClassJobLevel})";
                }
                var selected = ImGui.Selectable(RecipeLabels[recipe.RowId], recipe.RowId == SelectedRecipe?.RowId);

                if (selected)
                {
                    subtableList.Clear();
                    SelectedRecipeRawIngredients.Clear();
                    SelectedRecipe = recipe;
                }

                ImGui.PopID();
            }
        }
        else if (!P.Config.ShowOnlyCraftable)
        {
            foreach (var recipe in LuminaSheets.RecipeSheet.Values)
            {
                try
                {
                    if (recipe.ItemResult.RowId == 0) continue;
                    if (recipe.Number == 0) continue;
                    if (!string.IsNullOrEmpty(Search) && !Regex.Match(recipe.ItemResult.Value.Name.GetText(true), Search, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Success) continue;
                    if (!RecipeLabels.ContainsKey(recipe.RowId))
                    {
                        RecipeLabels[recipe.RowId] = $"{recipe.ItemResult.Value.Name.ToDalamudString()} ({LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation} {recipe.RecipeLevelTable.Value.ClassJobLevel})";
                    }
                    var selected = ImGui.Selectable(RecipeLabels[recipe.RowId], recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }

                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "DrawRecipeList");
                }
            }
        }
    }


    private void DrawRecipeOptions()
    {
        {
            List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.ToDalamudString().ToString() == SelectedRecipe.Value.ItemResult.Value.Name.ToDalamudString().ToString()).Select(x => x.CraftType.Value.RowId + 8).ToList();
            string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
            ImGui.Text($"制作职业: {string.Join(", ", jobstrings)}");
            ImGuiComponents.HelpMarker($"刻木匠——CRP      锻铁匠——BSM        铸甲匠——ARM           雕金匠——GSM\n制革匠——LTW      裁衣匠——WVR        炼金术士——ALC       烹调师——CUL\n园艺工——BTN        采矿工——MIN        捕鱼人——FSH");
        }

        var ItemsRequired = SelectedRecipe.Value.Ingredients();

        int numRows = RetainerInfo.ATools ? 6 : 5;
        if (ImGui.BeginTable("###RecipeTable", numRows, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("所需", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("库存", ImGuiTableColumnFlags.WidthFixed);
            if (RetainerInfo.ATools)
                ImGui.TableSetupColumn("雇员", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("方式", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("来源", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();
            try
            {
                foreach (var value in ItemsRequired.Where(x => x.Amount > 0))
                {
                    jobs.Clear();
                    string ingredient = LuminaSheets.ItemSheet[value.Item.RowId].Name.ToString();
                    Recipe? ingredientRecipe = CraftingListHelpers.GetIngredientRecipe(value.Item.RowId);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGuiEx.Text($"{ingredient}");
                    ImGui.TableNextColumn();
                    ImGuiEx.Text($"{value.Amount}");
                    ImGui.TableNextColumn();
                    var invCount = CraftingListUI.NumberOfIngredient(value.Item.RowId);
                    ImGuiEx.Text($"{invCount}");

                    if (invCount >= value.Amount)
                    {
                        var color = ImGuiColors.HealerGreen;
                        color.W -= 0.3f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }

                    ImGui.TableNextColumn();
                    if (RetainerInfo.ATools && RetainerInfo.CacheBuilt)
                    {
                        int retainerCount = 0;
                        retainerCount = RetainerInfo.GetRetainerItemCount(value.Item.RowId);

                        ImGuiEx.Text($"{retainerCount}");

                        if (invCount + retainerCount >= value.Amount)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }

                        ImGui.TableNextColumn();
                    }

                    if (ingredientRecipe is not null)
                    {
                        if (ImGui.Button($"已制作###search{ingredientRecipe.Value.RowId}"))
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
                            jobs.AddRange(LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.RowId == ingredientRecipe.Value.ItemResult.RowId).Select(x => x.CraftType.RowId + 8));
                            string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                            ImGui.Text(string.Join(", ", jobstrings));
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error(ex, "JobStrings");
                        }

                    }
                    else
                    {
                        try
                        {
                            var gatheringItem = LuminaSheets.GatheringItemSheet?.Where(x => x.Value.Item.RowId == value.Item.RowId).FirstOrDefault().Value;
                            if (gatheringItem != null)
                            {
                                var jobs = LuminaSheets.GatheringPointBaseSheet?.Values.Where(x => x.Item.Any(y => y.RowId == gatheringItem.Value.RowId)).Select(x => x.GatheringType).ToList();
                                List<string> tempArray = new();
                                if (jobs!.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.ToString());
                                if (jobs!.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.ToString());
                                if (jobs!.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.ToString());
                                ImGui.Text($"{string.Join(", ", tempArray)}");
                                continue;
                            }

                            var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.Item.RowId).FirstOrDefault().Value;
                            if (spearfish != null && spearfish.Value.Item.Value.Name.ToString() == ingredient)
                            {
                                ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.ToString()}");
                                continue;
                            }

                            var fishSpot = LuminaSheets.FishParameterSheet?.Where(x => x.Value.Item.RowId == value.Item.RowId).FirstOrDefault().Value;
                            if (fishSpot != null)
                            {
                                ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.ToString()}");
                            }


                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error(ex, "JobStrings");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "RecipeIngreds");
            }

            ImGui.EndTable();
        }

    }

    public override void OnClose()
    {
        Table?.Dispose();
        source.Cancel();
        P.ws.RemoveWindow(this);
    }

    private void DrawIngredients()
    {
        if (SelectedList.ID != 0)
        {
            if (SelectedList.Recipes.Count > 0)
            {
                DrawTotalIngredientsTable();
            }
            else
            {
                ImGui.Text($"请向你的清单添加物品以填充材料标签。");
            }
        }
    }
    private void DrawTotalIngredientsTable()
    {
        if (Table == null && RegenerateTask.IsCompleted)
        {
            if (ImGui.Button($"创建表格时出现错误，再试试？"))
            {
                RefreshTable(null, true);
            }
            return;
        }
        if (Table == null)
        {
            ImGui.TextUnformatted($"材料列表仍在填充中，请稍候。");
            var a = IngredientHelper.CurrentIngredient;
            var b = IngredientHelper.MaxIngredient;
            ImGui.ProgressBar((float)a / b, new(ImGui.GetContentRegionAvail().X, default), $"{a * 100.0f / b:f2}% ({a}/{b})");
            return;
        }
        ImGui.BeginChild("###IngredientsListTable", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - (ColourValidation ? (RetainerInfo.ATools ? 90f.Scale() : 60f.Scale()) : 30f.Scale())));
        Table._nameColumn.ShowColour = ColourValidation;
        Table._inventoryColumn.HQOnlyCrafts = HQSubcraftsOnly;
        Table._retainerColumn.HQOnlyCrafts = HQSubcraftsOnly;
        Table._nameColumn.ShowHQOnly = HQSubcraftsOnly;
        Table.Draw(ImGui.GetTextLineHeightWithSpacing());
        ImGui.EndChild();

        ImGui.Checkbox($"仅显示 HQ 材料", ref HQSubcraftsOnly);

        ImGuiComponents.HelpMarker($"对于可以制作的材料，这将仅显示背包 {(RetainerInfo.ATools ? " 和雇员" : "")} 库存中 HQ 材料的数量。");

        ImGui.SameLine();
        ImGui.Checkbox("启用颜色验证", ref ColourValidation);

        ImGui.SameLine();

        if (ImGui.GetIO().KeyShift)
        {
            if (ImGui.Button($"将所需材料导出为纯文本"))
            {
                StringBuilder sb = new();
                foreach (var item in Table.ListItems.Where(x => x.Required > 0))
                {
                    sb.AppendLine($"{item.Required}x {item.Data.Name}");
                }

                if (!string.IsNullOrEmpty(sb.ToString()))
                {
                    ImGui.SetClipboardText(sb.ToString());
                    Notify.Success($"所需材料已复制到剪贴板。");
                }
                else
                {
                    Notify.Error($"没有需要导出的材料。");
                }
            }
        }
        else
        {
            if (ImGui.Button($"将剩余材料导出为纯文本"))
            {
                StringBuilder sb = new();
                foreach (var item in Table.ListItems.Where(x => x.Remaining > 0))
                {
                    sb.AppendLine($"{item.Remaining}x {item.Data.Name}");
                }

                if (!string.IsNullOrEmpty(sb.ToString()))
                {
                    ImGui.SetClipboardText(sb.ToString());
                    Notify.Success($"剩余材料已复制到剪贴板。");
                }
                else
                {
                    Notify.Error($"没有需要导出的材料。");
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGuiEx.Tooltip($"按住 Shift 键可从剩余材料切换到所需材料。");
            }

        }


        ImGui.SameLine();
        if (ImGui.Button("需要帮助？"))
            ImGui.OpenPopup("HelpPopup");

        if (ColourValidation)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.HealerGreen);
            ImGui.BeginDisabled(true);
            ImGui.Button("", new Vector2(23, 23));
            ImGui.EndDisabled();
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
            ImGui.Text($" - 库存中拥有所有所需的材料 {(SelectedList.SkipIfEnough && SelectedList.SkipLiteral ? "" : "或由于拥有使用该材料的制作物品而不再需要")}");

            if (RetainerInfo.ATools)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudOrange);
                ImGui.BeginDisabled(true);
                ImGui.Button("", new Vector2(23, 23));
                ImGui.EndDisabled();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
                ImGui.Text($" - 保雇员和背包的库存组合中拥有所有所需材料。");
            }

            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.ParsedBlue);
            ImGui.BeginDisabled(true);
            ImGui.Button("", new Vector2(23, 23));
            ImGui.EndDisabled();
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
            ImGui.Text($" - 库存和可制作物品的组合中拥有所有所需材料。");
        }


        var windowSize = new Vector2(1024 * ImGuiHelpers.GlobalScale,
            ImGui.GetTextLineHeightWithSpacing() * 13 + 2 * ImGui.GetFrameHeightWithSpacing());
        ImGui.SetNextWindowSize(windowSize);
        ImGui.SetNextWindowPos((ImGui.GetIO().DisplaySize - windowSize) / 2);

        using var popup = ImRaii.Popup("HelpPopup",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.Modal);
        if (!popup)
            return;

        ImGui.TextWrapped($"此材料表显示了制作清单上物品所需的所有物品。该表的基本功能向你展示了库存中某种材料的数量、某种材料的来源、是否有可以采集该材料的区域等信息。");
        ImGui.Dummy(new Vector2(0));
        ImGui.BulletText($"你可以点击列标题来过滤结果，既可以通过输入文本，也可以通过预设的过滤器进行。");
        ImGui.BulletText($"右键点击标题将允许你显示/隐藏不同的列或调整列的大小。");
        ImGui.BulletText($"右键点击材料名称将打开一个上下文菜单，提供更多选项。");
        ImGui.BulletText($"在列标题之间的空白区域点击并拖动（如高亮所示）可以重新排序列。");
        ImGui.BulletText($"没有看到任何物品？检查表格标题是否有红色标题。这表示该列正在被过滤，右键点击标题将清除过滤。");
        ImGui.BulletText($"你可以通过安装以下插件来扩展表格的功能:\n- Allagan Tools (启用所有雇员功能)\n- Item Vendor Lookup\n- Gatherbuddy\n- Monster Loot Hunter");
        ImGui.BulletText($"提示：在采集时，过滤 \"剩余所需\" 和 \"来源\" 可以帮助筛选缺失的物品，同时按采集区域排序可以减少传送时间。");

        ImGui.SetCursorPosY(windowSize.Y - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y);
        if (ImGui.Button("关闭帮助", -Vector2.UnitX))
            ImGui.CloseCurrentPopup();
    }

    private void DrawListSettings()
    {
        ImGui.BeginChild("清单设置", ImGui.GetContentRegionAvail(), false);
        var skipIfEnough = SelectedList.SkipIfEnough;
        if (ImGui.Checkbox("跳过制作不必要的材料", ref skipIfEnough))
        {
            SelectedList.SkipIfEnough = skipIfEnough;
            P.Config.Save();
        }
        ImGuiComponents.HelpMarker($"将会跳过清单中任何非必要制作的材料");

        if (skipIfEnough)
        {
            ImGui.Indent();
            if (ImGui.Checkbox("跳过至清单数量", ref SelectedList.SkipLiteral))
            {
                P.Config.Save();
            }

            ImGuiComponents.HelpMarker("当从零开始制作时，将在您的库存中材料不足时继续制作材料，直到达到列表所需的数量。\n\n" +
                "[配方最终数量] x [制作次数] < [库存数量].\n\n" +
                "在为未在清单中的物品（例如部队工坊项目）制作材料时使用。\n\n" +
                "这还将调整材料表的剩余列和颜色验证，以排除检查可能使用该材料制作的物品。");
            ImGui.Unindent();
        }

        if (!RawInformation.Character.CharacterInfo.MateriaExtractionUnlocked())
            ImGui.BeginDisabled();

        var materia = SelectedList.Materia;
        if (ImGui.Checkbox("自动精制魔晶石", ref materia))
        {
            SelectedList.Materia = materia;
            P.Config.Save();
        }

        if (!RawInformation.Character.CharacterInfo.MateriaExtractionUnlocked())
        {
            ImGui.EndDisabled();

            ImGuiComponents.HelpMarker("该角色尚未解锁精制魔晶石，此设置将被忽略。");
        }
        else
            ImGuiComponents.HelpMarker("一旦装备的精炼度达到 100%，将自动精制魔晶石。");

        var repair = SelectedList.Repair;
        if (ImGui.Checkbox("自动修理装备", ref repair))
        {
            SelectedList.Repair = repair;
            P.Config.Save();
        }

        ImGuiComponents.HelpMarker($"如果启用，Artisan 将在任何装备达到配置的修理阈值时自动修理你的装备。\n\n当前最低装备耐久度为 {RepairManager.GetMinEquippedPercent()}%，在商人处修理的费用为 {RepairManager.GetNPCRepairPrice()} gil。\n\n如果无法使用暗物质进行修理，将尝试寻找附近的修理 NPC。");

        if (SelectedList.Repair)
        {
            ImGui.PushItemWidth(200);
            if (ImGui.SliderInt("##repairp", ref SelectedList.RepairPercent, 10, 100, "%d%%"))
                P.Config.Save();
        }

        if (ImGui.Checkbox("将新物品作为简易制作添加到清单", ref SelectedList.AddAsQuickSynth))
            P.Config.Save();

        ImGui.EndChild();
    }

    private void DrawRecipes()
    {
        DrawRecipeData();

        ImGui.Spacing();
        RecipeSelector.Draw(RecipeSelector.maxSize + 16f + ImGui.GetStyle().ScrollbarSize);
        ImGui.SameLine();

        if (RecipeSelector.Current?.ID > 0)
            ItemDetailsWindow.Draw("配方选项", DrawRecipeSettingsHeader, DrawRecipeSettings);
    }

    private void DrawRecipeSettings()
    {
        var selectedListItem = RecipeSelector.Items[RecipeSelector.CurrentIdx].ID;
        var recipe = LuminaSheets.RecipeSheet[RecipeSelector.Current.ID];
        var count = RecipeSelector.Items[RecipeSelector.CurrentIdx].Quantity;

        ImGui.TextWrapped("调整数量");
        ImGuiEx.SetNextItemFullWidth(-30);
        if (ImGui.InputInt("###AdjustQuantity", ref count))
        {
            if (count >= 0)
            {
                SelectedList.Recipes.First(x => x.ID == selectedListItem).Quantity = count;
                P.Config.Save();
            }

            NeedsToRefreshTable = true;
        }

        if (SelectedList.Recipes.First(x => x.ID == selectedListItem).ListItemOptions is null)
        {
            SelectedList.Recipes.First(x => x.ID == selectedListItem).ListItemOptions = new();
        }

        var options = SelectedList.Recipes.First(x => x.ID == selectedListItem).ListItemOptions;

        if (recipe.CanQuickSynth)
        {
            var NQOnly = options.NQOnly;
            if (ImGui.Checkbox("简易制作该物品", ref NQOnly))
            {
                options.NQOnly = NQOnly;
                P.Config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("应用到所有###QuickSynthAll"))
            {
                foreach (var r in SelectedList.Recipes.Where(n => LuminaSheets.RecipeSheet[n.ID].CanQuickSynth))
                {
                    if (r.ListItemOptions == null)
                    { r.ListItemOptions = new(); }
                    r.ListItemOptions.NQOnly = options.NQOnly;
                }
                Notify.Success($"简易制作已应用于所有清单项目。");
                P.Config.Save();
            }

            if (NQOnly && !P.Config.UseConsumablesQuickSynth)
            {
                if (ImGui.Checkbox("您未启用在简易制作中使用消耗品设置，要开启吗？", ref P.Config.UseConsumablesQuickSynth))
                    P.Config.Save();
            }
        }
        else
        {
            ImGui.TextWrapped("该物品不能被简易制作。");
        }

        // Retrieve the list of recipes matching the selected recipe name from the preprocessed lookup table.
        var matchingRecipes = LuminaSheets.recipeLookup[selectedListItem.NameOfRecipe()].ToList();

        if (matchingRecipes.Count > 1)
        {
            var pre = $"{LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation.ToString()}";
            ImGui.TextWrapped("切换制作职业");
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo("###SwitchJobCombo", pre))
            {
                foreach (var altJob in matchingRecipes)
                {
                    var altJ = $"{LuminaSheets.ClassJobSheet[altJob.CraftType.RowId + 8].Abbreviation.ToString()}";
                    if (ImGui.Selectable($"{altJ}"))
                    {
                        try
                        {
                            if (SelectedList.Recipes.Any(x => x.ID == altJob.RowId))
                            {
                                SelectedList.Recipes.First(x => x.ID == altJob.RowId).Quantity += SelectedList.Recipes.First(x => x.ID == selectedListItem).Quantity;
                                SelectedList.Recipes.Remove(SelectedList.Recipes.First(x => x.ID == selectedListItem));
                                RecipeSelector.Items.RemoveAt(RecipeSelector.CurrentIdx);
                                RecipeSelector.Current = RecipeSelector.Items.First(x => x.ID == altJob.RowId);
                                RecipeSelector.CurrentIdx = RecipeSelector.Items.IndexOf(RecipeSelector.Current);
                            }
                            else
                            {
                                SelectedList.Recipes.First(x => x.ID == selectedListItem).ID = altJob.RowId;
                                RecipeSelector.Items[RecipeSelector.CurrentIdx].ID = altJob.RowId;
                                RecipeSelector.Current = RecipeSelector.Items[RecipeSelector.CurrentIdx];
                            }
                            NeedsToRefreshTable = true;

                            P.Config.Save();
                        }
                        catch
                        {

                        }
                    }
                }

                ImGui.EndCombo();
            }
        }

        var config = P.Config.RecipeConfigs.GetValueOrDefault(selectedListItem) ?? new();
        {
            if (config.DrawFood(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"应用到所有###FoodApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.RequiredFood = config.RequiredFood;
                    o.RequiredFoodHQ = config.RequiredFoodHQ;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }
        {
            if (config.DrawPotion(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"应用到所有###PotionApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.RequiredPotion = config.RequiredPotion;
                    o.RequiredPotionHQ = config.RequiredPotionHQ;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }

        {
            if (config.DrawManual(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"应用到所有###ManualApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.RequiredManual = config.RequiredManual;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }

        {
            if (config.DrawSquadronManual(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"应用到所有###SquadManualApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.RequiredSquadronManual = config.RequiredSquadronManual;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }

        var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.RowId);
        stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
        var craft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.RowId, recipe);
        if (config.DrawSolver(craft))
        {
            P.Config.RecipeConfigs[selectedListItem] = config;
            P.Config.Save();
        }
        
        ImGuiEx.TextV("Requirements:");
        ImGui.SameLine();
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));
        ImGui.SameLine(137.6f.Scale());
        ImGui.TextWrapped($"Difficulty: {craft.CraftProgress} | Durability: {craft.CraftDurability} | Quality: {(craft.CraftCollectible ? craft.CraftQualityMin3 : craft.CraftQualityMax)}");
        ImGuiComponents.HelpMarker($"Shows the crafting requirements: Progress needed to complete the craft, how much Durability the recipe has, and Quality target required to reach the highest Quality level (In case of a Collectible). Use this information to select an appropriate macro, if desired.");

        ImGui.Checkbox($"Assume Max Starting Quality (for simulator)", ref hqSim);

        var solverHint = Simulator.SimulatorResult(recipe, config, craft, out var hintColor, hqSim);
        if (!recipe.IsExpert)
            ImGuiEx.TextWrapped(hintColor, solverHint);
        else
            ImGuiEx.TextWrapped($"请在模拟器中运行此配方以获取结果。");
    }

    private void DrawRecipeSettingsHeader()
    {
        if (!RenameMode)
        {
            if (IconButtons.IconTextButton(FontAwesomeIcon.Pen, $"{SelectedList.Name.Replace($"%", "%%")}"))
            {
                newName = SelectedList.Name;
                RenameMode = true;
            }
        }
        else
        {
            if (ImGui.InputText("###RenameMode", ref newName, 200, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (newName.Length > 0)
                {
                    SelectedList.Name = newName;
                    P.Config.Save();
                }
                RenameMode = false;
            }
        }
    }

    public void Dispose()
    {
        source.Cancel();
        RecipeSelector.ItemAdded -= RefreshTable;
        RecipeSelector.ItemDeleted -= RefreshTable;
        RecipeSelector.ItemSkipTriggered -= RefreshTable;
    }
}

internal class RecipeSelector : ItemSelector<ListItem>
{
    public float maxSize = 100;

    private readonly NewCraftingList List;

    public RecipeSelector(int list)
        : base(
            P.Config.NewCraftingLists.First(x => x.ID == list).Recipes.Distinct().ToList(),
            Flags.Add | Flags.Delete | Flags.Move)
    {
        List = P.Config.NewCraftingLists.First(x => x.ID == list);
    }

    protected override bool Filtered(int idx)
    {
        return false;
    }

    protected override bool OnAdd(string name)
    {
        if (name.Trim().All(char.IsDigit))
        {
            var id = Convert.ToUInt32(name);
            if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.RowId == id))
            {
                var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.RowId == id);
                if (recipe.Number == 0) return false;
                if (List.Recipes.Any(x => x.ID == recipe.RowId))
                {
                    List.Recipes.First(x => x.ID == recipe.RowId).Quantity += 1;
                }
                else
                {
                    List.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1 });
                }

                if (!Items.Any(x => x.ID == recipe.RowId)) Items.Add(List.Recipes.First(x => x.ID == recipe.RowId));
            }
        }
        else
        {
            if (LuminaSheets.RecipeSheet.Values.FindFirst(
                    x => x.ItemResult.Value.Name.ToDalamudString().ToString().Equals(name, StringComparison.CurrentCultureIgnoreCase),
                    out var recipe))
            {
                if (recipe.Number == 0) return false;
                if (List.Recipes.Any(x => x.ID == recipe.RowId))
                {
                    List.Recipes.First(x => x.ID == recipe.RowId).Quantity += 1;
                }
                else
                {
                    List.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1 });
                }

                if (!Items.Any(x => x.ID == recipe.RowId)) Items.Add(List.Recipes.First(x => x.ID == recipe.RowId));
            }
        }

        P.Config.Save();

        return true;
    }

    protected override bool OnDelete(int idx)
    {
        var ItemId = Items[idx];
        List.Recipes.Remove(ItemId);
        Items.RemoveAt(idx);
        P.Config.Save();
        return true;
    }

    protected override bool OnDraw(int idx, out bool changes)
    {
        changes = false;
        var ItemId = Items[idx];
        var itemCount = ItemId.Quantity;
        var yield = LuminaSheets.RecipeSheet[ItemId.ID].AmountResult * itemCount;
        var label =
            $"{idx + 1}. {ItemId.ID.NameOfRecipe()} x{itemCount}{(yield != itemCount ? $" (总数 {yield})" : string.Empty)}";
        maxSize = ImGui.CalcTextSize(label).X > maxSize ? ImGui.CalcTextSize(label).X : maxSize;

        if (ItemId.ListItemOptions is null)
        {
            ItemId.ListItemOptions = new();
            P.Config.Save();
        }

        using (var col = ImRaii.PushColor(ImGuiCol.Text, itemCount == 0 || ItemId.ListItemOptions.Skipping ? ImGuiColors.DalamudRed : ImGuiColors.DalamudWhite))
        {
            var res = ImGui.Selectable(label, idx == CurrentIdx);
            ImGuiEx.Tooltip($"右键以 {(ItemId.ListItemOptions.Skipping ? "应用" : "跳过")} 这个配方。");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ItemId.ListItemOptions.Skipping = !ItemId.ListItemOptions.Skipping;
                changes = true;
                P.Config.Save();
            }
            return res;
        }
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        List.Recipes.Move(idx1, idx2);
        Items.Move(idx1, idx2);
        P.Config.Save();
        return true;
    }
}

internal class ListFolders : ItemSelector<NewCraftingList>
{
    public ListFolders()
        : base(P.Config.NewCraftingLists, Flags.Add | Flags.Delete | Flags.Move | Flags.Filter | Flags.Duplicate)
    {
        CurrentIdx = -1;
    }

    protected override string DeleteButtonTooltip()
    {
        return "永久删除此制作清单。\r\n按住 Ctrl 点击。\r\n此操作无法撤消。";
    }

    protected override bool Filtered(int idx)
    {
        return Filter.Length != 0 && !Items[idx].Name.Contains(
                   Filter,
                   StringComparison.InvariantCultureIgnoreCase);
    }

    protected override bool OnAdd(string name)
    {
        var list = new NewCraftingList { Name = name };
        list.SetID();
        list.Save(true);

        return true;
    }

    protected override bool OnDelete(int idx)
    {
        if (P.ws.Windows.FindFirst(
                x => x.WindowName.Contains(CraftingListUI.selectedList.ID.ToString()) && x.GetType() == typeof(ListEditor),
                out var window))
        {
            P.ws.RemoveWindow(window);
        }

        P.Config.NewCraftingLists.RemoveAt(idx);
        P.Config.Save();

        if (!CraftingListUI.Processing)
            CraftingListUI.selectedList = new NewCraftingList();
        return true;
    }

    protected override bool OnDraw(int idx, out bool changes)
    {
        changes = false;
        if (CraftingListUI.Processing && CraftingListUI.selectedList.ID == P.Config.NewCraftingLists[idx].ID)
            ImGui.BeginDisabled();

        using var id = ImRaii.PushId(idx);
        var selected = ImGui.Selectable($"{P.Config.NewCraftingLists[idx].Name} (ID: {P.Config.NewCraftingLists[idx].ID})", idx == CurrentIdx);
        if (selected)
        {
            if (!P.ws.Windows.Any(x => x.WindowName.Contains(P.Config.NewCraftingLists[idx].ID.ToString())))
            {
                Interface.SetupValues();
                ListEditor editor = new(P.Config.NewCraftingLists[idx].ID);
            }
            else
            {
                P.ws.Windows.FindFirst(
                    x => x.WindowName.Contains(P.Config.NewCraftingLists[idx].ID.ToString()),
                    out var window);
                window.BringToFront();
            }

            if (!CraftingListUI.Processing)
                CraftingListUI.selectedList = P.Config.NewCraftingLists[idx];
        }

        if (!CraftingListUI.Processing)
        {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                if (CurrentIdx == idx)
                {
                    CurrentIdx = -1;
                    CraftingListUI.selectedList = new NewCraftingList();
                }
                else
                {
                    CurrentIdx = idx;
                    CraftingListUI.selectedList = P.Config.NewCraftingLists[idx];
                }
            }
        }

        if (CraftingListUI.Processing && CraftingListUI.selectedList.ID == P.Config.NewCraftingLists[idx].ID)
            ImGui.EndDisabled();

        return selected;
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        var baseList = P.Config.NewCraftingLists[idx];
        NewCraftingList newList = new NewCraftingList();
        newList = baseList.JSONClone();
        newList.Name = name;
        newList.SetID();
        newList.Save();
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        P.Config.NewCraftingLists.Move(idx1, idx2);
        return true;
    }
}