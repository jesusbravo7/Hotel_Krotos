using System.Collections.ObjectModel;
using System.Windows;

namespace LosKrotos.Desktop;

public partial class ReservationDialog : Window
{
    private readonly IEnumerable<Reservation> _reservations;
    public Reservation? CreatedReservation { get; private set; }
    public Guest? CreatedGuest { get; private set; }

    public ReservationDialog(ObservableCollection<Room> rooms, ObservableCollection<Guest> guests, IEnumerable<Reservation> reservations)
    {
        InitializeComponent();
        _reservations = reservations;
        GuestPicker.ItemsSource = guests.OrderBy(g => g.FullName).ToList();
        RoomPicker.ItemsSource = rooms.OrderBy(r => r.Number).ToList();
        StartPicker.SelectedDate = DateTime.Today;
        EndPicker.SelectedDate = DateTime.Today.AddDays(1);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var newName = NewGuestName.Text.Trim();
        var guest = GuestPicker.SelectedItem as Guest;
        if (newName.Length > 0 && newName.Length < 3) { Inform("Escribe el nombre completo del nuevo huésped."); return; }
        if (newName.Length == 0 && guest is null) { Inform("Selecciona un huésped o escribe un nombre nuevo."); return; }
        if (RoomPicker.SelectedItem is not Room room) { Inform("Selecciona una habitación."); return; }
        if (StartPicker.SelectedDate is not DateTime start || EndPicker.SelectedDate is not DateTime end || start.Date < DateTime.Today || end.Date <= start.Date)
        { Inform("La entrada debe ser hoy o después y la salida posterior a la entrada."); return; }
        if (!int.TryParse(AdultsBox.Text, out var adults) || !int.TryParse(ChildrenBox.Text, out var children) ||
            adults < 1 || children < 0 || adults + children > room.Capacity)
        { Inform($"La ocupación debe ser válida y no superar {room.Capacity} persona(s)."); return; }
        if (room.Status == "Maintenance") { Inform("La habitación está en mantenimiento."); return; }
        if (room.Status == "Dirty" && start.Date == DateTime.Today) { Inform("La habitación debe estar limpia para una llegada hoy."); return; }
        if (_reservations.Any(r => r.RoomNumber == room.Number && (r.Status == "Confirmed" || r.Status == "CheckedIn") &&
            start.Date < r.CheckOut.Date && end.Date > r.CheckIn.Date))
        { Inform("Esta habitación ya tiene una reserva durante esas fechas."); return; }
        if (room.Status == "Occupied" && start.Date == DateTime.Today)
        { Inform("La habitación está ocupada actualmente."); return; }

        if (newName.Length > 0)
        {
            CreatedGuest = new Guest { FullName = newName };
            guest = CreatedGuest;
        }
        CreatedReservation = new Reservation
        {
            GuestId = guest!.Id, GuestName = guest.FullName, RoomNumber = room.Number,
            CheckIn = start.Date, CheckOut = end.Date, Adults = adults, Children = children
        };
        DialogResult = true;
    }

    private void Inform(string text) => MessageBox.Show(this, text, "Revisar reserva", MessageBoxButton.OK, MessageBoxImage.Information);
}
