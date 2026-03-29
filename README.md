[![Build](https://github.com/BSchneppe/relic-stats-sts2/actions/workflows/build.yml/badge.svg)](https://github.com/BSchneppe/relic-stats-sts2/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/BSchneppe.Sts2.RelicStats)](https://www.nuget.org/packages/BSchneppe.Sts2.RelicStats)

# Relic Stats for Slay the Spire 2

Tracks and displays how much value each relic has provided during your run. Stat descriptions are appended to relic hover tooltips, showing things like total damage dealt, cards drawn, gold earned, healing received — per relic, per run.

Inspired by [ForgottenArbiter's Relic Stats for STS1](https://github.com/ForgottenArbiter/StsRelicStats).

## Installation

1. Install [BaseLib](https://github.com/Alchyr/BaseLib-StS2) (required dependency)
2. Download `RelicStats.dll` and `RelicStats.json` from [Releases](../../releases)
3. Place both files in `Slay the Spire 2/mods/RelicStats/`

## Coverage

**183 of 290** relics tracked.

<details>
<summary>Click to expand full relic coverage</summary>

| Relic | Tracked | What is tracked |
|-------|:-------:|----------------|
| Akabeko | :white_check_mark: | Gained X Vigor |
| Alchemical Coffer | | |
| Amethyst Aubergine | :white_check_mark: | Gained X Gold |
| Anchor | :white_check_mark: | Gained X Block |
| Arcane Scroll | | |
| Archaic Tooth | | |
| Art Of War | :white_check_mark: | Complex tracking |
| Astrolabe | | |
| Bag Of Marbles | :white_check_mark: | Applied Vulnerable X times |
| Bag Of Preparation | :white_check_mark: | Drew X additional cards |
| Beating Remnant | | |
| Beautiful Bracelet | | |
| Bellows | :white_check_mark: | Upgraded X hands |
| Belt Buckle | :white_check_mark: | Granted X Dexterity |
| Big Hat | :white_check_mark: | Generated X Ethereal cards |
| Big Mushroom | :white_check_mark: | Drew X fewer cards |
| Biiig Hug | :white_check_mark: | Added X soot cards |
| Bing Bong | :white_check_mark: | Duplicated X cards |
| Black Blood | :white_check_mark: | Healed X HP |
| Black Star | :white_check_mark: | Gained X extra relic rewards |
| Blessed Antler | :white_check_mark: | Complex tracking |
| Blood Soaked Rose | :white_check_mark: | Complex tracking |
| Blood Vial | :white_check_mark: | Healed X HP |
| Bone Flute | :white_check_mark: | Gained X Block |
| Bone Tea | :white_check_mark: | Upgraded X hands |
| Book Of Five Rings | :white_check_mark: | Healed X HP from adding cards |
| Book Repair Knife | :white_check_mark: | Healed X HP |
| Bookmark | :white_check_mark: | Reduced card costs X times |
| Booming Conch | :white_check_mark: | Drew X additional cards |
| Bound Phylactery | | |
| Bowler Hat | :white_check_mark: | Gained X bonus Gold |
| Bread | :white_check_mark: | Complex tracking |
| Brilliant Scarf | | |
| Brimstone | :white_check_mark: | Gained X Strength |
| Bronze Scales | :white_check_mark: | Applied X Thorns |
| Burning Blood | :white_check_mark: | Healed X HP |
| Burning Sticks | :white_check_mark: | Duplicated X cards |
| Byrdpip | | |
| Calling Bell | | |
| Candelabra | :white_check_mark: | Generated X Energy |
| Captains Wheel | :white_check_mark: | Gained X Block |
| Cauldron | | |
| Centennial Puzzle | :white_check_mark: | Drew X cards on hit |
| Chandelier | :white_check_mark: | Generated X Energy |
| Charons Ashes | :white_check_mark: | Dealt X Damage |
| Chemical X | :white_check_mark: | Added X to X values |
| Choices Paradox | :white_check_mark: | Generated X cards to choose from |
| Chosen Cheese | :white_check_mark: | Gained X max HP |
| Circlet | | |
| Claws | | |
| Cloak Clasp | :white_check_mark: | Gained X Block |
| Cracked Core | :white_check_mark: | Channeled X Lightning orbs |
| Crossbow | :white_check_mark: | Generated X free attacks |
| Cursed Pearl | | |
| Darkstone Periapt | :white_check_mark: | Gained X max HP |
| Data Disk | :white_check_mark: | Applied X Focus |
| Daughter Of The Wind | :white_check_mark: | Gained X Block |
| Delicate Frond | :white_check_mark: | Generated potions X times |
| Demon Tongue | :white_check_mark: | Healed X HP |
| Deprecated Relic | | |
| Diamond Diadem | :white_check_mark: | Applied DiamondDiademPower X times |
| Dingy Rug | | |
| Distinguished Cape | | |
| Divine Destiny | | |
| Divine Right | :white_check_mark: | Gained X Stars |
| Dollys Mirror | | |
| Dragon Fruit | :white_check_mark: | Gained X Max HP |
| Dream Catcher | | |
| Driftwood | | |
| Dusty Tome | | |
| Ectoplasm | :white_check_mark: | Complex tracking |
| Electric Shrymp | | |
| Ember Tea | :white_check_mark: | Applied X Strength |
| Emotion Chip | | |
| Empty Cage | | |
| Eternal Feather | :white_check_mark: | Healed X HP |
| Fake Anchor | :white_check_mark: | Gained X Block |
| Fake Blood Vial | :white_check_mark: | Healed X HP |
| Fake Happy Flower | :white_check_mark: | Generated X Energy |
| Fake Lees Waffle | | |
| Fake Mango | | |
| Fake Merchants Rug | | |
| Fake Orichalcum | :white_check_mark: | Gained X Block |
| Fake Snecko Eye | :white_check_mark: | Applied Confused X times |
| Fake Strike Dummy | :white_check_mark: | Added X Damage to Strikes |
| Fake Venerable Tea Set | :white_check_mark: | Generated X Energy |
| Fencing Manual | :white_check_mark: | Gained X Forge |
| Festive Popper | :white_check_mark: | Dealt X Damage |
| Fiddle | :white_check_mark: | Drew X additional cards |
| Forgotten Soul | :white_check_mark: | Dealt X Damage |
| Fragrant Mushroom | | |
| Fresnel Lens | :white_check_mark: | Enchanted X cards |
| Frozen Egg | :white_check_mark: | Upgraded X power cards |
| Funerary Mask | :white_check_mark: | Generated X Soul cards |
| Fur Coat | | |
| Galactic Dust | | |
| Gambling Chip | :white_check_mark: | Swapped X cards |
| Game Piece | :white_check_mark: | Drew X cards from Powers |
| Ghost Seed | | |
| Girya | :white_check_mark: | Gained X Strength |
| Glass Eye | | |
| Glitter | | |
| Gnarled Hammer | | |
| Gold Plated Cables | :white_check_mark: | Doubled first orb passive X times |
| Golden Compass | | |
| Golden Pearl | | |
| Gorget | :white_check_mark: | Gained X Plating |
| Gremlin Horn | :white_check_mark: | Triggered X times (drew cards + gained Energy) |
| Hand Drill | :white_check_mark: | Applied Vulnerable X times |
| Happy Flower | :white_check_mark: | Generated X Energy |
| Helical Dart | :white_check_mark: | Gained X Dexterity from Shivs |
| History Course | :white_check_mark: | Auto-replayed X cards |
| Horn Cleat | :white_check_mark: | Gained X Block |
| Ice Cream | :white_check_mark: | Preserved energy X times |
| Infused Core | :white_check_mark: | Channeled X Lightning orbs |
| Intimidating Helmet | :white_check_mark: | Gained X Block |
| Iron Club | :white_check_mark: | Drew X cards |
| Ivory Tile | | |
| Jeweled Mask | :white_check_mark: | Drew X free Powers |
| Jewelry Box | | |
| Joss Paper | :white_check_mark: | Drew X cards |
| Juzu Bracelet | | |
| Kifuda | | |
| Kunai | :white_check_mark: | Gained X Dexterity |
| Kusarigama | :white_check_mark: | Dealt X Damage |
| Lantern | :white_check_mark: | Generated X Energy |
| Large Capsule | | |
| Lasting Candy | :white_check_mark: | Added X extra Power cards to rewards |
| Lava Lamp | :white_check_mark: | Upgraded card rewards X times |
| Lava Rock | | |
| Lead Paperweight | | |
| Leafy Poultice | | |
| Lees Waffle | | |
| Letter Opener | :white_check_mark: | Dealt X Damage |
| Lizard Tail | :white_check_mark: | Healed X HP on revive |
| Looming Fruit | | |
| Lords Parasol | | |
| Lost Coffer | | |
| Lost Wisp | :white_check_mark: | Dealt X Damage |
| Lucky Fysh | :white_check_mark: | Gained X Gold |
| Lunar Pastry | :white_check_mark: | Gained X Stars |
| Mango | | |
| Massive Scroll | | |
| Maw Bank | :white_check_mark: | Gained X Gold |
| Meal Ticket | :white_check_mark: | Healed X HP |
| Meat Cleaver | | |
| Meat On The Bone | :white_check_mark: | Healed X HP |
| Membership Card | | |
| Mercury Hourglass | :white_check_mark: | Dealt X Damage |
| Metronome | | |
| Mini Regent | :white_check_mark: | Gained X Strength |
| Miniature Cannon | :white_check_mark: | Added X Damage to upgraded attacks |
| Miniature Tent | | |
| Molten Egg | :white_check_mark: | Upgraded X attack cards |
| Mr Struggles | :white_check_mark: | Dealt X Damage |
| Mummified Hand | :white_check_mark: | Made X cards free |
| Music Box | :white_check_mark: | Copied X attacks as Ethereal |
| Mystic Lighter | :white_check_mark: | Added X Damage to enchanted attacks |
| Neows Torment | | |
| New Leaf | | |
| Ninja Scroll | :white_check_mark: | Created X Shivs |
| Nunchaku | :white_check_mark: | Gained X Energy |
| Nutritious Oyster | | |
| Nutritious Soup | | |
| Oddly Smooth Stone | :white_check_mark: | Applied X Dexterity |
| Old Coin | | |
| Orange Dough | :white_check_mark: | Added X colorless cards |
| Orichalcum | :white_check_mark: | Gained X Block |
| Ornamental Fan | :white_check_mark: | Complex tracking |
| Orrery | | |
| Paels Blood | :white_check_mark: | Drew X additional cards |
| Paels Claw | | |
| Paels Eye | :white_check_mark: | Complex tracking |
| Paels Flesh | :white_check_mark: | Generated X Energy |
| Paels Growth | | |
| Paels Horn | | |
| Paels Legion | | |
| Paels Tears | :white_check_mark: | Complex tracking |
| Paels Tooth | | |
| Paels Wing | :white_check_mark: | Sacrificed X card rewards |
| Pandoras Box | | |
| Pantograph | :white_check_mark: | Healed X HP |
| Paper Krane | | |
| Paper Phrog | | |
| Parrying Shield | :white_check_mark: | Dealt X Damage |
| Pear | | |
| Pen Nib | :white_check_mark: | Complex tracking |
| Pendulum | :white_check_mark: | Drew X cards |
| Permafrost | :white_check_mark: | Gained X Block |
| Petrified Toad | :white_check_mark: | Generated X potions |
| Philosophers Stone | :white_check_mark: | Complex tracking |
| Phylactery Unbound | :white_check_mark: | Complex tracking |
| Planisphere | :white_check_mark: | Healed X HP |
| Pocketwatch | :white_check_mark: | Drew X additional cards |
| Pollinous Core | :white_check_mark: | Drew X additional cards |
| Pomander | | |
| Potion Belt | | |
| Power Cell | | |
| Prayer Wheel | :white_check_mark: | Added X extra card rewards |
| Precarious Shears | | |
| Precise Scissors | | |
| Preserved Fog | | |
| Prismatic Gem | :white_check_mark: | Generated X Energy |
| Pumpkin Candle | :white_check_mark: | Generated X Energy |
| Punch Dagger | | |
| Radiant Pearl | :white_check_mark: | Generated X Luminesce cards |
| Rainbow Ring | | |
| Razor Tooth | :white_check_mark: | Upgraded X cards |
| Red Mask | :white_check_mark: | Applied weakness X times |
| Red Skull | | |
| Regal Pillow | :white_check_mark: | Healed X extra HP |
| Regalite | :white_check_mark: | Gained X Block |
| Reptile Trinket | :white_check_mark: | Gained X temporary Strength from potions |
| Ring Of The Drake | :white_check_mark: | Drew X additional cards |
| Ring Of The Snake | :white_check_mark: | Drew X additional cards |
| Ringing Triangle | :white_check_mark: | Retained hand X times |
| Ripple Basin | :white_check_mark: | Gained X Block |
| Royal Poison | :white_check_mark: | Dealt X Damage to self |
| Royal Stamp | | |
| Ruined Helmet | :white_check_mark: | Doubled strength X times |
| Runic Capacitor | :white_check_mark: | Added X orb slots |
| Runic Pyramid | :white_check_mark: | Retained hand X times |
| Sai | :white_check_mark: | Gained X Block |
| Sand Castle | | |
| Screaming Flagon | :white_check_mark: | Dealt X Damage |
| Scroll Boxes | | |
| Sea Glass | | |
| Seal Of Gold | :white_check_mark: | Complex tracking |
| Self Forming Clay | :white_check_mark: | Gained X Block |
| Sere Talon | | |
| Shovel | :white_check_mark: | Offered dig X times |
| Shuriken | :white_check_mark: | Gained X Strength |
| Signet Ring | | |
| Silver Crucible | :white_check_mark: | Upgraded card rewards X times |
| Sling Of Courage | :white_check_mark: | Gained X Strength |
| Small Capsule | | |
| Snecko Eye | :white_check_mark: | Complex tracking |
| Snecko Skull | :white_check_mark: | Added X extra Poison |
| Sozu | :white_check_mark: | Complex tracking |
| Sparkling Rouge | :white_check_mark: | Gained Strength+Dexterity X times |
| Spiked Gauntlets | :white_check_mark: | Complex tracking |
| Stone Calendar | :white_check_mark: | Dealt X Damage |
| Stone Cracker | :white_check_mark: | Upgraded X cards in boss combats |
| Stone Humidifier | :white_check_mark: | Gained X Max HP |
| Storybook | | |
| Strawberry | | |
| Strike Dummy | :white_check_mark: | Added X Damage to Strikes |
| Sturdy Clamp | | |
| Sword Of Jade | :white_check_mark: | Applied X Strength |
| Sword Of Stone | :white_check_mark: | Defeated X elites |
| Symbiotic Virus | :white_check_mark: | Channeled X Dark orbs |
| Tanxs Whistle | | |
| Tea Of Discourtesy | | |
| The Abacus | :white_check_mark: | Gained X Block |
| The Boot | :white_check_mark: | Boosted damage to 5 X times |
| The Courier | | |
| Throwing Axe | :white_check_mark: | Doubled first card X times |
| Tingsha | :white_check_mark: | Dealt X Damage |
| Tiny Mailbox | | |
| Toasty Mittens | :white_check_mark: | Gained X Strength and exhausted cards |
| Toolbox | :white_check_mark: | Offered cards X times |
| Touch Of Orobas | | |
| Tough Bandages | :white_check_mark: | Gained X Block |
| Toxic Egg | :white_check_mark: | Upgraded X skill cards |
| Toy Box | | |
| Tri Boomerang | | |
| Tungsten Rod | :white_check_mark: | Prevented X HP loss |
| Tuning Fork | :white_check_mark: | Gained X Block |
| Twisted Funnel | :white_check_mark: | Applied X Poison |
| Unceasing Top | :white_check_mark: | Drew X cards from empty hand |
| Undying Sigil | | |
| Unsettling Lamp | | |
| Vajra | :white_check_mark: | Gained X Strength |
| Vakuu Card Selector | | |
| Vambrace | :white_check_mark: | Doubled first Block X times |
| Velvet Choker | :white_check_mark: | Hit card limit X times |
| Venerable Tea Set | :white_check_mark: | Generated X Energy |
| Very Hot Cocoa | :white_check_mark: | Generated X Energy |
| Vexing Puzzlebox | :white_check_mark: | Generated X free cards |
| Vitruvian Minion | | |
| War Hammer | :white_check_mark: | Upgraded X cards after elite combats |
| War Paint | | |
| Whetstone | | |
| Whispering Earring | :white_check_mark: | Triggered X times |
| White Beast Statue | | |
| White Star | | |
| Wing Charm | | |
| Wongo Customer Appreciation Badge | | |
| Wongos Mystery Ticket | :white_check_mark: | Completed X combats toward relic |
| Yummy Cookie | | |

</details>

## Building from source

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Slay the Spire 2 installed via Steam (the build references `sts2.dll`, `0Harmony.dll`, and `GodotSharp.dll` from your game install)

### Build

```bash
dotnet build -c Debug
```

The build auto-detects your game install path on macOS, Windows, and Linux. To override:

```bash
dotnet build -p:STS2GameDir=/path/to/game/data -p:STS2ModsDir=/path/to/mods
```

The output DLL and manifest are automatically copied to your mods directory.

## Testing

### Unit tests

Pure logic tests (description formatting, JSON persistence) that don't require the game:

```bash
dotnet test tests/
```

### In-game integration tests

Debug builds include a test harness that drives game state and asserts relic counters. In the game's debug console:

```
relicstats test              # run all relic tests
relicstats test ANCHOR       # run a specific relic test
relicstats test results      # show results from last run
```

Tests use a step-based state machine (`Do` → `WaitFor` → `Assert` → `Cleanup`) driven by Harmony-patched game events. Each test adds a relic, triggers the relevant game action, and asserts the tracked value.

## Architecture

```
Core/
  IRelicStats.cs             # Interface: GetDescription, Save/Load, Reset
  SimpleCounterStats.cs      # Base class for single-counter relics (~80% of cases)
  RelicStatsRegistry.cs      # Auto-discovers all IRelicStats via reflection
  StatsPersistence.cs        # JSON save/load
  Fmt.cs                     # BBCode color helpers
  Testing/                   # In-game test harness (DEBUG only)
Patches/
  HoverTipPatch.cs           # Single patch on RelicModel.HoverTips — injects all stats
  GlobalCounterPatches.cs    # Turn/combat counters, wax melt, new run reset
  SaveManagerPatch.cs        # Hooks save/load for persistence
Relics/
  BlockRelics.cs             # Block-related relics
  CardRelics.cs              # Card draw/hand relics
  DamageRelics.cs            # Damage relics
  EnergyRelics.cs            # Energy relics
  GoldRelics.cs              # Gold relics
  HealingRelics.cs           # Healing relics
  MiscRelics.cs              # Everything else
```

Key design decisions:
- **One interface, one base class** — `IRelicStats` for the contract, `SimpleCounterStats<T>` for the common case
- **Single tooltip patch** — one Harmony postfix on `RelicModel.get_HoverTip` handles all relics
- **Auto-discovery** — no manual registration; the registry scans the assembly at init
- **Category files, not per-relic files** — relics grouped by what they track, ~7 files total

## Adding stats for a relic from another mod

If your mod adds custom relics and you want Relic Stats to track them, add a dependency on RelicStats and implement `SimpleCounterStats<T>`:

```csharp
using HarmonyLib;
using RelicStats.Core;
using YourMod.Relics;

namespace YourMod.RelicStats;

// Track how much block your custom relic grants
[HarmonyPatch(typeof(MyCustomRelic), nameof(MyCustomRelic.OnCombatStart))]
public sealed class MyCustomRelicStats : SimpleCounterStats<MyCustomRelic>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";

    public static void Postfix(MyCustomRelic __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
}
```

That's it. The registry auto-discovers your class, the tooltip patch displays it, and persistence handles save/load. Your mod just needs to:

1. Reference `RelicStats.dll`
2. Add `"RelicStats"` to your mod manifest `dependencies`
3. Define a `SimpleCounterStats<YourRelic>` subclass with a `[HarmonyPatch]` and a `Format` string

## License

MIT
