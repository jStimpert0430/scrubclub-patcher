using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueDepot.Core;

public sealed class Ingredient
{
    public string Name { get; }
    public int Amount { get; }
    public int Quality { get; }
    public Ingredient(string name, int amount, int quality = -1)
    {
        if (string.IsNullOrEmpty(name) || amount < 0) throw new ArgumentException("Invalid ingredient");
        Name = name; Amount = amount; Quality = quality;
    }
}

public static class CraftingPlan
{
    public static Ingredient? ChooseQuality(string name, int amount, int maxQuality,
        Func<string, int, int> carried, Func<string, int, int> available)
    {
        // Prefer a complete carried stack before fetching a different quality.
        foreach (var count in new[] { carried, available })
            for (int quality = 1; quality <= maxQuality; quality++)
                if (count(name, quality) >= amount) return new Ingredient(name, amount, quality);
        return null;
    }
    // Each alternative is a complete recipe; never combine partial alternatives.
    public static Ingredient[]? Select(IEnumerable<IEnumerable<Ingredient>> alternatives, Func<string, int, int> count)
    {
        foreach (var alternative in alternatives)
        {
            var grouped = alternative.GroupBy(i => (i.Name, i.Quality))
                .Select(g => new Ingredient(g.Key.Name, checked(g.Sum(i => i.Amount)), g.Key.Quality)).ToArray();
            if (grouped.All(i => count(i.Name, i.Quality) >= i.Amount)) return grouped;
        }
        return null;
    }
    public static int Missing(Ingredient item, Func<string, int, int> count) => Math.Max(0, item.Amount - count(item.Name, item.Quality));
}
