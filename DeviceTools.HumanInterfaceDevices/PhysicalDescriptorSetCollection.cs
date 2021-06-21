using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.HumanInterfaceDevices
{
    public readonly struct PhysicalDescriptorSetCollection : IReadOnlyList<PhysicalDescriptorSet>, IList<PhysicalDescriptorSet>, IEquatable<PhysicalDescriptorSetCollection>
    {
        public struct Enumerator : IEnumerator<PhysicalDescriptorSet>
        {
            private readonly PhysicalDescriptorSetCollection _collection;
            private byte _index;
            // Cache the item counts so as to avoid unaligned reads.
            private readonly byte _count;
            private readonly ushort _physicalDescriptorCount;

            internal Enumerator(PhysicalDescriptorSetCollection collection)
            {
                _collection = collection;
                _index = 0xFF;
                _physicalDescriptorCount = (ushort)_collection.PhysicalDescriptorCount;
                _count = (byte)_collection.Count;
            }

            public PhysicalDescriptorSet Current
            {
                get
                {
                    if (_index >= _count) throw new InvalidOperationException();

                    return new PhysicalDescriptorSet(_collection, 3 + (1 + _physicalDescriptorCount * 2) * _index);
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {
                byte i = (byte)(_index + 1);

                if (_index + 1 < _count)
                {
                    _index = i;
                    return true;
                }

                return false;
            }

            void IEnumerator.Reset() => _index = 0xFF;
        }

        public static readonly PhysicalDescriptorSetCollection Empty = new PhysicalDescriptorSetCollection(new byte[3]);

        public PhysicalDescriptorSetCollection(ReadOnlySpan<byte> data) : this(data, true) { }

        internal PhysicalDescriptorSetCollection(ReadOnlySpan<byte> data, bool revalidateData)
        {
            int length = ValidateDataLength(data);

            _data = data.Slice(0, length).ToArray();

            if (revalidateData && ValidateDataLength(_data) != length)
                throw new InvalidDataException("Data has been modified during construction.");
        }

        private PhysicalDescriptorSetCollection(byte[] data) => _data = data;

        private static int ValidateDataLength(ReadOnlySpan<byte> data)
        {
            if (data.Length < 3) throw new ArgumentOutOfRangeException();

            byte setCount = data[0];
            ushort descriptorCount = MemoryMarshal.Read<ushort>(data.Slice(1, 2));

            int length = 3 + setCount * (1 + 2 * descriptorCount);

            if (data.Length < length) throw new InvalidDataException();

            return length;
        }

        // An array at least big enough to hold the data
        internal readonly byte[] _data;

        public int Count => _data[0];

        internal int PhysicalDescriptorCount => Unsafe.ReadUnaligned<ushort>(ref _data[1]);

        public PhysicalDescriptorSet this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));

                return new PhysicalDescriptorSet(this, 3 + (1 + PhysicalDescriptorCount * 2) * index);
            }
        }

        PhysicalDescriptorSet IList<PhysicalDescriptorSet>.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        bool ICollection<PhysicalDescriptorSet>.IsReadOnly => true;

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<PhysicalDescriptorSet> IEnumerable<PhysicalDescriptorSet>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(PhysicalDescriptorSet item)
            => _data == item._collection._data ? (item._startIndex - 3) / (1 + PhysicalDescriptorCount * 2) : -1;

        public bool Contains(PhysicalDescriptorSet item) => _data == item._collection._data;

        public void CopyTo(PhysicalDescriptorSet[] array, int arrayIndex)
        {
            if ((uint)arrayIndex > (uint)array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < Count) throw new ArgumentException();

            int count = Count;
            int physicalDescriptorCount = PhysicalDescriptorCount;
            for (int i = 0; i < count; i++)
            {
                array[arrayIndex + i] = new PhysicalDescriptorSet(this, 3 + (1 + physicalDescriptorCount * 2) * i);
            }
        }

        public override bool Equals(object? obj) => obj is PhysicalDescriptorSetCollection collection && Equals(collection);
        public bool Equals(PhysicalDescriptorSetCollection other) => _data == other._data;
        public override int GetHashCode() => -301143667 + _data.GetHashCode();

        void IList<PhysicalDescriptorSet>.Insert(int index, PhysicalDescriptorSet item) => throw new NotSupportedException();
        void IList<PhysicalDescriptorSet>.RemoveAt(int index) => throw new NotSupportedException();
        void ICollection<PhysicalDescriptorSet>.Add(PhysicalDescriptorSet item) => throw new NotSupportedException();
        bool ICollection<PhysicalDescriptorSet>.Remove(PhysicalDescriptorSet item) => throw new NotSupportedException();
        void ICollection<PhysicalDescriptorSet>.Clear() => throw new NotSupportedException();

        public static bool operator ==(PhysicalDescriptorSetCollection left, PhysicalDescriptorSetCollection right) => left.Equals(right);
        public static bool operator !=(PhysicalDescriptorSetCollection left, PhysicalDescriptorSetCollection right) => !(left == right);
    }
}
