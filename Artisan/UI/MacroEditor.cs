using Artisan.RawInformation.Character;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using Artisan.RawInformation;
using Newtonsoft.Json;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.CraftingLogic;
using System.Windows.Forms;

namespace Artisan.UI
{
    internal class MacroEditor : Window
    {
        private MacroSolverSettings.Macro SelectedMacro;
        private bool renameMode = false;
        private string renameMacro = "";
        private int selectedStepIndex = -1;
        private bool Raweditor = false;
        private static string _rawMacro = string.Empty;

        public MacroEditor(MacroSolverSettings.Macro macro) : base($"生产宏编辑器###{macro.ID}", ImGuiWindowFlags.None)
        {
            SelectedMacro = macro;
            selectedStepIndex = macro.Steps.Count - 1;
            this.IsOpen = true;
            P.ws.AddWindow(this);
            this.Size = new Vector2(600, 600);
            this.SizeCondition = ImGuiCond.Appearing;
            ShowCloseButton = true;

            Crafting.CraftStarted += OnCraftStarted;
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

        public override void OnClose()
        {
            Crafting.CraftStarted -= OnCraftStarted;
            base.OnClose();
            P.ws.RemoveWindow(this);
        }

        public override void Draw()
        {
            if (SelectedMacro.ID != 0)
            {
                if (!renameMode)
                {
                    ImGui.TextUnformatted($"选择生产宏: {SelectedMacro.Name}");
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                    {
                        renameMode = true;
                    }
                }
                else
                {
                    renameMacro = SelectedMacro.Name!;
                    if (ImGui.InputText("", ref renameMacro, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        SelectedMacro.Name = renameMacro;
                        P.Config.Save();

                        renameMode = false;
                        renameMacro = String.Empty;
                    }
                }
                if (ImGui.Button("删除宏 (按住 Ctrl)") && ImGui.GetIO().KeyCtrl)
                {
                    P.Config.MacroSolverConfig.Macros.Remove(SelectedMacro);
                    foreach (var e in P.Config.RecipeConfigs)
                        if (e.Value.SolverType == typeof(MacroSolverDefinition).FullName && e.Value.SolverFlavour == SelectedMacro.ID)
                            P.Config.RecipeConfigs.Remove(e.Key); // TODO: do we want to preserve other configs?..
                    P.Config.Save();
                    SelectedMacro = new();
                    selectedStepIndex = -1;

                    this.IsOpen = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("切换编辑器"))
                {
                    _rawMacro = string.Join("\r\n", SelectedMacro.Steps.Select(x => $"{x.Action.NameOfAction()}"));
                    Raweditor = !Raweditor;
                }

                ImGui.SameLine();
                var exportButton = ImGuiHelpers.GetButtonSize("导出宏");
                ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - exportButton.X);

                if (ImGui.Button("导出宏###ExportButton"))
                {
                    Clipboard.SetText(JsonConvert.SerializeObject(SelectedMacro));
                    Notify.Success("生产宏已复制到剪贴板。");
                }

                ImGui.Spacing();
                if (ImGui.Checkbox("如果品质达到 100%，则跳过品质技能", ref SelectedMacro.Options.SkipQualityIfMet))
                {
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("一旦品质达到 100%，宏将跳过与品质相关的所有技能，包括增益。");
                ImGui.SameLine();
                if (ImGui.Checkbox("若状态不是黑球低品质则跳过观察", ref SelectedMacro.Options.SkipObservesIfNotPoor))
                {
                    P.Config.Save();
                }


                if (ImGui.Checkbox("提升品质技能", ref SelectedMacro.Options.UpgradeQualityActions))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果你获得高品质或最高品质的状态并且宏处于提高品质的步骤中（不包括比尔格的祝福），那么它会将技能升级为集中加工。");
                ImGui.SameLine();

                if (ImGui.Checkbox("升级进展技能", ref SelectedMacro.Options.UpgradeProgressActions))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果你获得高品质或最高品质的状态并且宏处于提高进展的步骤中，那么它会将技能升级为集中制作。");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("最低作业精度", ref SelectedMacro.Options.MinCraftsmanship))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果在选择此宏的情况下你未达到最低作业精度，Artisan 将不会开始制作。");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("最低加工精度", ref SelectedMacro.Options.MinControl))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果在选择此宏的情况下你未达到最低加工精度，Artisan 将不会开始制作。");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("最低制作力", ref SelectedMacro.Options.MinCP))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果在选择此宏的情况下你未达到最低制作力，Artisan 将不会开始制作。");

                if (!Raweditor)
                {
                    if (ImGui.Button($"插入新技能 ({Skills.BasicSynthesis.NameOfAction()})"))
                    {
                        SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = Skills.BasicSynthesis });
                        ++selectedStepIndex;
                        P.Config.Save();
                    }

                    if (selectedStepIndex >= 0)
                    {
                        if (ImGui.Button($"插入新技能 - 与上一个相同 ({SelectedMacro.Steps[selectedStepIndex].Action.NameOfAction()})"))
                        {
                            SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = SelectedMacro.Steps[selectedStepIndex].Action });
                            ++selectedStepIndex;
                            P.Config.Save();
                        }
                    }
                    

                    ImGui.Columns(2, "actionColumns", true);
                    ImGui.SetColumnWidth(0, 220f.Scale());
                    ImGuiEx.ImGuiLineCentered("###MacroActions", () => ImGuiEx.TextUnderlined("宏 技 能"));
                    ImGui.Indent();
                    for (int i = 0; i < SelectedMacro.Steps.Count; i++)
                    {
                        var step = SelectedMacro.Steps[i];
                        var selectedAction = ImGui.Selectable($"{i + 1}. {(step.Action == Skills.None ? "Artisan 建议" : step.Action.NameOfAction())}###selectedAction{i}", i == selectedStepIndex);
                        if (selectedAction)
                            selectedStepIndex = i;
                    }
                    ImGui.Unindent();
                    if (selectedStepIndex >= 0)
                    {
                        var step = SelectedMacro.Steps[selectedStepIndex];

                        ImGui.NextColumn();
                        ImGuiEx.CenterColumnText($"选择技能: {(step.Action == Skills.None ? "Artisan 建议" : step.Action.NameOfAction())}", true);
                        if (selectedStepIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
                            {
                                selectedStepIndex--;
                            }
                        }

                        if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight))
                            {
                                selectedStepIndex++;
                            }
                        }

                        ImGui.Dummy(new Vector2(0, 0));
                        ImGui.SameLine();
                        if (ImGui.Checkbox($"跳过此技能的升级", ref step.ExcludeFromUpgrade))
                            P.Config.Save();

                        ImGui.Spacing();
                        ImGuiEx.CenterColumnText($"跳过这些状态条件", true);

                        ImGui.BeginChild("ConditionalExcludes", new Vector2(ImGui.GetContentRegionAvail().X, 80f.Scale()), false, ImGuiWindowFlags.AlwaysAutoResize);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                        ImGui.Columns(3, null, false);
                        if (ImGui.Checkbox($"通常", ref step.ExcludeNormal))
                            P.Config.Save();
                        if (ImGui.Checkbox($"低品质", ref step.ExcludePoor))
                            P.Config.Save();
                        if (ImGui.Checkbox($"高品质", ref step.ExcludeGood))
                            P.Config.Save();
                        if (ImGui.Checkbox($"最高品质", ref step.ExcludeExcellent))
                            P.Config.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"安定", ref step.ExcludeCentered))
                            P.Config.Save();
                        if (ImGui.Checkbox($"结实", ref step.ExcludeSturdy))
                            P.Config.Save();
                        if (ImGui.Checkbox($"高效", ref step.ExcludePliant))
                            P.Config.Save();
                        if (ImGui.Checkbox($"大进展", ref step.ExcludeMalleable))
                            P.Config.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"长持续", ref step.ExcludePrimed))
                            P.Config.Save();
                        if (ImGui.Checkbox($"好兆头", ref step.ExcludeGoodOmen))
                            P.Config.Save();

                        ImGui.Columns(1);
                        ImGui.PopStyleVar();
                        ImGui.EndChild();
                        if (ImGui.Button("删除技能 (按住 Ctrl)") && ImGui.GetIO().KeyCtrl)
                        {
                            SelectedMacro.Steps.RemoveAt(selectedStepIndex);
                            P.Config.Save();
                            if (selectedStepIndex == SelectedMacro.Steps.Count)
                                selectedStepIndex--;
                        }

                        if (ImGui.BeginCombo("###ReplaceAction", "替换技能"))
                        {
                            if (ImGui.Selectable($"Artisan 建议"))
                            {
                                step.Action = Skills.None;
                                P.Config.Save();
                            }

                            ImGuiComponents.HelpMarker("使用合适的默认求解器推荐，例如标准配方求解器用于常规配方，专家配方求解器用于专家配方");

                            if (ImGui.Selectable($"加工连击"))
                            {
                                step.Action = Skills.TouchCombo;
                                P.Config.Save();
                            }

                            ImGuiComponents.HelpMarker("此设置将使用合适的三步加工连击步骤，具体取决于上一次实际使用的技能。这对于提升品质或在特定条件下跳过技能非常有用。");

                            ImGui.Separator();

                            foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>().OrderBy(SheetExtensions.NameOfAction))
                            {
                                if (ImGui.Selectable(opt.NameOfAction()))
                                {
                                    step.Action = opt;
                                    P.Config.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.Text("重新排序技能");
                        if (selectedStepIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                            {
                                SelectedMacro.Steps.Reverse(selectedStepIndex - 1, 2);
                                selectedStepIndex--;
                                P.Config.Save();
                            }
                        }

                        if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                        {
                            ImGui.SameLine();
                            if (selectedStepIndex == 0)
                            {
                                ImGui.Dummy(new Vector2(22));
                                ImGui.SameLine();
                            }

                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                            {
                                SelectedMacro.Steps.Reverse(selectedStepIndex, 2);
                                selectedStepIndex++;
                                P.Config.Save();
                            }
                        }

                    }
                    ImGui.Columns(1);
                }
                else
                {
                    ImGui.Text($"生产宏技能（每个技能一行）");
                    ImGuiComponents.HelpMarker("你可以像游戏内用户宏一样直接复制/粘贴宏，也可以将每个技能逐行列出。\n例如:\n/ac 坚信\n\n等效于\n\n坚信\n\n你还可以使用 * (星号) 或 'Artisan 推荐' 来将 Artisan 的推荐技能作为宏中的一步。");
                    ImGui.InputTextMultiline("###MacroEditor", ref _rawMacro, 10000000, new Vector2(ImGui.GetContentRegionAvail().X - 30f, ImGui.GetContentRegionAvail().Y - 30f));
                    if (ImGui.Button("保存"))
                    {
                        var steps = MacroUI.ParseMacro(_rawMacro);
                        if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                        {
                            SelectedMacro.Steps = steps;
                            P.Config.Save();
                            DuoLog.Information($"生产宏已更新");
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("保存并关闭"))
                    {
                        var steps = MacroUI.ParseMacro(_rawMacro);
                        if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                        {
                            SelectedMacro.Steps = steps;
                            P.Config.Save();
                            DuoLog.Information($"生产宏已更新");
                        }

                        Raweditor = !Raweditor;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("关闭"))
                    {
                        Raweditor = !Raweditor;
                    }
                }


                ImGuiEx.ImGuiLineCentered("MTimeHead", delegate
                {
                    ImGuiEx.TextUnderlined($"预估生产宏长度");
                });
                ImGuiEx.ImGuiLineCentered("MTimeArtisan", delegate
                {
                    ImGuiEx.Text($"Artisan: {MacroUI.GetMacroLength(SelectedMacro)} s");
                });
                ImGuiEx.ImGuiLineCentered("MTimeTeamcraft", delegate
                {
                    ImGuiEx.Text($"普通生产宏: {MacroUI.GetTeamcraftMacroLength(SelectedMacro)} s");
                });
            }
            else
            {
                selectedStepIndex = -1;
            }
        }

        private void OnCraftStarted(Lumina.Excel.GeneratedSheets.Recipe recipe, CraftState craft, StepState initialStep, bool trial) => IsOpen = false;
    }
}
