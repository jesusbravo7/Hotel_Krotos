using System.Windows;
using System.Windows.Controls;

namespace LosKrotos.Desktop;

public partial class RoomDetailDialog : Window
{
    private readonly Room _room;
    private readonly bool _hasActiveStay;
    private readonly bool _hasFutureReservation;
    public bool StatusChanged { get; private set; }
    public bool EditRequested { get; private set; }

    public RoomDetailDialog(RoomTile tile, bool hasActiveStay, bool hasFutureReservation)
    {
        InitializeComponent();
        _room = tile.Room;
        _hasActiveStay = hasActiveStay;
        _hasFutureReservation = hasFutureReservation;
        RoomTitle.Text = $"Habitación {_room.Number}";
        TypeText.Text = _room.Type;
        StatusText.Text = tile.StatusLabel;
        HeroImage.Source = tile.Image;
        RateText.Text = $"₡ {_room.Rate:N0}";
        CapacityText.Text = $"{_room.Capacity} personas";
        DescriptionText.Text = string.IsNullOrWhiteSpace(_room.Description)
            ? "Habitación cómoda para disfrutar de la estadía en Los Krotos."
            : _room.Description;
        StatusPicker.SelectedItem = StatusPicker.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, _room.Status));
        if (_hasActiveStay || _room.Status == "Occupied")
        {
            StatusPicker.IsEnabled = false;
            SaveStatusButton.IsEnabled = false;
            StatusHelpText.Text = "Habitación ocupada: registra el check-out antes de cambiar su estado.";
        }
        else if (_hasFutureReservation)
        {
            StatusPicker.IsEnabled = false;
            SaveStatusButton.IsEnabled = false;
            StatusHelpText.Text = "Hay una reserva confirmada. Gestiona su limpieza desde Limpieza; aquí no se puede cambiar el estado.";
        }
    }

    private void SaveStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_hasActiveStay || _room.Status == "Occupied")
        {
            MessageBox.Show(this, "Registra el check-out antes de cambiar el estado.", "Los Krotos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (StatusPicker.SelectedItem is not ComboBoxItem item) return;
        var status = item.Tag?.ToString() ?? _room.Status;
        if (_hasFutureReservation)
        {
            MessageBox.Show(this, "Hay una reserva confirmada para esta habitación. No se puede cambiar su estado desde aquí.",
                "Los Krotos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (status != _room.Status)
        {
            _room.Status = status;
            StatusChanged = true;
        }
        DialogResult = true;
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        EditRequested = true;
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
