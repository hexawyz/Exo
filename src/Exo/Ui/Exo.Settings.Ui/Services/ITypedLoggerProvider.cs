using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Exo.Settings.Ui.Services;

internal interface ITypedLoggerProvider
{
	ILogger<T> GetLogger<T>();
}

internal sealed class TypedLoggerProvider : ITypedLoggerProvider, IDisposable
{
	// This cache is built to conditionally support multiple entries, but under normal circumstances, there should never be more than one.
	private static class LoggerCache<T>
	{
		private static object? _cache;

		public static ILogger<T> Get(ILoggerFactory factory, int index, Lock @lock)
		{
			ILogger<T>?[]? array;
			var value = Volatile.Read(ref _cache);
			if (value is not null)
			{
				if ((array = value as ILogger<T>?[]) is not null)
				{
					if ((uint)index < (uint)array.Length)
					{
						var logger = array[index];
						if (logger is not null) return logger;
					}
				}
				else if (index == 0)
				{
					return Unsafe.As<ILogger<T>>(value);
				}
			}
			return GetSlow(factory, index, @lock);
		}

		private static ILogger<T> GetSlow(ILoggerFactory factory, int index, Lock @lock)
		{
			ILogger<T>?[]? array;
			ILogger<T>? logger;
			lock (@lock)
			{
				var value = Volatile.Read(ref _cache);
				if (value is null)
				{
					if (index == 0)
					{
						logger = factory.CreateLogger<T>();
						Volatile.Write(ref _cache, logger);
					}
					else
					{
						array = new ILogger<T>[index + 1];
						Volatile.Write(ref array[index], logger = factory.CreateLogger<T>());
						Volatile.Write(ref _cache, array);
					}
				}
				else if ((array = value as ILogger<T>[]) is not null)
				{
					if ((uint)index < (uint)array.Length)
					{
						logger = array[index];
						if (logger is null)
						{
							Volatile.Write(ref array[index], logger = factory.CreateLogger<T>());
						}
					}
					else
					{
						Array.Resize(ref array, index + 1);
						array[index] = logger = factory.CreateLogger<T>();
						Volatile.Write(ref _cache, array);
					}
				}
				else if (index == 0)
				{
					logger = Unsafe.As<ILogger<T>>(value);
				}
				else
				{
					array = new ILogger<T>[index + 1];
					array[0] = Unsafe.As<ILogger<T>>(value);
					Volatile.Write(ref array[index], logger = factory.CreateLogger<T>());
					Volatile.Write(ref _cache, array);
				}
				return logger;
			}
		}
	}

	private static object? _loggerFactories;
	private static Lock? _factoriesUpdateLock;

	private readonly ILoggerFactory _loggerFactory;
	private readonly Lock _lock;
	private readonly int _index;

	public TypedLoggerProvider(ILoggerFactory loggerFactory)
	{
		var loggerFactories = Interlocked.CompareExchange(ref _loggerFactories, loggerFactory, null);
		if (loggerFactories is null) goto Registered;
		var @lock = Volatile.Read(ref _factoriesUpdateLock);

		if (@lock is null)
		{
			@lock = new();
			@lock = Interlocked.CompareExchange(ref _factoriesUpdateLock, @lock, null) ?? @lock;
		}
		lock (@lock)
		{
			loggerFactories = Volatile.Read(ref _loggerFactories);
			ILoggerFactory?[]? array;
			if ((array = loggerFactories as ILoggerFactory[]) is not null)
			{
				for (int i = 0; i < array.Length; i++)
				{
					if (Interlocked.CompareExchange(ref array[i], loggerFactory, null) is null)
					{
						_index = i;
						goto Registered;
					}
				}
				_index = array.Length;
				Array.Resize(ref array, array.Length + 1);
				array[_index] = loggerFactory;
				Volatile.Write(ref _loggerFactories, array);
				goto Registered;
			}
			else
			{
				array = new ILoggerFactory?[2];
				array[0] = Unsafe.As<ILoggerFactory>(loggerFactories);
				array[1] = loggerFactory;
				Volatile.Write(ref _loggerFactories, array);
				_index = 1;
				goto Registered;
			}
		}
	Registered:;
		_loggerFactory = loggerFactory;
		_lock = new();
	}

	~TypedLoggerProvider()
	{
		Dispose(false);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			// TODO: Free allocated loggers
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public ILogger<T> GetLogger<T>()
		=> LoggerCache<T>.Get(_loggerFactory, _index, _lock);
}
