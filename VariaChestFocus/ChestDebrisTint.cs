using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VariaChestFocus
{
    internal sealed class ChestDebrisTint : MonoBehaviour
    {
        private sealed class Slot
        {
            internal Renderer Renderer;
            internal int Index;
            internal ChestTintMaterial Material;
        }
        private static readonly HashSet<ChestDebrisTint> Live = new HashSet<ChestDebrisTint>();
        private readonly List<Slot> _slots = new List<Slot>();

        internal static void Assign(Renderer renderer, Material[] materials)
        {
            Material[] assigned = null;
            for (int i = 0; i < materials.Length; ++i)
            {
                ChestTintMaterial copy = ChestTintMaterial.CloneForDebris(materials[i]);
                if (copy == null) continue;
                if (assigned == null) assigned = (Material[])materials.Clone();
                ChestDebrisTint owner = renderer.GetComponent<ChestDebrisTint>()
                    ?? renderer.gameObject.AddComponent<ChestDebrisTint>();
                Live.Add(owner);
                owner._slots.Add(new Slot { Renderer = renderer, Index = i, Material = copy });
                assigned[i] = copy.Tint;
            }
            renderer.sharedMaterials = assigned ?? materials;
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            foreach (Slot slot in _slots)
            {
                // Also used when the plugin unloads while fragments are still visible.
                if (slot.Renderer != null)
                {
                    Material[] materials = slot.Renderer.sharedMaterials;
                    if (slot.Index < materials.Length && materials[slot.Index] == slot.Material.Tint)
                    {
                        materials[slot.Index] = slot.Material.Original;
                        slot.Renderer.sharedMaterials = materials;
                    }
                }
                slot.Material.Release();
            }
            _slots.Clear();
            Live.Remove(this);
        }

        internal static void RemoveAll()
        {
            foreach (ChestDebrisTint owner in new List<ChestDebrisTint>(Live))
            {
                if (owner == null) continue;
                owner.Release();
                UnityEngine.Object.Destroy(owner);
            }
        }
    }

    [HarmonyPatch(typeof(Destructible), nameof(Destructible.CreateFragments))]
    internal static class ChestFragmentMaterialsPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var setter = AccessTools.PropertySetter(typeof(Renderer), nameof(Renderer.sharedMaterials));
            var assign = AccessTools.Method(typeof(ChestDebrisTint), nameof(ChestDebrisTint.Assign));
            int replacements = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (!instruction.Calls(setter)) continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = assign;
                replacements++;
            }
            if (replacements == 0)
                VariaChestFocusPlugin.Log?.LogWarning("Chest color: fragment material assignment was not found; debris coloring may be unavailable.");
            return code;
        }
    }
}
