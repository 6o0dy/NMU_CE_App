using Android.App;
using Android.Runtime;

namespace NMU_CE_App;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		Android.Util.Log.Error("NMU_CE", "MainApplication ctor reached");
		System.Diagnostics.Debug.WriteLine("[MainApplication] ctor reached");
	}

	protected override MauiApp CreateMauiApp()
	{
		try
		{
			return MauiProgram.CreateMauiApp();
		}
		catch (Exception ex)
		{
			Android.Util.Log.Error("NMU_CE", $"FATAL CreateMauiApp override: {ex}");
			System.Diagnostics.Debug.WriteLine($"[FATAL] CreateMauiApp override: {ex}");
			throw;
		}
	}
}