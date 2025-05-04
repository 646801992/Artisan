using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.Sounds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Game.Text.SeStringHandling;
using System.Linq;

namespace Artisan.Autocraft
{
    // TODO: this should be all moved to appropriate places
    public static class EnduranceCraftWatcher
    {
        public static void Setup()
        {
            Crafting.CraftFinished += OnCraftFinished;
            Crafting.QuickSynthProgress += OnQuickSynthProgress;
            Svc.Chat.ChatMessage += ScanForHQItems;
        }

        private static void ScanForHQItems(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (type == (XivChatType)2242 && Svc.Condition[ConditionFlag.Crafting])
            {
                if (message.Payloads.Any(x => x.Type == PayloadType.Item))
                {
                    var item = (ItemPayload)message.Payloads.First(x => x.Type == PayloadType.Item);
                    if (item.Item.CanBeHq)
                    {
                        if (Endurance.Enable && P.Config.EnduranceStopNQ && !item.IsHQ)
                        {
                            Endurance.Enable = false;
                            Svc.Toasts.ShowError("你制作了一个非 HQ 物品，正在禁用续航模式。");
                            DuoLog.Error("你制作了一个非 HQ 物品，正在禁用续航模式。");
                        }
                    }
                }
            }
        }

        public static void Dispose()
        {
            Crafting.CraftFinished -= OnCraftFinished;
            Crafting.QuickSynthProgress -= OnQuickSynthProgress;
            Svc.Chat.ChatMessage -= ScanForHQItems;
        }

        private static void OnCraftFinished(Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
        {
            Svc.Log.Debug($"制作已结束。");

            if (CraftingListUI.Processing)
            {
                if (!cancelled)
                {
                    Svc.Log.Verbose("进阶制作清单");
                    CraftingListFunctions.CurrentIndex++;
                }
                if (cancelled)
                {
                    CraftingListFunctions.Paused = true;
                    CraftingListFunctions.CLTM.Abort();
                }
            }

            if (Endurance.Enable)
            {
                if (cancelled)
                {
                    Endurance.Enable = false;
                    Svc.Toasts.ShowError("你取消了一个制作，正在禁用续航模式。");
                    DuoLog.Error("你取消了一个制作，正在禁用续航模式。");
                }
                else if (finalStep.Progress < craft.CraftProgress && P.Config.EnduranceStopFail)
                {
                    Endurance.Enable = false;
                    Svc.Toasts.ShowError("你生产失败了一个制作，正在禁用续航模式。");
                    DuoLog.Error("你生产失败了一个制作，正在禁用续航模式。");
                }
                else if (P.Config.CraftingX && P.Config.CraftX > 0)
                {
                    P.Config.CraftX -= 1;
                    if (P.Config.CraftX == 0)
                    {
                        P.Config.CraftingX = false;
                        Endurance.Enable = false;
                        if (P.Config.PlaySoundFinishEndurance)
                            SoundPlayer.PlaySound();
                        DuoLog.Information("制作 X 次已完成。");

                    }
                }
            }
        }

        private static void OnQuickSynthProgress(int cur, int max)
        {
            if (cur == 0)
                return;

            CraftingListFunctions.CurrentIndex++;
            if (P.Config.QuickSynthMode && Endurance.Enable && P.Config.CraftX > 0)
                P.Config.CraftX--;

            if (cur == max)
            {
                Operations.CloseQuickSynthWindow();
            }
        }
    }
}
