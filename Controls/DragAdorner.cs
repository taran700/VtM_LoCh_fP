using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace VtM_LoCh_fP.Controls
{
    /// <summary>
    /// Полупрозрачная копия вкладки, которая едет за курсором во время
    /// перетаскивания. Без неё пользователь не видит, что именно тащит.
    /// </summary>
    internal sealed class DragAdorner : Adorner
    {
        private readonly VisualBrush _brush;
        private readonly Size _size;
        private readonly Point _grabOffset;
        private Point _cursor;

        internal DragAdorner(UIElement adornedElement, UIElement dragged, Point grabOffset)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            _grabOffset = grabOffset;
            _size = new Size(
                Math.Max(dragged.RenderSize.Width, 1),
                Math.Max(dragged.RenderSize.Height, 1));
            _brush = new VisualBrush(dragged)
            {
                Opacity = 0.75,
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };
        }

        /// <summary>Текущая позиция курсора в координатах адорнированного элемента.</summary>
        internal void MoveTo(Point cursor)
        {
            _cursor = cursor;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var origin = new Point(_cursor.X - _grabOffset.X, _cursor.Y - _grabOffset.Y);
            drawingContext.DrawRectangle(_brush, null, new Rect(origin, _size));
        }
    }
}
