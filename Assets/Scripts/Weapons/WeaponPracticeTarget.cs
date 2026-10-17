using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Weapons
{
    /// <summary>Только мишень полигона: показывает реальное здоровье и позволяет повторить опыт.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class WeaponPracticeTarget : Interactable
    {
        private Health health;
        private TextMesh label;
        private string title;
        private void Awake() { health=GetComponent<Health>(); label=GetComponentInChildren<TextMesh>(); if(label!=null) title=label.text; }
        private void LateUpdate() { if(label!=null) label.text=title+"\n"+Mathf.CeilToInt(health.Current)+" / "+health.Maximum+" HP"; }
        public override string GetPrompt(PlayerInteractor actor)=>"Восстановить здоровье мишени";
        protected override bool PerformInteraction(PlayerInteractor actor) { health.Revive(); return true; }
    }
}
