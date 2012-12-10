using System;
using System.Collections.Generic;

namespace TwistedOak.Util.Soul {
    ///<summary>A doubly-linked list node for a circular linked list.</summary>
    internal sealed class DoublyLinkedNode<T> {
        private DoublyLinkedNode<T> _next;
        private DoublyLinkedNode<T> _prev;
        private readonly T _item;
        
        ///<summary>Creates a node doubly linked to itself.</summary>
        public static DoublyLinkedNode<T> CreateEmptyCycle() {
            return new DoublyLinkedNode<T>();
        } 
        private DoublyLinkedNode() {
            this._next = this._prev = this;
        }

        ///<summary>Creates a new node containing the given item, inserted preceeding this node.</summary>
        public DoublyLinkedNode<T> Prepend(T item) {
            return new DoublyLinkedNode<T>(item, this);
        }
        private DoublyLinkedNode(T item, DoublyLinkedNode<T> next) {
            this._item = item;
            this._next = next;
            this._prev = next._prev;
            this._prev._next = this;
            this._next._prev = this;
        }

        ///<summary>Removes the node from any list it is in, linking it to itself instead.</summary>
        public void Unlink() {
            this._prev._next = this._next;
            this._next._prev = this._prev;
            this._next = this._prev = this;
        }

        ///<summary>Enumerates all the nodes, except this one, that are in the same circular linked list.</summary>
        public IEnumerable<T> EnumerateOthers() {
            var h = this;
            while (true) {
                var n = h._next;
                if (n == this) break;
                if (ReferenceEquals(n, h)) 
                    throw new InvalidOperationException("List destructively modified while being enumerated.");
                h = n;
                yield return h._item;
            }
        }
    }
}
