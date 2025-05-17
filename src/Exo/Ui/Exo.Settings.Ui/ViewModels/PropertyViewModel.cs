using System.Windows.Input;
using Exo.Lighting;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

internal abstract partial class PropertyViewModel : ChangeableBindableObject
{
	protected readonly ConfigurablePropertyInformation PropertyInformation;
	private readonly Commands.ResetCommand _resetCommand;
	private readonly int _paddingLength;

	public LightingDataType DataType => PropertyInformation.DataType;

	public string Name => PropertyInformation.Name;

	public string DisplayName => PropertyInformation.DisplayName;

	public ICommand ResetCommand => _resetCommand;

	public virtual bool IsRange => false;
	public virtual bool IsEnumeration => false;
	public virtual bool IsArray => false;

	// Represents the amount of padding between this property and the following one.
	// This is used for reading and writing the raw data.
	public int PaddingLength => _paddingLength;

	public PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength)
	{
		PropertyInformation = propertyInformation;
		_resetCommand = new(this);
		_paddingLength = paddingLength;
	}

	internal abstract void Reset();
	internal abstract int ReadInitialValue(ReadOnlySpan<byte> data);
	internal abstract void WriteValue(BinaryWriter writer);

	protected sealed override void OnChanged(bool isChanged)
	{
		_resetCommand.OnChanged();
		base.OnChanged(isChanged);
	}

	private static partial class Commands
	{
		[GeneratedBindableCustomProperty]
		public sealed partial class ResetCommand : ICommand
		{
			private readonly PropertyViewModel _property;

			public ResetCommand(PropertyViewModel property) => _property = property;

			public bool CanExecute(object? parameter) => _property.IsChanged;
			public void Execute(object? parameter) => _property.Reset();

			public event EventHandler? CanExecuteChanged;

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
