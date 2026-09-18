using System;
using HarmonyLib;
using VariaFood;
using VariaFood.Patches;

internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        var harmony = new Harmony("com.varia.food.tests");
        foreach (var type in new[] { typeof(PlayerUpdateFoodPatch), typeof(PlayerEatFoodPatch),
            typeof(PlayerCanEatPatch), typeof(PlayerConsumeItemDrinkPatch), typeof(FoodCanEatAgainPatch),
            typeof(PlayerLoadFoodPatch), typeof(ItemDataGetTooltipPatch), typeof(HudAwakeFoodSlotsPatch), typeof(HudUpdateFoodDrinkHintPatch) })
            harmony.CreateClassProcessor(type).Patch();

        Run("expiry updates every food and catches up elapsed seconds", () =>
        {
            var player = new Player();
            Add(player, Item("A"), 0.5f); Add(player, Item("B"), 1f);
            var survivor = Add(player, Item("C"), 20f);
            player.UpdateFood(3.25f, false);
            Equal(1, player.m_foods.Count); Near(17f, survivor.m_time);
            Near(0.25f, player.m_foodUpdateTimer); Near(45f, player.MaxHealth);
        });
        Run("forced refresh does not age foods or create timer debt", () =>
        {
            var player = new Player { m_foodUpdateTimer = 0.25f };
            var food = Add(player, Item("A"), 100f);
            for (int i = 0; i < 10; i++) player.UpdateFood(0f, true);
            Near(100f, food.m_time); Near(0.25f, player.m_foodUpdateTimer);
        });
        Run("first eat applies duration and stats before max values", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot;
            cfg.DurationMult = 2f; cfg.HealthMult = 3f;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var player = new Player();
            True(player.EatFood(Item("A")));
            Near(200f, player.m_foods[0].m_time); Near(85f, player.MaxHealth);
            Near(0f, player.m_foodUpdateTimer);
        });
        Run("duration and pending item work with vanilla slots", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot;
            cfg.ExtraSlots = false; cfg.DurationMult = 2f;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var player = new Player();
            player.EatFood(Item("A"));
            Near(200f, player.m_foods[0].m_time); Near(45f, player.MaxHealth);
            True(PlayerUpdateFoodPatch.Pending.Item == null);
        });
        Run("pending item cannot leak to another player or through exceptions", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; cfg.ExtraSlots = false;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var other = new Player(); var otherFood = Add(other, Item("A"), 30f);
            var player = new Player { ThrowOnEat = true, DuringEat = () => other.UpdateFood(0f, true) };
            try { player.EatFood(Item("A")); throw new Exception("Expected injected failure"); }
            catch (InvalidOperationException) { }
            Near(30f, otherFood.m_time); True(PlayerUpdateFoodPatch.Pending.Item == null);
        });
        Run("pulse clock continues while no foods are active", () =>
        {
            var player = new Player(); player.UpdateFood(9f, false);
            Add(player, Item("A"), 100f); player.UpdateFood(1f, false);
            Equal(1, player.Heals); Near(3f, player.Health);
            Near(0f, player.m_foodRegenTimer);
        });
        Run("tick-only regeneration waits for a pulse", () =>
        {
            var player = new Player(); Add(player, Item("A"), 100f);
            for (int i = 0; i < 9; i++) player.UpdateFood(1f, false);
            Equal(0, player.Heals); player.UpdateFood(1f, false); Equal(1, player.Heals);
        });
        Run("continuous regeneration preserves rate and status modifiers", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot;
            cfg.HealthRegenMode = RegenMode.PerSecond;
            cfg.StaminaRegenMode = RegenMode.PerSecond; cfg.StaminaRegenMult = 1f;
            cfg.StaminaRegenScale = RegenScaleMode.StatBoost;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var player = new Player(); player.m_seman.HealthModifier = 0.5f;
            Add(player, Item("A"), 100f);
            for (int i = 0; i < 10; i++) player.UpdateFood(1f, false);
            Near(2f, player.Health); Near(2.5f, player.Stamina);
        });
        Run("potion entries do not supply food regeneration or food statistics", () =>
        {
            var player = new Player(); var potion = Potion(); potion.m_shared.m_foodRegen = 99f;
            player.ConsumeItem(new Inventory(), potion);
            Equal(0, player.m_foods.Count); Equal(1, Display(player).Count); Equal(0, Game.instance.FoodEaten);
            PlayerUpdateFoodPatch.ComputeRegenTickAmounts(player, VariaFoodPlugin.ConfigSnapshot,
                out var hp, out var stamina, out var eitr);
            Near(0f, hp); Near(0f, stamina); Near(0f, eitr);
        });
        Run("potion timers follow live effects even when food drain is paused", () =>
        {
            var player = new Player(); var potion = Potion();
            potion.m_shared.m_consumeStatusEffect = new StatusEffect { m_ttl = 100f };
            var active = new StatusEffect { m_ttl = 200f, Remaining = 160f };
            player.m_seman.Effects[1] = active;
            Game.m_foodRate = 0f;
            player.ConsumeItem(new Inventory(), potion);
            Near(160f, Display(player)[0].m_time);
            active.Remaining = 125f; player.UpdateFood(1f, false);
            Near(125f, Display(player)[0].m_time);
            player.m_seman.Effects.Clear(); player.UpdateFood(1f, false);
            Equal(0, Display(player).Count);
        });
        Run("fallback potion time ignores food drain multiplier", () =>
        {
            var player = new Player(); Game.m_foodRate = 2f;
            player.ConsumeItem(new Inventory(), Potion());
            player.UpdateFood(5f, false); Near(1195f, Display(player)[0].m_time);
        });
        Run("burn duration reflects changed item data and config", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; var potion = Potion();
            potion.m_shared.m_consumeStatusEffect = new StatusEffect { m_ttl = 30f };
            Near(30f, FoodMath.EffectiveBurnTime(potion, cfg));
            potion.m_shared.m_consumeStatusEffect.m_ttl = 60f;
            Near(60f, FoodMath.EffectiveBurnTime(potion, cfg));
            potion.m_shared.m_consumeStatusEffect = null; cfg.DrinkBurn = 90f;
            Near(90f, FoodMath.EffectiveBurnTime(potion, cfg));
        });
        Run("drink-first eating maps to a dedicated HUD slot with gaps", () =>
        {
            var player = new Player(); Add(player, Potion(), 100f); Add(player, Item("A"), 100f);
            var map = new int[5]; FoodHudLayout.MapSlots(player.m_foods, VariaFoodPlugin.ConfigSnapshot, map);
            Equal(1, map[0]); Equal(-1, map[1]); Equal(-1, map[2]); Equal(-1, map[3]); Equal(0, map[4]);
            Equal("MeadTasty", player.m_foods[0].m_name);
        });
        Run("one food plus one drink uses two logical HUD slots", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; cfg.FoodSlots = 1;
            Equal(2, FoodSlots.TotalSlots(cfg));
            var player = new Player(); Add(player, Potion(), 100f);
            var map = new int[3]; FoodHudLayout.MapSlots(player.m_foods, cfg, map);
            Equal(-1, map[0]); Equal(0, map[1]); Equal(-1, map[2]);
        });
        Run("full solid slots cannot evict a drink", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; cfg.FoodSlots = 1;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var player = new Player(); Add(player, Potion(), 1f); Add(player, Item("A"), 100f);
            True(!player.CanEat(Item("B"), false));
            player.m_foods[1].m_time = 20f; True(player.EatFood(Item("B")));
            Equal("MeadTasty", player.m_foods[0].m_name); Equal("B", player.m_foods[1].m_name);
        });
        Run("drink detection supports uppercase and acronym tokens", () =>
        {
            True(DrinkClassifier.IsDrink(Item("APPLE_JUICE")));
            True(DrinkClassifier.IsDrink(Item("VIPTea")));
            True(DrinkClassifier.IsDrink(Item("BarleyWine")));
            True(!DrinkClassifier.IsDrink(Item("Steak")));
            True(!DrinkClassifier.IsDrink(Item("MeadowBerries")));
            DrinkClassifier.Rebuild("soda"); True(DrinkClassifier.IsDrink(Item("SODA")));
        });
        Run("classification responds to new hints and item data", () =>
        {
            var item = Item("Unknown"); item.m_dropPrefab = null;
            True(!DrinkClassifier.IsDrink(item));
            True(DrinkClassifier.IsDrink(item, "AppleJuice"));
            item.m_shared.m_name = "Sausage";
            True(!DrinkClassifier.IsDrink(item, "Sausage"));
        });
        Run("refilling food retains the newly consumed item data", () =>
        {
            var player = new Player(); Add(player, Item("A"), 1f);
            var upgraded = Item("A"); upgraded.m_shared.m_food = 70f;
            True(player.EatFood(upgraded));
            Near(95f, player.MaxHealth); True(player.m_foods[0].m_item == upgraded);
            player.UpdateFood(1f, false); Near(95f, player.MaxHealth);
        });
        Run("disabled healing removes vanilla tooltip healing line", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; cfg.HealthRegenMult = 0f;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            ItemDrop.ItemData.OriginalTooltip = "Food\n$item_food_regen: <color=orange>2 hp/tick</color>";
            Equal("Food", ItemDrop.ItemData.GetTooltip(Item("A"), 1, false, 1f));
        });
        Run("load removes stale display entries with feature disabled", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; cfg.Enabled = false; cfg.ExtraSlots = false;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var player = new Player(); Add(player, Potion(), 100f); Add(player, Item("A"), 100f);
            player.Load(new ZPackage()); Equal(1, player.m_foods.Count); Equal("A", player.m_foods[0].m_name);
        });
        Run("vanilla-equivalent configuration passes through", () =>
        {
            var cfg = VariaFoodPlugin.ConfigSnapshot; cfg.NoDecay = false; cfg.ExtraSlots = false;
            VariaFoodPlugin.ConfigSnapshot = cfg;
            var player = new Player(); player.UpdateFood(1f, false); Equal(1, player.VanillaUpdates);
        });
        Run("potion indicators cannot occupy food slots or be vomited/saved", () =>
        {
            var player = new Player();
            player.ConsumeItem(new Inventory(), Potion());
            Add(player, Item("A"), 100f); Add(player, Item("B"), 100f);
            Equal(2, player.GetFoods().Count);
            player.m_foods.Clear(); // Vanilla food removal touches only this list.
            Equal(1, Display(player).Count);
            player.Load(new ZPackage()); Equal(0, Display(player).Count);
        });
        Run("disabling potion slots clears transient indicators and preserves food", () =>
        {
            var player=new Player(); player.ConsumeItem(new Inventory(), Potion());
            Add(player,Item("A"),100f);
            var cfg=VariaFoodPlugin.ConfigSnapshot; cfg.ExtraSlots=false; cfg.NoDecay=false;
            VariaFoodPlugin.ConfigSnapshot=cfg;
            player.UpdateFood(0f,false);
            cfg.ExtraSlots=true; VariaFoodPlugin.ConfigSnapshot=cfg;
            Equal(0,Display(player).Count); Equal(1,player.m_foods.Count);
        });
        Run("food drinks have HUD priority over display-only potions", () =>
        {
            var player=new Player(); player.ConsumeItem(new Inventory(),Potion());
            Add(player,Item("AppleJuice"),100f);
            var combined=new System.Collections.Generic.List<Player.Food>(player.m_foods);
            DrinkDisplay.AppendTo(player,combined,VariaFoodPlugin.ConfigSnapshot);
            var map=new int[5]; FoodHudLayout.MapSlots(combined,VariaFoodPlugin.ConfigSnapshot,map);
            Equal(0,map[4]); Equal(1,player.m_foods.Count);
        });
        foreach (bool grouped in new[] { false, true })
        Run("HUD live growth, shrink, drink relocation and unload (grouped=" + grouped + ")", () =>
        {
            HudAwakeFoodSlotsPatch.Shutdown();
            var root = new UnityEngine.GameObject("HUD");
            var hud = new Hud { m_foodBars = new UnityEngine.UI.Image[3], m_foodIcons = new UnityEngine.UI.Image[3], m_foodTime = new TMPro.TMP_Text[3] };
            for (int i=0; i<3; i++)
            {
                var group = grouped ? new UnityEngine.GameObject("Slot" + i) : root;
                if(grouped) { group.transform.SetParent(root.transform,false); ((UnityEngine.RectTransform)group.transform).anchoredPosition=new UnityEngine.Vector2(i*40,0); }
                var icon = new UnityEngine.GameObject("Icon",typeof(UnityEngine.UI.Image));
                var time = new UnityEngine.GameObject("Timer",typeof(TMPro.TMP_Text));
                var bar = new UnityEngine.GameObject("Bar",typeof(UnityEngine.UI.Image));
                icon.transform.SetParent(group.transform,false); time.transform.SetParent(group.transform,false); bar.transform.SetParent(root.transform,false);
                ((UnityEngine.RectTransform)icon.transform).anchoredPosition=new UnityEngine.Vector2(i*40,0);
                ((UnityEngine.RectTransform)time.transform).anchoredPosition=new UnityEngine.Vector2(i*40,20);
                ((UnityEngine.RectTransform)bar.transform).anchoredPosition=new UnityEngine.Vector2(i*40,40);
                hud.m_foodIcons[i]=icon.GetComponent<UnityEngine.UI.Image>();
                hud.m_foodBars[i]=bar.GetComponent<UnityEngine.UI.Image>();
                hud.m_foodTime[i]=time.GetComponent<TMPro.TMP_Text>();
            }
            var originals=hud.m_foodIcons;
            var potion=Potion();
            var prefab=new UnityEngine.GameObject("MeadTasty",typeof(ItemDrop)); prefab.GetComponent<ItemDrop>().m_itemData=potion;
            ObjectDB.instance=new ObjectDB(); ObjectDB.instance.m_items.Add(prefab);
            hud.Awake(); Equal(5,hud.m_foodIcons.Length);
            var player=new Player(); var food=Item("A"); Add(player,food,100f);
            player.ConsumeItem(new Inventory(),potion); hud.UpdateFood(player);
            True(hud.m_foodIcons[0].sprite==food.GetIcon()); True(hud.m_foodIcons[4].sprite==potion.GetIcon());
            Equal("20m",hud.m_foodTime[4].text);
            var cfg=VariaFoodPlugin.ConfigSnapshot; cfg.FoodSlots=6; cfg.DrinkSlots=2; VariaFoodPlugin.ConfigSnapshot=cfg;
            hud.UpdateFood(player); Equal(8,hud.m_foodIcons.Length); True(hud.m_foodIcons[6].sprite==potion.GetIcon());
            cfg.FoodSlots=1; cfg.DrinkSlots=1; VariaFoodPlugin.ConfigSnapshot=cfg;
            hud.UpdateFood(player); True(hud.m_foodIcons[1].sprite==potion.GetIcon());
            for(int i=2;i<8;i++) True(!hud.m_foodIcons[i].gameObject.activeSelf);
            cfg.Enabled=false; VariaFoodPlugin.ConfigSnapshot=cfg; player.UpdateFood(0f,false); hud.UpdateFood(player);
            foreach(var transform in root.GetComponentsInChildren<UnityEngine.Transform>(true))
                if(transform.name.StartsWith("VariaDrinkHint_")) True(!transform.gameObject.activeSelf);
            cfg.Enabled=true; VariaFoodPlugin.ConfigSnapshot=cfg; hud.UpdateFood(player); True(!hud.m_foodIcons[1].gameObject.activeSelf);
            HudAwakeFoodSlotsPatch.Shutdown(); True(ReferenceEquals(originals,hud.m_foodIcons));
            foreach(var transform in root.GetComponentsInChildren<UnityEngine.Transform>(true))
                True(!transform.name.StartsWith("Varia_") && !transform.name.StartsWith("VariaDrinkHint_"));
        });
        Console.WriteLine($"Passed {_passed} VariaFood regression checks.");
    }

    private static void Run(string name, Action test)
    {
        VariaFoodPlugin.ConfigSnapshot = new FoodConfigSnapshot
        {
            Enabled = true, NoDecay = true, ExtraSlots = true, FoodSlots = 4, DrinkSlots = 1,
            DurationMult = 1f, HealthMult = 1f, StaminaMult = 1f, EitrMult = 1f,
            HealthRegenMult = 1f, DrinkBurn = 1200f,
            EitrRegenMult = 0f, EitrRegenMode = RegenMode.PerSecond, EitrRegenScale = RegenScaleMode.StatBoost
        };
        Game.m_foodRate = 1f; Game.instance = new Game(); PlayerUpdateFoodPatch.Pending = default;
        DrinkDisplay.ClearAll();
        DrinkClassifier.Rebuild("");
        test(); _passed++; Console.WriteLine("PASS " + name);
    }
    private static ItemDrop.ItemData Item(string name) => new ItemDrop.ItemData
    {
        m_dropPrefab = new UnityEngine.GameObject { name = name },
        m_shared = new ItemDrop.ItemData.SharedData
        { m_name = name, m_food = 20f, m_foodStamina = 10f, m_foodBurnTime = 100f, m_foodRegen = 2f }
    };
    private static ItemDrop.ItemData Potion()
    {
        var item = Item("MeadTasty");
        item.m_shared.m_food = item.m_shared.m_foodStamina = item.m_shared.m_foodBurnTime = 0f;
        return item;
    }
    private static Player.Food Add(Player player, ItemDrop.ItemData item, float time)
    {
        var food = new Player.Food { m_item = item, m_name = item.m_dropPrefab.name, m_time = time };
        player.m_foods.Add(food); return food;
    }
    private static System.Collections.Generic.List<Player.Food> Display(Player player)
    {
        var list=new System.Collections.Generic.List<Player.Food>();
        DrinkDisplay.AppendTo(player,list,VariaFoodPlugin.ConfigSnapshot); return list;
    }
    private static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
    private static void Near(float expected, float actual)
    {
        if (Math.Abs(expected - actual) > 0.001f) throw new Exception($"Expected {expected}, got {actual}");
    }
}
