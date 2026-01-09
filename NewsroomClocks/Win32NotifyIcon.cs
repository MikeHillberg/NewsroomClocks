using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Drawing = System.Drawing;

namespace NewsroomClocks;

/// <summary>
/// Win32-based notification icon implementation using Shell_NotifyIcon API.
/// Provides similar functionality to WinForms NotifyIcon without the WinForms dependency.
/// </summary>
internal class Win32NotifyIcon : IDisposable
{
    private const int WM_APP = 0x8000;
    private const int WM_TRAYICON = WM_APP + 1;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const int TPM_BOTTOMALIGN = 0x0020;
    private const int TPM_LEFTALIGN = 0x0000;

    private readonly MessageWindow _messageWindow;
    private NOTIFYICONDATAW _notifyIconData;
    private HMENU _contextMenu;
    private bool _visible;
    private bool _disposed;
    private Drawing.Icon? _icon;

    public event EventHandler<MouseButton>? MouseClick;

    public Win32NotifyIcon()
    {
        _messageWindow = new MessageWindow(this);
        
        unsafe
        {
            _notifyIconData = new NOTIFYICONDATAW
            {
                cbSize = (uint)sizeof(NOTIFYICONDATAW),
                hWnd = new HWND(_messageWindow.Handle),
                uID = 1,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                uCallbackMessage = WM_TRAYICON
            };
        }
    }

    internal bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                if (_visible)
                {
                    AddIcon();
                }
                else
                {
                    RemoveIcon();
                }
            }
        }
    }

    internal Drawing.Icon? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                UpdateIcon();
            }
        }
    }

    internal string Text
    {
        get
        {
            unsafe
            {
                fixed (char* pText = _notifyIconData.szTip.AsSpan())
                {
                    return new string(pText);
                }
            }
        }
        set
        {
            if (value != null)
            {
                if (value.Length > 127)
                {
                    value = value.Substring(0, 127);
                }

                unsafe
                {
                    fixed (char* pText = _notifyIconData.szTip.AsSpan())
                    {
                        value.AsSpan().CopyTo(new Span<char>(pText, 128));
                        pText[value.Length] = '\0';
                    }
                }
            }

            if (_visible)
            {
                ModifyIcon();
            }
        }
    }

    internal void SetContextMenu(params (string text, Action action)[] menuItems)
    {
        // Destroy existing menu if any
        if (!_contextMenu.IsNull)
        {
            PInvoke.DestroyMenu(_contextMenu);
            _contextMenu = default;
        }

        // Create new popup menu
        _contextMenu = PInvoke.CreatePopupMenu();

        // Add menu items
        for (int i = 0; i < menuItems.Length; i++)
        {
            var (text, action) = menuItems[i];
            uint menuId = (uint)(i + 1);
            
            unsafe
            {
                fixed (char* pText = text)
                {
                    PInvoke.AppendMenu(_contextMenu, MENU_ITEM_FLAGS.MF_STRING, menuId, pText);
                }
            }

            _messageWindow.RegisterMenuAction(menuId, action);
        }
    }

    private void AddIcon()
    {
        unsafe
        {
            fixed (NOTIFYICONDATAW* pData = &_notifyIconData)
            {
                PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, pData);
            }
        }
    }

    private void RemoveIcon()
    {
        unsafe
        {
            fixed (NOTIFYICONDATAW* pData = &_notifyIconData)
            {
                PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, pData);
            }
        }
    }

    private void ModifyIcon()
    {
        unsafe
        {
            fixed (NOTIFYICONDATAW* pData = &_notifyIconData)
            {
                PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, pData);
            }
        }
    }

    private void UpdateIcon()
    {
        if (_icon != null)
        {
            _notifyIconData.hIcon = new HICON(_icon.Handle);
        }
        else
        {
            _notifyIconData.hIcon = default;
        }

        if (_visible)
        {
            ModifyIcon();
        }
    }

    private void OnTrayIconMessage(uint message, int x, int y)
    {
        switch (message)
        {
            case WM_LBUTTONUP:
                MouseClick?.Invoke(this, MouseButton.Left);
                break;

            case WM_RBUTTONUP:
                if (!_contextMenu.IsNull)
                {
                    ShowContextMenu(x, y);
                }
                break;
        }
    }

    private void ShowContextMenu(int x, int y)
    {
        // Set foreground window to ensure menu works correctly
        PInvoke.SetForegroundWindow(_notifyIconData.hWnd);

        // Show menu at cursor position
        unsafe
        {
            PInvoke.TrackPopupMenuEx(
                _contextMenu,
                (uint)(TPM_BOTTOMALIGN | TPM_LEFTALIGN),
                x,
                y,
                _notifyIconData.hWnd,
                null);
        }

        // Post a message to dismiss menu if clicked outside
        PInvoke.PostMessage(_notifyIconData.hWnd, 0, default, default);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            if (_visible)
            {
                RemoveIcon();
            }

            if (!_contextMenu.IsNull)
            {
                PInvoke.DestroyMenu(_contextMenu);
            }

            _messageWindow.Dispose();
        }
    }

    /// <summary>
    /// Hidden message-only window to receive notification icon callbacks.
    /// </summary>
    private class MessageWindow : IDisposable
    {
        private const string WindowClassName = "NewsroomClocks_NotifyIcon_MessageWindow";
        private readonly Win32NotifyIcon _owner;
        private readonly HWND _hwnd;
        private readonly Dictionary<uint, Action> _menuActions = new();
        private GCHandle _gcHandle;

        public IntPtr Handle => _hwnd;

        public MessageWindow(Win32NotifyIcon owner)
        {
            _owner = owner;
            
            // Keep this object alive while the window exists
            _gcHandle = GCHandle.Alloc(this);

            // Register window class
            unsafe
            {
                fixed (char* pClassName = WindowClassName)
                {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                    var wndClass = new WNDCLASSEXW
                    {
                        cbSize = (uint)sizeof(WNDCLASSEXW),
                        lpfnWndProc = WndProc,
                        hInstance = PInvoke.GetModuleHandle((PCWSTR)null),
                        lpszClassName = pClassName
                    };
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

                    PInvoke.RegisterClassEx(wndClass);

                    // Create message-only window
                    _hwnd = PInvoke.CreateWindowEx(
                        0,
                        WindowClassName,
                        WindowClassName,
                        0,
                        0, 0, 0, 0,
                        new HWND(new IntPtr(-3)), // HWND_MESSAGE
                        default,
                        default,
                        null);
                }
            }

            // Register this instance
            WindowInstances[_hwnd] = this;
        }

        public void RegisterMenuAction(uint menuId, Action action)
        {
            _menuActions[menuId] = action;
        }

        private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            try
            {
                if (msg == WM_TRAYICON)
                {
                    // Find the MessageWindow instance
                    // We'll store it in a static dictionary keyed by HWND
                    if (WindowInstances.TryGetValue(hwnd, out var window))
                    {
                        uint iconMessage = (uint)((long)lParam & 0xFFFF);
                        
                        if (iconMessage == WM_LBUTTONUP || iconMessage == WM_RBUTTONUP)
                        {
                            // Get cursor position
                            PInvoke.GetCursorPos(out var point);
                            window._owner.OnTrayIconMessage(iconMessage, point.X, point.Y);
                        }
                    }
                }
                else if (msg == 0x0111) // WM_COMMAND
                {
                    uint menuId = (uint)((nuint)wParam & 0xFFFF);
                    if (WindowInstances.TryGetValue(hwnd, out var window))
                    {
                        if (window._menuActions.TryGetValue(menuId, out var action))
                        {
                            action?.Invoke();
                        }
                    }
                }
            }
            catch
            {
                // Ignore exceptions in window proc
            }

            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        // Static dictionary to map HWNDs to MessageWindow instances
        private static readonly Dictionary<HWND, MessageWindow> WindowInstances = new();

        public void Dispose()
        {
            if (!_hwnd.IsNull)
            {
                WindowInstances.Remove(_hwnd);
                PInvoke.DestroyWindow(_hwnd);
            }

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }
        }
    }
}

internal enum MouseButton
{
    Left,
    Right
}
