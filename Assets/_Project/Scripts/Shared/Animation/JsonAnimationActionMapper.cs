using System.Collections.Generic;
using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    public class JsonAnimationActionMapper
    {
        private readonly Dictionary<int, string> hashToActionName = new Dictionary<int, string>();

        public void Configure(
            JsonAnimatorActionMap[] customMappings,
            string meleeAttackActionName,
            string rangedAttackActionName,
            string comboAttackActionName
        )
        {
            hashToActionName.Clear();
            if (customMappings != null)
            {
                foreach (JsonAnimatorActionMap map in customMappings)
                {
                    SetActionMapping(map.animatorStateName, map.jsonActionName);
                }
            }

            SetActionMapping("walk", "Move");
            SetActionMapping("Attack", meleeAttackActionName);
            SetActionMapping("SlashAttack", meleeAttackActionName);
            SetActionMapping("Boss_slash_attack", meleeAttackActionName);
            SetActionMapping("rangeAttack", rangedAttackActionName);
            SetActionMapping("EnergyBlast", rangedAttackActionName);
            SetActionMapping("Boss_energy_blast", rangedAttackActionName);
            SetActionMapping("ComboSlashBlast", comboAttackActionName);
            SetActionMapping("Boss_combo_slash_blast", comboAttackActionName);
        }

        public bool TryGetActionName(int stateHash, out string actionName)
        {
            return hashToActionName.TryGetValue(stateHash, out actionName);
        }

        private void SetActionMapping(string animatorStateName, string actionName)
        {
            if (string.IsNullOrWhiteSpace(animatorStateName) || string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            hashToActionName[Animator.StringToHash(animatorStateName)] = actionName;
        }
    }
}
