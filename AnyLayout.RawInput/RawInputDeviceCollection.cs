using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace AnyLayout.RawInput
{
    /// <summary>A collection of <see cref="RawInputDevice"/>.</summary>
    /// <remarks>
    /// <para>
    /// Any <see cref="RawInputDevice"/> instance will be associated with an instance of <see cref="RawInputDeviceCollection"/>
    /// which will manage unmanaged resources associated with individual <see cref="RawInputDevice"/> instances.
    /// </para>
    /// <para>
    /// Instances of <see cref="RawInputDevice"/> are safe to use concurrently, with no specific guarantee to the coherence of data when instances are accessed from multiple threads.
    /// It is up to the caller to make sure that calls to various properties or methods of <see cref="RawInputDeviceCollection"/> are done in a way that keep the data returned coherent.
    /// i.e. If one calls <see cref="Refresh"/> at any time, this may change the contents of the collection and the value of <see cref="Count"/> visible from all threads.
    /// </para>
    /// </remarks>
    public sealed class RawInputDeviceCollection : IReadOnlyCollection<RawInputDevice>, ICollection<RawInputDevice>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    {
        // Wrap the structrue Dictionary<IntPtr, RawInputDevice>.ValueCollection.Enumerator in case we want to change implementation later on.
        public struct Enumerator : IEnumerator<RawInputDevice>
        {
            private Dictionary<IntPtr, RawInputDevice>.ValueCollection.Enumerator  _enumerator;

            internal Enumerator(Dictionary<IntPtr, RawInputDevice>.ValueCollection.Enumerator enumerator) => _enumerator = enumerator;

            public RawInputDevice Current => _enumerator.Current;
            object IEnumerator.Current => _enumerator.Current;

            public void Dispose() => _enumerator.Dispose();
            public bool MoveNext() => _enumerator.MoveNext();
            public void Reset() => throw new NotImplementedException();
        }

        private static readonly PropertyChangedEventArgs CountChangedEventArgs = new PropertyChangedEventArgs(nameof(Count));

        // Dictionary used to store devices. A dictionary is helpful in order to detect new and old ones when refreshing the collection, and O(1) lookup.
        // Since items are not required to be accessed by index, we can get away here by only using a dictionary for storage. If stronger
        // item ordering guarantees were required, we could still add a separate array for storage.
        // In order to be accessed outside the lock, this instance must not be mutated once assigned.
        private Dictionary<IntPtr, RawInputDevice> _devices = new Dictionary<IntPtr, RawInputDevice>();
        // Lock object shared will all RawInputDevice instances. (NB: This could become a ReaderWriterLockSlim to increase throughput of operations if required)
        internal readonly object _lock = new object();
        // Determines if the collection is disposed.
        private int _isDisposed;

        private Dictionary<IntPtr, RawInputDevice> Devices => Volatile.Read(ref _devices);
        public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private void EnsureNotDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(RawInputDeviceCollection));
        }

        public void Refresh()
        {
            EnsureNotDisposed();
            lock (_lock)
            {
                EnsureNotDisposed();
                var nativeDevices = NativeMethods.GetDevices();
                var oldDevices = _devices;
                var devices = new Dictionary<IntPtr, RawInputDevice>();

                // Track changes to the collection when necessary.
                var collectionChanged = CollectionChanged;
                var propertyChanged = PropertyChanged;
                var addedDevices = collectionChanged is object ? new List<RawInputDevice>() : null;

                for (int i = 0; i < nativeDevices.Length; i++)
                {
                    var nativeDevice = nativeDevices[i];
                    if (!oldDevices.TryGetValue(nativeDevice.Handle, out var device))
                    {
                        device = RawInputDevice.Create(this, nativeDevice.Handle);
                        addedDevices?.Add(device);
                    }
                    devices.Add(nativeDevice.Handle, device);
                }

                Volatile.Write(ref _devices, devices);

                if (collectionChanged is object)
                {
                    var removedDevices = new List<RawInputDevice>();

                    foreach (var device in oldDevices.Values)
                    {
                        if (!devices.ContainsKey(device.Handle))
                        {
                            removedDevices.Add(device);
                        }
                    }

                    if (removedDevices.Count > 0)
                    {
                        if (removedDevices.Count == 1)
                        {
                            collectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedDevices[0]));
                        }
                        else
                        {
                            collectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedDevices.ToArray()));
                        }
                    }
                    if (addedDevices!.Count > 0)
                    {
                        if (addedDevices.Count == 1)
                        {
                            collectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedDevices[0]));
                        }
                        else
                        {
                            collectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedDevices.ToArray()));
                        }
                    }
                }

                // Dispose the devices once CollectionChanged notifications have been emitted.
                foreach (var device in oldDevices.Values)
                {
                    if (!devices.ContainsKey(device.Handle))
                    {
                        device.OnCollectionDisposed();
                    }
                }

                if (propertyChanged is object && devices.Count != oldDevices.Count)
                {
                    propertyChanged(this, CountChangedEventArgs);
                }
            }
        }

        internal void OnDeviceAdded(IntPtr handle)
        {
            lock (_lock)
            {
                if (!_devices.ContainsKey(handle))
                {
                    var devices = new Dictionary<IntPtr, RawInputDevice>(_devices);
                    var device = RawInputDevice.Create(this, handle);

                    devices.Add(handle, device);

                    Volatile.Write(ref _devices, devices);

                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, device));
                    PropertyChanged?.Invoke(this, CountChangedEventArgs);
                }
            }
        }

        internal void OnDeviceRemoved(IntPtr handle)
        {
            lock (_lock)
            {
                if (_devices.TryGetValue(handle, out var device))
                {
                    var devices = new Dictionary<IntPtr, RawInputDevice>(_devices);

                    devices.Remove(handle);

                    Volatile.Write(ref _devices, devices);

                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, device));

                    device.OnCollectionDisposed();

                    PropertyChanged?.Invoke(this, CountChangedEventArgs);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed == 0)
                {
                    foreach (var device in _devices.Values)
                    {
                        device.OnCollectionDisposed();
                    }
                }

                Volatile.Write(ref _isDisposed, 1);
            }
        }

        public int Count => Devices.Count;

        bool ICollection<RawInputDevice>.IsReadOnly => true;

        void ICollection<RawInputDevice>.Add(RawInputDevice item) => throw new NotSupportedException();
        bool ICollection<RawInputDevice>.Remove(RawInputDevice item) => throw new NotSupportedException();
        void ICollection<RawInputDevice>.Clear() => throw new NotSupportedException();

        public bool Contains(RawInputDevice item) => Devices.TryGetValue(item.Handle, out var device) && ReferenceEquals(device, item);
        public void CopyTo(RawInputDevice[] array, int index) => Devices.Values.CopyTo(array, index);
        public Enumerator GetEnumerator() => new Enumerator(Devices.Values.GetEnumerator());
        IEnumerator<RawInputDevice> IEnumerable<RawInputDevice>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
