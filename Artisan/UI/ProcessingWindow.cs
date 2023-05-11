using Artisan.CraftingLists;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Linq;

namespace Artisan.UI
{
    internal class ProcessingWindow : Window
    {
        public ProcessingWindow() : base("Processing List###ProcessingList", ImGuiWindowFlags.AlwaysAutoResize)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
        }

        public override bool DrawConditions()
        {
            if (CraftingListUI.Processing)
                return true;

            return false;  
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
            {
                P.Style.Push();
                ImGui.PushFont(P.CustomFont);
                P.StylePushed = true;
            }
        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                ImGui.PopFont();
                P.StylePushed = false;
            }
        }

        public unsafe override void Draw()
        {
            if (CraftingListUI.Processing)
            {
                Service.Framework.RunOnFrameworkThread(() => CraftingListFunctions.ProcessList(CraftingListUI.selectedList));
                
                if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "打开设置" }))
                {
                    P.PluginUi.Visible = true;
                }

                ImGui.Text($"当前进展: {CraftingListUI.selectedList.Name}");
                ImGui.Separator();
                ImGui.Spacing();
                if (CraftingListUI.CurrentProcessedItem != 0)
                {
                    ImGuiEx.TextV($"尝试制作: {CraftingListUI.FilteredList[CraftingListUI.CurrentProcessedItem].ItemResult.Value.Name.RawString}");
                    ImGuiEx.TextV($"总体进展: {CraftingListFunctions.CurrentIndex + 1} / {CraftingListUI.selectedList.Items.Count}");
                }

                if (!CraftingListFunctions.Paused)
                {
                    if (ImGui.Button("暂停"))
                    {
                        CraftingListFunctions.Paused = true;
                    }
                }
                else
                {
                    if (ImGui.Button("恢复"))
                    {
                        if (CraftingListFunctions.RecipeWindowOpen())
                            CraftingListFunctions.CloseCraftingMenu();

                        Svc.Framework.RunOnTick(() => CraftingListFunctions.OpenRecipeByID(CraftingListUI.CurrentProcessedItem, true), TimeSpan.FromSeconds(1));

                        CraftingListFunctions.Paused = false;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    CraftingListUI.Processing = false;
                }
            }
        }
    }
}
