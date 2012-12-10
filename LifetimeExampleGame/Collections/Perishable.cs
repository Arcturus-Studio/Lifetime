using System.Diagnostics;

namespace TwistedOak.Util {
    ///<summary>An item augmented with a lifetime.</summary>
    [DebuggerDisplay("{ToString()}")]
    public struct Perishable<T> {
        public readonly T Value;
        public readonly Lifetime Lifetime;
        public Perishable(T value, Lifetime lifetime) {
            this.Value = value;
            this.Lifetime = lifetime;
        }
        public override string ToString() {
            return string.Format("({0}) {1}", Lifetime, Value);
        }
    }
}
