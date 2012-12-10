using System.Diagnostics;
using TwistedOak.Util;

namespace TwistedOak.Collections {
    ///<summary>An item augmented with a lifetime.</summary>
    [DebuggerDisplay("{ToString()}")]
    public struct Perishable<T> {
        ///<summary>The value that 'perishes' when the associated lifetime ends.</summary>
        public readonly T Value;
        ///<summary>The lifetime of the perishable value.</summary>
        public readonly Lifetime Lifetime;
        ///<summary>Creates a new perishable with the given value and lifetime.</summary>
        public Perishable(T value, Lifetime lifetime) {
            this.Value = value;
            this.Lifetime = lifetime;
        }
        ///<summary>A string representation of the perishable value.</summary>
        public override string ToString() {
            return string.Format("({0}) {1}", Lifetime, Value);
        }
    }
}
