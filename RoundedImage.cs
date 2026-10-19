using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LosKrotos.Desktop;

public static class RoundedImage
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius", typeof(double), typeof(RoundedImage), new PropertyMetadata(0d, OnRadiusChanged));

    public static void SetRadius(DependencyObject element, double value) => element.SetValue(RadiusProperty, value);
    public static double GetRadius(DependencyObject element) => (double)element.GetValue(RadiusProperty);

    private static void OnRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Image image) return;
        image.SizeChanged -= UpdateClip;
        if ((double)e.NewValue > 0)
        {
            image.SizeChanged += UpdateClip;
            ApplyClip(image);
        }
        else image.Clip = null;
    }

    private static void UpdateClip(object sender, SizeChangedEventArgs e) => ApplyClip((Image)sender);

    private static void ApplyClip(Image image)
    {
        var radius = GetRadius(image);
        image.Clip = image.ActualWidth > 0 && image.ActualHeight > 0
            ? new RectangleGeometry(new Rect(0, 0, image.ActualWidth, image.ActualHeight), radius, radius)
            : null;
    }
}
