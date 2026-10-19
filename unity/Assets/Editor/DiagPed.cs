using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;

namespace Studio.Editor
{
    /// <summary>Auditoria Mecanim standalone — retorna string p/ run_script.</summary>
    public static class DiagPed
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            var go = PrefabUtility.LoadPrefabContents(
                "Assets/Game/Resources/Art/Pedestrians/npc_csl_00_character_01f_01_baked.prefab");
            var anim = go.GetComponentInChildren<Animator>();
            sb.AppendLine($"Controller: {anim?.runtimeAnimatorController?.name}");
            sb.AppendLine($"Avatar: {anim?.avatar?.name} valid={anim?.avatar?.isValid} human={anim?.avatar?.isHuman}");
            if (anim.avatar != null && anim.avatar.isHuman)
            {
                var hd = anim.avatar.humanDescription;
                int mapped = 0, unresolved = 0;
                var sb2 = new StringBuilder();
                foreach (var hb in (HumanBodyBones[])System.Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (hb == HumanBodyBones.LastBone) continue;
                    var t = anim.GetBoneTransform(hb);
                    if (t != null) mapped++;
                    else { unresolved++; sb2.Append(hb).Append(' '); }
                }
                sb.AppendLine($"avatar-mapped bones: {mapped}, nao resolvidos: {unresolved} [{sb2}]");
                foreach (var h in hd.human)
                    sb.AppendLine($"  map: {h.humanName} => {h.boneName}");
            }
            // esqueleto dirigido pelo avatar
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform skel = hips;
            while (skel != null && skel.parent != null && skel.parent != go.transform)
                skel = skel.parent;
            sb.AppendLine($"skel root: {skel?.name}");
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            sb.AppendLine($"SMRs: {smrs.Length}");
            foreach (var smr in smrs)
            {
                int on = 0;
                foreach (var b in smr.bones)
                    if (b != null && skel != null && b.IsChildOf(skel)) on++;
                if (on < smr.bones.Length)
                    sb.AppendLine($"  SMR {smr.name}: {on}/{smr.bones.Length} bones no skel (root={smr.rootBone?.name})");
            }
            PrefabUtility.UnloadPrefabContents(go);
            return sb.ToString();
        }
    }
}
