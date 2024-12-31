using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using System;

[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]

// Set the display message
foreach (char character in "Hello, Win32!")
{
    AddCharTyped(character);
}

var window = CreateWindow();
RunMessageLoop(window);

static unsafe void RunMessageLoop(HWND window)
{
    MSG msg;
    // Continuously process messages from the message queue
    while (GetMessageW(&msg, window, 0, 0) && !Closed)
    {
        TranslateMessage(&msg);

        // Dispatch message to the WinProc function
        DispatchMessageW(&msg);
    }
}

[UnmanagedCallersOnly]
static unsafe LRESULT WinProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
{
    // Handling WM_PAINT message
    // Handling keyboard input (key down)
    if (message == WM.WM_KEYDOWN)
    {
        if (FirstInput)
        {
            Console.WriteLine("First input");
            FirstInput = false;
            Redraw = true;
        }

        uint keyCode = wParam.Value.ToUInt32();  // The key that was pressed
        Console.WriteLine($"Key pressed: {keyCode}");

        // Example: Handling the 'Enter' key
        if (keyCode == VK.VK_RETURN)  // VK_RETURN is the virtual key code for the Enter key
        {
            Console.WriteLine("Enter key was pressed.");

            HDC hdc = GetDC(window);  // Get the device context of the window

            // The character to measure (e.g., the letter 'A')
            char character = 'A';

            // To get the width, we'll use GetTextExtentPoint32
            SIZE size;

            GetTextExtentPoint32(hdc, &character, 1, &size);

            // Clean up
            ReleaseDC(window, hdc);

            // Adcance curson down
            cursor = new(cursor.X, cursor.Y + (int)((size.cy) * 1.5));
        }

        // Example: Handling the 'Escape' key
        if (keyCode == VK.VK_ESCAPE)  // VK_ESCAPE is the virtual key code for the Escape key
        {
            Console.WriteLine("Escape key was pressed. Closing window.");
            PostMessageW(window, WM.WM_CLOSE, 0, 0);  // Close the window when Esc is pressed
        }

        return 0;
    }

    // Handling WM_CHAR for printable characters
    if (message == WM.WM_CHAR)
    {
        // Render the text
        uint charCode = wParam.Value.ToUInt32();  // The character pressed
        var character = Convert.ToChar(charCode);
        
        Console.WriteLine($"Character pressed: {Convert.ToChar(charCode)}");

        AddCharTyped(character);

        // Force OS to draw the window (and send us WM_PAINT message)
        InvalidateRect(window, null, BOOL.FALSE);

        return 0;
    }

    if (message == WM.WM_PAINT)
    {
        var ps = new PAINTSTRUCT();
        var deviceContextHandle = BeginPaint(window, &ps);

        // Filling in the background
        if (Redraw)
        {
            Console.WriteLine("Redraw");
            FillRect(deviceContextHandle, &ps.rcPaint, HBRUSH.NULL);
            Redraw = false;
        }

        // If there is at least one chracter in the queue
        if (CharTyped.Count > 0)
        {
            // Render the text
            var text = string.Join("", GetCharTyped());  // The text you want to display
            var x = cursor.X;  // X coordinate for the text
            var y = cursor.Y;  // Y coordinate for the text

            Console.WriteLine(String.Format("Rendering: {0} at ({1},{2})", text, x, y));

            // Define bounding rectangle
            RECT r = new RECT(0, 0, 0, 0);

            // Convert string to char* (win32)
            fixed (char* p = text)
            {
                DrawText(deviceContextHandle, p, text.Length, &r, 
                    DT.DT_CALCRECT | DT.DT_NOPREFIX | DT.DT_SINGLELINE); // DrawText is used to get the size of the text
            }


            TextOut(deviceContextHandle, cursor.X, cursor.Y, text, text.Length);
            
            // Move the cursor accordingly
            cursor = new(cursor.X + Math.Abs(r.right - r.left), cursor.Y);

            // Clear the CharTyped Queue
            ClearCharTyped();
        }

        // End painting
        EndPaint(window, &ps);
        return 0; // We successfully handled the message
    }

    // Handling when mouse moved in the window
    if (message == WM.WM_MOUSEMOVE)
    {
        // Force OS to draw the window (and send us WM_PAINT message)
        InvalidateRect(window, null, BOOL.FALSE);

        return 0;
    }

    // Handling when window was resized
    if (message == WM.WM_SIZE)
    {
        // When window was resized, we need to make sure that the background is there
        Redraw = true;
        return 0;
    }

    // Track when we need to stop the message loop
    if (message == WM.WM_CLOSE)
    {
        Closed = true;
        return 0;
    }

    // Ignoring everything else
    return DefWindowProcW(window, message, wParam, lParam);
}

static unsafe HWND CreateWindow()
{
    var className = "windowClass";

    fixed (char* classNamePtr = className) // Hey, GC, please don't move the string
    {
        var windowClass = new WNDCLASSEXW();
        windowClass.cbSize = (uint)sizeof(WNDCLASSEXW); // Size (in bytes) of WNDCLASSEXW structure
        windowClass.hbrBackground = HBRUSH.NULL;
        windowClass.hCursor = HCURSOR.NULL;
        windowClass.hIcon = HICON.NULL;
        windowClass.hIconSm = HICON.NULL;
        windowClass.hInstance = HINSTANCE.NULL;
        windowClass.lpszClassName = classNamePtr; // The UTF-16 window class name
        windowClass.lpszMenuName = null;
        windowClass.style = 0;
        windowClass.lpfnWndProc = &WinProc; // Pointer to WinProc function

        var classId = RegisterClassExW(&windowClass);
    }

    var windowName = "windowName";
    fixed (char* windowNamePtr = windowName) // Hey, GC, please don't move the string
    fixed (char* classNamePtr = className) // GC, do not move this one too
    {
        var width = 500;
        var height = 500;
        var x = 0;
        var y = 0;

        // Set window style to ensure it is visible and interactive
        var styles = WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE; // Ensure the window is visible and interactive
        var exStyles = 0; // Extended styles that we do not care about

        return CreateWindowExW((uint)exStyles,
            classNamePtr,  // UTF-16 window class name
            windowNamePtr, // UTF-16 window name (this will be in the title bar)
            (uint)styles,
            x, y, // Window initial position
            width, height, // Window initial size
            HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
    }
}

static void AddCharTyped(Char point)
{
    lock (queueLock)
    {
        CharTyped.Enqueue(point.ToString());
    }
}

static IEnumerable<String> GetCharTyped()
{
    lock (queueLock)
    {
        return CharTyped.ToList();  // Return a copy to prevent direct modification.
    }
}

static void ClearCharTyped()
{
    lock (queueLock)
    {
        CharTyped.Clear();
    }
}

/// <summary>
/// Top-level statements in C# are wrapped in an implicit Program class.
/// We can extend that Program class via implementing the additional stuff in a partial class
/// </summary>
partial class Program
{
    private static bool Closed;

    private static bool Redraw;

    private static bool FirstInput = true;

    static Queue<String> CharTyped = new();

    static InputCursor cursor = new (10, 10);

    static object queueLock = new object();

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int nCount);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateFontW(int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight, uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet, uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality, uint fdwPitchAndFamily, string lpszFace);

    record InputCursor(int X, int Y);
}