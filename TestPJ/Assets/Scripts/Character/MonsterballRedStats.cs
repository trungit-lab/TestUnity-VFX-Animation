using System;
using UnityEngine;

namespace Yeolha.BeltScroll
{
    /// <summary>
    /// Identity and per-instance runtime stats for the red Monsterball.
    /// CharacterAbility remains immutable base data; CharacterRuntimeStats owns current HP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterballRedStats : MonoBehaviour, IDamageReceiver
    {
        [Header("Monster information")]
        [SerializeField] private string monsterId = "monsterball_red";
        [SerializeField, TextArea] private string description;
        [SerializeField] private CharacterAbility ability;

        [Header("Monster attack modifiers")]
        [SerializeField, Min(0f)] private float rollSpeedMultiplier = 1.67f;
        [SerializeField, Min(0f)] private float jumpAttackHeightMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float rollDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float jumpDamageMultiplier = 1f;

        private CharacterRuntimeStats runtime;

        public string MonsterId => monsterId;
        public string Description => description;
        public string DisplayName => ability != null ? ability.DisplayName : "Monsterball";
        public CharacterAbility Ability => ability;
        public CharacterRuntimeStats Runtime { get { Initialize(); return runtime; } }
        public float CurrentHP => Runtime.CurrentHP;
        public float MaxHP => Runtime.FinalMaxHP;
        public float AttackPower => Runtime.FinalAttackPower;
        public float Defense => Runtime.FinalDefense;
        public float RollSpeed => Runtime.FinalMoveSpeed * rollSpeedMultiplier;
        public float JumpAttackHeight => Runtime.FinalJumpHeight * jumpAttackHeightMultiplier;
        public float RollDamage => AttackPower * rollDamageMultiplier;
        public float JumpDamage => AttackPower * jumpDamageMultiplier;
        public bool IsDead => CurrentHP <= 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action Died;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (runtime != null) return;
            runtime = new CharacterRuntimeStats();
            runtime.Bind(ability);
        }

        public void ReceiveDamage(DamageInfo damage) => ApplyDamage(damage.Damage);

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDead) return;
            runtime.CurrentHP = Mathf.Max(0f, CurrentHP - amount);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            if (IsDead) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead) return;
            runtime.CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void ResetHealth()
        {
            Initialize();
            runtime.CurrentHP = MaxHP;
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
        }
    }
}
