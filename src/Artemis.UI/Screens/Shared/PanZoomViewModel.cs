﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Artemis.UI.Extensions;
using RGB.NET.Core;
using SkiaSharp;
using Stylet;
using Point = System.Windows.Point;

namespace Artemis.UI.Screens.Shared
{
    public class PanZoomViewModel : PropertyChangedBase
    {
        private double _canvasHeight;
        private double _canvasWidth;
        private Point? _lastPanPosition;
        private bool _limitToZero;
        private double _panX;
        private double _panY;
        private double _zoom = 1;

        public Point? LastPanPosition
        {
            get => _lastPanPosition;
            set => SetAndNotify(ref _lastPanPosition, value);
        }

        public double Zoom
        {
            get => _zoom;
            set
            {
                if (!SetAndNotify(ref _zoom, value)) return;
                NotifyOfPropertyChange(nameof(ZoomPercentage));
            }
        }

        public double ZoomPercentage
        {
            get => Zoom * 100;
            set => SetZoomFromPercentage(value);
        }

        public double PanX
        {
            get => _panX;
            set
            {
                if (!SetAndNotify(ref _panX, value)) return;
                NotifyOfPropertyChange(nameof(BackgroundViewport));
            }
        }

        public double PanY
        {
            get => _panY;
            set
            {
                if (!SetAndNotify(ref _panY, value)) return;
                NotifyOfPropertyChange(nameof(BackgroundViewport));
            }
        }

        public double CanvasWidth
        {
            get => _canvasWidth;
            set => SetAndNotify(ref _canvasWidth, value);
        }

        public double CanvasHeight
        {
            get => _canvasHeight;
            set => SetAndNotify(ref _canvasHeight, value);
        }

        public bool LimitToZero
        {
            get => _limitToZero;
            set => SetAndNotify(ref _limitToZero, value);
        }

        public Rect BackgroundViewport => new(PanX, PanY, 20, 20);

        public void ProcessMouseScroll(object sender, MouseWheelEventArgs e)
        {
            Point relative = GetRelativeMousePosition(sender, e);
            double absoluteX = relative.X * Zoom + PanX;
            double absoluteY = relative.Y * Zoom + PanY;

            if (e.Delta > 0)
                Zoom *= 1.1;
            else
                Zoom *= 0.9;

            // Limit to a min of 0.1 and a max of 2.5 (10% - 250% in the view)
            Zoom = Math.Max(0.1, Zoom);

            // Update the PanX/Y to enable zooming relative to cursor
            if (LimitToZero)
            {
                PanX = Math.Min(0, absoluteX - relative.X * Zoom);
                PanY = Math.Min(0, absoluteY - relative.Y * Zoom);
            }
            else
            {
                PanX = absoluteX - relative.X * Zoom;
                PanY = absoluteY - relative.Y * Zoom;
            }
        }

        public void ProcessMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                LastPanPosition = null;
                return;
            }

            if (LastPanPosition == null)
                LastPanPosition = e.GetPosition((IInputElement) sender);

            Point position = e.GetPosition((IInputElement) sender);
            Vector? delta = LastPanPosition - position;

            if (LimitToZero)
            {
                PanX = Math.Min(0, PanX - delta.Value.X);
                PanY = Math.Min(0, PanY - delta.Value.Y);
            }
            else
            {
                PanX -= delta.Value.X;
                PanY -= delta.Value.Y;
            }

            LastPanPosition = position;
        }

        public void Reset()
        {
            Zoom = 1;
            PanX = 0;
            PanY = 0;
        }

        public void Reset(SKRect rect)
        {
            Zoom = Math.Min(CanvasWidth / rect.Width, CanvasHeight / rect.Height);
            PanX = rect.Left * -1 * Zoom;
            PanY = rect.Top * -1 * Zoom;
        }

        public Rect TransformContainingRect(Rect rect)
        {
            // Create the same transform group the view is using
            TransformGroup transformGroup = new();
            transformGroup.Children.Add(new ScaleTransform(Zoom, Zoom));
            transformGroup.Children.Add(new TranslateTransform(PanX, PanY));

            // Apply it to the device rect
            return transformGroup.TransformBounds(rect);
        }

        public Rect UnTransformContainingRect(Rect rect)
        {
            // Create the same transform group the view is using
            TransformGroup transformGroup = new();
            transformGroup.Children.Add(new TranslateTransform(PanX * -1, PanY * -1));
            transformGroup.Children.Add(new ScaleTransform(1 / Zoom, 1 / Zoom));

            // Apply it to the device rect
            return transformGroup.TransformBounds(rect);
        }

        public Point GetRelativeMousePosition(object container, MouseEventArgs e)
        {
            // Get the mouse position relative to the panned / zoomed panel, not very MVVM but I can't find a better way
            return e.GetPosition(((Panel) container).Children[0]);
        }

        private void SetZoomFromPercentage(double value)
        {
            Point relative = new((PanX * -1 + CanvasWidth / 2) / Zoom, (PanY * -1 + CanvasHeight / 2) / Zoom);
            double absoluteX = relative.X * Zoom + PanX;
            double absoluteY = relative.Y * Zoom + PanY;

            Zoom = value / 100;

            // Focus towards the center of the zoomed area
            PanX = absoluteX - relative.X * Zoom;
            PanY = absoluteY - relative.Y * Zoom;
        }
    }
}