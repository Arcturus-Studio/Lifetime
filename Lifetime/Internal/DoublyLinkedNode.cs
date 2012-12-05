using System.Collections.Generic;

namespace TwistedOak.Util {
    ///<summary>A doubly-linked list node.</summary>
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
            _prev._next = this;
            _next._prev = this;
        }

        public void Unlink() {
            _prev._next = _next;
            _next._prev = _prev;
            _next = _prev = this;
        }

        public IEnumerable<T> EnumerateOthers() {
            var h = this;
            while (true) {
                var n = h._next;
                if (n == this) break;
                h = n;
                yield return h._item;
            }
        }
    }
}
