﻿using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Interface.Colors;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.Sheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.UI.Tables
{
    internal class IngredientTable : Table<Ingredient>, IDisposable
    {
        private static float _nameColumnWidth = 0;
        private static float _requiredColumnWidth = 80;
        private static float _idColumnWidth = 80;
        private static float _inventoryColumnWidth = 80;
        private static float _retainerColumnWidth = 80;
        private static float _remainingColumnWidth = 100;
        private static float _canCraftColumnWidth = 100;
        private static float _craftableCountColumnWidth = 100;
        private static float _craftItemsColumnWidth = 0;
        private static float _itemCategoryColumnWidth = 0;
        private static float _gatherItemLocationColumWidth = 0;
        private static float _cheapestColumnWidth = 100;
        private static float _numberForSaleWidth = 100;

        public readonly IdColumn _idColumn = new() { Label = "ID" };
        public readonly NameColumn _nameColumn = new() { Label = "物品名称" };
        public readonly RequiredColumn _requiredColumn = new() { Label = "所需" };
        public readonly InventoryCountColumn _inventoryColumn = new() { Label = "库存" };
        public readonly RetainerCountColumn _retainerColumn = new() { Label = "雇员" };
        public readonly RemaingCountColumn _remainingColumn = new() { Label = "剩余所需" };
        public readonly CraftableColumn _craftableColumn = new() { Label = "来源" };
        public readonly CraftableCountColumn _craftableCountColumn = new() { Label = "可制作数量" };
        public readonly CraftItemsColumn _craftItemsColumn = new() { Label = "可用来制作" };
        public readonly ItemCategoryColumn _itemCategoryColumn = new() { Label = "分类" };
        public readonly GatherItemLocationColumn _gatherItemLocationColumn = new() { Label = "采集区域" };
        public readonly CheapestServerColumn _cheapestServerColumn = new() { Label = "最佳购买服务器" };
        public readonly NumberForSaleColumn _numberForSaleColumn = new() { Label = "出售数量（所有服务器）" };

        private static bool GatherBuddy =>
            DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var _, false, true);

        private static bool ItemVendor =>
            DalamudReflector.TryGetDalamudPlugin("ItemVendorLocation", out var _, false, true);

        private static bool MonsterLookup =>
            DalamudReflector.TryGetDalamudPlugin("MonsterLootHunter", out var _, false, true);

        private static bool Marketboard =>
            DalamudReflector.TryGetDalamudPlugin("MarketBoardPlugin", out var _, false, true);

        private static bool Lifestream =>
    DalamudReflector.TryGetDalamudPlugin("Lifestream", out var _, false, true);

        private static unsafe void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);

        public List<Ingredient> ListItems;

        private bool CraftFiltered = false;
        private bool? isOnList = null;

        public IngredientTable(List<Ingredient> ingredientList)
            : base("IngredientTable", ingredientList)
        {
            if (P.Config.DefaultHideInventoryColumn) _inventoryColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideRetainerColumn) _retainerColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideRemainingColumn) _remainingColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCraftableColumn) _craftableColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCraftableCountColumn) _craftableCountColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCraftItemsColumn) _craftItemsColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCategoryColumn) _itemCategoryColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideGatherLocationColumn) _gatherItemLocationColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideIdColumn) _idColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;

            List<Column<Ingredient>> headers = new() { _nameColumn, _requiredColumn, _inventoryColumn, _remainingColumn, _craftableColumn, _craftableCountColumn, _craftItemsColumn, _itemCategoryColumn, _gatherItemLocationColumn, _idColumn };
            if (RetainerInfo.ATools) headers.Insert(3, _retainerColumn);
            if (P.Config.UseUniversalis)
            {
                headers.Insert(headers.Count - 1, _cheapestServerColumn);
                headers.Insert(headers.Count - 1, _numberForSaleColumn);
            }
            this.Headers = headers.ToArray();

            Sortable = true;
            ListItems = ingredientList;
            Flags |= ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable;

            _nameColumn.OnContextMenuRequest += OpenContextMenu;
            _remainingColumn.SourceList = ListItems;

            foreach (var item in Items)
            {
                item.OnRemainingChange += SetFilterDirty;
            }
        }

        private void SetFilterDirty(object? sender, bool e)
        {
            foreach (var item in Items)
            {
                item.AmountUsedForSubcrafts = item.GetSubCraftCount();
            }
            this.FilterDirty = true;
        }

        public void Dispose()
        {
            _nameColumn.OnContextMenuRequest -= OpenContextMenu;

            foreach (var item in Items)
            {
                item.OnRemainingChange -= SetFilterDirty;
            }
        }

        public sealed class NameColumn : ColumnString<Ingredient>
        {
            public NameColumn()
               => Flags |= ImGuiTableColumnFlags.NoHide;

            public override string ToName(Ingredient item)
            {
                return item.Data.Name.ToString();
            }

            public bool ShowColour = false;
            public bool ShowHQOnly = false;

            public override float Width => _nameColumnWidth * ImGuiHelpers.GlobalScale;

            public override void DrawColumn(Ingredient item, int _)
            {
                if (ShowColour)
                {
                    int invAmount = ShowHQOnly && item.CanBeCrafted ? item.InventoryHQ : item.Inventory;
                    int retainerAmount = ShowHQOnly && item.CanBeCrafted ? item.ReainterCountHQ : item.RetainerCount;

                    if (item.CanBeCrafted && retainerAmount + invAmount + item.TotalCraftable >= item.Required)
                    {
                        var color = ImGuiColors.TankBlue;
                        color.W -= 0.6f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }

                    if (retainerAmount + invAmount >= item.Required)
                    {
                        var color = ImGuiColors.DalamudOrange;
                        color.W -= 0.6f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }

                    if (invAmount >= item.Required - (item.OriginList.SkipIfEnough && item.OriginList.SkipLiteral ? 0 : item.GetSubCraftCount()))
                    {
                        var color = ImGuiColors.HealerGreen;
                        color.W -= 0.3f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }
                }

                if (item.Icon is not null)
                ImGuiUtil.HoverIcon(item.Icon, Interface.LineIconSize);
                ImGui.SameLine();

                var selected = ImGui.Selectable($"{item.Data.Name.ToString()}");
                InvokeContextMenu(item);

                if (selected)
                {
                    ImGui.SetClipboardText(item.Data.Name.ToString());
                    Notify.Success("名称已复制到剪贴板");
                }

                if (ImGui.IsItemHovered())
                {
                    StringBuilder sb = new();
                    foreach (var usedin in item.UsedInCrafts)
                    {
                        var recipe = LuminaSheets.RecipeSheet[usedin];
                        var amountUsed = recipe.Ingredients().FirstOrDefault(x => x.Item.RowId == item.Data.RowId).Amount * item.OriginList.Recipes.First(x => x.ID == recipe.RowId).Quantity;

                        sb.Append($"{usedin.NameOfRecipe()} - {amountUsed}\r\n");
                    }
                    ImGui.BeginTooltip();
                    ImGui.Text($"被用于:\r\n{sb}");
                    ImGui.EndTooltip();
                }
            }
        }

        public sealed class RequiredColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _requiredColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Required.CompareTo(rhs.Required);

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGuiUtil.Center($"{ToName(item)}");
            }

            public override string ToName(Ingredient item)
            {
                return item.Required.ToString();
            }
        }

        public sealed class IdColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _idColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Data.RowId.CompareTo(rhs.Data.RowId);

            public override string ToName(Ingredient item)
            {
                return item.Data.RowId.ToString();
            }
        }

        public sealed class InventoryCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _inventoryColumnWidth;

            public bool HQOnlyCrafts = false;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Inventory.CompareTo(rhs.Inventory);

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGuiUtil.Center($"{ToName(item)}");
            }

            public unsafe override string ToName(Ingredient item)
            {
                if (!HQOnlyCrafts || !item.CanBeCrafted)
                    return item.Inventory.ToString();

                int HQ = InventoryManager.Instance()->GetInventoryItemCount(item.Data.RowId, true, false, false);
                return HQ.ToString();
            }
        }

        public sealed class RetainerCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _retainerColumnWidth;

            public bool HQOnlyCrafts = false;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.RetainerCount.CompareTo(rhs.RetainerCount);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center($"{ToName(item)}");

            public override string ToName(Ingredient item)
            {
                if (!HQOnlyCrafts || !item.CanBeCrafted)
                    return item.RetainerCount.ToString();

                int retainerHQ = item.ReainterCountHQ;
                return retainerHQ.ToString();
            }
        }

        public sealed class CraftableCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _craftableCountColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.TotalCraftable.CompareTo(rhs.TotalCraftable);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center(ToName(item));


            public override string ToName(Ingredient item)
            {
                return item.Sources.Contains(1) ? item.TotalCraftable.ToString() : "N/A";
            }
        }

        public sealed class CraftItemsColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _craftItemsColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.UsedInCrafts.First().CompareTo(rhs.UsedInCrafts.First());

            public override string ToName(Ingredient item)
            {
                return string.Join(", ", item.UsedInCrafts.Select(x => x.NameOfRecipe()));
            }

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGui.Text(ToName(item));
            }

        }

        public sealed class CheapestServerColumn : ColumnString<Ingredient>
        {
            public override float Width => _cheapestColumnWidth;
            public Dictionary<uint, (string World, double Qty, double Cost)> CheapestListings = new();

            public override int Compare(Ingredient lhs, Ingredient rhs)
            {
                var lh = lhs.MarketboardData?.LowestWorld;
                var rh = rhs.MarketboardData?.LowestWorld;

                if (lh == null || rh == null)
                    return 0;

                return lh.CompareTo(rh);
            }

            public override string ToName(Ingredient item)
            {
                if (item.Remaining == 0) return $"不需要购买";
                if (item.MarketboardData != null && !CheapestListings.ContainsKey(item.Data.RowId))
                {
                    double totalCost = 0;
                    double qty = 0;

                    double currentWorldCost = 0;
                    string currentWorld = "";
                    double currentWorldQty = 0;

                    foreach (var world in item.MarketboardData.AllListings.Select(x => x.World))
                    {
                        totalCost = 0;
                        qty = 0;

                        foreach (var listing in item.MarketboardData.AllListings.Where(x => x.World == world).OrderBy(x => x.TotalPrice))
                        {
                            if (qty >= item.Remaining) break;
                            qty += listing.Quantity;
                            totalCost += listing.TotalPrice;
                        }

                        if ((totalCost < currentWorldCost && qty >= item.Remaining) || currentWorldCost == 0 || (qty > currentWorldQty && qty < item.Remaining))
                        {
                            currentWorldCost = totalCost;
                            currentWorld = world;
                            currentWorldQty = qty;
                        }
                    }

                    CheapestListings.TryAdd(item.Data.RowId, new(currentWorld, currentWorldQty, currentWorldCost));

                    item.MarketboardData.LowestWorld = currentWorld;
                }

                if (CheapestListings.ContainsKey(item.Data.RowId))
                {
                    var listing = CheapestListings[item.Data.RowId];

                    return $"{listing.World} - 花费 {listing.Cost.ToString("N0")}, 数量 {listing.Qty}";

                }

                return "错误 - 没有清单（可能是 Universalis 连接问题）";
            }

            public override void DrawColumn(Ingredient item, int _)
            {
                if (item.MarketboardData != null)
                {
                    ImGui.Text($"{ToName(item)}");
                    if (Lifestream && CheapestListings.ContainsKey(item.Data.RowId) && item.Remaining > 0)
                    {
                        var server = CheapestListings[item.Data.RowId].World;
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text($"点击以传送到 {server}.");
                            ImGui.EndTooltip();
                        }

                        if (ImGui.IsItemClicked())
                        {
                            Chat.Instance.SendMessage($"/li {server}");
                        }
                    }
                }
                else if (P.Config.UniversalisOnDemand && P.Config.UseUniversalis)
                {
                    if (item.Remaining == 0)
                    {
                        ImGui.Text($"不需要购买");
                        return;
                    }

                    using var smallBtnStyle = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0));
                    if (ImGui.Button($"获取价格"))
                    {
                        P.UniversalsisClient.PlayerWorld = Svc.ClientState.LocalPlayer?.CurrentWorld.RowId;
                        if (P.Config.LimitUnversalisToDC)
                            Task.Run(() => P.UniversalsisClient.GetDCData(item.Data.RowId, ref item.MarketboardData));
                        else
                            Task.Run(() => P.UniversalsisClient.GetRegionData(item.Data.RowId, ref item.MarketboardData));
                    }
                }
            }
        }

        public sealed class NumberForSaleColumn : ColumnString<Ingredient>
        {
            public override float Width => _numberForSaleWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
            {
                var lh = lhs.MarketboardData?.TotalQuantityOfUnits;
                var rh = rhs.MarketboardData?.TotalQuantityOfUnits;

                if (lh == null || rh == null)
                    return 0;

                return lh.Value.CompareTo(rh.Value);
            }

            public override string ToName(Ingredient item)
            {
                if (item.MarketboardData != null)
                {
                    var qty = item.MarketboardData.TotalQuantityOfUnits;
                    var listings = item.MarketboardData.TotalNumberOfListings;

                    return $"{listings:N0} listings - {qty:N0} total items";
                }
                return "";
            }

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGui.Text($"{ToName(item)}");
            }
        }


        public sealed class GatherItemLocationColumn : ItemFilterColumn
        {
            public GatherItemLocationColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.GatherZone, ItemFilter.NoGatherZone, ItemFilter.TimedNode, ItemFilter.NonTimedNode);
                SetNames("采集区域", "无采集区域", "特定时间", "无特定时间");

            }
            public override float Width
                => _gatherItemLocationColumWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.GatherZone.PlaceName.Value.Name.ToString().CompareTo(rhs.GatherZone.PlaceName.Value.Name.ToString());

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGui.Text(item.GatherZone.PlaceName.Value.Name.ToString());
            }

            public override bool FilterFunc(Ingredient item)
            {
                bool zone = item.GatherZone.RowId switch
                {
                    1 => FilterValue.HasFlag(ItemFilter.NoGatherZone),
                    _ => FilterValue.HasFlag(ItemFilter.GatherZone)
                };

                bool timed = item.TimedNode switch
                {
                    true => FilterValue.HasFlag(ItemFilter.TimedNode),
                    false => FilterValue.HasFlag(ItemFilter.NonTimedNode)
                };

                return zone & timed;
            }
        }

        public sealed class ItemCategoryColumn : ItemFilterColumn
        {
            public ItemCategoryColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.NonCrystals, ItemFilter.Crystals);
                SetNames("非水晶", "水晶");
            }


            public override float Width
                => _itemCategoryColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Category.CompareTo(rhs.Category);

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGui.Text(Svc.Data.Excel.GetSheet<ItemSearchCategory>().GetRow(item.Category).Name.ToString());
            }

            public override bool FilterFunc(Ingredient item)
            {
                return item.Category switch
                {
                    58 => FilterValue.HasFlag(ItemFilter.Crystals),
                    _ => FilterValue.HasFlag(ItemFilter.NonCrystals)
                };
            }
        }

        public class ItemFilterColumn : ColumnFlags<ItemFilter, Ingredient>
        {
            private ItemFilter[] FlagValues = Array.Empty<ItemFilter>();
            private string[] FlagNames = Array.Empty<string>();

            protected void SetFlags(params ItemFilter[] flags)
            {
                FlagValues = flags;
                AllFlags = FlagValues.Aggregate((f, g) => f | g);
            }

            protected void SetFlagsAndNames(params ItemFilter[] flags)
            {
                SetFlags(flags);
                SetNames(flags.Select(f => f.ToString()).ToArray());
            }

            protected void SetNames(params string[] names)
                => FlagNames = names;

            protected sealed override IReadOnlyList<ItemFilter> Values
                => FlagValues;

            protected sealed override string[] Names
                => FlagNames;

            public sealed override ItemFilter FilterValue
                => P.Config.ShowItemsV1;

            protected sealed override void SetValue(ItemFilter f, bool v)
            {
                var tmp = v ? FilterValue | f : FilterValue & ~f;
                if (tmp == FilterValue)
                    return;

                P.Config.ShowItemsV1 = tmp;
                P.Config.Save();
            }
        }

        public sealed class RemaingCountColumn : ItemFilterColumn
        {
            public RemaingCountColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.MissingItems, ItemFilter.NoMissingItems);
                SetNames("缺失物品", "没有缺失物品");
            }

            public override float Width
                => _remainingColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Remaining.CompareTo(rhs.Remaining);

            public List<Ingredient> SourceList = new();

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGuiUtil.Center($"{item.Remaining}");

                if (!(item.OriginList.SkipIfEnough && item.OriginList.SkipLiteral) && ImGui.IsItemHovered())
                {
                    StringBuilder sb = new StringBuilder();
                    if (item.UsedInMaterialsListCount.Count > 0)
                    {
                        foreach (var i in item.UsedInMaterialsListCount.Where(x => x.Value > 0))
                        {
                            var owned = RetainerInfo.GetRetainerItemCount(LuminaSheets.RecipeSheet[i.Key].ItemResult.RowId) + CraftingListUI.NumberOfIngredient(LuminaSheets.RecipeSheet[i.Key].ItemResult.RowId);
                            if (SourceList.FindFirst(x => x.CraftedRecipe.RowId == i.Key, out var ingredient))
                            {
                                sb.AppendLine($"{i.Value} less is required due to having {(owned > ingredient.Required ? "at least " : "")}{Math.Min(ingredient.Required, owned)}x {i.Key.NameOfRecipe()}");
                            }
                        }
                    }

                    if (item.SubSubMaterials.Count > 0)
                    {
                        foreach (var i in item.SubSubMaterials)
                        {
                            if (item.UsedInMaterialsListCount.ContainsKey(i.Key))
                                continue;

                            sb.AppendLine($"{i.Value.Sum(x => x.Item2)} less is required for {i.Key.NameOfRecipe()}");
                            foreach (var m in i.Value)
                            {
                                var owned = RetainerInfo.GetRetainerItemCount(LuminaSheets.RecipeSheet[m.Item1].ItemResult.RowId) + CraftingListUI.NumberOfIngredient(LuminaSheets.RecipeSheet[m.Item1].ItemResult.RowId);
                                if (SourceList.FindFirst(x => x.CraftedRecipe.RowId == m.Item1, out var ingredient))
                                {
                                    sb.AppendLine($"└ {m.Item1.NameOfRecipe()} uses {i.Key.NameOfRecipe()}, you have {(owned > ingredient.Required ? "at least " : "")}{Math.Min(ingredient.Required, owned)} {m.Item1.NameOfRecipe()} so {m.Item2}x {item.Data.Name} less is required as a result.");
                                }
                            }
                        }
                    }

                    ImGuiUtil.HoverTooltip(sb.ToString().Trim());
                }

            }

            public override bool FilterFunc(Ingredient item)
            {
                return item.Remaining switch
                {
                    0 => FilterValue.HasFlag(ItemFilter.NoMissingItems),
                    _ => FilterValue.HasFlag(ItemFilter.MissingItems)
                };
            }
        }

        public sealed class CraftableColumn : ItemFilterColumn
        {
            public CraftableColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.Crafted, ItemFilter.Gathered, ItemFilter.Fishing, ItemFilter.Vendor, ItemFilter.MonsterDrop, ItemFilter.Unknown);
                SetNames("制作", "采集", "钓鱼", "商人", "怪物掉落", "未知");
            }


            public override float Width
                => _canCraftColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => string.Join(", ", lhs.Sources).CompareTo(string.Join(", ", rhs.Sources));

            public override void DrawColumn(Ingredient item, int idx)
            {
                List<string> outputs = new();

                if (item.Sources.Contains(1)) outputs.Add("制作");
                if (item.Sources.Contains(2)) outputs.Add("采集");
                if (item.Sources.Contains(3)) outputs.Add("钓鱼");
                if (item.Sources.Contains(4)) outputs.Add("商人");
                if (item.Sources.Contains(5)) outputs.Add("怪物掉落");
                if (item.Sources.Contains(-1)) outputs.Add("未知");

                ImGui.Text($"{string.Join(", ", outputs)}");
            }

            public override bool FilterFunc(Ingredient item)
            {
                if (item.Sources.Contains(1) && FilterValue.HasFlag(ItemFilter.Crafted)) return true;
                if (item.Sources.Contains(2) && FilterValue.HasFlag(ItemFilter.Gathered)) return true;
                if (item.Sources.Contains(3) && FilterValue.HasFlag(ItemFilter.Fishing)) return true;
                if (item.Sources.Contains(4) && FilterValue.HasFlag(ItemFilter.Vendor)) return true;
                if (item.Sources.Contains(5) && FilterValue.HasFlag(ItemFilter.MonsterDrop)) return true;
                if (item.Sources.Contains(-1) && FilterValue.HasFlag(ItemFilter.Unknown)) return true;


                return false;
            }
        }

        private void OpenContextMenu(object? sender, Ingredient item)
        {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup(item.Data.RowId.ToString());

            using var popup = ImRaii.Popup(item.Data.RowId.ToString());
            if (!popup)
                return;

            DrawGatherItem(item);
            DrawSearchItem(item);
            DrawItemVendorLookup(item);
            DrawMonsterLootLookup(item);
            DrawMarketBoardLookup(item);
            DrawFilterOnCrafts(item);
            DrawRestockFromRetainer(item);
            //DrawCraftThisItem(item);
        }

        private void DrawMarketBoardLookup(Ingredient item)
        {

            if (item.Data.RowId == 0)
                return;

            if (Marketboard)
            {
                if (ImGui.Selectable("市场板查询"))
                {
                    Chat.Instance.SendMessage($"/pmb {item.Data.Name.ToDalamudString()}");
                }
            }
        }

        private void DrawRestockFromRetainer(Ingredient item)
        {
            if (item.Data.RowId == 0 || item.RetainerCount == 0 || item.Required <= item.Inventory)
                return;

            if (RetainerInfo.GetReachableRetainerBell() == null)
            {
                ImGui.TextDisabled($"从雇员处获取（请站在雇员铃旁）");
            }
            else
            {
                if (RetainerInfo.TM.IsBusy)
                {
                    ImGui.TextDisabled($"正在获取中，请稍候。");
                    return;
                }

                if (!ImGui.Selectable("从雇员处获取"))
                    return;

                var howManyToGet = item.Required - item.Inventory;
                if (howManyToGet > 0)
                {
                    RetainerInfo.RestockFromRetainers(item.Data.RowId, howManyToGet);
                }
            }
        }

        private void DrawFilterOnCrafts(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (FilteredItems.Count == Items.Count || Headers.Any(x => x.FilterFunc(item)))
            {
                if (isOnList == null)
                {
                    isOnList = item.OriginList.Recipes.Any(x => LuminaSheets.RecipeSheet.Values.Any(y => y.ItemResult.RowId == item.Data.RowId && y.RowId == x.ID));
                }

                if (item.Sources.Contains(1) && isOnList.Value)
                {
                    if (ImGui.Selectable($"显示用于此的材料"))
                    {
                        FilteredItems.Clear();
                        var idx = 0;
                        FilteredItems.Add((item, idx));
                        idx++;
                        foreach (var ingredient in CraftingListHelpers.GetIngredientRecipe(item.Data.RowId).Value.Ingredients().Where(x => x.Amount > 0))
                        {
                            if (Items.FindFirst(x => x.Data.RowId == ingredient.Item.RowId, out var result))
                                FilteredItems.Add((result, idx));
                            idx++;
                        }

                        CraftFiltered = true;
                    }
                }
            }

            if (CraftFiltered)
            {
                if (!ImGui.Selectable($"清除过滤"))
                    return;

                CraftFiltered = false;
                FilterDirty = true;

            }
        }

        private static void DrawMonsterLootLookup(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (MonsterLookup)
            {
                if (!ImGui.Selectable("怪物掉落查询"))
                    return;

                try
                {
                    Chat.Instance.SendMessage($"/mloot {item.Data.Name.ToString()}");
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            else
            {
                ImGui.TextDisabled("怪物掉落查询 (请安装 Monster Loot Hunter)");
            }
        }

        private static void DrawItemVendorLookup(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (ItemVendor)
            {
                if (ItemVendorLocation.ItemHasVendor(item.Data.RowId))
                {
                    if (!ImGui.Selectable("物品商人查询"))
                        return;

                    try
                    {
                        ItemVendorLocation.OpenContextMenu(item.Data.RowId);
                    }
                    catch (Exception e)
                    {
                        e.Log();
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("物品商人查询 (请安装 Item Vendor Location)");
            }
        }

        private static void DrawSearchItem(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (!ImGui.Selectable("查找物品"))
                return;

            try
            {
                SearchItem(item.Data.RowId);
            }
            catch (Exception e)
            {
                e.Log();
            }

        }

        private static void DrawGatherItem(Ingredient item)
        {
            if (item.Data.RowId == 0 || item.Sources.Contains(1))
                return;

            if (GatherBuddy)
            {
                if (!ImGui.Selectable("采集物品"))
                    return;

                try
                {
                    if (LuminaSheets.GatheringItemSheet!.Any(x => x.Value.Item.RowId == item.Data.RowId))
                        Chat.Instance.SendMessage($"/gather {item.Data.Name.ToString()}");
                    else
                        Chat.Instance.SendMessage($"/gatherfish {item.Data.Name.ToString()}");
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            else
            {
                ImGui.TextDisabled("采集物品 (请安装 Gatherbuddy)");
            }
        }
    }

    [Flags]
    public enum ItemFilter
    {
        NoItems = 0,
        MissingItems = 1,
        NoMissingItems = 2,

        Crafted = 4,
        Gathered = 8,
        Fishing = 16,
        Vendor = 32,
        MonsterDrop = 64,
        Unknown = 128,

        NonCrystals = 256,
        Crystals = 512,

        GatherZone = 4096,
        NoGatherZone = 8192,
        TimedNode = 16384,
        NonTimedNode = 32768,

        All = MissingItems + NoMissingItems +
                Crafted + Gathered + Fishing + Vendor + MonsterDrop + Unknown +
                NonCrystals + Crystals +
                GatherZone + NoGatherZone + TimedNode + NonTimedNode,
    }
}
