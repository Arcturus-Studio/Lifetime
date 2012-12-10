using System;
using System.Collections.Generic;
using System.Reactive;
using TwistedOak.Util;

namespace TwistedOak.Collections {
    /// <summary>
    /// A thread-safe collection that supports both enumeration and observation, where added items are automatically removed when they perish.
    /// Supports constant time addition and removal of items.
    /// Useful for when you want to create projected or filtered views of a collection that support 'an item was removed' notifications in a nice way.
    /// In particular, the pairing of items with lifetimes means the views don't have to worry about details like 'do I remove X_1 or X_2 when I find out that X was removed?'.
    /// </summary>
    public sealed class PerishableCollection<T> {
        private sealed class Link {
            public Link Next;
            public Link Prev;
            public Perishable<T> Item;
        }

        private event Action<Perishable<T>> OnItem;
        private readonly Link _root;

        ///<summary>Creates a new empty perishable collection.</summary>
        public PerishableCollection() {
            this._root = new Link();
            _root.Next = _root.Prev = _root;
        }

        ///<summary>Adds an item to the collection, removing it when the given lifetime ends.</summary>
        public void Add(T item, Lifetime lifetime) {
            Add(new Perishable<T>(item, lifetime));
        }

        ///<summary>Adds an item to the collection, removing it when the item perishes.</summary>
        public void Add(Perishable<T> item) {
            // insert at end of linked list
            Link node;
            Action<Perishable<T>> onItemDuringInsert;
            lock (_root) {
                node = _root.Prev.Next = _root.Prev = new Link {
                    Item = item, 
                    Prev = _root.Prev, 
                    Next = _root
                };
                onItemDuringInsert = OnItem;
            }

            // eventual removal
            item.Lifetime.WhenDead(() => {
                lock (_root) {
                    node.Prev.Next = node.Next;
                    node.Next.Prev = node.Prev;
                    // leave removed node linked outward, so enumerations can escape
                }
            });

            // inform any listeners of the new item
            if (onItemDuringInsert != null) 
                onItemDuringInsert(item);
        }

        /// <summary>
        /// First, enumerates the items in the collection, passing them to a callback, until caught up with the collection's current state.
        /// Then, observes future items by subscribing the callback to the OnItem event.
        /// Stops enumerating and/or observing when the given subscription lifetime ends.
        /// </summary>
        private void EnumerateAndObserve(Action<Perishable<T>> onItem, Lifetime subscriptionLifetime = default(Lifetime)) {
            if (onItem == null) throw new ArgumentNullException("onItem");

            var h = _root;
            while (true) {
                // observe current items by enumerating through the linked list
                while (!subscriptionLifetime.IsDead) {
                    var n = h.Next;
                    if (n == _root) break;
                    h = n;
                    onItem(h.Item);
                }

                // switch to observing by events if no items were inserted during the enumeration
                lock (_root) {
                    if (h.Next != _root) continue; // enumerate more then try again
                    // observe until subscription ended
                    OnItem += onItem;
                    subscriptionLifetime.WhenDead(() => OnItem -= onItem);
                    break;
                }
            }
        }

        /// <summary>
        /// Enumerates non-perished items currently in the collection.
        /// Items may be added or perished while enumeration is occuring.
        /// Items added or perished during the enumeration may or may not be included in the result.
        /// </summary>
        public IEnumerable<Perishable<T>> CurrentItems() {
            var h = _root;
            while (true) {
                h = h.Next;
                if (h == _root) break;
                yield return h.Item;
            }
        }

        /// <summary>
        /// Returns an observable that observes all non-perished items added to the collection, now and in the future.
        /// Items may perish while being observed.
        /// </summary>
        public IObservable<Perishable<T>> AsObservable() {
            return new AnonymousObservable<Perishable<T>>(observer => {
                var d = new DisposableLifetime();
                EnumerateAndObserve(observer.OnNext, d.Lifetime);
                return d;
            });
        }
    }
}
