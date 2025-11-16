namespace PoEKompanion;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

internal sealed class VirtualKeyboard : IDisposable
{
    private const int UINPUT_MAX_NAME_SIZE = 80;
    private const uint UI_SET_EVBIT = 0x40045564;
    private const uint UI_SET_KEYBIT = 0x40045565;
    private const uint UI_DEV_SETUP = 0x405C5503;
    private const uint UI_DEV_CREATE = 0x5501;
    private const uint UI_DEV_DESTROY = 0x5502;
    private const int EV_KEY = 0x01;
    private const int EV_SYN = 0x00;
    private const int SYN_REPORT = 0;
    private const int KEY_LEFTSHIFT = 42;
    private const int KEY_ENTER = 28;

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, int value);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, ref UinputSetup setup);

    [DllImport("libc", SetLastError = true)]
    private static extern int write(int fd, byte[] buffer, int count);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr strerror(int errnum);

    [StructLayout(LayoutKind.Sequential)]
    private struct InputEvent
    {
        public long TimeSec;
        public long TimeUsec;
        public ushort Type;
        public ushort Code;
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct UinputSetup
    {
        public UinputId Id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = UINPUT_MAX_NAME_SIZE)]
        public byte[] Name;
        public uint FfEffectsMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UinputId
    {
        public ushort BusType;
        public ushort Vendor;
        public ushort Product;
        public ushort Version;
    }

    private int virtualKeyboardFd = -1;
    private Dictionary<char, KeycodMapping>? layoutMap;

    public bool IsInitialized => this.virtualKeyboardFd >= 0;

    public void Initialize()
    {
        if (this.virtualKeyboardFd >= 0) return;

        const int O_WRONLY = 1;
        const int O_NONBLOCK = 0x800;

        this.virtualKeyboardFd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
        if (this.virtualKeyboardFd < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"Failed to open /dev/uinput: {errMsg} (errno={errno})");
            return;
        }

        Console.WriteLine("Successfully opened /dev/uinput");

        ioctl(this.virtualKeyboardFd, UI_SET_EVBIT, EV_KEY);
        ioctl(this.virtualKeyboardFd, UI_SET_EVBIT, EV_SYN);

        for (var i = 0; i < 256; i++)
        {
            ioctl(this.virtualKeyboardFd, UI_SET_KEYBIT, i);
        }

        var nameBytes = new byte[UINPUT_MAX_NAME_SIZE];
        var nameStr = "PoE Kompanion Virtual Keyboard";
        System.Text.Encoding.ASCII.GetBytes(nameStr, 0, Math.Min(nameStr.Length, UINPUT_MAX_NAME_SIZE - 1), nameBytes, 0);

        var setup = new UinputSetup
        {
            Id = new UinputId
            {
                BusType = 0x03,
                Vendor = 0x1234,
                Product = 0x5678,
                Version = 1
            },
            Name = nameBytes,
            FfEffectsMax = 0
        };

        var ret = ioctl(this.virtualKeyboardFd, UI_DEV_SETUP, ref setup);
        if (ret < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"UI_DEV_SETUP failed: {errMsg} (errno={errno})");
        }

        ret = ioctl(this.virtualKeyboardFd, UI_DEV_CREATE, 0);
        if (ret < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"UI_DEV_CREATE failed: {errMsg} (errno={errno})");
            close(this.virtualKeyboardFd);
            this.virtualKeyboardFd = -1;
            return;
        }

        Console.WriteLine("Virtual keyboard created successfully");
    }

    public void SetLayoutMap(CharKeyMapping[] layoutArray)
    {
        this.layoutMap = layoutArray.ToDictionary(
            m => m.Character,
            m => new KeycodMapping(m.Keycode, m.Shift)
        );
        Console.WriteLine($"Received layout map with {this.layoutMap.Count} character mappings");

        if (this.layoutMap.Count == 0)
        {
            Console.WriteLine("WARNING: Received empty layout map! Character input will not work.");
        }
    }

    public async Task SendChatCommandAsync(string command)
    {
        if (!this.IsInitialized)
        {
            Console.WriteLine("Virtual keyboard not available");
            return;
        }

        try
        {
            Console.WriteLine($"Attempting to send chat command: {command}");

            this.SendKey(KEY_ENTER, true);
            this.SendKey(KEY_ENTER, false);

            await Task.Delay(10);

            foreach (var c in command)
            {
                this.SendChar(c);
                await Task.Delay(1);
            }

            await Task.Delay(1);

            this.SendKey(KEY_ENTER, true);
            this.SendKey(KEY_ENTER, false);

            Console.WriteLine($"Successfully sent chat command: {command}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending chat command: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private unsafe void SendKey(int keyCode, bool press)
    {
        var evt = new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = EV_KEY,
            Code = (ushort)keyCode,
            Value = press ? 1 : 0
        };

        var evtSize = sizeof(InputEvent);
        var evtBuffer = stackalloc byte[evtSize];
        Marshal.StructureToPtr(evt, (IntPtr)evtBuffer, false);

        var evtBytes = new Span<byte>(evtBuffer, evtSize).ToArray();
        var ret = write(this.virtualKeyboardFd, evtBytes, evtBytes.Length);

        if (ret < 0)
        {
            Console.WriteLine($"Failed to write key event (keycode={keyCode}, press={press})");
        }

        var synEvt = new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = EV_SYN,
            Code = SYN_REPORT,
            Value = 0
        };

        var synBuffer = stackalloc byte[evtSize];
        Marshal.StructureToPtr(synEvt, (IntPtr)synBuffer, false);

        var synBytes = new Span<byte>(synBuffer, evtSize).ToArray();
        write(this.virtualKeyboardFd, synBytes, synBytes.Length);
    }

    private void SendChar(char c)
    {
        var map = this.GetLayoutMap();

        if (!map.TryGetValue(c, out var mapping))
        {
            Console.WriteLine($"Warning: No keycode mapping for character '{c}'");
            return;
        }

        var keyCode = mapping.Keycode;
        var needsShift = mapping.Shift;

        if (needsShift)
        {
            this.SendKey(KEY_LEFTSHIFT, true);
        }

        this.SendKey(keyCode, true);
        this.SendKey(keyCode, false);

        if (needsShift)
        {
            this.SendKey(KEY_LEFTSHIFT, false);
        }
    }

    private Dictionary<char, KeycodMapping> GetLayoutMap()
    {
        if (this.layoutMap != null)
        {
            if (this.layoutMap.Count == 0)
            {
                Console.WriteLine("ERROR: Layout map is empty! This likely means xmodmap failed in the foreground process.");
            }
            return this.layoutMap;
        }

        Console.WriteLine("ERROR: Layout map not yet received from foreground process");
        return new Dictionary<char, KeycodMapping>();
    }

    public void Dispose()
    {
        if (this.virtualKeyboardFd >= 0)
        {
            ioctl(this.virtualKeyboardFd, UI_DEV_DESTROY, 0);
            close(this.virtualKeyboardFd);
            this.virtualKeyboardFd = -1;
            Console.WriteLine("Virtual keyboard destroyed");
        }
    }
}
