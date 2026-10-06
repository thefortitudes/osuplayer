using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using OsuPlayer.Windows;
using Splat;

namespace OsuPlayer;

public class App : Application
{
    public App()
    {
        if (OperatingSystem.IsMacOS())
            Name = "osu!player";
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (OperatingSystem.IsMacOS())
            Name = "osu!player";
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (OperatingSystem.IsMacOS())
            SetMacOsApplicationIcon();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = Locator.Current.GetService<FluentAppWindow>();

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetMacOsApplicationIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://OsuPlayer/Resources/playerLogo.png"));
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var nsDataClass = objc_getClass("NSData");
                var dataWithBytesSel = sel_registerName("dataWithBytes:length:");
                var nsData = objc_msgSend_IntPtr_nuint(nsDataClass, dataWithBytesSel, handle.AddrOfPinnedObject(), (nuint) bytes.Length);
                if (nsData == IntPtr.Zero) return;

                var nsImageClass = objc_getClass("NSImage");
                var allocSel = sel_registerName("alloc");
                var initWithDataSel = sel_registerName("initWithData:");
                var nsImage = objc_msgSend_IntPtr(objc_msgSend(nsImageClass, allocSel), initWithDataSel, nsData);
                if (nsImage == IntPtr.Zero) return;

                var nsAppClass = objc_getClass("NSApplication");
                var sharedAppSel = sel_registerName("sharedApplication");
                var setIconSel = sel_registerName("setApplicationIconImage:");
                var nsApp = objc_msgSend(nsAppClass, sharedAppSel);
                if (nsApp != IntPtr.Zero)
                    objc_msgSend_IntPtr(nsApp, setIconSel, nsImage);

                var releaseSel = sel_registerName("release");
                objc_msgSend(nsImage, releaseSel);
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            // Ignore icon errors on macOS so startup is unaffected
        }
    }

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_nuint(IntPtr receiver, IntPtr selector, IntPtr arg1, nuint arg2);
}