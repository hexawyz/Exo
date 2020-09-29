using System;
using System.Runtime.InteropServices;

namespace DeviceTools.RawInput
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PhysicalDescriptor : IEquatable<PhysicalDescriptor>
    {
        public HidDesignator Designator { get; }
        private readonly byte _flags;

        public PhysicalDescriptorQualifier Qualifier => (PhysicalDescriptorQualifier)(_flags >> 5);
        public byte Effort => (byte)(_flags & 0x1F);

        public override bool Equals(object? obj) => obj is PhysicalDescriptor descriptor && Equals(descriptor);
        public bool Equals(PhysicalDescriptor other) => Designator == other.Designator && _flags == other._flags;

        public override int GetHashCode()
        {
            int hashCode = -1300345432;
            hashCode = hashCode * -1521134295 + Designator.GetHashCode();
            hashCode = hashCode * -1521134295 + _flags.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(PhysicalDescriptor left, PhysicalDescriptor right) => left.Equals(right);
        public static bool operator !=(PhysicalDescriptor left, PhysicalDescriptor right) => !(left == right);
    }
}
