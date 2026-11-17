using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SumikkoCompanion
{
    public enum AudioSource
    {
        Off,
        Microphone,
        Playback
    }

    public sealed class AudioDeviceInfo
    {
        public string Id { get; private set; }
        public string Name { get; private set; }

        public AudioDeviceInfo(string id, string name)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentException("An endpoint ID is required.", "id");
            Id = id;
            Name = name ?? String.Empty;
        }
    }

    // Poll and dispose on the same thread (normally the WPF UI thread). No key or audio data is stored.
    public sealed class NativeSignals : IDisposable
    {
        private const int ClsctxInprocServer = 1;
        private const int ENotFound = unchecked((int)0x80070490);
        private const uint DrvQueryFunctionInstanceId = 2065;
        private const uint DrvQueryFunctionInstanceIdSize = 2066;
        private const uint DeviceStateActive = 1;
        private static readonly Guid MeterInterfaceId = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        private static readonly Guid EnumeratorClassId = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        private static readonly PropertyKey FriendlyNameKey = new PropertyKey
        {
            FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), PropertyId = 14
        };
        private IAudioMeterInformation meter;
        private IntPtr microphone;
        private WaveBuffer[] microphoneBuffers;
        private bool microphoneCleanupPending;
        // A driver that will not let go is asked again a few times a second, not every frame.
        internal static readonly TimeSpan CleanupRetryInterval = TimeSpan.FromMilliseconds(200);
        private DateTime nextCleanupUtc;
        // How often Poll has asked the driver to release the microphone (for the checks).
        internal int CleanupAttempts { get; private set; }
        private DateTime lastMicrophonePeakUtc;
        private AudioSource source;
        private string selectedDeviceId = String.Empty;
        private DateTime nextBindUtc;
        private bool disposed;
        private readonly bool[] previousTypingKeys = new bool[256];
        private bool keyboardPrimed, buttonsPrimed = true;

        public double MouseX { get; private set; }
        public double MouseY { get; private set; }
        public bool KeyPressed { get; private set; }
        // True for the frame in which Enter went down: the hand lands that press harder.
        public bool KeyEmphatic { get; private set; }
        // True when this frame's press is a held key repeating rather than a new key going down.
        public bool KeyRepeated { get; private set; }
        // Like the keyboard's own auto-repeat: a held key starts hammering after a short
        // delay and keeps going until every typing key is released.
        internal const double RepeatDelay = .4, RepeatInterval = .075;
        private double repeatAt = double.PositiveInfinity;
        private readonly System.Diagnostics.Stopwatch keyClock = System.Diagnostics.Stopwatch.StartNew();
        public bool MousePressed { get; private set; }
        public double AudioLevel { get; private set; }
        public string AudioStatus { get; private set; }

        public NativeSignals()
        {
            AudioStatus = "Off";
        }

        public static AudioDeviceInfo[] GetAudioDevices(AudioSource audioSource)
        {
            if (audioSource != AudioSource.Microphone && audioSource != AudioSource.Playback)
                throw new ArgumentOutOfRangeException("audioSource");
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            var result = new List<AudioDeviceInfo>();
            try
            {
                enumerator = CreateEnumerator();
                int hr = enumerator.EnumAudioEndpoints(audioSource == AudioSource.Microphone ? 1 : 0,
                    (int)DeviceStateActive, out collection);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                uint count;
                hr = collection.GetCount(out count);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                for (uint index = 0; index < count; index++)
                {
                    IMMDevice device = null;
                    try
                    {
                        hr = collection.Item(index, out device);
                        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                        string id = ReadEndpointId(device);
                        result.Add(new AudioDeviceInfo(id, ReadFriendlyName(device, id)));
                    }
                    finally { ReleaseCom(device); }
                }
                return result.ToArray();
            }
            finally
            {
                ReleaseCom(collection);
                ReleaseCom(enumerator);
            }
        }

        public void Poll()
        {
            // A driver that is slow to give back its buffers only holds up audio: the buffers
            // stay allocated and no device is reopened until it lets go, while PollInput
            // keeps the cursor, buttons and keys updating every frame.
            bool audioOnHold = ServiceMicrophoneCleanup();
            if (disposed) return;
            PollInput();
            if (!audioOnHold) PollAudio();
        }

        // The level only, for a device test that must not touch the keyboard state.
        public void PollAudioOnly()
        {
            bool audioOnHold = ServiceMicrophoneCleanup();
            if (disposed || audioOnHold) return;
            PollAudio();
        }

        // Forget which keys were down, e.g. after the companion was paused: a key held when it
        // comes back counts as already held, and nothing pressed meanwhile is replayed.
        public void ResetInputState()
        {
            keyboardPrimed = false;
            buttonsPrimed = false;
            repeatAt = double.PositiveInfinity;
            KeyPressed = KeyEmphatic = KeyRepeated = false;
            MousePressed = false;
        }

        // True while the microphone is still held by its driver (audio stays off meanwhile).
        private bool ServiceMicrophoneCleanup()
        {
            if (!microphoneCleanupPending) return false;
            if (DateTime.UtcNow >= nextCleanupUtc)
            {
                CleanupAttempts++;
                if (StopMicrophone())
                {
                    if (source == AudioSource.Off) AudioStatus = "Off";
                    return false;
                }
            }
            AudioLevel = 0;
            return true;
        }

        private void PollAudio()
        {
            if (source == AudioSource.Off) return;
            if (source == AudioSource.Microphone)
            {
                if (microphone == IntPtr.Zero && DateTime.UtcNow >= nextBindUtc) BindMicrophone();
                if (microphone != IntPtr.Zero) PollMicrophone();
                return;
            }
            if (meter == null && DateTime.UtcNow >= nextBindUtc) BindMeter();
            if (meter == null) return;

            try
            {
                float peak;
                int hr = meter.GetPeakValue(out peak);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                AudioLevel = Math.Max(0.0, Math.Min(1.0, peak));
            }
            catch (Exception)
            {
                AudioLevel = 0;
                AudioStatus = "Audio meter unavailable";
                ReleaseMeter();
                nextBindUtc = DateTime.UtcNow.AddSeconds(2);
            }
        }

        private void PollInput()
        {
            Point cursor;
            if (GetCursorPos(out cursor))
            {
                MouseX = cursor.X;
                MouseY = cursor.Y;
            }

            // GetAsyncKeyState's high bit is the current state; its low bit is unreliable.
            PollButtons(IsDown);
            PollKeyboard(IsDown, keyClock.Elapsed.TotalSeconds);
        }

        // After ResetInputState a button that is already down (e.g. the double-click that
        // brought the companion back) is ignored until it has been let go.
        internal void PollButtons(Func<int, bool> readKey)
        {
            bool down = readKey(0x01) || readKey(0x02) || readKey(0x04) || readKey(0x05) || readKey(0x06);
            if (!down) buttonsPrimed = true;
            MousePressed = down && buttonsPrimed;
        }

        public void SetAudioSource(AudioSource value)
        {
            SetAudioSource(value, String.Empty);
        }

        public void SetAudioSource(AudioSource value, string deviceId)
        {
            if (disposed) return;
            bool microphoneStopped = StopMicrophone();
            ReleaseMeter();
            AudioLevel = 0;
            source = value;
            selectedDeviceId = deviceId ?? String.Empty;
            if (!microphoneStopped)
            {
                AudioStatus = "Microphone cleanup pending";
                nextBindUtc = DateTime.MinValue;
                return;
            }
            if (value == AudioSource.Off)
            {
                AudioStatus = "Off";
                return;
            }
            if (value != AudioSource.Microphone && value != AudioSource.Playback)
            {
                source = AudioSource.Off;
                AudioStatus = "Off";
                return;
            }
            nextBindUtc = DateTime.MinValue;
            if (value == AudioSource.Microphone) BindMicrophone();
            else BindMeter();
        }

        private void BindMicrophone()
        {
            // A live capture stream makes the level available even when an endpoint's software meter is idle.
            int[] rates = { 16000, 44100, 48000 };
            uint waveDeviceId = 0;
            bool mapped = true;
            bool missingDefault = false;
            try
            {
                string targetId = selectedDeviceId;
                if (targetId.Length == 0) targetId = ReadDefaultEndpointId(1);
                mapped = TryFindWaveInputId(targetId, out waveDeviceId);
            }
            catch (COMException ex)
            {
                mapped = false;
                missingDefault = selectedDeviceId.Length == 0 && ex.ErrorCode == ENotFound;
            }
            catch (Exception) { mapped = false; }
            if (!mapped)
            {
                AudioStatus = missingDefault ? "No default microphone" :
                    selectedDeviceId.Length == 0 ? "Default microphone unavailable" : "Selected microphone unavailable";
                AudioLevel = 0;
                nextBindUtc = DateTime.UtcNow.AddSeconds(2);
                return;
            }
            string failure = "no supported input device";
            foreach (int rate in rates)
            for (int channels = 1; channels <= 2; channels++)
            {
                var format = new WaveFormat
                {
                    FormatTag = 1,
                    Channels = (ushort)channels,
                    SamplesPerSecond = (uint)rate,
                    AverageBytesPerSecond = (uint)(rate * channels * 2),
                    BlockAlign = (ushort)(channels * 2),
                    BitsPerSample = 16,
                    ExtraSize = 0
                };
                try
                {
                    uint openResult = WaveInOpen(out microphone, waveDeviceId, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                    if (openResult != 0)
                    {
                        failure = "waveInOpen " + openResult;
                        microphone = IntPtr.Zero;
                        continue;
                    }
                    int headerSize = Marshal.SizeOf(typeof(WaveHeader));
                    int bytes = rate * channels * 2 / 25; // 40 ms of 16-bit PCM
                    microphoneBuffers = new WaveBuffer[3];
                    for (int index = 0; index < microphoneBuffers.Length; index++)
                    {
                        var buffer = new WaveBuffer();
                        microphoneBuffers[index] = buffer;
                        buffer.Data = Marshal.AllocHGlobal(bytes);
                        buffer.Header = Marshal.AllocHGlobal(headerSize);
                        var header = new WaveHeader { Data = buffer.Data, BufferLength = (uint)bytes };
                        Marshal.StructureToPtr(header, buffer.Header, false);
                        if (WaveInPrepareHeader(microphone, buffer.Header, (uint)headerSize) != 0)
                            throw new InvalidOperationException("Microphone buffer preparation failed");
                        buffer.Prepared = true;
                        if (WaveInAddBuffer(microphone, buffer.Header, (uint)headerSize) != 0)
                            throw new InvalidOperationException("Microphone buffer queue failed");
                    }
                    if (WaveInStart(microphone) != 0)
                        throw new InvalidOperationException("Microphone start failed");
                    AudioStatus = "Microphone ready";
                    lastMicrophonePeakUtc = DateTime.UtcNow;
                    return;
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    if (!StopMicrophone())
                    {
                        AudioStatus = "Microphone cleanup pending";
                        AudioLevel = 0;
                        nextBindUtc = DateTime.UtcNow.AddSeconds(2);
                        return;
                    }
                }
            }
            AudioStatus = microphoneCleanupPending ? "Microphone cleanup pending" : "Microphone unavailable: " + failure;
            AudioLevel = 0;
            nextBindUtc = DateTime.UtcNow.AddSeconds(2);
        }

        private void PollMicrophone()
        {
            double peak = 0;
            bool completed = false;
            int headerSize = Marshal.SizeOf(typeof(WaveHeader));
            try
            {
                foreach (WaveBuffer buffer in microphoneBuffers)
                {
                    var header = (WaveHeader)Marshal.PtrToStructure(buffer.Header, typeof(WaveHeader));
                    if ((header.Flags & 1) == 0) continue; // WHDR_DONE
                    completed = true;
                    int byteCount = (int)Math.Min(header.BytesRecorded, header.BufferLength) & ~1;
                    for (int offset = 0; offset < byteCount; offset += 2)
                    {
                        int magnitude = Math.Abs((int)Marshal.ReadInt16(buffer.Data, offset));
                        peak = Math.Max(peak, magnitude / 32768.0);
                    }
                    if (WaveInAddBuffer(microphone, buffer.Header, (uint)headerSize) != 0)
                        throw new InvalidOperationException("Microphone buffer requeue failed");
                }
                if (completed)
                {
                    AudioLevel = peak;
                    lastMicrophonePeakUtc = DateTime.UtcNow;
                }
                else if ((DateTime.UtcNow - lastMicrophonePeakUtc).TotalMilliseconds > 150)
                {
                    AudioLevel = 0;
                }
            }
            catch (Exception)
            {
                bool stopped = StopMicrophone();
                AudioStatus = stopped ? "Microphone unavailable" : "Microphone cleanup pending";
                AudioLevel = 0;
                nextBindUtc = DateTime.UtcNow.AddSeconds(2);
            }
        }

        private bool StopMicrophone()
        {
            if (microphone != IntPtr.Zero) WaveInReset(microphone);
            bool buffersReleased = true;
            if (microphoneBuffers != null)
            {
                uint headerSize = (uint)Marshal.SizeOf(typeof(WaveHeader));
                foreach (WaveBuffer buffer in microphoneBuffers)
                {
                    if (buffer == null) continue;
                    if (buffer.Prepared)
                    {
                        // STILLPLAYING means the driver may still access both allocations.
                        if (microphone == IntPtr.Zero ||
                            WaveInUnprepareHeader(microphone, buffer.Header, headerSize) != 0)
                        {
                            buffersReleased = false;
                            continue;
                        }
                        buffer.Prepared = false;
                    }
                    if (buffer.Header != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(buffer.Header);
                        buffer.Header = IntPtr.Zero;
                    }
                    if (buffer.Data != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(buffer.Data);
                        buffer.Data = IntPtr.Zero;
                    }
                }
                if (buffersReleased) microphoneBuffers = null;
            }
            if (buffersReleased && microphone != IntPtr.Zero && WaveInClose(microphone) == 0)
                microphone = IntPtr.Zero;
            microphoneCleanupPending = !buffersReleased || microphone != IntPtr.Zero;
            if (microphoneCleanupPending) nextCleanupUtc = DateTime.UtcNow + CleanupRetryInterval;
            return !microphoneCleanupPending;
        }

        private void BindMeter()
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioMeterInformation newMeter = null;
            try
            {
                if (selectedDeviceId.Length != 0)
                {
                    bool activeRender = false;
                    foreach (AudioDeviceInfo item in GetAudioDevices(AudioSource.Playback))
                    {
                        if (String.Equals(item.Id, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
                        {
                            activeRender = true;
                            break;
                        }
                    }
                    if (!activeRender)
                    {
                        AudioStatus = "Selected playback device unavailable";
                        return;
                    }
                }
                enumerator = CreateEnumerator();
                int hr = selectedDeviceId.Length == 0
                    ? enumerator.GetDefaultAudioEndpoint(0, 1, out device)
                    : enumerator.GetDevice(selectedDeviceId, out device);
                if (hr == ENotFound)
                {
                    AudioStatus = selectedDeviceId.Length == 0
                        ? "No default playback device" : "Selected playback device unavailable";
                    return;
                }
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                hr = device.Activate(ref MeterInterfaceIdForCall, ClsctxInprocServer, IntPtr.Zero, out newMeter);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                meter = newMeter;
                newMeter = null;
                AudioStatus = "Playback ready";
            }
            catch (Exception)
            {
                AudioStatus = selectedDeviceId.Length == 0
                    ? "Audio meter unavailable" : "Selected playback device unavailable";
            }
            finally
            {
                ReleaseCom(newMeter);
                ReleaseCom(device);
                ReleaseCom(enumerator);
                if (meter == null) nextBindUtc = DateTime.UtcNow.AddSeconds(2);
            }
        }

        // A ref GUID parameter cannot point to a readonly field.
        private static Guid MeterInterfaceIdForCall = MeterInterfaceId;

        private static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClassId));
        }

        private static string ReadEndpointId(IMMDevice device)
        {
            IntPtr idPointer = IntPtr.Zero;
            try
            {
                int hr = device.GetId(out idPointer);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                return Marshal.PtrToStringUni(idPointer);
            }
            finally { if (idPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(idPointer); }
        }

        private static string ReadDefaultEndpointId(int dataFlow)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            try
            {
                enumerator = CreateEnumerator();
                int hr = enumerator.GetDefaultAudioEndpoint(dataFlow, 1, out device);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                return ReadEndpointId(device);
            }
            finally
            {
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        private static string ReadFriendlyName(IMMDevice device, string fallback)
        {
            IPropertyStore store = null;
            PropVariant value = new PropVariant();
            try
            {
                int hr = device.OpenPropertyStore(0, out store);
                if (hr < 0) return fallback;
                PropertyKey key = FriendlyNameKey;
                hr = store.GetValue(ref key, out value);
                if (hr < 0 || value.Type != 31 || value.StringPointer == IntPtr.Zero) return fallback;
                string name = Marshal.PtrToStringUni(value.StringPointer);
                return String.IsNullOrWhiteSpace(name) ? fallback : name;
            }
            finally
            {
                PropVariantClear(ref value);
                ReleaseCom(store);
            }
        }

        private static bool TryFindWaveInputId(string endpointId, out uint waveId)
        {
            uint count = WaveInGetNumDevs();
            for (uint candidate = 0; candidate < count; candidate++)
            {
                string waveEndpointId = ReadWaveInputEndpointId(candidate);
                if (String.Equals(waveEndpointId, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    waveId = candidate;
                    return true;
                }
            }
            waveId = 0;
            return false;
        }

        private static string ReadWaveInputEndpointId(uint waveId)
        {
            IntPtr sizePointer = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr idPointer = IntPtr.Zero;
            try
            {
                Marshal.WriteIntPtr(sizePointer, IntPtr.Zero);
                IntPtr device = new IntPtr((long)waveId);
                if (WaveInMessage(device, DrvQueryFunctionInstanceIdSize, sizePointer, IntPtr.Zero) != 0)
                    return null;
                long reportedSize = Marshal.ReadIntPtr(sizePointer).ToInt64();
                if (reportedSize < 2 || reportedSize > 65536 || (reportedSize & 1) != 0) return null;
                int size = (int)reportedSize;
                idPointer = Marshal.AllocHGlobal(size);
                if (WaveInMessage(device, DrvQueryFunctionInstanceId, idPointer, new IntPtr(size)) != 0)
                    return null;
                string result = Marshal.PtrToStringUni(idPointer, size / 2);
                int terminator = result.IndexOf('\0');
                return terminator >= 0 ? result.Substring(0, terminator) : result;
            }
            finally
            {
                if (idPointer != IntPtr.Zero) Marshal.FreeHGlobal(idPointer);
                Marshal.FreeHGlobal(sizePointer);
            }
        }

        // One-frame press events, not a continuously true held-key state. Exclude
        // reserved/IME virtual keys and modifiers so idle IME state cannot animate typing.
        // now is in seconds and only has to keep increasing.
        internal void PollKeyboard(Func<int, bool> readKey, double now)
        {
            KeyPressed = false;
            KeyEmphatic = false;
            KeyRepeated = false;
            bool held = false, pressed = false;
            for (int key = 8; key <= 226; key++)
            {
                if (!IsTypingKey(key)) continue;
                bool down = readKey(key);
                if (down) held = true;
                if (keyboardPrimed && down && !previousTypingKeys[key])
                {
                    KeyPressed = pressed = true;
                    if (key == 13) KeyEmphatic = true;
                }
                previousTypingKeys[key] = down;
            }
            keyboardPrimed = true;
            // Only a key that went down while watched repeats; one already held at start-up does not.
            if (pressed) repeatAt = now + RepeatDelay;
            else if (!held) repeatAt = double.PositiveInfinity;
            else if (now >= repeatAt)
            {
                KeyPressed = KeyRepeated = true;
                repeatAt = now + RepeatInterval;
            }
        }

        private static bool IsTypingKey(int key)
        {
            return key == 8 || key == 9 || key == 13 || key == 27 ||
                (key >= 32 && key <= 40) || key == 45 || key == 46 ||
                (key >= 48 && key <= 57) || (key >= 65 && key <= 90) ||
                (key >= 96 && key <= 111) || (key >= 112 && key <= 135) ||
                (key >= 186 && key <= 192) || (key >= 219 && key <= 222) || key == 226;
        }

        private static bool IsDown(int key)
        {
            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        private void ReleaseMeter()
        {
            ReleaseCom(meter);
            meter = null;
        }

        private static void ReleaseCom(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // A failed driver release is retried without blocking the UI thread.
            for (int attempt = 0; attempt < 3 && !StopMicrophone(); attempt++) { }
            ReleaseMeter();
            AudioLevel = 0;
            AudioStatus = "Off";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WaveFormat
        {
            public ushort FormatTag;
            public ushort Channels;
            public uint SamplesPerSecond;
            public uint AverageBytesPerSecond;
            public ushort BlockAlign;
            public ushort BitsPerSample;
            public ushort ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public uint BufferLength;
            public uint BytesRecorded;
            public IntPtr User;
            public uint Flags;
            public uint Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        private sealed class WaveBuffer
        {
            public IntPtr Data;
            public IntPtr Header;
            public bool Prepared;
        }

        [DllImport("winmm.dll", EntryPoint = "waveInOpen")] private static extern uint WaveInOpen(out IntPtr handle, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll", EntryPoint = "waveInPrepareHeader")] private static extern uint WaveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll", EntryPoint = "waveInAddBuffer")] private static extern uint WaveInAddBuffer(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll", EntryPoint = "waveInStart")] private static extern uint WaveInStart(IntPtr handle);
        [DllImport("winmm.dll", EntryPoint = "waveInReset")] private static extern uint WaveInReset(IntPtr handle);
        [DllImport("winmm.dll", EntryPoint = "waveInUnprepareHeader")] private static extern uint WaveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll", EntryPoint = "waveInClose")] private static extern uint WaveInClose(IntPtr handle);
        [DllImport("winmm.dll", EntryPoint = "waveInGetNumDevs")] private static extern uint WaveInGetNumDevs();
        [DllImport("winmm.dll", EntryPoint = "waveInMessage")] private static extern uint WaveInMessage(IntPtr device, uint message, IntPtr param1, IntPtr param2);
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsctx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.Interface)] out IAudioMeterInformation result);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
            [PreserveSig] int GetId(out IntPtr id);
            [PreserveSig] int GetState(out int state);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public int PropertyId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public IntPtr StringPointer;
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
            [PreserveSig] int GetMeteringChannelCount(out int count);
            [PreserveSig] int GetChannelsPeakValues(int count, IntPtr peaks);
            [PreserveSig] int QueryHardwareSupport(out int mask);
        }
    }
}
