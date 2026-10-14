using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LosKrotos.Desktop;

public partial class MainWindow : Window
{
    private readonly HotelData _data;
    public ObservableCollection<Room> Rooms => _data.Rooms;
    public ObservableCollection<Guest> Guests => _data.Guests;
    public ObservableCollection<Reservation> FilteredReservations { get; } = [];
    public ObservableCollection<Reservation> UpcomingReservations { get; } = [];
    public ObservableCollection<Reservation> UpcomingDepartures { get; } = [];

    public MainWindow()
    {
        _data = HotelStore.Load();
        InitializeComponent();
        DataContext = this;
        Pages.SelectedIndex = 0;
        RefreshAll();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out var index))
            Pages.SelectedIndex = index;
    }

    private void OpenRooms_Click(object sender, RoutedEventArgs e) => Pages.SelectedIndex = 2;
    private void OpenCleaning_Click(object sender, RoutedEventArgs e) => Pages.SelectedIndex = 4;

    private void NewReservation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ReservationDialog(_data.Rooms, _data.Guests, _data.Reservations) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.CreatedReservation is null) return;
        if (dialog.CreatedGuest is not null) _data.Guests.Add(dialog.CreatedGuest);
        _data.Reservations.Add(dialog.CreatedReservation);
        PersistAndRefresh();
        Pages.SelectedIndex = 1;
        ReservationsGrid.SelectedItem = dialog.CreatedReservation;
    }

    private void ReservationSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshReservationList();

    private void ReservationsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var reservation = ReservationsGrid.SelectedItem as Reservation;
        SelectedReservationName.Text = reservation?.GuestName ?? "Ninguna seleccionada";
        SelectedReservationInfo.Text = reservation is null ? "" :
            $"Habitación {reservation.RoomNumber} · {reservation.CheckIn:dd/MM/yyyy} al {reservation.CheckOut:dd/MM/yyyy}\n{reservation.Adults} adulto(s), {reservation.Children} niño(s) · {reservation.StatusLabel}";
    }

    private void CheckIn_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationsGrid.SelectedItem is not Reservation reservation) { Inform("Selecciona una reserva."); return; }
        if (reservation.Status != "Confirmed") { Inform("Solo se puede registrar la entrada de una reserva confirmada."); return; }
        if (reservation.CheckIn.Date > DateTime.Today) { Inform("La fecha de entrada aún no ha llegado."); return; }
        var room = _data.Rooms.First(r => r.Number == reservation.RoomNumber);
        if (room.Status != "Available") { Inform("La habitación debe estar disponible y limpia antes del check-in."); return; }
        if (reservation.CheckOut.Date <= DateTime.Today) { Inform("La fecha de salida de esta reserva ya pasó."); return; }
        reservation.Status = "CheckedIn";
        room.Status = "Occupied";
        PersistAndRefresh();
    }

    private void CheckOut_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationsGrid.SelectedItem is not Reservation reservation) { Inform("Selecciona una reserva."); return; }
        if (reservation.Status != "CheckedIn") { Inform("Solo se puede registrar la salida de un huésped en casa."); return; }
        reservation.Status = "Completed";
        _data.Rooms.First(r => r.Number == reservation.RoomNumber).Status = "Dirty";
        PersistAndRefresh();
    }

    private void CancelReservation_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationsGrid.SelectedItem is not Reservation reservation) { Inform("Selecciona una reserva."); return; }
        if (reservation.Status != "Confirmed") { Inform("Solo se pueden cancelar reservas confirmadas."); return; }
        if (MessageBox.Show(this, $"¿Cancelar la reserva de {reservation.GuestName}?", "Confirmar cancelación",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        reservation.Status = "Cancelled";
        PersistAndRefresh();
    }

    private void RoomsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoomsGrid.SelectedItem is not Room room) return;
        RoomNumberBox.IsReadOnly = true;
        RoomNumberBox.Text = room.Number;
        RoomTypeBox.SelectedItem = RoomTypeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Content, room.Type));
        RoomCapacityBox.Text = room.Capacity.ToString();
        RoomRateBox.Text = room.Rate.ToString(CultureInfo.InvariantCulture);
        RoomDescriptionBox.Text = room.Description;
    }

    private void NewRoom_Click(object sender, RoutedEventArgs e)
    {
        RoomsGrid.SelectedItem = null;
        RoomNumberBox.IsReadOnly = false;
        RoomNumberBox.Clear();
        RoomTypeBox.SelectedIndex = 0;
        RoomCapacityBox.Text = "2";
        RoomRateBox.Text = "450";
        RoomDescriptionBox.Clear();
        RoomNumberBox.Focus();
    }

    private void SaveRoom_Click(object sender, RoutedEventArgs e)
    {
        var room = RoomsGrid.SelectedItem as Room;
        var number = RoomNumberBox.Text.Trim();
        if (number.Length is < 2 or > 8 || !number.All(char.IsLetterOrDigit))
        { Inform("El número debe tener entre 2 y 8 letras o dígitos."); return; }
        if (room is null && _data.Rooms.Any(r => r.Number.Equals(number, StringComparison.OrdinalIgnoreCase)))
        { Inform("Ya existe una habitación con ese número."); return; }
        if (!int.TryParse(RoomCapacityBox.Text, out var capacity) || capacity < 1 || capacity > 12 ||
            !decimal.TryParse(RoomRateBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) || rate <= 0 ||
            RoomTypeBox.SelectedItem is not ComboBoxItem typeItem)
        { Inform("Revisa el tipo, la capacidad (1–12) y la tarifa positiva. Usa punto para decimales."); return; }
        if (room is null)
        {
            room = new Room { Number = number };
            _data.Rooms.Add(room);
        }
        room.Type = typeItem.Content.ToString()!;
        room.Capacity = capacity;
        room.Rate = rate;
        room.Description = RoomDescriptionBox.Text.Trim();
        PersistAndRefresh();
        RoomNumberBox.IsReadOnly = true;
        RoomsGrid.SelectedItem = room;
    }

    private void DeleteRoom_Click(object sender, RoutedEventArgs e)
    {
        if (RoomsGrid.SelectedItem is not Room room) { Inform("Selecciona una habitación existente."); return; }
        if (_data.Reservations.Any(r => r.RoomNumber == room.Number))
        { Inform("Esta habitación tiene historial de reservas y no se puede eliminar."); return; }
        if (MessageBox.Show(this, $"¿Eliminar la habitación {room.Number}?", "Confirmar eliminación",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _data.Rooms.Remove(room);
        RoomNumberBox.Clear();
        RoomCapacityBox.Clear();
        RoomRateBox.Clear();
        RoomDescriptionBox.Clear();
        PersistAndRefresh();
    }

    private void GuestsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GuestsGrid.SelectedItem is not Guest guest) return;
        GuestNameBox.Text = guest.FullName;
        GuestPhoneBox.Text = guest.Phone;
        GuestEmailBox.Text = guest.Email;
        GuestDocumentBox.Text = guest.Document;
        var history = _data.Reservations.Where(r => r.GuestId == guest.Id).OrderByDescending(r => r.CheckIn)
            .Select(r => $"{r.CheckIn:dd/MM/yyyy} · Hab. {r.RoomNumber} · {r.StatusLabel}");
        GuestHistoryText.Text = string.Join("\n", history);
        if (GuestHistoryText.Text.Length == 0) GuestHistoryText.Text = "Todavía no tiene reservas.";
    }

    private void NewGuest_Click(object sender, RoutedEventArgs e)
    {
        GuestsGrid.SelectedItem = null;
        GuestNameBox.Clear();
        GuestPhoneBox.Clear();
        GuestEmailBox.Clear();
        GuestDocumentBox.Clear();
        GuestHistoryText.Text = "";
        GuestNameBox.Focus();
    }

    private void SaveGuest_Click(object sender, RoutedEventArgs e)
    {
        var name = GuestNameBox.Text.Trim();
        if (name.Length < 3) { Inform("Escribe el nombre completo del huésped."); return; }
        var email = GuestEmailBox.Text.Trim();
        if (email.Length > 0 && (!email.Contains('@') || !email.Contains('.')))
        { Inform("Revisa el correo electrónico."); return; }
        var guest = GuestsGrid.SelectedItem as Guest;
        if (guest is null)
        {
            guest = new Guest();
            _data.Guests.Add(guest);
        }
        guest.FullName = name;
        guest.Phone = GuestPhoneBox.Text.Trim();
        guest.Email = email;
        guest.Document = GuestDocumentBox.Text.Trim();
        foreach (var reservation in _data.Reservations.Where(r => r.GuestId == guest.Id))
            reservation.GuestName = name;
        PersistAndRefresh();
        GuestsGrid.SelectedItem = guest;
        GuestsGrid.Items.Refresh();
    }

    private void CleaningGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CleaningRoomText.Text = CleaningGrid.SelectedItem is Room room
            ? $"Habitación {room.Number} · {room.StatusLabel}" : "Ninguna seleccionada";
    }
    private void MarkClean_Click(object sender, RoutedEventArgs e) => ChangeCleaningStatus("Available");
    private void MarkDirty_Click(object sender, RoutedEventArgs e) => ChangeCleaningStatus("Dirty");
    private void MarkMaintenance_Click(object sender, RoutedEventArgs e) => ChangeCleaningStatus("Maintenance");
    private void ChangeCleaningStatus(string status)
    {
        if (CleaningGrid.SelectedItem is not Room room) { Inform("Selecciona una habitación."); return; }
        if (room.Status == "Occupied") { Inform("Registra el check-out antes de cambiar el estado de esta habitación."); return; }
        room.Status = status;
        PersistAndRefresh();
        CleaningRoomText.Text = $"Habitación {room.Number} · {room.StatusLabel}";
    }

    private void PersistAndRefresh()
    {
        try { HotelStore.Save(_data); }
        catch (Exception ex) { MessageBox.Show(this, $"No se pudieron guardar los cambios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (!IsLoaded && Pages is null) return;
        RefreshReservationList();
        var today = DateTime.Today;
        Replace(UpcomingReservations, _data.Reservations.Where(r => r.Status == "Confirmed" && r.CheckIn.Date >= today && r.CheckIn.Date <= today.AddDays(7)).OrderBy(r => r.CheckIn).Take(8));
        Replace(UpcomingDepartures, _data.Reservations.Where(r => r.Status == "CheckedIn" && r.CheckOut.Date >= today && r.CheckOut.Date <= today.AddDays(7)).OrderBy(r => r.CheckOut));
        TodayReservationsText.Text = _data.Reservations.Count(r => r.Status == "Confirmed" && r.CheckIn.Date == today).ToString();
        OccupancyText.Text = _data.Rooms.Count == 0 ? "0%" : $"{_data.Rooms.Count(r => r.Status == "Occupied") * 100 / _data.Rooms.Count}%";
        AvailableText.Text = _data.Rooms.Count(r => r.Status == "Available").ToString();
        DirtyText.Text = _data.Rooms.Count(r => r.Status == "Dirty").ToString();
        ReportTotalText.Text = _data.Reservations.Count.ToString();
        ReportConfirmedText.Text = _data.Reservations.Count(r => r.Status == "Confirmed").ToString();
        ReportActiveText.Text = _data.Reservations.Count(r => r.Status == "CheckedIn").ToString();
        ReportCompletedText.Text = _data.Reservations.Count(r => r.Status == "Completed").ToString();
        RoomsGrid.Items.Refresh();
        CleaningGrid.Items.Refresh();
        GuestsGrid.Items.Refresh();
        ReservationsGrid.Items.Refresh();
        ReservationsGrid_SelectionChanged(ReservationsGrid, null!);
    }

    private void RefreshReservationList()
    {
        if (ReservationSearch is null) return;
        var q = ReservationSearch.Text.Trim();
        Replace(FilteredReservations, _data.Reservations
            .Where(r => q.Length == 0 || r.GuestName.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || r.RoomNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.StatusLabel.Contains(q, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(r => r.CheckIn));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }
    private void Inform(string message) => MessageBox.Show(this, message, "Los Krotos", MessageBoxButton.OK, MessageBoxImage.Information);
}
