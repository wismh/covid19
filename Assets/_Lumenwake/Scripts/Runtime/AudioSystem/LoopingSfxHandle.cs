using System;

namespace Project.Core.AudioSystem
{
    public readonly struct LoopingSfxHandle : IEquatable<LoopingSfxHandle>
    {
        internal const int InvalidId = 0;

        internal LoopingSfxHandle(int id) =>
            Id = id;

        internal int Id { get; }

        public bool IsValid =>
            Id != InvalidId;

        public bool Equals(LoopingSfxHandle other) =>
            Id == other.Id;

        public override bool Equals(object obj) =>
            obj is LoopingSfxHandle other && Equals(other);

        public override int GetHashCode() =>
            Id;

        public static bool operator ==(LoopingSfxHandle left, LoopingSfxHandle right) =>
            left.Equals(right);

        public static bool operator !=(LoopingSfxHandle left, LoopingSfxHandle right) =>
            !left.Equals(right);
    }
}
