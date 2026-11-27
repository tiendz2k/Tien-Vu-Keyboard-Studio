using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.3 — GameSense: lop phu chong loa toan man hinh khi bi flashbang.
    // Cua so khong vien, luon o tren cung, phu kin man hinh chinh bang mot
    // anh (den/xam co san hoac anh nguoi dung chon) voi do mo tuy chinh.
    // Chi hien trong luc do flash con cao; game van nhan input binh thuong
    // o cua so game phia duoi (overlay khong nhan focus/chuot).
    // =====================================================================
    public sealed class FlashOverlay : Window
    {
        private readonly Image _img;
        private readonly Border _solid;

        public FlashOverlay()
        {
            SystemDecorations = SystemDecorations.None;
            WindowState = WindowState.FullScreen;
            Topmost = true;
            CanResize = false;
            ShowInTaskbar = false;
            Background = Brushes.Transparent;
            // Khong lay focus, khong chan chuot cua game
            Focusable = false;

            _img = new Image { Stretch = Stretch.UniformToFill, IsVisible = false };
            _solid = new Border { Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x08)) };
            var grid = new Grid();
            grid.Children.Add(_solid);
            grid.Children.Add(_img);
            Content = grid;
        }

        /// <summary>
        /// imageSpec: "black" (den tuyen) / "gray" (xam diu) / duong dan file anh.
        /// opacity: 0.3..1.0 — do che phu cua lop.
        /// </summary>
        public void ShowOverlay(string imageSpec, double opacity)
        {
            opacity = Math.Min(1.0, Math.Max(0.1, opacity));
            Opacity = opacity;
            if (!string.IsNullOrEmpty(imageSpec) && imageSpec != "black" && imageSpec != "gray" &&
                File.Exists(imageSpec))
            {
                try
                {
                    _img.Source = new Bitmap(imageSpec);
                    _img.IsVisible = true;
                    _solid.IsVisible = false;
                }
                catch { _img.IsVisible = false; _solid.IsVisible = true; }
            }
            else
            {
                _img.IsVisible = false;
                _solid.IsVisible = true;
                _solid.Background = new SolidColorBrush(imageSpec == "gray"
                    ? Color.FromRgb(0x3A, 0x3D, 0x46)
                    : Color.FromRgb(0x05, 0x05, 0x08));
            }
            if (!IsVisible) Show();
            Activate();
        }

        public void HideOverlay()
        {
            if (IsVisible) Hide();
        }
    }
}
