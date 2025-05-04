using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.FCWorkshops;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using PunishLib.ImGuiMethods;
using System;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using ThreadLoadImageHandler = ECommons.ImGuiMethods.ThreadLoadImageHandler;

namespace Artisan.UI
{
    unsafe internal class PluginUI : Window
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;


        private bool visible = false;
        public OpenWindow OpenWindow { get; set; }

        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private bool craftingVisible = false;
        public bool CraftingVisible
        {
            get { return this.craftingVisible; }
            set { if (this.craftingVisible != value) CraftingWindowStateChanged?.Invoke(this, value); this.craftingVisible = value; }
        }

        public PluginUI() : base($"{P.Name} {P.GetType().Assembly.GetName().Version}###Artisan")
        {
            this.RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new(250, 100),
                MaximumSize = new(9999, 9999)
            };
            P.ws.AddWindow(this);
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

        public void Dispose()
        {

        }

        public override void Draw()
        {
            if (DalamudInfo.IsOnStaging())
            {
                ImGui.Text($"Artisan 不适用于非 release 版本的 Dalamud。请在聊天框中输入 /xlbranch，然后选择 'release'，并点击 'Pick & Restart'以重启并切换到 release 版本");
                return;
            }

            var region = ImGui.GetContentRegionAvail();
            var itemSpacing = ImGui.GetStyle().ItemSpacing;

            var topLeftSideHeight = region.Y;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f.Scale(), 0));
            try
            {
                ShowEnduranceMessage();

                using (var table = ImRaii.Table($"ArtisanTableContainer", 2, ImGuiTableFlags.Resizable))
                {
                    if (!table)
                        return;

                    ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                    ImGui.TableNextColumn();

                    var regionSize = ImGui.GetContentRegionAvail();

                    ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                    using (var leftChild = ImRaii.Child($"###ArtisanLeftSide", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                    {
                        var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan-icon.png");

                        if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                        {
                            ImGuiEx.LineCentered("###ArtisanLogo", () =>
                            {
                                ImGui.Image(logo.ImGuiHandle, new(125f.Scale(), 125f.Scale()));
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"您是第 69 位发现这个秘密的人，太棒了！");
                                    ImGui.EndTooltip();
                                }
                            });

                        }
                        ImGui.Spacing();
                        ImGui.Separator();

                        if (ImGui.Selectable("主页", OpenWindow == OpenWindow.Overview))
                        {
                            OpenWindow = OpenWindow.Overview;
                        }
                        if (ImGui.Selectable("设置", OpenWindow == OpenWindow.Main))
                        {
                            OpenWindow = OpenWindow.Main;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("续航模式", OpenWindow == OpenWindow.Endurance))
                        {
                            OpenWindow = OpenWindow.Endurance;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("生产宏", OpenWindow == OpenWindow.Macro))
                        {
                            OpenWindow = OpenWindow.Macro;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("制作清单", OpenWindow == OpenWindow.Lists))
                        {
                            OpenWindow = OpenWindow.Lists;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("清单生成器", OpenWindow == OpenWindow.SpecialList))
                        {
                            OpenWindow = OpenWindow.SpecialList;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("部队合建", OpenWindow == OpenWindow.FCWorkshop))
                        {
                            OpenWindow = OpenWindow.FCWorkshop;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("模拟器", OpenWindow == OpenWindow.Simulator))
                        {
                            OpenWindow = OpenWindow.Simulator;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("关于", OpenWindow == OpenWindow.About))
                        {
                            OpenWindow = OpenWindow.About;
                        }


#if DEBUG
                        ImGui.Spacing();
                        if (ImGui.Selectable("DEBUG", OpenWindow == OpenWindow.Debug))
                        {
                            OpenWindow = OpenWindow.Debug;
                        }
                        ImGui.Spacing();
#endif

                    }

                    ImGui.PopStyleVar();
                    ImGui.TableNextColumn();
                    using (var rightChild = ImRaii.Child($"###ArtisanRightSide", Vector2.Zero, false))
                    {
                        switch (OpenWindow)
                        {
                            case OpenWindow.Main:
                                DrawMainWindow();
                                break;
                            case OpenWindow.Endurance:
                                Endurance.Draw();
                                break;
                            case OpenWindow.Lists:
                                CraftingListUI.Draw();
                                break;
                            case OpenWindow.About:
                                AboutTab.Draw("Artisan");
                                break;
                            case OpenWindow.Debug:
                                DebugTab.Draw();
                                break;
                            case OpenWindow.Macro:
                                MacroUI.Draw();
                                break;
                            case OpenWindow.FCWorkshop:
                                FCWorkshopUI.Draw();
                                break;
                            case OpenWindow.SpecialList:
                                SpecialLists.Draw();
                                break;
                            case OpenWindow.Overview:
                                DrawOverview();
                                break;
                            case OpenWindow.Simulator:
                                SimulatorUI.Draw();
                                break;
                            case OpenWindow.None:
                                break;
                            default:
                                break;
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
            ImGui.PopStyleVar();
        }

        private void DrawOverview()
        {
            var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
            {
                ImGuiEx.LineCentered("###ArtisanTextLogo", () =>
                {
                    ImGui.Image(logo.ImGuiHandle, new Vector2(logo.Width, 100f.Scale()));
                });
            }

            ImGuiEx.LineCentered("###ArtisanOverview", () =>
            {
                ImGuiEx.TextUnderlined("Artisan - 主页");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"首先，感谢你下载我的小型制作插件。我从2022年6月开始一直在开发 Artisan，这是我最引以为豪的作品。");
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"在你开始使用 Artisan 之前，我们需要先了解一下插件的工作方式。一旦理解了几个关键因素，Artisan 会变得非常容易上手。");

            ImGui.Spacing();
            ImGuiEx.LineCentered("###ArtisanModes", () =>
            {
                ImGuiEx.TextUnderlined("制作模式");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan 具有一个 \"自动执行模式\" ，它会根据生成的建议自动执行操作。" +
                                " 默认情况下，该模式的执行速度与游戏允许的最快速度一致，比普通生产宏更快。" +
                                " 这样做并不会绕过任何游戏限制，不过你可以选择设置延迟。" +
                                " 启用该模式不会影响 Artisan 默认使用的建议生成过程。");

            var automode = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/AutoMode.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(automode, out var example))
            {
                ImGuiEx.LineCentered("###AutoModeExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"如果你未启用自动模式，则可以使用另外两种模式： \"半手动模式\" and \"全手动模式\"。" +
                                $"当你开始制作时， \"半手动模式\" 会以一个小弹窗的形式出现。");

            var craftWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/ThemeCraftingWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(craftWindowExample, out example))
            {
                ImGuiEx.LineCentered("###CraftWindowExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"点击 \"执行建议操作\" 按钮，即指示插件执行它推荐的操作。" +
                $" 这被视为半手动模式，因为你仍需要手动控制每一个操作，但不用在热键栏上寻找技能。" +
                $" \"全手动\" 模式则是通过正常方式在热键栏上按下技能来执行操作。" +
                $" 默认情况下，Artisan 会帮助你，如果操作已在快捷栏中放置，插件会将其高亮显示（可以在设置中禁用此功能）。");

            var outlineExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/OutlineExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(outlineExample, out example))
            {
                ImGuiEx.LineCentered("###OutlineExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGui.Spacing();
            ImGuiEx.LineCentered("###ArtisanSuggestions", () =>
            {
                ImGuiEx.TextUnderlined("求解器/生产宏");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"默认情况下，Artisan 会为你提供下一步制作步骤的建议。不过，这个求解器并不完美，绝对不能用来取代合适的生产装备。" +
                $"你无需进行任何设置，只要启用 Artisan 就能获得这些制作建议。" +
                $"\r\n\r\n" +
                $"如果你尝试的制作超出了默认求解器的能力范围，Artisan 允许你创建生产宏，宏可以用作建议来替代默认求解器。" +
                $"Artisan 宏的优点是长度不受限制，可以以游戏允许的最快速度执行，并且还允许在运行时进行一些额外的调整。");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"单击此处进入生产宏菜单。");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Macro;
            }
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"创建生产宏后，你需要将其分配给一个制作配方，这可以通过配方窗口的下拉菜单轻易完成。\n默认情况下，该菜单固定于游戏内制作笔记窗口的右上角，但可以在设置中解除固定。");


            var recipeWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/RecipeWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(recipeWindowExample, out example))
            {
                ImGuiEx.LineCentered("###RecipeWindowExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }


            ImGuiEx.TextWrapped($"从下拉框中选择你创建的生产宏。" +
                $"当你去制作该物品时，步骤建议将会被你选择的生产宏内容所替代。");


            ImGui.Spacing();
            ImGuiEx.LineCentered("###Endurance", () =>
            {
                ImGuiEx.TextUnderlined("续航模式");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan 有一个名为 \"续航模式\" 的模式，这实际上是 \"自动重复模式\" 的一种更高级的说法，它会不断尝试为你制作同一物品。" +
                $"续航模式通过从游戏内制作笔记中选择配方并启用该功能来工作。" +
                $"你的角色将尽可能多地制作该物品，直到你用完材料为止。" +
                $"\r\n\r\n" +
                $"其他功能应该也相对简单易懂，因为续航模式还可以管理在制作之间使用的食物、药水、工程学指南、修理装备和精制魔晶石。" +
                $"修理功能仅支持自己使用暗物质进行修理，不支持找修理 NPC。");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"单击此处进入续航模式菜单。");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Endurance;
            }

            ImGui.Spacing();
            ImGuiEx.LineCentered("###Lists", () =>
            {
                ImGuiEx.TextUnderlined("制作清单");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan 还可以创建一个物品清单，并自动开始逐个制作这些物品。" +
                $"制作清单具有许多强大的工具，以简化从材料到最终产品的过程。" +
                $"它还支持 Teamcraft 的导入和导出。");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"单击此处进入制作清单菜单。");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Lists;
            }

            ImGui.Spacing();
            ImGuiEx.LineCentered("###Questions", () =>
            {
                ImGuiEx.TextUnderlined("还有问题？");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"如果你对这里没有列出的内容有疑问，你可以在我们的");
            ImGui.SameLine(ImGui.GetCursorPosX(), 1.5f);
            ImGuiEx.TextUnderlined($"Discord 服务器");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked())
                {
                    Util.OpenLink("https://discord.gg/Zzrcc8kmvy");
                }
            }
            ImGui.SameLine(ImGui.GetCursorPosX(), 1.5f);
            ImGuiEx.TextWrapped($"提出来。");

            ImGuiEx.TextWrapped($"你也可以在我们的");
            ImGui.SameLine(ImGui.GetCursorPosX(), 2f);
            ImGuiEx.TextUnderlined($"Github 页面");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                if (ImGui.IsItemClicked())
                {
                    Util.OpenLink("https://github.com/PunishXIV/Artisan");
                }
            }
            ImGui.SameLine(ImGui.GetCursorPosX(), 2f);
            ImGuiEx.TextWrapped($"提交 Issues.");

        }

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"在这里，你可以更改 Artisan 将使用的一些设置。其中一些也可以在制作过程中进行切换。");
            ImGui.TextWrapped($"为了使用 Artisan 的手动技能高亮功能，请将你解锁的每个制作技能放置到可见的热键栏中。");
            bool autoEnabled = P.Config.AutoMode;
            bool delayRec = P.Config.DelayRecommendation;
            bool failureCheck = P.Config.DisableFailurePrediction;
            int maxQuality = P.Config.MaxPercentage;
            bool useTricksGood = P.Config.UseTricksGood;
            bool useTricksExcellent = P.Config.UseTricksExcellent;
            bool useSpecialist = P.Config.UseSpecialist;
            //bool showEHQ = P.Config.ShowEHQ;
            //bool useSimulated = P.Config.UseSimulatedStartingQuality;
            bool disableGlow = P.Config.DisableHighlightedAction;
            bool disableToasts = P.Config.DisableToasts;

            ImGui.Separator();

            if (ImGui.CollapsingHeader("常规设置"))
            {
                if (ImGui.Checkbox("自动执行模式", ref autoEnabled))
                {
                    P.Config.AutoMode = autoEnabled;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker($"自动使用每个推荐的操作。");
                if (autoEnabled)
                {
                    var delay = P.Config.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("执行延迟 (ms)###ActionDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        P.Config.AutoDelay = delay;
                        P.Config.Save();
                    }
                }

                if (ImGui.Checkbox("延迟获取建议", ref delayRec))
                {
                    P.Config.DelayRecommendation = delayRec;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("如果你在应该触发最终评估时遇到问题，请使用此选项。");

                if (delayRec)
                {
                    var delay = P.Config.RecommendationDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("设置延迟 (ms)###RecommendationDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        P.Config.RecommendationDelay = delay;
                        P.Config.Save();
                    }
                }

                bool requireFoodPot = P.Config.AbortIfNoFoodPot;
                if (ImGui.Checkbox("强制使用消耗品", ref requireFoodPot))
                {
                    P.Config.AbortIfNoFoodPot = requireFoodPot;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("如果找不到配置的食物、工程系指南或药品，Artisan 将要求去使用这些消耗品，并拒绝进行制作。");

                if (ImGui.Checkbox("在制作练习中使用消耗品", ref P.Config.UseConsumablesTrial))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox("在简易制作作业中使用消耗品", ref P.Config.UseConsumablesQuickSynth))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox($"优先考虑 NPC 修理装备而非自己修理", ref P.Config.PrioritizeRepairNPC))
                {
                    P.Config.Save();
                }

                ImGuiComponents.HelpMarker("在修理装备时，如果附近有修理 NPC，它将优先选择让他们修理，而不是自己修理。如果找不到 NPC，且你具备修理所需的等级，仍会尝试进行自己修理。");

                if (ImGui.Checkbox($"如果无法修理装备，则禁用续航模式", ref P.Config.DisableEnduranceNoRepair))
                    P.Config.Save();

                ImGuiComponents.HelpMarker($"一旦达到修理阈值，如果你无法自己修理或通过 NPC 修理，则禁用续航模式。");

                if (ImGui.Checkbox($"如果无法修理装备，则暂停制作清单", ref P.Config.DisableListsNoRepair))
                    P.Config.Save();

                ImGuiComponents.HelpMarker($"一旦达到修理阈值，如果你无法自己修理或通过 NPC 修理，则暂停当前的制作清单。");

                bool requestStop = P.Config.RequestToStopDuty;
                bool requestResume = P.Config.RequestToResumeDuty;
                int resumeDelay = P.Config.RequestToResumeDelay;

                if (ImGui.Checkbox("当任务搜索器准备就绪时，让 Artisan 关闭续航模式 / 暂停制作清单。", ref requestStop))
                {
                    P.Config.RequestToStopDuty = requestStop;
                    P.Config.Save();
                }

                if (requestStop)
                {
                    if (ImGui.Checkbox("离开副本后，让 Artisan 恢复续航模式 / 恢复制作清单。", ref requestResume))
                    {
                        P.Config.RequestToResumeDuty = requestResume;
                        P.Config.Save();
                    }

                    if (requestResume)
                    {
                        if (ImGui.SliderInt("延迟恢复 (秒)", ref resumeDelay, 5, 60))
                        {
                            P.Config.RequestToResumeDelay = resumeDelay;
                        }
                    }
                }

                if (ImGui.Checkbox("禁用自动装备生产所需的装备", ref P.Config.DontEquipItems))
                    P.Config.Save();

                if (ImGui.Checkbox("续航模式完成后播放声音", ref P.Config.PlaySoundFinishEndurance))
                    P.Config.Save();

                if (ImGui.Checkbox($"制作清单完成后播放声音", ref P.Config.PlaySoundFinishList))
                    P.Config.Save();

                if (P.Config.PlaySoundFinishEndurance || P.Config.PlaySoundFinishList)
                {
                    if (ImGui.SliderFloat("音量", ref P.Config.SoundVolume, 0f, 1f, "%.2f"))
                        P.Config.Save();
                }
            }
            if (ImGui.CollapsingHeader("生产宏设置"))
            {
                if (ImGui.Checkbox("如果无法使用该步骤，则跳过这一步", ref P.Config.SkipMacroStepIfUnable))
                    P.Config.Save();

                if (ImGui.Checkbox($"在生产宏完成后禁止 Artisan 继续操作", ref P.Config.DisableMacroArtisanRecommendation))
                    P.Config.Save();
            }
            if (ImGui.CollapsingHeader("标准配方求解器设置"))
            {
                if (ImGui.Checkbox($"使用 {Skills.TricksOfTrade.NameOfAction()} - {LuminaSheets.AddonSheet[227].Text.RawString}", ref useTricksGood))
                {
                    P.Config.UseTricksGood = useTricksGood;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"使用 {Skills.TricksOfTrade.NameOfAction()} - {LuminaSheets.AddonSheet[228].Text.RawString}", ref useTricksExcellent))
                {
                    P.Config.UseTricksExcellent = useTricksExcellent;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker($"这两个选项允许你在条件为 {LuminaSheets.AddonSheet[227].Text.RawString} 或 {LuminaSheets.AddonSheet[228].Text.RawString} 时优先使用 {Skills.TricksOfTrade.NameOfAction()} 。\n\n这将取代 {Skills.PreciseTouch.NameOfAction()} 和 {Skills.IntensiveSynthesis.NameOfAction()} 的使用。\n\n无论设置如何，{Skills.TricksOfTrade.NameOfAction()} 仍然会在学习这些技能之前或在某些情况下使用。");
                if (ImGui.Checkbox("使用专家技能", ref useSpecialist))
                {
                    P.Config.UseSpecialist = useSpecialist;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("如果当前职业是专家，将消耗你拥有的能工巧匠图纸。\n设计变动取代观察。\n专心致志会在集中加工前使用。");
                ImGui.TextWrapped("最高品质%%");
                ImGuiComponents.HelpMarker($"一旦品质达到以下百分比，Artisan 将只专注于进展。");
                if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"%d%%"))
                {
                    P.Config.MaxPercentage = maxQuality;
                    P.Config.Save();
                }

                ImGui.Text($"收藏品价值阈值");
                ImGuiComponents.HelpMarker("一旦收藏品品质达到阈值，求解器将停止追求品质。");

                if (ImGui.RadioButton($"最小", P.Config.SolverCollectibleMode == 1))
                {
                    P.Config.SolverCollectibleMode = 1;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"中间", P.Config.SolverCollectibleMode == 2))
                {
                    P.Config.SolverCollectibleMode = 2;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"最大", P.Config.SolverCollectibleMode == 3))
                {
                    P.Config.SolverCollectibleMode = 3;
                    P.Config.Save();
                }

                if (ImGui.Checkbox($"使用品质起手 ({Skills.Reflect.NameOfAction()})", ref P.Config.UseQualityStarter))
                    P.Config.Save();
                ImGuiComponents.HelpMarker($"在耐久度较低的配方中，这通常更为有利。");

                //if (ImGui.Checkbox("Low Stat Mode", ref P.Config.LowStatsMode))
                //    P.Config.Save();

                //ImGuiComponents.HelpMarker("This swaps out Waste Not II & Groundwork for Prudent Synthesis");

                ImGui.TextWrapped($"{Skills.PreparatoryTouch.NameOfAction()} - 最高 {Buffs.InnerQuiet.NameOfBuff()} 层数");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker($"将只使用 {Skills.PreparatoryTouch.NameOfAction()} 来叠加 {Buffs.InnerQuiet.NameOfBuff()} 层数，这对调整节约制作力很有用。");
                if (ImGui.SliderInt($"###MaxIQStacksPrepTouch", ref P.Config.MaxIQPrepTouch, 0, 10))
                    P.Config.Save();


            }
            bool openExpert = false;
            if (ImGui.CollapsingHeader("专家配方求解器设置"))
            {
                openExpert = true;
                if (P.Config.ExpertSolverConfig.expertIcon is not null)
                {
                    ImGui.SameLine();
                    ImGui.Image(P.Config.ExpertSolverConfig.expertIcon.ImGuiHandle, new(P.Config.ExpertSolverConfig.expertIcon.Width * ImGuiHelpers.GlobalScaleSafe, ImGui.GetItemRectSize().Y), new(0, 0), new(1, 1), new(0.94f, 0.57f, 0f, 1f));
                }
                if (P.Config.ExpertSolverConfig.Draw())
                    P.Config.Save();
            }
            if (!openExpert)
            {
                if (P.Config.ExpertSolverConfig.expertIcon is not null)
                {
                    ImGui.SameLine();
                    ImGui.Image(P.Config.ExpertSolverConfig.expertIcon.ImGuiHandle, new(P.Config.ExpertSolverConfig.expertIcon.Width * ImGuiHelpers.GlobalScaleSafe, ImGui.GetItemRectSize().Y), new(0, 0), new(1, 1), new(0.94f, 0.57f, 0f, 1f));
                }
            }
            if (ImGui.CollapsingHeader("脚本求解器设置"))
            {
                if (P.Config.ScriptSolverConfig.Draw())
                    P.Config.Save();
            }
            if (ImGui.CollapsingHeader("UI 设置"))
            {
                if (ImGui.Checkbox("禁用高亮边框", ref disableGlow))
                {
                    P.Config.DisableHighlightedAction = disableGlow;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("这是在全手动模式时高亮显示热键栏上生产技能的边框。");

                if (ImGui.Checkbox($"禁用推荐 Toast 提示", ref disableToasts))
                {
                    P.Config.DisableToasts = disableToasts;
                    P.Config.Save();
                }

                ImGuiComponents.HelpMarker("这些是每当推荐新操作时出现的弹出提示。");

                bool lockMini = P.Config.LockMiniMenuR;
                if (ImGui.Checkbox("将制作笔记迷你菜单的位置固定在制作笔记上。", ref lockMini))
                {
                    P.Config.LockMiniMenuR = lockMini;
                    P.Config.Save();
                }

                if (!P.Config.LockMiniMenuR)
                {
                    if (ImGui.Checkbox($"固定迷你菜单位置", ref P.Config.PinMiniMenu))
                    {
                        P.Config.Save();
                    }
                }

                if (ImGui.Button("重置迷你菜单位置"))
                {
                    AtkResNodeFunctions.ResetPosition = true;
                }

                if (ImGui.Checkbox($"扩展制作笔记搜索栏功能", ref P.Config.ReplaceSearch))
                {
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker($"在制作笔记中扩展搜索栏，提供即时结果和点击打开配方的功能。");

                bool hideQuestHelper = P.Config.HideQuestHelper;
                if (ImGui.Checkbox($"隐藏任务助手", ref hideQuestHelper))
                {
                    P.Config.HideQuestHelper = hideQuestHelper;
                    P.Config.Save();
                }

                bool hideTheme = P.Config.DisableTheme;
                if (ImGui.Checkbox("禁用自定义主题", ref hideTheme))
                {
                    P.Config.DisableTheme = hideTheme;
                    P.Config.Save();
                }
                ImGui.SameLine();

                if (IconButtons.IconTextButton(FontAwesomeIcon.Clipboard, "复制主题"))
                {
                    Clipboard.SetText("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA");
                    Notify.Success("主题已复制到剪贴板");
                }

                if (ImGui.Checkbox("禁用 Allagan Tools 的清单集成", ref P.Config.DisableAllaganTools))
                    P.Config.Save();

                if (ImGui.Checkbox("禁用 Artisan 上下文菜单选项", ref P.Config.HideContextMenus))
                    P.Config.Save();

                ImGuiComponents.HelpMarker("这些是在制作笔记中右键或按下□键时出现的新选项。");

                ImGui.Indent();
                if (ImGui.CollapsingHeader("模拟器设置"))
                {
                    if (ImGui.Checkbox("隐藏配方窗口模拟器结果", ref P.Config.HideRecipeWindowSimulator))
                        P.Config.Save();

                    if (ImGui.SliderFloat("模拟器技能图标大小", ref P.Config.SimulatorActionSize, 5f, 70f))
                    {
                        P.Config.Save();
                    }
                    ImGuiComponents.HelpMarker("设置在模拟器中出现的技能图标的缩放大小。");

                    if (ImGui.Checkbox("启用手动模式悬停预览", ref P.Config.SimulatorHoverMode))
                        P.Config.Save();

                    if (ImGui.Checkbox($"隐藏技能提示", ref P.Config.DisableSimulatorActionTooltips))
                        P.Config.Save();

                    ImGuiComponents.HelpMarker("在手动模式下，当悬停在技能上时，将不会显示技能提示。");
                }
                ImGui.Unindent();
            }
            if (ImGui.CollapsingHeader("清单设置"))
            {
                ImGui.TextWrapped($"这些设置将在创建制作清单时自动应用。");

                if (ImGui.Checkbox("跳过你已经拥有足够数量的物品", ref P.Config.DefaultListSkip))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox("自动精制魔晶石", ref P.Config.DefaultListMateria))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox("自动修理", ref P.Config.DefaultListRepair))
                {
                    P.Config.Save();
                }

                if (P.Config.DefaultListRepair)
                {
                    ImGui.TextWrapped($"修理阈值：");
                    ImGui.SameLine();
                    if (ImGui.SliderInt("###SliderRepairDefault", ref P.Config.DefaultListRepairPercent, 0, 100, $"%d%%"))
                    {
                        P.Config.Save();
                    }
                }

                if (ImGui.Checkbox("将添加到列表的新物品设置为简易制作", ref P.Config.DefaultListQuickSynth))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox($@"在添加到列表后重置“添加次数", ref P.Config.ResetTimesToAdd))
                    P.Config.Save();

                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("使用上下文菜单的添加次数", ref P.Config.ContextMenuLoops))
                {
                    if (P.Config.ContextMenuLoops <= 0)
                        P.Config.ContextMenuLoops = 1;

                    P.Config.Save();
                }

                ImGui.PushItemWidth(400);
                if (ImGui.SliderFloat("每次制作间的延迟", ref P.Config.ListCraftThrottle2, 0.2f, 2f, "%.1f"))
                {
                    if (P.Config.ListCraftThrottle2 < 0.2f)
                        P.Config.ListCraftThrottle2 = 0.2f;

                    if (P.Config.ListCraftThrottle2 > 2f)
                        P.Config.ListCraftThrottle2 = 2f;

                    P.Config.Save();
                }

                ImGui.Indent();
                if (ImGui.CollapsingHeader("材料表设置"))
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"如果你已经查看过某个清单的材料表，则该清单所有列设置将不会生效。");

                    if (ImGui.Checkbox($@"默认隐藏 ""物品"" 列", ref P.Config.DefaultHideInventoryColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"雇员\" 列", ref P.Config.DefaultHideRetainerColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"剩余所需\" 列", ref P.Config.DefaultHideRemainingColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"来源\" 列", ref P.Config.DefaultHideCraftableColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"可制作数量\" 列", ref P.Config.DefaultHideCraftableCountColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"可用于制作\" 列", ref P.Config.DefaultHideCraftItemsColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"分类\" 列", ref P.Config.DefaultHideCategoryColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"采集区域\" 列", ref P.Config.DefaultHideGatherLocationColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏 \"ID\" 列", ref P.Config.DefaultHideIdColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认开启 \"只显示 HQ 制作\"", ref P.Config.DefaultHQCrafts))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认开启 \"颜色验证\"", ref P.Config.DefaultColourValidation))
                        P.Config.Save();

                    if (ImGui.Checkbox($"从 Universalis 获取价格", ref P.Config.UseUniversalis))
                        P.Config.Save();

                    if (P.Config.UseUniversalis)
                    {
                        if (ImGui.Checkbox($"将 Universalis 限制为只显示当前大区", ref P.Config.LimitUnversalisToDC))
                            P.Config.Save();

                        if (ImGui.Checkbox($"仅按需获取价格", ref P.Config.UniversalisOnDemand))
                            P.Config.Save();

                        ImGuiComponents.HelpMarker("你需要点击一个按钮来获取每个物品的价格。");
                    }
                }

                ImGui.Unindent();
            }
        }

        private void ShowEnduranceMessage()
        {
            if (!P.Config.ViewedEnduranceMessage)
            {
                P.Config.ViewedEnduranceMessage = true;
                P.Config.Save();

                ImGui.OpenPopup("EndurancePopup");

                var windowSize = new Vector2(512 * ImGuiHelpers.GlobalScale,
                    ImGui.GetTextLineHeightWithSpacing() * 13 + 2 * ImGui.GetFrameHeightWithSpacing() * 2f);
                ImGui.SetNextWindowSize(windowSize);
                ImGui.SetNextWindowPos((ImGui.GetIO().DisplaySize - windowSize) / 2);

                using var popup = ImRaii.Popup("EndurancePopup",
                    ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.Modal);
                if (!popup)
                    return;

                ImGui.TextWrapped($@"I have been receiving quite a number of messages regarding ""buggy"" Endurance mode not setting ingredients anymore. As of the previous update, the old functionality of Endurance has been moved to a new setting.");
                ImGui.Dummy(new Vector2(0));

                var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/EnduranceNewSetting.png");

                if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var img))
                {
                    ImGuiEx.ImGuiLineCentered("###EnduranceNewSetting", () =>
                    {
                        ImGui.Image(img.ImGuiHandle, new Vector2(img.Width,img.Height));
                    });
                }

                ImGui.Spacing();

                ImGui.TextWrapped($"This change was made to bring back the very original behaviour of Endurance mode. If you do not care about your ingredient ratio, please make sure to enable Max Quantity Mode.");

                ImGui.SetCursorPosY(windowSize.Y - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y);
                if (ImGui.Button("关闭", -Vector2.UnitX))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    public enum OpenWindow
    {
        None = 0,
        Main = 1,
        Endurance = 2,
        Macro = 3,
        Lists = 4,
        About = 5,
        Debug = 6,
        FCWorkshop = 7,
        SpecialList = 8,
        Overview = 9,
        Simulator = 10,
    }
}
