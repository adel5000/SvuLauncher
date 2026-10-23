using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SvuLauncher.Core;
using SvuLauncher.Infrastructure.Audio.Interop;

namespace SvuLauncher.Infrastructure.Audio;

/// <summary>
/// Default capture device control through Windows Core Audio (IMMDeviceEnumerator + IAudioEndpointVolume).
/// Replaces the legacy clsVolume winmm mixer code. Create and use it on one thread (the UI thread);
/// <see cref="Changed"/> is raised on Core Audio's notification threads.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CoreAudioMicrophoneService : IMicrophoneService, IDisposable
{
    private const int ClsCtxAll = 0x17;
    private const ushort VtLpwstr = 31;
    private static readonly PropertyKey FriendlyNameKey = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    private readonly IMMDeviceEnumerator _enumerator;
    private readonly NotificationClient _notifications;
    private readonly VolumeCallback _volumeCallback;
    private IAudioEndpointVolume? _volume;
    private string _deviceName = "";
    private volatile bool _stale = true;
    private bool _disposed;

    public CoreAudioMicrophoneService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _notifications = new NotificationClient(this);
        _volumeCallback = new VolumeCallback(this);
        Marshal.ThrowExceptionForHR(_enumerator.RegisterEndpointNotificationCallback(_notifications));
    }

    public event EventHandler? Changed;

    public MicrophoneState? GetState()
    {
        var volume = Attach();
        if (volume is null)
            return null;
        Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));
        Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out var level));
        return new MicrophoneState(_deviceName, muted, level);
    }

    public void SetMuted(bool muted)
    {
        var volume = Attach() ?? throw new InvalidOperationException("No microphone is available.");
        var context = Guid.Empty;
        Marshal.ThrowExceptionForHR(volume.SetMute(muted, ref context));
    }

    public void SetLevel(float level)
    {
        var volume = Attach() ?? throw new InvalidOperationException("No microphone is available.");
        var context = Guid.Empty;
        Marshal.ThrowExceptionForHR(volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Detach();
        _enumerator.UnregisterEndpointNotificationCallback(_notifications);
        Marshal.ReleaseComObject(_enumerator);
    }

    private IAudioEndpointVolume? Attach()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_stale)
            return _volume;

        Detach();
        _stale = false;
        // Console role: the device legacy waveIn/Java Sound capture uses by default.
        // Fails with E_NOTFOUND when there is no active capture device.
        if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.Capture, ERole.Console, out var device) != 0 || device is null)
            return null;
        try
        {
            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var activated));
            _volume = (IAudioEndpointVolume)activated;
            _deviceName = ReadFriendlyName(device) ?? "";
            Marshal.ThrowExceptionForHR(_volume.RegisterControlChangeNotify(_volumeCallback));
            return _volume;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private void Detach()
    {
        if (_volume is null)
            return;
        _volume.UnregisterControlChangeNotify(_volumeCallback);
        Marshal.ReleaseComObject(_volume);
        _volume = null;
    }

    private static string? ReadFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(0 /* STGM_READ */, out var store) != 0)
            return null;
        try
        {
            var key = FriendlyNameKey;
            if (store.GetValue(ref key, out var value) != 0)
                return null;
            try
            {
                return value.Type == VtLpwstr ? Marshal.PtrToStringUni(value.Pointer) : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    // Notification callbacks run on Core Audio threads and must not call back into the API,
    // so they only flag the device as stale and raise Changed; GetState() re-attaches.
    private void OnDefaultDeviceChanged()
    {
        _stale = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnVolumeChanged() => Changed?.Invoke(this, EventArgs.Empty);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    private sealed class NotificationClient(CoreAudioMicrophoneService owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, int newState) { owner.OnDefaultDeviceChanged(); return 0; }
        public int OnDeviceAdded(string deviceId) => 0;
        public int OnDeviceRemoved(string deviceId) { owner.OnDefaultDeviceChanged(); return 0; }

        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
            if (flow == EDataFlow.Capture && role == ERole.Console)
                owner.OnDefaultDeviceChanged();
            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }

    private sealed class VolumeCallback(CoreAudioMicrophoneService owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr notificationData)
        {
            owner.OnVolumeChanged();
            return 0;
        }
    }
}
