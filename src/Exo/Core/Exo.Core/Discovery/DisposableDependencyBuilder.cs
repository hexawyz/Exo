using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.Discovery;

public ref struct DisposableDependencyBuilder
{
	private IAsyncDisposable? _item0;
	private IAsyncDisposable? _item1;
	private IAsyncDisposable? _item2;
	private IAsyncDisposable? _item3;
	private IAsyncDisposable? _item4;
	private IAsyncDisposable? _item5;
	private object? _item6OrArray;
	private int _count;

	public void Add(IAsyncDisposable? disposable)
	{
		if (disposable is null) return;

		switch (_count)
		{
		case 0:
			_item0 = disposable;
			break;
		case 1:
			_item1 = disposable;
			break;
		case 2:
			_item2 = disposable;
			break;
		case 3:
			_item3 = disposable;
			break;
		case 4:
			_item4 = disposable;
			break;
		case 5:
			_item5 = disposable;
			break;
		case 6:
			_item6OrArray = disposable;
			break;
		case 7:
			_item6OrArray = new IAsyncDisposable[] { _item0!, _item1!, _item2!, _item3!, _item4!, _item5!, Unsafe.As<IAsyncDisposable>(_item6OrArray)!, disposable };
			_item0 = null;
			_item1 = null;
			_item2 = null;
			_item3 = null;
			_item4 = null;
			_item5 = null;
			break;
		default:
			var array = Unsafe.As<IAsyncDisposable[]>(_item6OrArray)!;
			if (_count >= array.Length)
			{
				Array.Resize(ref array, array.Length * 2);
				_item6OrArray = array;
			}
			array[_count] = disposable;
			break;
		}
		_count++;
	}

	internal readonly ImmutableArray<IAsyncDisposable> ToImmutableArray()
		=> _count switch
		{
			0 => [],
			1 => [_item0!],
			2 => [_item0!, _item1!],
			3 => [_item0!, _item1!, _item2!],
			4 => [_item0!, _item1!, _item2!, _item3!],
			5 => [_item0!, _item1!, _item2!, _item3!, _item4!],
			6 => [_item0!, _item1!, _item2!, _item3!, _item4!, _item5!],
			7 => [_item0!, _item1!, _item2!, _item3!, _item4!, _item5!, Unsafe.As<IAsyncDisposable>(_item6OrArray)!],
			8 => ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<IAsyncDisposable[]>(_item6OrArray)!),
			_ => GetImmutableArray(Unsafe.As<IAsyncDisposable[]>(_item6OrArray)!, _count),
		};

	private static ImmutableArray<IAsyncDisposable> GetImmutableArray(IAsyncDisposable[] array, int count)
		=> array.Length == count ?
			ImmutableCollectionsMarshal.AsImmutableArray(array) :
			array.AsSpan(0, count).ToImmutableArray();
}
