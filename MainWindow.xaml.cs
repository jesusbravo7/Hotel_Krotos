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
    public ObservableCollection<Reservation> DeskArrivalReservations { get; } = [];
    public ObservableCollection<Reservation> InHouseReservations { get; } = [];
    public ObservableCollection<PaymentEntry> SelectedPayments { get; } = [];
    public ObservableCollection<RoomTile> FilteredRoomTiles { get; } = [];
    public ObservableCollection<string> ScheduleDays { get; } = [];
    public ObservableCollection<ScheduleRow> ScheduleRows { get; } = [];
    private string _roomStatusFilter = "All";
    private DateTime _scheduleMonday = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7);

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

    private void Pages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Pages) || NavMenu is null) return;
        foreach (var button in NavMenu.Children.OfType<Button>())
        {
            var selected = button.Tag?.ToString() == Pages.SelectedIndex.ToString();
            button.Background = selected ? new SolidColorBrush(Color.FromRgb(32, 77, 119)) : Brushes.Transparent;
            button.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(184, 205, 225));
        }
    }

    private void OpenRooms_Click(object sender, RoutedEventArgs e) => Pages.SelectedIndex = 2;
    private void OpenCleaning_Click(object sender, RoutedEventArgs e) => Pages.SelectedIndex = 4;
    private void PrintReport_Click(object sender, RoutedEventArgs e) => ReportPrinter.Print(this, _data);

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
        Replace(SelectedPayments, reservation is null ? [] :
            _data.Payments.Where(p => p.ReservationId == reservation.Id).OrderByDescending(p => p.RecordedAt));
        if (reservation is null)
        {
            PaymentSummaryText.Text = "Selecciona una reserva para ver sus abonos.";
            return;
        }
        var total = reservation.NightlyRate * (reservation.CheckOut.Date - reservation.CheckIn.Date).Days;
        var paid = _data.Payments.Where(p => p.ReservationId == reservation.Id).Sum(p => p.Amount);
        PaymentSummaryText.Text = $"Estancia ₡ {total:N2}   ·   Abonado ₡ {paid:N2}\nPendiente ₡ {Math.Max(0, total - paid):N2}";
    }

    private void AddPayment_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationsGrid.SelectedItem is not Reservation reservation) { Inform("Selecciona una reserva."); return; }
        if (!decimal.TryParse(PaymentAmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) &&
            !decimal.TryParse(PaymentAmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        { Inform("Escribe un monto válido."); return; }
        if (amount <= 0) { Inform("El monto debe ser mayor que cero."); return; }
        amount = decimal.Round(amount, 2);
        var total = reservation.NightlyRate * (reservation.CheckOut.Date - reservation.CheckIn.Date).Days;
        var paid = _data.Payments.Where(p => p.ReservationId == reservation.Id).Sum(p => p.Amount);
        if (amount > total - paid) { Inform("El abono supera el saldo pendiente de la reserva."); return; }
        if (PaymentMethodBox.SelectedItem is not ComboBoxItem method) { Inform("Selecciona un método de pago."); return; }
        _data.Payments.Add(new PaymentEntry
        {
            ReservationId = reservation.Id,
            Amount = amount,
            Method = method.Content.ToString()!,
            Note = PaymentNoteBox.Text.Trim()
        });
        PaymentAmountBox.Clear();
        PaymentNoteBox.Clear();
        PersistAndRefresh();
        ReservationsGrid.SelectedItem = reservation;
    }

    private void CheckIn_Click(object sender, RoutedEventArgs e) => PerformCheckIn(ReservationsGrid.SelectedItem as Reservation);
    private void FrontDeskCheckIn_Click(object sender, RoutedEventArgs e) => PerformCheckIn(ArrivalDeskGrid.SelectedItem as Reservation);
    private void PerformCheckIn(Reservation? reservation)
    {
        if (reservation is null) { Inform("Selecciona una reserva."); return; }
        if (reservation.Status != "Confirmed") { Inform("Solo se puede registrar la entrada de una reserva confirmada."); return; }
        if (reservation.CheckIn.Date > DateTime.Today) { Inform("La fecha de entrada aún no ha llegado."); return; }
        var room = _data.Rooms.First(r => r.Number == reservation.RoomNumber);
        if (room.Status != "Available") { Inform("La habitación debe estar disponible y limpia antes del check-in."); return; }
        if (reservation.CheckOut.Date <= DateTime.Today) { Inform("La fecha de salida de esta reserva ya pasó."); return; }
        reservation.Status = "CheckedIn";
        room.Status = "Occupied";
        PersistAndRefresh();
    }

    private void CheckOut_Click(object sender, RoutedEventArgs e) => PerformCheckOut(ReservationsGrid.SelectedItem as Reservation);
    private void FrontDeskCheckOut_Click(object sender, RoutedEventArgs e) => PerformCheckOut(InHouseGrid.SelectedItem as Reservation);
    private void PerformCheckOut(Reservation? reservation)
    {
        if (reservation is null) { Inform("Selecciona una reserva."); return; }
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
        RoomEditorPanel.Visibility = Visibility.Visible;
        RoomNumberBox.IsReadOnly = true;
        RoomNumberBox.Text = room.Number;
        RoomTypeBox.SelectedItem = RoomTypeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Content, room.Type));
        RoomCapacityBox.Text = room.Capacity.ToString();
        RoomRateBox.Text = room.Rate.ToString(CultureInfo.CurrentCulture);
        RoomDescriptionBox.Text = room.Description;
        RoomHeroImage.Source = RoomArtwork.ForType(room.Type);
    }

    private void NewRoom_Click(object sender, RoutedEventArgs e)
    {
        RoomsGrid.SelectedItem = null;
        RoomEditorPanel.Visibility = Visibility.Visible;
        RoomNumberBox.IsReadOnly = false;
        RoomNumberBox.Clear();
        RoomTypeBox.SelectedIndex = 0;
        RoomCapacityBox.Text = "2";
        RoomRateBox.Text = "28000";
        RoomDescriptionBox.Clear();
        RoomHeroImage.Source = RoomArtwork.ForType("Estándar");
        RoomNumberBox.Focus();
    }

    private void CloseRoomEditor_Click(object sender, RoutedEventArgs e)
    {
        RoomEditorPanel.Visibility = Visibility.Collapsed;
        RoomsGrid.SelectedItem = null;
    }

    private void RoomTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Room room })
        {
            var dialog = new RoomDetailDialog(CreateRoomTile(room), HasActiveStay(room), HasFutureReservation(room)) { Owner = this };
            dialog.ShowDialog();
            if (dialog.StatusChanged) PersistAndRefresh();
            if (dialog.EditRequested) RoomsGrid.SelectedItem = room;
        }
    }

    private void SaveRoom_Click(object sender, RoutedEventArgs e)
    {
        var room = RoomsGrid.SelectedItem as Room;
        var number = RoomNumberBox.Text.Trim();
        if (number.Length is < 2 or > 8 || !number.All(char.IsLetterOrDigit))
        { Inform("El número debe tener entre 2 y 8 letras o dígitos."); return; }
        if (room is null && _data.Rooms.Any(r => r.Number.Equals(number, StringComparison.OrdinalIgnoreCase)))
        { Inform("Ya existe una habitación con ese número."); return; }
        var validRate = decimal.TryParse(RoomRateBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var rate) ||
            decimal.TryParse(RoomRateBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out rate);
        if (!int.TryParse(RoomCapacityBox.Text, out var capacity) || capacity < 1 || capacity > 12 ||
            !validRate || rate <= 0 ||
            RoomTypeBox.SelectedItem is not ComboBoxItem typeItem)
        { Inform("Revisa el tipo, la capacidad (1–12) y la tarifa positiva en colones."); return; }
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
        RoomEditorPanel.Visibility = Visibility.Collapsed;
        PersistAndRefresh();
    }

    private void GuestsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GuestsGrid.SelectedItem is not Guest guest) return;
        GuestNameBox.Text = guest.FullName;
        GuestPhoneBox.Text = guest.Phone;
        GuestEmailBox.Text = guest.Email;
        GuestDocumentTypeBox.SelectedItem = GuestDocumentTypeBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => Equals(i.Content, guest.DocumentType)) ?? GuestDocumentTypeBox.Items[0];
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
        GuestDocumentTypeBox.SelectedIndex = 0;
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
        if (GuestDocumentTypeBox.SelectedItem is not ComboBoxItem documentType)
        { Inform("Selecciona el tipo de documento."); return; }
        var document = GuestDocumentBox.Text.Trim();
        if (document.Length > 30)
        { Inform("El número de documento no puede superar 30 caracteres."); return; }
        var guest = GuestsGrid.SelectedItem as Guest;
        if (guest is null)
        {
            guest = new Guest();
            _data.Guests.Add(guest);
        }
        guest.FullName = name;
        guest.Phone = GuestPhoneBox.Text.Trim();
        guest.Email = email;
        guest.DocumentType = documentType.Content.ToString()!;
        guest.Document = document;
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
        if (room.Status == "Occupied" || HasActiveStay(room))
        { Inform("Registra el check-out antes de cambiar el estado de esta habitación."); return; }
        if (status == "Maintenance" && HasFutureReservation(room))
        { Inform("Esta habitación tiene una reserva confirmada. Reasígnala o cancélala antes de enviarla a mantenimiento."); return; }
        room.Status = status;
        PersistAndRefresh();
        CleaningRoomText.Text = $"Habitación {room.Number} · {room.StatusLabel}";
    }

    private bool HasActiveStay(Room room) => _data.Reservations.Any(r =>
        r.RoomNumber == room.Number && r.Status == "CheckedIn");

    private bool HasFutureReservation(Room room) => _data.Reservations.Any(r =>
        r.RoomNumber == room.Number && r.Status == "Confirmed" && r.CheckOut.Date > DateTime.Today);

    private void RoomFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            _roomStatusFilter = button.Tag?.ToString() ?? "All";
            foreach (var filter in RoomFilters.Children.OfType<Button>())
                filter.Style = (Style)FindResource(Equals(filter.Tag, button.Tag) ? "Primary" : "Secondary");
            RefreshRoomTiles();
        }
    }

    private void RefreshRoomTiles()
    {
        var tiles = _data.Rooms.OrderBy(r => r.Number).Select(CreateRoomTile).ToList();
        RoomTotalText.Text = tiles.Count.ToString();
        RoomAvailableText.Text = tiles.Count(t => t.Status == "Available").ToString();
        RoomOccupiedText.Text = tiles.Count(t => t.Status == "Occupied").ToString();
        RoomReservedText.Text = tiles.Count(t => t.Status == "Reserved").ToString();
        RoomMaintenanceText.Text = tiles.Count(t => t.Status == "Maintenance").ToString();
        RoomDirtyText.Text = tiles.Count(t => t.Status == "Dirty").ToString();
        Replace(FilteredRoomTiles, tiles.Where(t => _roomStatusFilter == "All" || t.Status == _roomStatusFilter));
    }

    private RoomTile CreateRoomTile(Room room)
    {
        var next = _data.Reservations.Where(r => r.RoomNumber == room.Number && r.Status == "Confirmed" && r.CheckOut.Date > DateTime.Today)
            .OrderBy(r => r.CheckIn).FirstOrDefault();
        var active = _data.Reservations.FirstOrDefault(r => r.RoomNumber == room.Number && r.Status == "CheckedIn");
        var status = room.Status == "Available" && next is not null ? "Reserved" : room.Status;
        var (label, accent, background) = status switch
        {
            "Occupied" => ("OCUPADA", "#80B7FF", "#203E65"),
            "Reserved" => ("RESERVADA", "#E6C86C", "#403A29"),
            "Dirty" => ("POR LIMPIAR", "#E6C86C", "#403A29"),
            "Maintenance" => ("MANTENIMIENTO", "#FF91A5", "#462B40"),
            _ => ("DISPONIBLE", "#5DDEBB", "#194744")
        };
        var detail = status == "Occupied" && active is not null ? $"Salida {active.CheckOut:dd/MM}" :
            status == "Reserved" && next is not null ? $"Entrada {next.CheckIn:dd/MM}" :
            $"{room.Capacity} persona(s)";
        return new RoomTile
        {
            Room = room, Status = status, StatusLabel = label, Accent = accent, Background = background,
            Detail = detail,
            Image = RoomArtwork.ForType(room.Type)
        };
    }

    private void PreviousWeek_Click(object sender, RoutedEventArgs e) { _scheduleMonday = _scheduleMonday.AddDays(-7); RefreshSchedule(); }
    private void NextWeek_Click(object sender, RoutedEventArgs e) { _scheduleMonday = _scheduleMonday.AddDays(7); RefreshSchedule(); }
    private void CurrentWeek_Click(object sender, RoutedEventArgs e)
    {
        _scheduleMonday = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7);
        RefreshSchedule();
    }

    private void RefreshSchedule()
    {
        ScheduleRangeText.Text = $"{_scheduleMonday:dd MMM} — {_scheduleMonday.AddDays(6):dd MMM yyyy}";
        Replace(ScheduleDays, Enumerable.Range(0, 7).Select(i =>
            _scheduleMonday.AddDays(i).ToString("ddd dd", CultureInfo.GetCultureInfo("es-CR"))));
        Replace(ScheduleRows, _data.Rooms.OrderBy(r => r.Number).Select(room =>
        {
            var row = new ScheduleRow { RoomNumber = room.Number };
            foreach (var i in Enumerable.Range(0, 7))
            {
                var date = _scheduleMonday.AddDays(i);
                var reservation = _data.Reservations.FirstOrDefault(r =>
                    r.RoomNumber == room.Number && (r.Status == "Confirmed" || r.Status == "CheckedIn") &&
                    r.CheckIn.Date <= date && r.CheckOut.Date > date);
                row.Cells.Add(reservation is null
                    ? new ScheduleCell()
                    : new ScheduleCell
                    {
                        Label = reservation.CheckIn.Date == date ? reservation.GuestName.Split(' ')[0] : "●",
                        Background = reservation.Status == "CheckedIn" ? "#244F7C" : "#1C596F",
                        Foreground = reservation.Status == "CheckedIn" ? "#DDEEFF" : "#D5F4FC"
                    });
            }
            return row;
        }));
    }

    private void RefreshDashboardChart()
    {
        var counts = Enumerable.Range(0, 14).Select(i =>
        {
            var day = DateTime.Today.AddDays(i);
            return _data.Reservations.Count(r => (r.Status == "Confirmed" || r.Status == "CheckedIn") &&
                r.CheckIn.Date <= day && r.CheckOut.Date > day);
        }).ToArray();
        var max = Math.Max(4, counts.Max());
        var line = new PointCollection();
        var area = new PointCollection { new Point(10, 170) };
        for (var i = 0; i < counts.Length; i++)
        {
            var point = new Point(10 + i * 44.5, 170 - counts[i] * 140.0 / max);
            line.Add(point);
            area.Add(point);
        }
        area.Add(new Point(588.5, 170));
        ReservationsLine.Points = line;
        ReservationsArea.Points = area;
        DonutTotalText.Text = _data.Rooms.Count.ToString();
        var share = _data.Rooms.Count == 0 ? 0 : (double)_data.Rooms.Count(r => r.Status == "Occupied") / _data.Rooms.Count;
        if (share <= 0) { OccupiedArc.Data = Geometry.Empty; return; }
        var angle = Math.Min(share, 0.9999) * Math.PI * 2;
        var start = new Point(66, 8);
        var end = new Point(66 + 58 * Math.Sin(angle), 66 - 58 * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(58, 58), 0, angle > Math.PI, SweepDirection.Clockwise, true));
        OccupiedArc.Data = new PathGeometry([figure]);
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
        Replace(DeskArrivalReservations, _data.Reservations.Where(r => r.Status == "Confirmed" && r.CheckOut.Date > today && r.CheckIn.Date <= today.AddDays(7)).OrderBy(r => r.CheckIn));
        Replace(InHouseReservations, _data.Reservations.Where(r => r.Status == "CheckedIn").OrderBy(r => r.CheckOut));
        TodayReservationsText.Text = _data.Reservations.Count(r => r.Status == "Confirmed" && r.CheckIn.Date == today).ToString();
        OccupancyText.Text = _data.Rooms.Count == 0 ? "0%" : $"{_data.Rooms.Count(r => r.Status == "Occupied") * 100 / _data.Rooms.Count}%";
        AvailableText.Text = _data.Rooms.Count(r => r.Status == "Available").ToString();
        DirtyText.Text = _data.Rooms.Count(r => r.Status == "Dirty").ToString();
        ReportTotalText.Text = _data.Reservations.Count.ToString();
        ReportConfirmedText.Text = _data.Reservations.Count(r => r.Status == "Confirmed").ToString();
        ReportActiveText.Text = _data.Reservations.Count(r => r.Status == "CheckedIn").ToString();
        ReportCompletedText.Text = _data.Reservations.Count(r => r.Status == "Completed").ToString();
        ReportPaidText.Text = $"₡ {_data.Payments.Sum(p => p.Amount):N2}";
        ReportPendingText.Text = $"₡ {_data.Reservations.Where(r => r.Status is "Confirmed" or "CheckedIn").Sum(r => Math.Max(0, r.NightlyRate * (r.CheckOut.Date - r.CheckIn.Date).Days - _data.Payments.Where(p => p.ReservationId == r.Id).Sum(p => p.Amount))):N2}";
        DeskArrivalsText.Text = DeskArrivalReservations.Count.ToString();
        DeskInHouseText.Text = InHouseReservations.Count.ToString();
        DeskDeparturesText.Text = InHouseReservations.Count(r => r.CheckOut.Date == today).ToString();
        StatusSummaryText.Text = $"{_data.Rooms.Count(r => r.Status == "Occupied")} ocupadas  ·  {_data.Rooms.Count(r => r.Status == "Available")} disponibles  ·  {_data.Rooms.Count(r => r.Status == "Dirty" || r.Status == "Maintenance")} pendientes";
        RefreshDashboardChart();
        RefreshRoomTiles();
        RefreshSchedule();
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
