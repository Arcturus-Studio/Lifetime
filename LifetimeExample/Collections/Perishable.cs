namespace TwistedOak.Util {
    ///<summary>An item augmented with a lifetime.</summary>
    public struct Perishable<T> {
        public readonly T Value;
        public readonly Lifetime Lifetime;
        public Perishable(T value, Lifetime lifetime) {
            this.Value = value;
            this.Lifetime = lifetime;
        }
    }
}
