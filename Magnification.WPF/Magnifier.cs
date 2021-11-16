using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Magnification.WPF
{
    public class Magnifier : IDisposable
    {
        private Window window;
        private IntPtr hwnd;
        private IntPtr hwndMag;
        private float magnification;
        private bool initialized;
        private RECT magWindowRect = new RECT();
        private DispatcherTimer timer;

        public Magnifier(Window window)
        {
            hwnd = new WindowInteropHelper(window).EnsureHandle();
            magnification = 2.0f;
            this.window = window;
            this.window.SizeChanged += form_Resize;
            this.window.Closing += form_FormClosing;

            timer = new DispatcherTimer();
            timer.Tick += timer_Tick;

            initialized = NativeMethods.MagInitialize();
            if (initialized)
            {
                SetupMagnifier();
                timer.Interval = TimeSpan.FromMilliseconds(NativeMethods.USER_TIMER_MINIMUM);
                timer.IsEnabled = true;
            }


        }

        void form_FormClosing(object sender, CancelEventArgs e)
        {
            timer.IsEnabled = false;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            UpdateMaginifier();
        }

        void form_Resize(object sender, RoutedEventArgs e)
        {
            ResizeMagnifier();
        }

        ~Magnifier()
        {
            Dispose(false);
        }

        protected virtual void ResizeMagnifier()
        {
            if (initialized && (hwndMag != IntPtr.Zero))
            {
                NativeMethods.GetClientRect(hwnd, ref magWindowRect);
                // Resize the control to fill the window.
                NativeMethods.SetWindowPos(hwndMag, IntPtr.Zero,
                    magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, 0);
            }
        }

        public virtual void UpdateMaginifier()
        {
            if ((!initialized) || (hwndMag == IntPtr.Zero))
                return;
            
            // mousePoint xy equle left,top
            POINT mousePoint = new POINT();
            RECT sourceRect = new RECT();

            NativeMethods.GetCursorPos(ref mousePoint);

            // height width
            int width = (int)((magWindowRect.right - magWindowRect.left) / magnification);
            int height = (int)((magWindowRect.bottom - magWindowRect.top) / magnification);

            sourceRect.left = mousePoint.x - width / 2;
            sourceRect.top = mousePoint.y - height / 2;


            // Don't scroll outside desktop area.
            if (sourceRect.left < 0)
            {
                sourceRect.left = 0;
            }
            if (sourceRect.left > NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) - width)
            {
                sourceRect.left = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) - width;
            }
            sourceRect.right = sourceRect.left + width;

            if (sourceRect.top < 0)
            {
                sourceRect.top = 0;
            }
            if (sourceRect.top > NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) - height)
            {
                sourceRect.top = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) - height;
            }
            sourceRect.bottom = sourceRect.top + height;

            if (this.window == null)
            {
                timer.IsEnabled = false;
                return;
            }

            //if (this.window.IsDisposed)
            //{
            //    timer.IsEnabled = false;
            //    return;
            //}

            // Set the source rectangle for the magnifier control.
            NativeMethods.MagSetWindowSource(hwndMag, sourceRect);

            // Reclaim topmost status, to prevent unmagnified menus from remaining in view. 
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                (int)SetWindowPosFlags.SWP_NOACTIVATE | (int)SetWindowPosFlags.SWP_NOMOVE | (int)SetWindowPosFlags.SWP_NOSIZE);

            // Force redraw.
            NativeMethods.InvalidateRect(hwndMag, IntPtr.Zero, true);
        }

        public float Magnification
        {
            get { return magnification; }
            set
            {
                if (magnification != value)
                {
                    magnification = value;
                    // Set the magnification factor.
                    Transformation matrix = new Transformation(magnification);
                    NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
                }
            }
        }

        protected void SetupMagnifier()
        {
            if (!initialized)
                return;

            IntPtr hInst;

            hInst = NativeMethods.GetModuleHandle(null);

            // Make the window opaque.
            //form.AllowTransparency = true; (done in xaml)
            //window.Background = Brushes.Transparent; (done in xaml)
            //window.Opacity = 255;

            // Create a magnifier control that fills the client area.
            NativeMethods.GetClientRect(hwnd, ref magWindowRect);
            hwndMag = NativeMethods.CreateWindow((int)ExtendedWindowStyles.WS_EX_CLIENTEDGE, NativeMethods.WC_MAGNIFIER,
                "MagnifierWindow", (int)WindowStyles.WS_CHILD | (int)MagnifierStyle.MS_SHOWMAGNIFIEDCURSOR |
                (int)WindowStyles.WS_VISIBLE,
                magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, hwnd, IntPtr.Zero, hInst, IntPtr.Zero);

            if (hwndMag == IntPtr.Zero)
            {
                return;
            }

            // Set the magnification factor.
            Transformation matrix = new Transformation(magnification);
            NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
        }

        protected void RemoveMagnifier()
        {
            if (initialized)
                NativeMethods.MagUninitialize();
        }

        protected virtual void Dispose(bool disposing)
        {
            timer.IsEnabled = false;
            //if (disposing)
            //    timer.Dispose();
            timer = null;
            window.SizeChanged -= form_Resize;
            RemoveMagnifier();
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
