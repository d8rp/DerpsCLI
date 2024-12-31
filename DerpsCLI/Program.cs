using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using System;
using DerpsCLI;
using static System.Net.Mime.MediaTypeNames;

[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]

// Set the display message
foreach (char character in "Hello, Win32!")
{
    AddToDrawingQueue(character);
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
            drawCursor = new (10, 10);
            FirstInput = false;
            Redraw = true;
        }

        uint keyCode = wParam.Value.ToUInt32();  // The key that was pressed
        Console.WriteLine($"Key pressed: {keyCode}");

        // Example: Handling the 'Enter' key
        if (keyCode == VK.VK_RETURN)  // VK_RETURN is the virtual key code for the Enter key
        {
            Console.WriteLine("Enter key was pressed.");

            // AddToDrawingQueue('\n');
            AddToTextStorage('\n');
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

        AddToTextStorage(character);
        // AddToDrawingQueue(character);

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

        // START from this part this is purely place holder code that is to be optimised
        // Define bounding rectangle
        RECT r1 = new RECT(0, 0, 0, 0);

        // Set right margin so that DT_CALCRECT flag works with DT_WORDBREAK flag
        int rightmarg1 = GetDeviceCaps(deviceContextHandle, HORZRES);
        r1.right = rightmarg1;

        var text1 = buffer.GetFullText();  // The text you want to display

        fixed (char* p = text1)
        {
            DrawText(deviceContextHandle, p, text1.Length, &r1,
                    DT.DT_NOPREFIX | DT.DT_CALCRECT | DT.DT_WORDBREAK);

            // Move rectangle to cursor so text appeart at teh right position
            r1.left += 10;
            r1.top += 10;
            r1.right += 10;
            r1.bottom += 10;

            Console.WriteLine("{0}, {1}, {2}, {3}", r1.left, r1.top, r1.right, r1.bottom);

            DrawText(deviceContextHandle, p, text1.Length, &r1,
                DT.DT_NOPREFIX | DT.DT_LEFT | DT.DT_WORDBREAK | DT.DT_EDITCONTROL);
        }
        // END
        if (DrawingQueue.Count > 0)
        {
            Console.WriteLine("1");
            // Render the text
            var text = string.Join("", GetDrawingQueue());  // The text you want to display
            var x = drawCursor.X;  // X coordinate for the text
            var y = drawCursor.Y;  // Y coordinate for the text

            Console.WriteLine(String.Format("Rendering: {0} at ({1},{2})", text, x, y));

            // Define bounding rectangle
            RECT r = new RECT(0, 0, 0, 0);
            int xOffset;
            int yOffset;

            // Set right margin so that DT_CALCRECT flag works with DT_WORDBREAK flag
            int rightmarg = GetDeviceCaps(deviceContextHandle, HORZRES);
            r.right = rightmarg;
            Console.WriteLine("2");
            // Convert string to char* (win32)
            fixed (char* p = text)
            {
                Console.WriteLine("3");
                DrawText(deviceContextHandle, p, text.Length, &r, 
                    DT.DT_CALCRECT | DT.DT_NOPREFIX | DT.DT_WORDBREAK ); // DrawText is used to get the size of the text

                // Set X and Y offset for later use
                xOffset = Math.Abs(r.right - r.left);
                yOffset = Math.Abs(r.bottom - r.top);

                // Check if there's a newline in the text
                // If there's a newline (i.e., cursor moves to a new line), adjust yOffset
                int lineCount = text.Count(c => c == '\n');
                yOffset = yOffset*lineCount / (lineCount + 1); // Multiply by the number of lines for multiple newlines and handle case where newline changes the height of bounging box
                Console.WriteLine("4");
                // Move rectangle to cursor so text appeart at teh right position
                r.left += drawCursor.X;
                r.top += drawCursor.Y;
                r.right += drawCursor.X;
                r.bottom += drawCursor.Y;

                Console.WriteLine("{0}, {1}, {2}, {3}", r.left, r.top, r.right, r.bottom);
                DrawText(deviceContextHandle, p, text.Length, &r,
                    DT.DT_NOPREFIX | DT.DT_LEFT | DT.DT_WORDBREAK | DT.DT_EDITCONTROL);
            }

            // TextOut(deviceContextHandle, drawCursor.X, drawCursor.Y, text, text.Length);
            
            // Move the cursor accordingly
            drawCursor = new(drawCursor.X + xOffset, drawCursor.Y + yOffset);

            // Clear the CharTyped Queue
            ClearDrawingQueue();
        }

        // End painting
        EndPaint(window, &ps);
        return 0; // We successfully handled the message
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



static IEnumerable<String> GetDrawingQueue()
{
    lock (queueLock)
    {
        return DrawingQueue.ToList();  // Return a copy to prevent direct modification.
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

    private static GapBuffer buffer = new GapBuffer();

    static Queue<String> DrawingQueue = new();
    static InputDrawCursor drawCursor = new (10, 10);
    static object queueLock = new object();
    static void AddToDrawingQueue(Char point)
    {
        lock (queueLock)
        {
            DrawingQueue.Enqueue(point.ToString());
        }
    }
    static void AddToDrawingQueue(String point)
    {
        lock (queueLock)
        {
            DrawingQueue.Enqueue(point);
        }
    }
    static void ClearDrawingQueue()
    {
        lock (queueLock)
        {
            DrawingQueue.Clear();
        }
    }

    static void AddToTextStorage(Char character)
    {
        buffer.Insert(character.ToString(), buffer.GetCursorPosition());
    }
    static void AddToTextStorage(string str)
    {
        buffer.Insert(str, buffer.GetCursorPosition());
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int nCount);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateFontW(int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight, uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet, uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality, uint fdwPitchAndFamily, string lpszFace);

    record InputDrawCursor(int X, int Y);
    
}