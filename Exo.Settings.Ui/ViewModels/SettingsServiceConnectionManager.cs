using Exo.Ui;
using Exo.Contracts.Ui.Settings;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using System.Runtime.ExceptionServices;
using System.ComponentModel;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsServiceConnectionManager : ServiceConnectionManager, INotifyPropertyChanged
{
	private TaskCompletionSource<IDeviceService> _deviceServiceTaskCompletionSource;
	private TaskCompletionSource<IMouseService> _mouseServiceTaskCompletionSource;
	private TaskCompletionSource<IMonitorService> _monitorServiceTaskCompletionSource;
	private TaskCompletionSource<ILightingService> _lightingServiceTaskCompletionSource;
	private TaskCompletionSource<ISensorService> _sensorServiceTaskCompletionSource;
	private TaskCompletionSource<IProgrammingService> _programmingServiceTaskCompletionSource;

	public event PropertyChangedEventHandler? PropertyChanged;
	private volatile bool _isConnected;

	public bool IsConnected => _isConnected;

	public SettingsServiceConnectionManager(string pipeName, int reconnectDelay) : base(pipeName, reconnectDelay)
	{
		_deviceServiceTaskCompletionSource = new();
		_mouseServiceTaskCompletionSource = new();
		_monitorServiceTaskCompletionSource = new();
		_lightingServiceTaskCompletionSource = new();
		_sensorServiceTaskCompletionSource = new();
		_programmingServiceTaskCompletionSource = new();
	}

	public Task<IDeviceService> GetDeviceServiceAsync(CancellationToken cancellationToken)
		=> _deviceServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<IMouseService> GetMouseServiceAsync(CancellationToken cancellationToken)
		=> _mouseServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<IMonitorService> GetMonitorServiceAsync(CancellationToken cancellationToken)
		=> _monitorServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<ILightingService> GetLightingServiceAsync(CancellationToken cancellationToken)
		=> _lightingServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<ISensorService> GetSensorServiceAsync(CancellationToken cancellationToken)
		=> _sensorServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<IProgrammingService> GetProgrammingServiceAsync(CancellationToken cancellationToken)
		=> _programmingServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	protected override void OnConnected(GrpcChannel channel)
	{
		_isConnected = true;
		Connect(channel, _deviceServiceTaskCompletionSource);
		Connect(channel, _mouseServiceTaskCompletionSource);
		Connect(channel, _monitorServiceTaskCompletionSource);
		Connect(channel, _lightingServiceTaskCompletionSource);
		Connect(channel, _sensorServiceTaskCompletionSource);
		Connect(channel, _programmingServiceTaskCompletionSource);
		PropertyChanged?.Invoke(this, ChangedProperty.IsConnected);
	}

	protected override void OnDisconnected()
	{
		_isConnected = false;
		Reset(ref _deviceServiceTaskCompletionSource);
		Reset(ref _mouseServiceTaskCompletionSource);
		Reset(ref _monitorServiceTaskCompletionSource);
		Reset(ref _lightingServiceTaskCompletionSource);
		Reset(ref _sensorServiceTaskCompletionSource);
		Reset(ref _programmingServiceTaskCompletionSource);
		PropertyChanged?.Invoke(this, ChangedProperty.IsConnected);
	}

	private static void Connect<T>(GrpcChannel channel, TaskCompletionSource<T> taskCompletionSource)
		where T : class
		=> taskCompletionSource.TrySetResult(channel.CreateGrpcService<T>());

	private static void Reset<T>(ref TaskCompletionSource<T> taskCompletionSource)
	{
		if (!taskCompletionSource.Task.IsCompleted)
		{
			taskCompletionSource.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(SettingsServiceConnectionManager).FullName)));
		}
		Volatile.Write(ref taskCompletionSource, new());
	}
}
