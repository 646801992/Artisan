using Artisan.CraftingLists;
using Artisan.RawInformation;
using ClickLib.Clicks;
using Dalamud.Logging;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe class RepairManager
    {
        internal static void Repair()
        {
            if (TryGetAddonByName<AddonRepair>("修理", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled && Throttler.Throttle(500))
            {
                new ClickRepair((IntPtr)addon).RepairAll();
            }
        }

        internal static void ConfirmYesNo()
        {
            if(TryGetAddonByName<AddonRepair>("修理", out var r) && 
                r->AtkUnitBase.IsVisible && TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) && 
                addon->AtkUnitBase.IsVisible && 
                addon->YesButton->IsEnabled && 
                addon->AtkUnitBase.UldManager.NodeList[15]->IsVisible && 
                Throttler.Throttle(500))
            {
                new ClickSelectYesNo((IntPtr)addon).Yes();
            }
        }

        internal static int GetMinEquippedPercent()
        {
            var ret = int.MaxValue;
            var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            for(var i  = 0; i < equipment->Size; i++)
            {
                var item = equipment->GetInventorySlot(i);
                if (item->Condition < ret) ret = item->Condition;
            }
            return (ret / 300);
        }

        internal static bool ProcessRepair(bool use = true, CraftingList? CraftingList = null)
        {
            int repairPercent = CraftingList != null ? CraftingList.RepairPercent : Service.Configuration.RepairPercent;
            if (GetMinEquippedPercent() >= repairPercent)
            {
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("状态良好");
                if (TryGetAddonByName<AddonRepair>("修理", out var r) && r->AtkUnitBase.IsVisible)
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("修理可见");
                    if (Throttler.Throttle(500))
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("关闭修复窗口");
                        Hotbars.actionManager->UseAction(ActionType.General, 6);
                    }
                    return false;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("return true");
                return true;
            }
            else
            {
                if (AutocraftDebugTab.Debug) PluginLog.Verbose($"状态不佳，状态是 {GetMinEquippedPercent()}, 设定是 {Service.Configuration.RepairPercent}");
                if (use)
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose($"执行修理");
                    if (TryGetAddonByName<AddonRepair>("修理", out var r) && r->AtkUnitBase.IsVisible)
                    {
                        //PluginLog.Verbose($"Repair visible");
                        ConfirmYesNo();
                        Repair();
                    }
                    else
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose($"修理不可见");
                        if (Throttler.Throttle(500))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose($"打开修理窗口");
                            Hotbars.actionManager->UseAction(ActionType.General, 6);
                        }
                    }
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose($"Returning false");
                return false;
            }
        }
    }
}
