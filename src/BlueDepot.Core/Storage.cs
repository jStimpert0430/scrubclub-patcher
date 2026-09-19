using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueDepot.Core;

public enum Category { Materials, Weapons, Armor, Food, Potions, Ammunition, Tools, Trophies, Miscellaneous }

public static class Categories
{
    public static bool IsTrophy(string type,string? prefabId=null)=>type=="Trophy" || prefabId=="AncientSeed";
    // ItemType names come from Valheim; unknown/modded types remain visible.
    public static Category Classify(string type, bool edible = false, bool potion = false,string? prefabId=null)
    {
        if(IsTrophy(type,prefabId))return Category.Trophies;
        if (type == "Consumable") return potion ? Category.Potions : edible ? Category.Food : Category.Miscellaneous;
        switch (type)
        {
            case "Material": return Category.Materials;
            case "OneHandedWeapon": case "TwoHandedWeapon": case "TwoHandedWeaponLeft":
            case "Bow": case "Attach_Atgeir": return Category.Weapons;
            case "Helmet": case "Chest": case "Legs": case "Shoulder": case "Shield": case "Utility": return Category.Armor;
            case "Ammo": case "AmmoNonEquipable": return Category.Ammunition;
            case "Tool": case "Torch": return Category.Tools;
            case "Trophy": return Category.Trophies;
            default: return Category.Miscellaneous;
        }
    }
}

public sealed class Stack
{
    public string Slot { get; }
    public string Key { get; }
    public string Name { get; }
    public string ItemId { get; }
    public bool PreferCart { get; }
    public Category Category { get; }
    public int Count { get; }
    public int Maximum { get; }
    public Stack(string slot, string key, string name, Category category, int count, int maximum, string? itemId = null, bool nonTeleportable = false)
    {
        if (count <= 0 || maximum <= 0 || count > maximum) throw new ArgumentOutOfRangeException(nameof(count));
        Slot = slot; Key = key; Name = name; Category = category; Count = count; Maximum = maximum; ItemId = itemId ?? key; PreferCart = nonTeleportable || StorageRules.IsOre(ItemId);
    }
}

public sealed class Chest
{
    public string Id { get; }
    public int Capacity { get; }
    public double Distance { get; }
    public bool Accessible { get; }
    public bool Private { get; }
    public bool Cart { get; }
    public IReadOnlyList<Stack> Items { get; }
    public Chest(string id, int capacity, double distance, bool accessible, IEnumerable<Stack> items, bool isPrivate = false, bool cart = false)
    {
        if (capacity < 0 || distance < 0 || double.IsNaN(distance)) throw new ArgumentOutOfRangeException(nameof(capacity));
        var copy = items.ToArray();
        if (copy.Length > capacity || copy.Select(x => x.Slot).Distinct().Count() != copy.Length)
            throw new ArgumentException("Invalid inventory slots");
        Id = id; Capacity = capacity; Distance = distance; Accessible = accessible; Items = copy; Private = isPrivate; Cart = cart;
    }
}

public sealed class StorageView
{
    public const int NativeSlots = 100;
    public IReadOnlyList<Chest> Chests { get; }
    public int Capacity => Chests.Sum(c => c.Capacity);
    public int Occupied => Chests.Sum(c => c.Items.Count);
    public IEnumerable<(Chest Chest, Stack Item)> Rows(Category? category = null, string search = "") =>
        Chests.SelectMany(c => c.Items.Select(i => (Chest: c, Item: i)))
            .Where(r => (!category.HasValue || r.Item.Category == category) &&
                r.Item.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(r => r.Item.Category).ThenBy(r => r.Item.ItemId, StringComparer.Ordinal)
            .ThenBy(r => r.Item.Key, StringComparer.Ordinal)
            .ThenBy(r => r.Chest.Id, StringComparer.Ordinal).ThenBy(r => r.Item.Slot, StringComparer.Ordinal);

    public StorageView(Chest depot, IEnumerable<Chest> nearby, double radius)
    {
        if (radius <= 0 || double.IsNaN(radius) || double.IsInfinity(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
        Chests = new[] { depot }.Concat(nearby)
            .Where(c => c.Accessible && (c.Id == depot.Id || (!depot.Private && !c.Private && c.Distance <= radius)))
            .GroupBy(c => c.Id, StringComparer.Ordinal).Select(g => g.First())
            .OrderBy(c => c.Distance).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
    }
}

public sealed class Placement
{
    public string ChestId { get; }
    public string Slot { get; }
    public int Amount { get; }
    public Placement(string chestId, string slot, int amount) { ChestId = chestId; Slot = slot; Amount = amount; }
}

public static class Routing
{
    // Returns one executable move. The game re-snapshots after each confirmed transfer;
    // never execute a stale bulk plan against inventories changed by another player.
    public static Placement? Next(Stack item, string sourceId, IEnumerable<Chest> candidates)
    {
        var targets = candidates.Where(c => c.Accessible && !c.Private && c.Id != sourceId)
            .GroupBy(c => c.Id).Select(g => g.First()).ToArray();
        // Ore and non-teleportable items fill eligible carts first, even ahead of partial stacks in boxes.
        // Within each tier, partial stacks still outrank empty slots.
        foreach (var tier in targets.GroupBy(c => item.PreferCart && c.Cart ? 0 : 1).OrderBy(g => g.Key))
        {
        foreach (var c in Rank(tier, item))
        foreach (var stack in c.Items.OrderBy(i => i.Slot, StringComparer.Ordinal))
            if (stack.Key == item.Key && stack.Maximum == item.Maximum && stack.Count < stack.Maximum)
                return new Placement(c.Id, stack.Slot, Math.Min(item.Count, stack.Maximum - stack.Count));
        foreach (var c in Rank(tier, item))
        {
            if (c.Items.Count >= c.Capacity) continue;
            var used = new HashSet<string>(c.Items.Select(i => i.Slot));
            for (int slot = 0; slot < c.Capacity; slot++)
                if (!used.Contains(slot.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    return new Placement(c.Id, slot.ToString(System.Globalization.CultureInfo.InvariantCulture), item.Count);
        }
        }
        return null; // Overflow stays at its source.
    }
    private static IEnumerable<Chest> Rank(IEnumerable<Chest> chests, Stack item) => chests
        .OrderByDescending(c => c.Items.Any(i => i.Key == item.Key))
        .ThenByDescending(c => c.Items.Any(i => i.Category == item.Category))
        .ThenBy(c => c.Distance).ThenBy(c => c.Id, StringComparer.Ordinal);
}

// A response disappearing from MUC's request queue is NOT proof of success.
// Only explicit responses complete a transfer. Timeouts do not automatically retry.
public sealed class TransferGate
{
    public int? Pending { get; private set; }
    public bool Uncertain { get; private set; }
    readonly HashSet<int> completedDuringDispatch=new HashSet<int>();
    public bool Dispatching { get; private set; }
    public bool Busy => Dispatching || Pending.HasValue || Uncertain;
    public bool BeginDispatch()
    {
        if(Busy)return false;
        completedDuringDispatch.Clear();Dispatching=true;return true;
    }
    public void EndDispatch(int? requestId)
    {
        Dispatching=false;
        // Locally routed RPCs can reply before the send call returns its request.
        // Never reopen an already-confirmed operation as pending.
        if(requestId.HasValue && !completedDuringDispatch.Contains(requestId.Value))Begin(requestId.Value);
        completedDuringDispatch.Clear();
    }
    public bool Begin(int id) { if (Busy) return false; Pending = id; return true; }
    public bool Complete(int id)
    {
        if(Dispatching){completedDuringDispatch.Add(id);return true;}
        if(Pending!=id)return false;
        Pending=null;Uncertain=false;return true;
    }
    public void Fault() { Uncertain = true; }
    public void Timeout() { if (Pending.HasValue) Uncertain = true; }
}

public static class TransferRules
{
    public static bool CanRemove(string expected, string? actual, int amount) => amount > 0 && actual != null && expected == actual;
    public static bool CanDeposit(string incoming, string? actual, int amount) => amount > 0 && (actual == null || incoming == actual);
}

public static class BulkDropRules
{
    public static bool Eligible(Category category, bool equipped, int inventoryRow, int count, bool blocked) =>
        (category == Category.Materials || category == Category.Trophies) && !equipped && inventoryRow > 0 && count > 0 && !blocked;
}
