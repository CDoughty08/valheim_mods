using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using VariaChestFocus;
using UObject = UnityEngine.Object;

internal static class VisualLifetimeChecks
{
    internal static void Run(Action<string, Action> check)
    {
        void Assert(bool value) { if (!value) throw new Exception("Visual lifetime assertion failed"); }
        ChestTintMaterial Painted()
        {
            var material = new ChestTintMaterial(new Material()) { Paint = new ChestPaintTextures.Lease() };
            material.Tint.Texture = material.Paint.Texture;
            material.Tint.ColorMarker = 123;
            return material;
        }
        check("debris outlives the last painted chest and releases its texture exactly once", () => {
            var source = Painted(); var paint = source.Paint;
            var debris = ChestTintMaterial.CloneForDebris(source.Tint);
            int clears = ChestPaintTextures.Clears;
            source.Release(); source.Release();
            Assert(source.Tint.Destroyed && !debris.Tint.Destroyed && !paint.Texture.Destroyed);
            Assert(paint.Users == 1 && ChestPaintTextures.Clears == clears);
            debris.Release(); debris.Release();
            Assert(paint.Texture.DestroyCalls == 1 && debris.Tint.DestroyCalls == 1);
            Assert(ChestPaintTextures.Clears == clears + 1);
        });
        check("each generated fragment owns its finish and untouched material slots remain shared", () => {
            var source = Painted(); var paint = source.Paint; var vanilla = new Material();
            Material[] inputs = { source.Tint, vanilla };
            var first = new GameObject().AddComponent<Renderer>();
            var second = new GameObject().AddComponent<Renderer>();
            ChestDebrisTint.Assign(first, inputs); ChestDebrisTint.Assign(second, inputs);
            Assert(inputs[0] == source.Tint && first.sharedMaterials[0] != source.Tint);
            Assert(first.sharedMaterials[0] != second.sharedMaterials[0] && first.sharedMaterials[1] == vanilla);
            source.Release(); UObject.Destroy(first.gameObject);
            Assert(!paint.Texture.Destroyed && paint.Users == 1);
            UObject.Destroy(second.gameObject);
            Assert(paint.Texture.Destroyed && !vanilla.Destroyed);
        });
        check("debris snapshots survive recoloring and do not release another color's texture", () => {
            var source = Painted(); var oldPaint = source.Paint;
            var debris = ChestTintMaterial.CloneForDebris(source.Tint);
            source.Paint = new ChestPaintTextures.Lease();
            source.Tint.Texture = source.Paint.Texture; source.Tint.ColorMarker = 456;
            ChestPaintTextures.Release(oldPaint);
            Assert(debris.Tint.ColorMarker == 123 && debris.Tint.Texture == oldPaint.Texture && !oldPaint.Texture.Destroyed);
            debris.Release();
            Assert(oldPaint.Texture.Destroyed && !source.Paint.Texture.Destroyed);
            source.Release();
        });
        check("fallback dye fragments retain textures and plugin cleanup restores original materials", () => {
            var source = new ChestTintMaterial(new Material()) { AlbedoSource = new Texture() };
            ChestDyeTextures.Users.Add(source.AlbedoSource, 1);
            source.Tint.Texture = source.AlbedoSource;
            var renderer = new GameObject().AddComponent<Renderer>();
            ChestDebrisTint.Assign(renderer, new[] { source.Tint });
            Material debris = renderer.sharedMaterials[0];
            int clears = ChestDyeTextures.Clears;
            source.Release();
            Assert(!source.AlbedoSource.Destroyed && ChestDyeTextures.Clears == clears);
            ChestDebrisTint.RemoveAll(); ChestDebrisTint.RemoveAll();
            Assert(renderer.sharedMaterials[0] == source.Original && !source.Original.Destroyed);
            Assert(debris.DestroyCalls == 1 && source.AlbedoSource.DestroyCalls == 1);
        });
        check("debris cleanup respects another mod's replacement and ignores vanilla fragments", () => {
            var source = Painted(); var renderer = new GameObject().AddComponent<Renderer>();
            ChestDebrisTint.Assign(renderer, new[] { source.Tint });
            var replacement = new Material(); renderer.sharedMaterials = new[] { replacement };
            source.Release(); ChestDebrisTint.RemoveAll();
            Assert(renderer.sharedMaterials[0] == replacement && !replacement.Destroyed);
            var vanilla = new GameObject().AddComponent<Renderer>();
            ChestDebrisTint.Assign(vanilla, new[] { replacement });
            Assert(vanilla.GetComponent<ChestDebrisTint>() == null && vanilla.sharedMaterials[0] == replacement);
        });
        check("fragment transpiler wraps material assignment and preserves instruction labels", () => {
            var setter = AccessTools.PropertySetter(typeof(Renderer), nameof(Renderer.sharedMaterials));
            var instruction = new CodeInstruction(OpCodes.Callvirt, setter);
            instruction.labels.Add(default);
            var result = ChestFragmentMaterialsPatch.Transpiler(new[] { instruction, new CodeInstruction(OpCodes.Ret) }).ToArray();
            Assert(result[0].Calls(AccessTools.Method(typeof(ChestDebrisTint), nameof(ChestDebrisTint.Assign))));
            Assert(result[0].labels.Count == 1 && result[1].opcode == OpCodes.Ret);
        });
    }
}
