# To be run within the Visual Studio developer command prompt.

Push-Location $PSScriptRoot

Try
{
	MSBuild Exo.Service\Exo.Service.csproj /t:Publish /p:Configuration=Release
	MSBuild .\Exo.Settings.Ui\Exo.Settings.Ui.csproj /t:Publish /p:Configuration=Release /p:RuntimeIdentifier=win10-x64
	MSBuild .\Exo.Overlay\Exo.Overlay.csproj /t:Publish /p:Configuration=Release /p:RuntimeIdentifier=win-x64

	if (Test-Path publish)
	{
		Remove-Item publish -Recurse
	}

	New-Item -ItemType Directory -Name publish | Out-Null
	New-Item -ItemType Directory -Name publish\Exo.Service | Out-Null
	Copy-Item -Path "Exo.Service\bin\Release\net8.0-windows\publish\*" -Destination "publish\Exo.Service" -Recurse -Exclude cfg,logs
	Copy-Item -Path "Exo.Settings.Ui\bin\Release\net8.0-windows10.0.19041.0\win10-x64\publish" -Destination "publish\Exo.Settings.Ui" -Recurse
	Copy-Item -Path "Exo.Overlay\bin\Release\net8.0-windows\win-x64\publish" -Destination "publish\Exo.Overlay" -Recurse
}
Finally
{
	Pop-Location
}
