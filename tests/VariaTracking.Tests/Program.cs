using UnityEngine;
using VariaTracking;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
void Near(float actual, float expected, float tolerance, string message) => Check(MathF.Abs(actual-expected) <= tolerance, $"{message}: {actual} vs {expected}");

// Compare dot-product geometry with an independent angular reference, including reflex cones.
for (int cone = 10; cone <= 360; cone += 10)
for (int bearing = -179; bearing <= 179; bearing += 2)
{
    float angle = bearing * MathF.PI / 180;
    var delta = new Vector3(MathF.Sin(angle)*20, 37, MathF.Cos(angle)*20);
    bool actual = TrackingGeometry.IsInsideCone(delta, new(0,0,1), MathF.Cos(cone * MathF.PI / 360));
    if (Math.Abs(Math.Abs(bearing) - cone/2f) > 0.01f)
        Check(actual == (Math.Abs(bearing) < cone/2f), $"Cone {cone}, bearing {bearing}");
}
Check(TrackingGeometry.IsInsideCone(new(0,50,0), new(0,0,1), 1), "Co-located XZ target");
var random = new Random(710);
for (int i = 0; i < 2000; i++)
{
    float radius = 1 + (float)random.NextDouble()*100;
    float height = ((float)random.NextDouble()*2-1)*radius;
    float bearing = (float)random.NextDouble()*MathF.PI*2;
    float distance = MathF.Sqrt(radius*radius-height*height)*(float)random.NextDouble();
    Vector3 original = new(MathF.Cos(bearing)*distance, height, MathF.Sin(bearing)*distance);
    float jitterAngle = (float)random.NextDouble()*MathF.PI*2;
    Vector3 result = TrackingGeometry.JitterPosition(original, new(), new(MathF.Cos(jitterAngle),MathF.Sin(jitterAngle)), 40, radius);
    Check(result.sqrMagnitude <= radius*radius + 0.01f, "Jitter stays within spherical radius even when jitter exceeds radius");
    Near(result.y, original.y, 0.0001f, "Jitter preserves height");
    Check((result-original).magnitude <= 40.01f, "Jitter remains within configured blur distance");
}

var cfg = new TrackingConfigSnapshot { ShowBoss=true, ShowHostile=true, ShowPassive=true,
    ShowStarred=true, PassiveColor=new(1,1,1,1),
    ExpRate=0.25f, MinMoveDirSqr=0.0001f, MinMoveSpeed=0.15f, XpIntervalSeconds=1, StarredExpBonus=0.25f };
var unlocks = new TrackingUnlockState { ShowHostility=true, ShowStars=false, ShowAllNames=false };
var player = new Player();
var dverger = new Character { faction=Character.Faction.Dverger, AI=new MonsterAI { Alerted=true } };
BaseAI.Enemy = (_, other) => { Check(ReferenceEquals(other, player), "Hostility is relative to local player"); return false; };
Check(CreatureClassifier.TryClassify(dverger, player, cfg, unlocks, out var kind, out _) && kind == MarkerKind.Passive, "Alerted neutral creature stays passive");
BaseAI.Enemy = (_,_) => true;
Check(CreatureClassifier.TryClassify(dverger, player, cfg, unlocks, out kind, out _) && kind == MarkerKind.Hostile, "Enemy creature becomes hostile");
dverger.AI = new AnimalAI();
Check(CreatureClassifier.TryClassify(dverger, player, cfg, unlocks, out kind, out _) && kind == MarkerKind.Passive, "Flee-only AI stays passive");
dverger.Tamed = true;
Check(!CreatureClassifier.TryClassify(dverger, player, cfg, unlocks, out _, out _), "Tamed creatures excluded");

TrackingRadar.LastTrackableCount = 1;
foreach (float dt in new[] { 1f/144, 1f/60, 0.6f })
{
    TrackingExperience.Reset(); player.Raised = 0;
    float duration = 0;
    Time.deltaTime = dt;
    for (int frame=0; frame < 10000; frame++) { TrackingExperience.Tick(player, cfg); duration += dt; }
    // At most one unawarded interval; fractional frames must not be discarded every tick.
    Near(player.Raised, duration*cfg.ExpRate, cfg.ExpRate*1.01f, "XP independent of frame duration");
}
TrackingExperience.Reset(); player.Raised=0; player.Skills.Value.m_level=80; player.Skills.EffectiveLevel=100;
Time.deltaTime=1;
TrackingExperience.Tick(player,cfg);
Near(player.Raised,0.25f,0.0001f,"Temporary skill bonus must not stop base-skill XP");
TrackingRadar.LastStarredInRange=1;
TrackingExperience.Tick(player,cfg);
Near(player.Raised,0.5625f,0.0001f,"Starred XP bonus");
TrackingExperience.Reset(); player.Raised=0; Time.deltaTime=0.75f;
TrackingExperience.Tick(player,cfg); TrackingExperience.Reset(); TrackingExperience.Tick(player,cfg);
Near(player.Raised,0,0.0001f,"Reset prevents XP crossing sessions");

GameObject Trophy(string name)
{
    var go = new GameObject { name=name };
    go.Components[typeof(ItemDrop)] = new ItemDrop { m_itemData = new() { m_shared = new() { m_itemType=ItemDrop.ItemData.ItemType.Trophy, m_name="$"+name } } };
    return go;
}
var creature = new Character { gameObject = new() { name="TestSpecies" } };
var drops = new CharacterDrop();
drops.m_drops.Add(new() { m_prefab=Trophy("TrophyA") });
drops.m_drops.Add(new() { m_prefab=Trophy("TrophyB") });
creature.gameObject.Components[typeof(CharacterDrop)] = drops;
Check(TrackingTrophyKnowledge.HasTrophyDrop(creature),"Trophy species detected");
Check(!TrackingTrophyKnowledge.IsStudied(player,creature),"Unknown species remains unknown");
player.m_trophies.Add("TrophyB");
Check(TrackingTrophyKnowledge.IsStudied(player,creature),"Second trophy drop unlocks species");
player.m_trophies.Clear(); player.Materials.Add("$TrophyB");
Check(TrackingTrophyKnowledge.IsStudied(player,creature),"Shared item name fallback");
drops.m_drops.Clear();
Check(!TrackingTrophyKnowledge.HasTrophyDrop(creature),"Live removal needs no cache invalidation");
int revision = TrackingKnowledge.Revision;
TrackingKnowledge.ResetSession();
Check(TrackingKnowledge.Revision != revision,"Session reset invalidates name cache");

// Boss filtering and color are independent of hostility and its unlock level.
var boss = new Character { m_boss=true };
foreach (bool hostilityUnlocked in new[] { false,true })
foreach (bool bossUnlocked in new[] { false,true })
foreach (bool showHostile in new[] { false,true })
foreach (bool showBoss in new[] { false,true })
{
    var options = cfg;
    options.ShowBoss = showBoss; options.ShowHostile = showHostile;
    options.BossColor = new(1,0,0,1); options.HostileColor = new(0,1,0,1);
    var state = new TrackingUnlockState { ShowHostility=hostilityUnlocked, ShowBossColor=bossUnlocked };
    bool included = CreatureClassifier.TryClassify(boss,player,options,state,out kind,out _);
    Check(included == showBoss, "ShowBoss alone controls boss visibility");
    var color = TrackingDisplay.ResolveColor(MarkerKind.Boss,state,options);
    if (showBoss && bossUnlocked) Check(color.Equals(options.BossColor), "Boss color unlock independent of hostility unlock");
    if (showBoss && !bossUnlocked && hostilityUnlocked) Check(color.Equals(options.HostileColor), "Boss generic hostile color before boss unlock");
}

// Live trophy additions/replacements and per-instance drop tables; no prefab cache.
cfg.ShowTooltips=true; cfg.TrophyEarlyNames=true; cfg.NameTrophylessCreatures=true;
Check(TrackingDisplay.CanShowRealName(creature,player,unlocks,cfg),"Trophyless name visible");
drops.m_drops.Add(new() { m_prefab=Trophy("TrophyUnknown") });
Check(!TrackingDisplay.CanShowRealName(creature,player,unlocks,cfg),"Adding unknown trophy hides early name immediately");
drops.m_drops[0].m_prefab = Trophy("TrophyB");
Check(TrackingDisplay.CanShowRealName(creature,player,unlocks,cfg),"Replacing entry with known trophy reveals name immediately");
var otherInstance = new Character { gameObject = new() { name=creature.name } };
var otherDrops = new CharacterDrop();
otherDrops.m_drops.Add(new() { m_prefab=Trophy("DifferentTrophy") });
otherInstance.gameObject.Components[typeof(CharacterDrop)] = otherDrops;
Check(!TrackingDisplay.CanShowRealName(otherInstance,player,unlocks,cfg),"Same prefab can have different live drops");
player.Materials.Clear();
Check(!TrackingDisplay.CanShowRealName(creature,player,unlocks,cfg),"Knowledge removal is visible without an invalidation hook");

// Reuse the same discovery roster; all these changes must take effect without rescanning.
var targets = new TrackingTargets();
var map = new Minimap();
var near = new Character { AI=new MonsterAI(), transform=new() { position=new(10,0,0) } };
var far = new Character { AI=new MonsterAI(), Level=2, transform=new() { position=new(20,0,0) } };
var roster = new[] { far, near };
targets.Update(roster,player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
Check(targets.Items.Count==2 && targets.StarredCount==1,"Initial live target counts");
Check(ReferenceEquals(targets.Items[0].Character,near),"Nearest first");
far.transform.position = new(5,0,0);
targets.Update(roster,player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
Check(ReferenceEquals(targets.Items[0].Character,far),"Movement updates nearest ordering before next scan");
far.Tamed=true;
targets.Update(roster,player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
Check(targets.Items.Count==1 && targets.StarredCount==0,"Taming removes marker and starred XP immediately");
far.Tamed=false; far.Dead=true; near.transform.position=new(50,0,0);
targets.Update(roster,player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
Check(targets.Items.Count==0,"Death and range exit remove XP eligibility immediately");
far.Dead=false; near.transform.position=new(10,0,0);
map.Explored = _ => false;
targets.Update(roster,player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
Check(targets.Items.Count==0,"Fog blocks live candidates");
var pierceState=unlocks; pierceState.PierceFog=true;
targets.Update(roster,player,map,cfg,pierceState,40);
Check(targets.Items.Count==2,"Fog unlock restores candidates without discovery scan");
map.Explored = _ => true;
var filtered=cfg; filtered.ShowHostile=false;
targets.Update(roster,player,map,filtered,unlocks,40);
Check(targets.Items.Count==0,"Filters update without discovery scan");
BaseAI.Enemy = (_,_) => false;
targets.Update(roster,player,map,filtered,unlocks,40);
Check(targets.Items.Count==2 && targets.Items.Any(t => t.Character == near && t.Kind==MarkerKind.Passive),"Changed relationship updates classification immediately");
far.transform.position=near.transform.position;
targets.Update(roster,player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
int firstId=targets.Items[0].InstanceId;
targets.Update(new[] { near,far },player,map,cfg,unlocks,40);
TrackingTargets.KeepNearest(targets.Items, targets.Items.Count);
Check(targets.Items[0].InstanceId==firstId,"Distance ties are stable across roster ordering");
targets.Update(roster,player,map,cfg,unlocks,0);
Check(targets.Items.Count==0 && targets.StarredCount==0,"Zero radius clears XP and markers");


// Selection must match an independent full sort for every cap, including tied distances.
for (int run = 0; run < 100; run++)
{
    var inputs = Enumerable.Range(0, random.Next(1, 250)).Select(i => new TrackedTarget
        { InstanceId=i, DistSqr=random.Next(0, 40) }).ToList();
    foreach (int cap in new[] { 0, 1, 4, 16, 64, 256 })
    {
        var expected = inputs.OrderBy(t => t.DistSqr).ThenBy(t => t.InstanceId).Take(cap).Select(t => t.InstanceId);
        var selected = new List<TrackedTarget>(inputs);
        TrackingTargets.KeepNearest(selected, cap);
        Check(expected.SequenceEqual(selected.Select(t => t.InstanceId)), "Bounded selection matches full ordering");
    }
}
// The same production accumulator is used by Weight and Tracking.
var experience = new Varia.Shared.MovementExperience();
var weightPlayer = new Player();
Time.deltaTime = 0.3f;
for (int i=0; i<2000; i++) experience.Tick(weightPlayer, TrackingSkill.SkillType, 0.2f, 1f, 0.0001f, 0.15f);
Near(weightPlayer.Raised, 120f, 0.01f, "Weight rate preserves fractional frames");
experience.Reset(); weightPlayer.Raised=0;
weightPlayer.Skills.Value.m_level=80; weightPlayer.Skills.EffectiveLevel=100;
Time.deltaTime=1;
experience.Tick(weightPlayer, TrackingSkill.SkillType, 0.2f, 1f, 0.0001f, 0.15f);
Near(weightPlayer.Raised,0.2f,0.0001f,"Effective skill cap does not stop permanent XP");
weightPlayer.Skills.Value.m_level=Skills.c_MaxSkillLevel;
experience.Tick(weightPlayer, TrackingSkill.SkillType, 0.2f, 1f, 0.0001f, 0.15f);
Near(weightPlayer.Raised,0.2f,0.0001f,"Permanent cap stops XP");
experience.Reset(); weightPlayer.Skills.Value.m_level=0; weightPlayer.Raised=0;
Time.deltaTime=0.5f;
experience.Tick(weightPlayer,TrackingSkill.SkillType,0.2f,1f,0.0001f,0.15f);
experience.Tick(weightPlayer,TrackingSkill.SkillType,0.6f,1f,0.0001f,0.15f);
Near(weightPlayer.Raised,0.4f,0.0001f,"Changing carried load is integrated per frame");
experience.Reset(); Time.deltaTime=0.75f;
experience.Tick(weightPlayer,TrackingSkill.SkillType,0.2f,1f,0.0001f,0.15f);
var nextPlayer=new Player();
experience.Tick(nextPlayer,TrackingSkill.SkillType,0.2f,1f,0.0001f,0.15f);
Near(nextPlayer.Raised,0f,0.0001f,"Switching character resets accumulated movement");
Console.WriteLine($"Passed {checks} total checks including shared XP and bounded selection.");
