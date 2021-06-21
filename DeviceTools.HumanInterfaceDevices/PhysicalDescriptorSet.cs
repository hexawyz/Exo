using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.HumanInterfaceDevices
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PhysicalDescriptorSet : IReadOnlyList<PhysicalDescriptor>, IList<PhysicalDescriptor>, IEquatable<PhysicalDescriptorSet>
    {
        public struct Enumerator : IEnumerator<PhysicalDescriptor>
        {
            private readonly PhysicalDescriptorSetCollection _collection;
            private readonly int _startIndex;
            private ushort _index;
            // Cache the item count so as to avoid unaligned reads.
            private readonly ushort _count;

            internal Enumerator(PhysicalDescriptorSet set)
            {
                _collection = set._collection;
                _startIndex = set._startIndex + 1;
                _index = 0xFFFF;
                _count = (byte)_collection.PhysicalDescriptorCount;
            }

            public PhysicalDescriptor Current
            {
                get
                {
                    if (_index >= _count) throw new InvalidOperationException();

                    return Unsafe.As<byte, PhysicalDescriptor>(ref _collection._data[_startIndex + _index * 2]);
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {
                ushort i = (ushort)(_index + 1);

                if (_index + 1 < _count)
                {
                    _index = i;
                    return true;
                }

                return false;
            }

            void IEnumerator.Reset() => _index = 0xFFFF;
        }

        internal readonly PhysicalDescriptorSetCollection _collection;
        internal readonly int _startIndex;

        internal PhysicalDescriptorSet(PhysicalDescriptorSetCollection collection, int startIndex)
        {
            _collection = collection;
            _startIndex = startIndex;
        }

        public PhysicalDescriptorSetBias Bias => (PhysicalDescriptorSetBias)(_collection._data[_startIndex] >> 5);
        public byte Preference => (byte)(_collection._data[_startIndex] & 0x1F);

        public int Count => _collection.PhysicalDescriptorCount;

        public PhysicalDescriptor this[int index]
            => (uint)index <= (uint)_collection.PhysicalDescriptorCount ?
                Unsafe.As<byte, PhysicalDescriptor>(ref _collection._data[_startIndex + 1 + index * 2]) :
                throw new ArgumentOutOfRangeException(nameof(index));

        PhysicalDescriptor IList<PhysicalDescriptor>.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        bool ICollection<PhysicalDescriptor>.IsReadOnly => true;

        public ReadOnlySpan<PhysicalDescriptor> AsSpan()
            => MemoryMarshal.Cast<byte, PhysicalDescriptor>(_collection._data.AsSpan(_startIndex, _collection.PhysicalDescriptorCount * Unsafe.SizeOf<PhysicalDescriptor>()));

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<PhysicalDescriptor> IEnumerable<PhysicalDescriptor>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(PhysicalDescriptor item) => AsSpan().IndexOf(item);
        public bool Contains(PhysicalDescriptor item) => IndexOf(item) >= 0;
        public void CopyTo(PhysicalDescriptor[] array, int arrayIndex) => AsSpan().CopyTo(array.AsSpan(arrayIndex));

        public override bool Equals(object? obj) => obj is PhysicalDescriptorSet set && Equals(set);
        public bool Equals(PhysicalDescriptorSet other) => _collection.Equals(other._collection) && _startIndex == other._startIndex;

        public override int GetHashCode()
        {
            int hashCode = -2061011458;
            hashCode = hashCode * -1521134295 + _collection.GetHashCode();
            hashCode = hashCode * -1521134295 + _startIndex.GetHashCode();
            return hashCode;
        }

        void IList<PhysicalDescriptor>.Insert(int index, PhysicalDescriptor item) => throw new NotSupportedException();
        void IList<PhysicalDescriptor>.RemoveAt(int index) => throw new NotSupportedException();
        void ICollection<PhysicalDescriptor>.Add(PhysicalDescriptor item) => throw new NotSupportedException();
        bool ICollection<PhysicalDescriptor>.Remove(PhysicalDescriptor item) => throw new NotSupportedException();
        void ICollection<PhysicalDescriptor>.Clear() => throw new NotSupportedException();

        public static bool operator ==(PhysicalDescriptorSet left, PhysicalDescriptorSet right) => left.Equals(right);
        public static bool operator !=(PhysicalDescriptorSet left, PhysicalDescriptorSet right) => !(left == right);
    }
}
