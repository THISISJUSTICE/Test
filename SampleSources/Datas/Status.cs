using System;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    [Serializable]
    public struct StatusPoint : IEquatable<StatusPoint>
    {
        private int _hp;
        private int _armor;
        private int _attack;

        public readonly static StatusPoint Zero = new StatusPoint(0, 0, 0);

        public int HP
        {
            get => _hp;
            set => SetHP(value);
        }

        public int Armor
        {
            get => _armor;
            set => SetArmor(value);
        }

        public int Attack
        {
            get => _attack;
            set => SetAttack(value);
        }

        public int TotalPoint => _hp + _armor + _attack;

        public StatusPoint(int hp, int armor, int attack)
        {
            _hp = 0;
            _armor = 0;
            _attack = 0;

            HP = hp;
            Armor = armor;
            Attack = attack;
        }

        public bool Equals(StatusPoint other)
        {
            if (_hp != other._hp) return false;
            if (_armor != other._armor) return false;
            if (_attack != other._attack) return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is StatusPoint statusPoint && Equals(statusPoint);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_hp, _armor, _attack);
        }

        public StatusPoint MakeChild()
        {
            const float rate = CubeConfig.Status.CHILD_REDUCE_RATE;
            return new StatusPoint(Mathf.FloorToInt(_hp * rate), Mathf.FloorToInt(_armor * rate), Mathf.FloorToInt(_attack * rate));
        }

        private void SetHP(int value) => _hp = Mathf.Max(value, 0);
        private void SetArmor(int value) => _armor = Mathf.Max(value, 0);
        private void SetAttack(int value) => _attack = Mathf.Max(value, 0);
    }

    [Serializable]
    public class StatusValue
    {
        public int MaxHP { get; private set; }
        public int HP { get; private set; }
        public int Armor { get; private set; }
        public int Attack { get; private set; }

        internal StatusValue(StatusPoint statusPoint)
        {
            Initialize(statusPoint);
        }

        private StatusValue(StatusValue statusValue)
        {
            MaxHP = statusValue.MaxHP;
            HP = statusValue.HP;
            Armor = statusValue.Armor;
            Attack = statusValue.Attack;
        }

        public bool IsFullHP()
        {
            return HP == MaxHP;
        }

        internal void EnhanceStatus(StatusPoint statusPoint, bool isHeal = true)
        {
            int previousMaxHP = MaxHP;
            int previousHP = HP;
            Initialize(statusPoint);

            if (isHeal)
                HP = Mathf.Min(MaxHP, previousHP + MaxHP - previousMaxHP);
            else
                HP = previousHP;
        }

        internal void ApplyDamage(int damage)
        {
            HP = Mathf.Max(HP - damage, 0);
        }

        internal int Heal(int heal)
        {
            int prevHP = HP;
            HP = Mathf.Min(MaxHP, HP + heal);

            return heal - HP + prevHP;
        }

        public StatusValue Clone() => new StatusValue(this);

        private void Initialize(StatusPoint statusPoint)
        {
            MaxHP = CubeConfig.Status.INIT_HP + statusPoint.HP * CubeConfig.Status.ADD_HP;
            HP = MaxHP;
            Armor = CubeConfig.Status.INIT_ARMOR + statusPoint.Armor * CubeConfig.Status.ADD_ARMOR;
            Attack = CubeConfig.Status.INIT_ATTACK + statusPoint.Attack * CubeConfig.Status.ADD_ATTACK;
        }
    }
}