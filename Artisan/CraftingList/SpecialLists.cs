﻿using Artisan.QuestSync;
using Artisan.RawInformation;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Artisan.CraftingLists
{
    internal static class SpecialLists
    {
        private static string listName = string.Empty;
        private static Dictionary<uint, bool> JobSelected = LuminaSheets.ClassJobSheet.Values.Where(x => x.RowId >= 8 && x.RowId <= 15).ToDictionary(x => x.RowId, x => false);
        private static Dictionary<ushort, bool> Durabilities = LuminaSheets.RecipeSheet.Values.Where(x => x.Number > 0).Select(x => (ushort)(x.RecipeLevelTable.Value.Durability * ((float)x.DurabilityFactor / 100))).Distinct().Order().ToDictionary(x => x, x => false);

        private static int minLevel = 1;
        private static int maxLevel = 100;

        private static int minCraftsmanship = LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredCraftsmanship);
        private static int minControl = LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredControl);

        private static Dictionary<int, bool> isExpert = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> hasToBeUnlocked = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> questRecipe = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isSecondary = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> alreadyCrafted = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> countsToLog = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isCollectable = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isHQAble = new Dictionary<int, bool>() { [1] = false, [2] = false };

        private static string Contains = string.Empty;

        private static Dictionary<int, bool> Yields = LuminaSheets.RecipeSheet.Values.DistinctBy(x => x.AmountResult).OrderBy(x => x.AmountResult).ToDictionary(x => (int)x.AmountResult, x => false);
        private static Dictionary<string, bool> Stars = LuminaSheets.RecipeLevelTableSheet.Values.DistinctBy(x => x.Stars).ToDictionary(x => "★".Repeat(x.Stars), x => false);
        private static Dictionary<int, bool> Stats = LuminaSheets.RecipeSheet.Values.SelectMany(x => x.ItemResult.Value.BaseParam).DistinctBy(x => x.Value.RowId).Where(x => x.RowId > 0).OrderBy(x => x.RowId).ToDictionary(x => (int)x.RowId, x => false);

        private static float DurY = 0f;

        public static void Draw()
        {
            ImGui.TextWrapped($@"此部分用于根据特定选项创建清单，而不是单独添加物品。为你的清单命名，并从以下选项中选择标准，然后点击“创建清单”，将创建一个包含所有匹配该标准的物品的新清单。如果你不选择任何复选框，则除了制作所需职业，该类别将被视为“任意”或“所有”。");
            ImGui.TextWrapped("生产职业：\n刻木匠——CRP      锻铁匠——BSM        铸甲匠——ARM           雕金匠——GSM\n制革匠——LTW      裁衣匠——WVR        炼金术士——ALC       烹调师——CUL");

            ImGui.Separator();

            ImGui.TextWrapped("清单名称");
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X / 2);
            ImGui.InputText("###NameInput", ref listName, 300);

            ImGui.Columns(6, null, false);

            ImGui.TextWrapped("选择职业");
            if (ImGui.BeginListBox("###JobSelectListBox", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 80f.Scale())))
            {
                ImGui.Columns(2, null, false);
                foreach (var item in JobSelected)
                {
                    string jobName = LuminaSheets.ClassJobSheet[item.Key].Abbreviation.ToString().ToUpper();
                    bool val = item.Value;
                    if (ImGui.Checkbox(jobName, ref val))
                    {
                        JobSelected[item.Key] = val;
                    }
                    ImGui.NextColumn();
                }

                ImGui.EndListBox();
            }


            ImGui.TextWrapped($"已制作的配方");
            if (ImGui.BeginListBox("###AlreadyCraftedRecipes", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = alreadyCrafted[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    alreadyCrafted[1] = yes;
                }
                ImGui.NextColumn();
                bool no = alreadyCrafted[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    alreadyCrafted[2] = no;
                }
                ImGui.EndListBox();
            }

            ImGui.TextWrapped($"可收集的配方");
            if (ImGui.BeginListBox("###CollectableRecipes", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = isCollectable[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    isCollectable[1] = yes;
                }
                ImGui.NextColumn();
                bool no = isCollectable[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    isCollectable[2] = no;
                }

                ImGui.EndListBox();
            }
            ImGui.NextColumn();

            ImGui.TextWrapped($"最大耐久度");
            if (ImGui.BeginListBox("###SpecialListDurability", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 80f.Scale())))
            {
                ImGui.Columns(2, null, false);
                foreach (var dur in Durabilities)
                {
                    var val = dur.Value;
                    if (ImGui.Checkbox($"{dur.Key}", ref val))
                    {
                        Durabilities[dur.Key] = val;
                    }
                    ImGui.NextColumn();
                }
                ImGui.EndListBox();

                DurY = ImGui.GetCursorPosY();
            }

            ImGui.TextWrapped($"分级配方");
            if (ImGui.BeginListBox("###CountsLog", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = countsToLog[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    countsToLog[1] = yes;
                }
                ImGui.NextColumn();
                bool no = countsToLog[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    countsToLog[2] = no;
                }
                
                ImGui.EndListBox();
            }


            ImGui.TextWrapped($"可制作 HQ 的配方");
            if (ImGui.BeginListBox("###HQRecipes", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = isHQAble[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    isHQAble[1] = yes;
                }
                ImGui.NextColumn();
                bool no = isHQAble[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    isHQAble[2] = no;
                }

                ImGui.EndListBox();
            }

            ImGui.NextColumn();
            ImGui.TextWrapped("最低等级");
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5 });
            ImGui.SliderInt("###SpecialListMinLevel", ref minLevel, 1, 100);
            ImGui.PopStyleVar();

            ImGui.TextWrapped($"\n");
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped($"秘籍中的配方");
            if (ImGui.BeginListBox("###UnlockableRecipe", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = hasToBeUnlocked[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    hasToBeUnlocked[1] = yes;
                }
                ImGui.NextColumn();
                bool no = hasToBeUnlocked[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    hasToBeUnlocked[2] = no;
                }
                ImGui.EndListBox();
            }

            ImGui.TextWrapped($"仅用于任务的配方");
            if (ImGui.BeginListBox("###QuestRecipe", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = questRecipe[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    questRecipe[1] = yes;
                }
                ImGui.NextColumn();
                bool no = questRecipe[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    questRecipe[2] = no;
                }
                ImGui.EndListBox();
            }


            ImGui.TextWrapped($"名称包含");
            ImGuiComponents.HelpMarker("支持正则表达式");
            ImGuiEx.SetNextItemFullWidth();
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5 });
            ImGui.InputText($"###NameContains", ref Contains, 100);
           
            ImGui.PopStyleVar();
            ImGui.NextColumn();

            ImGui.TextWrapped("最大等级");
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5});
            ImGui.SliderInt("###SpecialListMaxLevel", ref maxLevel, 1, 100);
            ImGui.PopStyleVar();

            ImGui.TextWrapped($"\n");
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped($"专家配方");
            if (ImGui.BeginListBox("###ExpertRecipe", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = isExpert[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    isExpert[1] = yes;
                }
                ImGui.NextColumn();
                bool no = isExpert[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    isExpert[2] = no;
                }
                ImGui.EndListBox();
            }

            ImGui.TextWrapped($"次要配方");
            if (ImGui.BeginListBox("###SecondaryRecipes", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20f.Scale())))
            {
                ImGui.Columns(2, null, false);
                bool yes = isSecondary[1];
                if (ImGui.Checkbox("是", ref yes))
                {
                    isSecondary[1] = yes;
                }
                ImGui.NextColumn();
                bool no = isSecondary[2];
                if (ImGui.Checkbox("否", ref no))
                {
                    isSecondary[2] = no;
                }
                ImGui.EndListBox();
            }

            ImGui.NextColumn();

            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped($"最小作业精度");
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5 });
            ImGui.SliderInt($"###MinCraftsmanship", ref minCraftsmanship, LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredCraftsmanship), LuminaSheets.RecipeSheet.Values.Max(x => x.RequiredCraftsmanship));
            ImGui.PopStyleVar();

            ImGui.TextWrapped("产出数量");
            if (ImGui.BeginListBox("###Yields", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 120f.Scale())))
            {
                ImGui.Columns(2, null, false);
                foreach (var yield in Yields)
                {
                    var val = yield.Value;
                    if (ImGui.Checkbox($"{yield.Key}", ref val))
                    {
                        Yields[yield.Key] = val;
                    }
                    ImGui.NextColumn();
                }
                ImGui.EndListBox();
            }

            ImGui.NextColumn();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped($"最小加工精度");
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5 });
            ImGui.SliderInt($"###MinControl", ref minControl, LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredControl), LuminaSheets.RecipeSheet.Values.Max(x => x.RequiredControl));
            ImGui.PopStyleVar();

            ImGui.TextWrapped("星级难度");
            if (ImGui.BeginListBox("###Stars", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 120f.Scale())))
            {
                foreach (var star in Stars)
                {
                    var val = star.Value;
                    if (ImGui.Checkbox($"{star.Key}", ref val))
                    {
                        Stars[star.Key] = val;
                    }
                }
                ImGui.EndListBox();
            }

            ImGui.NextColumn();
            

            ImGui.Columns(1);
            //ImGui.SetCursorPosY(DurY + 10);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4);
            ImGui.TextWrapped("状态参数");
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4);
            if (ImGui.BeginListBox("###Stats", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 80f.Scale())))
            {
                ImGui.Columns(6, null, false);
                foreach (var stat in Stats)
                {
                    var val = stat.Value;
                    if (ImGui.Checkbox($"###{Svc.Data.GetExcelSheet<BaseParam>()?.First(x => x.RowId == stat.Key).Name.ExtractText()}", ref val))
                    {
                        Stats[stat.Key] = val;
                    }
                    ImGui.SameLine();
                    ImGui.TextWrapped($"{Svc.Data.GetExcelSheet<BaseParam>()?.First(x => x.RowId == stat.Key).Name.ExtractText()}");
                    ImGui.NextColumn();
                }

                ImGui.EndListBox();
            }
            ImGui.Columns(1);

            ImGui.Spacing();
            if (ImGui.Button("创建清单", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                if (listName.IsNullOrWhitespace())
                {
                    Notify.Error("请给你的清单命名。");
                    return;
                }

                Notify.Info("正在创建你的清单，请稍后。");
                Task.Run(() => CreateList(false)).ContinueWith(result => NotifySuccess(result));
            }
            if (ImGui.Button("创建清单（包含半成品）", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                if (listName.IsNullOrWhitespace())
                {
                    Notify.Error("请给你的清单命名。");
                    return;
                }

                Notify.Info("正在创建你的清单，请稍后。");
                Task.Run(() => CreateList(true)).ContinueWith(result => NotifySuccess(result));
            }
        }

        private static bool NotifySuccess(Task<bool> result)
        {
            if (result.Result)
            {
                Notify.Success($"清单：{listName} 已创建。");
                return true;
            }
            return false;
        }

        private static bool CreateList(bool withSubcrafts)
        {
            var craftingList = new NewCraftingList();
            craftingList.Name = listName;
            var recipes = new List<Recipe>();

            foreach (var job in JobSelected)
            {
                if (job.Value)
                {
                    recipes.AddRange(LuminaSheets.RecipeSheet.Values.Where(x => x.Number > 0 && x.CraftType.RowId == job.Key - 8));

                    if (Stats.Any(x => x.Value))
                    {
                        recipes.RemoveAll(x => x.ItemResult.Value.BaseParam.All(y => y.RowId == 0));
                        foreach (var v in Stats.Where(x => x.Key > 0).OrderByDescending(x => x.Key == 70 || x.Key == 71 || x.Key == 72 || x.Key == 73).ThenBy(x => x.Key))
                        {
                            if (!v.Value)
                            {
                                recipes.RemoveAll(x => x.ItemResult.Value.BaseParam[0].RowId == v.Key);
                            }
                            else
                            {
                                recipes.AddRange(LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.BaseParam.Any(y => y.RowId == v.Key) && x.CraftType.RowId == job.Key - 8));
                            }
                        }
                    }
                }
            }

            foreach (var quest in QuestList.Quests)
            {
                recipes.RemoveAll(x => x.RowId == quest.Value.CRP);
                recipes.RemoveAll(x => x.RowId == quest.Value.BSM);
                recipes.RemoveAll(x => x.RowId == quest.Value.ARM);
                recipes.RemoveAll(x => x.RowId == quest.Value.GSM);
                recipes.RemoveAll(x => x.RowId == quest.Value.LTW);
                recipes.RemoveAll(x => x.RowId == quest.Value.WVR);
                recipes.RemoveAll(x => x.RowId == quest.Value.ALC);
                recipes.RemoveAll(x => x.RowId == quest.Value.CUL);
            }


            recipes.RemoveAll(x => x.RecipeLevelTable.Value.ClassJobLevel < minLevel);
            recipes.RemoveAll(x => x.RecipeLevelTable.Value.ClassJobLevel > maxLevel);
            recipes.RemoveAll(x => x.RequiredCraftsmanship < minCraftsmanship);
            recipes.RemoveAll(x => x.RequiredControl < minControl);


            if (Durabilities.Any(x => x.Value))
            {
                foreach (var dur in Durabilities)
                {
                    if (!dur.Value)
                    {
                        recipes.RemoveAll(x => (ushort)(x.RecipeLevelTable.Value.Durability * ((float)x.DurabilityFactor / 100)) == dur.Key);
                    }
                }
            }

            if (hasToBeUnlocked.Any(x => x.Value))
            {
                foreach (var v in hasToBeUnlocked)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.SecretRecipeBook.RowId > 0);
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.SecretRecipeBook.RowId == 0);
                        }
                    }
                }
            }

            if (alreadyCrafted.Any(x => x.Value))
            {
                foreach (var v in alreadyCrafted)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => P.ri.HasRecipeCrafted(x.RowId));
                        }
                        else
                        {
                            recipes.RemoveAll(x => !P.ri.HasRecipeCrafted(x.RowId));
                        }
                    }
                }
            }

            if (countsToLog.Any(x => x.Value))
            {
                foreach (var v in countsToLog)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.RecipeNotebookList.RowId < 1000);
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.RecipeNotebookList.RowId > 1000);
                        }
                    }
                }
            }

            if (isExpert.Any(x => x.Value))
            {
                foreach (var v in isExpert)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.IsExpert);
                        }
                        else
                        {
                            recipes.RemoveAll(x => !x.IsExpert);
                        }
                    }
                }
            }

            if (questRecipe.Any(x => x.Value))
            {
                foreach (var v in questRecipe)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.Quest.RowId > 0);
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.Quest.RowId == 0);
                        }
                    }
                }
            }

            if (isSecondary.Any(x => x.Value))
            {
                foreach (var v in isSecondary)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.IsSecondary);
                        }
                        else
                        {
                            recipes.RemoveAll(x => !x.IsSecondary);
                        }
                    }
                }
            }

            if (isCollectable.Any(x => x.Value))
            {
                foreach (var v in isCollectable)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.ItemResult.Value.AlwaysCollectable);
                        }
                        if (v.Key == 2)
                        {
                            recipes.RemoveAll(x => !x.ItemResult.Value.AlwaysCollectable);
                        }
                    }
                }
            }

            if (isHQAble.Any(x => x.Value))
            {
                foreach (var v in isHQAble)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.CanHq);
                        }
                        if (v.Key == 2)
                        {
                            recipes.RemoveAll(x => !x.CanHq);
                        }
                    }
                }
            }

            if (Yields.Any(x => x.Value))
            {
                foreach (var v in Yields)
                {
                    if (!v.Value)
                    {
                        recipes.RemoveAll(x => x.AmountResult == v.Key);
                    }
                }
            }

            if (Stars.Any(x => x.Value))
            {
                foreach (var v in Stars)
                {
                    if (!v.Value)
                    {
                        recipes.RemoveAll(x => x.RecipeLevelTable.Value.Stars == v.Key.Length);
                    }
                }
            }

            if (!string.IsNullOrEmpty(Contains))
            {
                Regex regex = new Regex(Contains);
                recipes.RemoveAll(x => !regex.IsMatch(x.ItemResult.Value.Name.ToDalamudString().ToString()));
            }

            if (recipes.Count == 0)
            {
                Notify.Error("没有物品满足你的清单要求。");
                return false;
            }

            if (!withSubcrafts)
            {
                foreach (var recipe in recipes.Distinct())
                {
                    craftingList.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1, ListItemOptions = new() });
                }
                CraftingListHelpers.TidyUpList(craftingList);
                craftingList.SetID();
                craftingList.Save(true);
            }
            else
            {
                foreach (var recipe in recipes.Distinct())
                {
                    Svc.Log.Debug($"{recipe.RowId.NameOfRecipe()}");
                    CraftingListUI.AddAllSubcrafts(recipe, craftingList, 1);
                    if (craftingList.Recipes.Any(x => x.ID == recipe.RowId))
                    {
                        craftingList.Recipes.First(x => x.ID == recipe.RowId).Quantity++;
                    }
                    else
                    {
                        craftingList.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1, ListItemOptions = new() });
                    }
                }
                CraftingListHelpers.TidyUpList(craftingList);
                craftingList.SetID();
                craftingList.Save(true);
            }

            return true;
        }
    }
}
