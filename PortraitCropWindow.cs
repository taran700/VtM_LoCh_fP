using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VtM_LoCh_fP
{
    /// <summary>Выбор изображения и кадрирование портрета в квадрат 512x512.</summary>
    internal sealed class PortraitCropWindow : Window
    {
        private const double PreviewSize = 420.0;
        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly Slider _zoom;
        private readonly TextBlock _hint;
        private BitmapSource _source;
        private double _imageWidth, _imageHeight, _left, _top, _baseScale;
        private Point _dragStart;
        private double _dragLeft, _dragTop;
        private bool _dragging;
        private bool _wheelZooming;

        public string ResultBase64 { get; private set; }

        public PortraitCropWindow(Window owner)
        {
            Owner = owner;
            // Синхронизируем системную строку заголовка и фон окна с выбранной темой.
            ThemeManager.AttachWindow(this);
            Title = "Портрет персонажа";
            Width = 570;
            Height = 650;
            MinWidth = 540;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            SetResourceReference(Window.BackgroundProperty, "CardBgBrush");
            SetResourceReference(Control.ForegroundProperty, "TextBrush");

            var root = new StackPanel { Margin = new Thickness(18) };
            Content = root;
            var choose = new Button { Content = "Выбрать изображение", Height = 36, Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Stretch };
            choose.Click += Choose_Click;
            root.Children.Add(choose);

            var viewport = new Border
            {
                Width = PreviewSize, Height = PreviewSize, HorizontalAlignment = HorizontalAlignment.Center,
                Background = (Brush)FindResource("InputBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1),
                ClipToBounds = true
            };
            _canvas = new Canvas { Width = PreviewSize, Height = PreviewSize, ClipToBounds = true };
            _image = new Image { Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
            _canvas.Children.Add(_image);
            viewport.Child = _canvas;
            root.Children.Add(viewport);
            _hint = new TextBlock { Text = "Выберите изображение. Перетаскивайте его мышью, а колёсиком меняйте масштаб кадрирования.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 6), Foreground = (Brush)FindResource("MutedBrush") };
            root.Children.Add(_hint);

            var zoomRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            zoomRow.Children.Add(new TextBlock { Text = "Масштаб", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            _zoom = new Slider { Minimum = 1, Maximum = 3, Value = 1, Width = 390, IsEnabled = false };
            _zoom.ValueChanged += (s, e) => { if (!_wheelZooming) ApplyZoom(); };
            _canvas.PreviewMouseWheel += Canvas_PreviewMouseWheel;
            zoomRow.Children.Add(_zoom);
            root.Children.Add(zoomRow);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancel = new Button { Content = "Отмена", Width = 100, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (s, e) => { DialogResult = false; };
            var save = new Button { Content = "Использовать портрет", Width = 170, Height = 34, IsEnabled = false };
            save.Click += Save_Click;
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            root.Children.Add(buttons);
            _saveButton = save;

            _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            _canvas.MouseMove += Canvas_MouseMove;
        }

        private readonly Button _saveButton;

        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите портрет персонажа",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.wdp|Все файлы|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                using (var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0) throw new InvalidDataException("В файле не найдено изображение.");
                    _source = decoder.Frames[0];
                    _source = new FormatConvertedBitmap(_source, PixelFormats.Pbgra32, null, 0);
                    _source.Freeze();
                }
                _image.Source = _source;
                _baseScale = Math.Max(PreviewSize / _source.PixelWidth, PreviewSize / _source.PixelHeight);
                _zoom.Value = 1;
                _zoom.IsEnabled = true;
                _saveButton.IsEnabled = true;
                ApplyZoom();
                _hint.Text = "Перемещайте изображение мышью, меняйте масштаб колёсиком или ползунком. Итоговый портрет будет сохранён в PNG 512 × 512.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть изображение. Возможно, формат файла не поддерживается.\n\n" + ex.Message, "Ошибка изображения", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyZoom()
        {
            if (_source == null) return;
            SetZoom(_zoom.Value, new Point(PreviewSize / 2.0, PreviewSize / 2.0));
        }

        private void Canvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_source == null) return;
            Point anchor = e.GetPosition(_canvas);
            double next = _zoom.Value * (e.Delta > 0 ? 1.12 : 1.0 / 1.12);
            next = Math.Max(_zoom.Minimum, Math.Min(_zoom.Maximum, next));
            _wheelZooming = true;
            _zoom.Value = next;
            _wheelZooming = false;
            SetZoom(next, anchor);
            e.Handled = true;
        }

        private void SetZoom(double value, Point anchor)
        {
            if (_source == null) return;
            double oldWidth = _imageWidth;
            double oldHeight = _imageHeight;
            double oldLeft = _left;
            double oldTop = _top;
            double scale = _baseScale * value;
            _imageWidth = _source.PixelWidth * scale;
            _imageHeight = _source.PixelHeight * scale;
            _image.Width = _imageWidth;
            _image.Height = _imageHeight;

            if (oldWidth > 0 && oldHeight > 0)
            {
                double imageX = (anchor.X - oldLeft) / oldWidth;
                double imageY = (anchor.Y - oldTop) / oldHeight;
                _left = anchor.X - imageX * _imageWidth;
                _top = anchor.Y - imageY * _imageHeight;
            }
            else
            {
                _left = (PreviewSize - _imageWidth) / 2.0;
                _top = (PreviewSize - _imageHeight) / 2.0;
            }
            SetImagePosition();
        }

        private void SetImagePosition()
        {
            _left = Math.Min(0, Math.Max(PreviewSize - _imageWidth, _left));
            _top = Math.Min(0, Math.Max(PreviewSize - _imageHeight, _top));
            Canvas.SetLeft(_image, _left);
            Canvas.SetTop(_image, _top);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_source == null) return;
            _dragging = true;
            _dragStart = e.GetPosition(_canvas);
            _dragLeft = _left;
            _dragTop = _top;
            _canvas.CaptureMouse();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point p = e.GetPosition(_canvas);
            _left = _dragLeft + p.X - _dragStart.X;
            _top = _dragTop + p.Y - _dragStart.Y;
            SetImagePosition();
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            _canvas.ReleaseMouseCapture();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_source == null) return;
            try
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(512.0 / PreviewSize, 512.0 / PreviewSize));
                    dc.DrawImage(_source, new Rect(_left, _top, _imageWidth, _imageHeight));
                    dc.Pop();
                }
                var bitmap = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var memory = new MemoryStream())
                {
                    encoder.Save(memory);
                    ResultBase64 = Convert.ToBase64String(memory.ToArray());
                }
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось подготовить портрет.\n\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
