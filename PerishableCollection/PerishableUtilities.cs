using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Linq;
using System.Reactive.Subjects;
using TwistedOak.Util;

namespace TwistedOak.Collections {
    ///<summary>Utility methods related to perishables and perishable collections.</summary>
    public static class PerishableUtilities {
        ///<summary>Feeds observed items into a new perishable collection, stopping if a given lifetime ends.</summary>
        public static PerishableCollection<T> ToPerishableCollection<T>(this IObservable<Perishable<T>> observable, Lifetime lifetime = default(Lifetime)) {
            if (observable == null) throw new ArgumentNullException("observable");
            var result = new PerishableCollection<T>();
            observable.Subscribe(result.Add);
            return result;
        }

        /// <summary>Projects the value of each perishable element of an observable sequence into a new form.</summary>
        public static IObservable<Perishable<TOut>> Select<TIn, TOut>(this IObservable<Perishable<TIn>> observable, Func<TIn, TOut> projection) {
            if (observable == null) throw new ArgumentNullException("observable");
            if (projection == null) throw new ArgumentNullException("projection");
            return observable.Select(e => new Perishable<TOut>(projection(e.Value), e.Lifetime));
        }
        /// <summary>Filters the perishable elements of an observable sequence by value based on a predicate.</summary>
        public static IObservable<Perishable<T>> Where<T>(this IObservable<Perishable<T>> observable, Func<T, bool> predicate) {
            if (observable == null) throw new ArgumentNullException("observable");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return observable.Where(e => predicate(e.Value));
        }
        /// <summary>Projects the value of each perishable element of a sequence into a new form.</summary>
        public static IEnumerable<Perishable<TOut>> Select<TIn, TOut>(this IEnumerable<Perishable<TIn>> sequence, Func<TIn, TOut> projection) {
            if (sequence == null) throw new ArgumentNullException("sequence");
            if (projection == null) throw new ArgumentNullException("projection");
            return sequence.Select(e => new Perishable<TOut>(projection(e.Value), e.Lifetime));
        }
        /// <summary>Filters the perishable elements of a sequence by value based on a predicate.</summary>
        public static IEnumerable<Perishable<T>> Where<T>(this IEnumerable<Perishable<T>> sequence, Func<T, bool> predicate) {
            if (sequence == null) throw new ArgumentNullException("sequence");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return sequence.Where(e => predicate(e.Value));
        }

        /// <summary>Tracks the number of observed items that have not yet perished, counting up from 0.</summary>
        /// <param name="observable">The source observable that provides perishable items to be counted.</param>
        /// <param name="completeWhenSourceCompletes">
        /// Determines when the resulting observable completes.
        /// If true, the resulting observable completes as soon as the source observable completes.
        /// If false, the resulting observable completes when the observed count is 0 and the source observable has completed.
        /// </param>
        public static IObservable<int> ObserveNonPerishedCount<T>(this IObservable<Perishable<T>> observable, bool completeWhenSourceCompletes) {
            if (observable == null) throw new ArgumentNullException("observable");
            return new AnonymousObservable<int>(observer => {
                var d = new DisposableLifetime();
                var count = 0;
                var syncRoot = new object();
                var isSourceComplete = false;
                observer.OnNext(0);
                Action tryComplete = () => {
                    if (isSourceComplete && (completeWhenSourceCompletes || count == 0)) {
                        observer.OnCompleted();
                    }
                };
                observable.Subscribe(
                    e => {
                        lock (syncRoot) {
                            count += 1;
                            observer.OnNext(count);
                        }
                        e.Lifetime.WhenDead(
                            () => {
                                lock (syncRoot) {
                                    // may have finished while acquiring the lock
                                    if (d.Lifetime.IsDead) return;

                                    count -= 1;
                                    observer.OnNext(count);
                                    tryComplete();
                                }
                            },
                            d.Lifetime);
                    },
                    ex => {
                        lock (syncRoot) {
                            d.Dispose();
                            observer.OnError(ex);
                        }
                    },
                    () => {
                        lock (syncRoot) {
                            isSourceComplete = true;
                            tryComplete();
                        }
                    },
                    d.Lifetime);
                return d;
            });
        }

        ///<summary>Groups perishable items into perishable groups that perish when they contain no more non-perished items.</summary>
        public static IObservable<Perishable<KeyValuePair<TKey, IObservable<Perishable<TVal>>>>> PerishableGroupBy<TKey, TVal>(
                this IObservable<Perishable<TVal>> collection,
                Func<TVal, TKey> keySelector,
                IEqualityComparer<TKey> keyEquality = null) {
            if (collection == null) throw new ArgumentNullException("collection");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            return new AnonymousObservable<Perishable<KeyValuePair<TKey, IObservable<Perishable<TVal>>>>>(observer => {
                var subLife = new DisposableLifetime();
                var groupMap = new Dictionary<TKey, PerishableGroup<TVal>>(keyEquality ?? EqualityComparer<TKey>.Default);
                var syncRoot = new object();
                var completion = new Subject<Perishable<TVal>>();
                collection.Subscribe(
                    item => {
                        var key = keySelector(item.Value);

                        // get or create the group the item belongs to
                        PerishableGroup<TVal> group;
                        bool wasCreated;
                        lock (syncRoot) {
                            wasCreated = !groupMap.TryGetValue(key, out group);
                            if (wasCreated) {
                                group = new PerishableGroup<TVal>();
                                groupMap.Add(key, group);
                            }
                            group.Size += 1;
                        }

                        // pass created groups along to the observer
                        if (wasCreated) observer.OnNext(
                            new Perishable<KeyValuePair<TKey, IObservable<Perishable<TVal>>>>(
                                new KeyValuePair<TKey, IObservable<Perishable<TVal>>>(
                                    key,
                                    new AnonymousObservable<Perishable<TVal>>(obs => {
                                        var groupSubLife = new DisposableLifetime();
                                        group.Collection.AsObservable().Subscribe(obs.OnNext, groupSubLife.Lifetime);
                                        completion.Subscribe(obs.OnNext, obs.OnError, obs.OnCompleted, groupSubLife.Lifetime);
                                        return groupSubLife;
                                    })),
                                group.LifetimeSource.Lifetime));

                        // add the item to its group
                        group.Collection.Add(item);

                        // remove the item from its group upon perishing, causing the group to perish if it contains no more items
                        item.Lifetime.WhenDead(
                            () => {
                                lock (syncRoot) {
                                    group = groupMap[key];
                                    group.Size -= 1;
                                    if (group.Size > 0) return;
                                    groupMap.Remove(key);
                                }
                                group.LifetimeSource.EndLifetime();
                            });
                    },
                    ex => {
                        completion.OnError(ex);
                        observer.OnError(ex);
                    },
                    () => {
                        completion.OnCompleted();
                        observer.OnCompleted();
                    },
                    subLife.Lifetime);
                return subLife;
            });
        }
        private sealed class PerishableGroup<TVal> {
            public readonly LifetimeSource LifetimeSource = new LifetimeSource();
            public readonly PerishableCollection<TVal> Collection = new PerishableCollection<TVal>();
            public int Size;
        }
    }
}
