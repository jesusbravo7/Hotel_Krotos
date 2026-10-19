using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LosKrotos.Desktop;

public static class ReportPrinter
{
    private static readonly CultureInfo CostaRica = CultureInfo.GetCultureInfo("es-CR");

    public static void Print(Window owner, HotelData data)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var document = CreateDocument(data);
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PagePadding = new Thickness(32);
        document.ColumnWidth = Math.Max(300, dialog.PrintableAreaWidth - 64);
        try
        {
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Reporte de Los Krotos");
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"No se pudo imprimir el reporte: {ex.Message}", "Error de impresión",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static FlowDocument CreateDocument(HotelData data)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"), FontSize = 10,
            Foreground = Brushes.Black, PagePadding = new Thickness(32)
        };
        document.Blocks.Add(new Paragraph(new Run("LOS KROTOS · REPORTE OPERATIVO"))
        {
            FontSize = 19, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5)
        });
        document.Blocks.Add(new Paragraph(new Run($"Emitido: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", CostaRica)} · Costa Rica · Importes en colones (₡)"))
        {
            Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 18)
        });

        var active = data.Reservations.Where(r => r.Status is "Confirmed" or "CheckedIn").ToList();
        var paidTotal = data.Payments.Sum(p => p.Amount);
        var pendingTotal = active.Sum(r => Math.Max(0, Total(r) - Paid(data, r)));
        AddHeading(document, "Resumen");
        AddLine(document, $"Habitaciones: {data.Rooms.Count}  ·  Ocupadas: {data.Rooms.Count(r => r.Status == "Occupied")}  ·  Disponibles: {data.Rooms.Count(r => r.Status == "Available")}");
        AddLine(document, $"Reservas: {data.Reservations.Count}  ·  Confirmadas: {data.Reservations.Count(r => r.Status == "Confirmed")}  ·  En casa: {data.Reservations.Count(r => r.Status == "CheckedIn")}  ·  Finalizadas: {data.Reservations.Count(r => r.Status == "Completed")}");
        AddLine(document, $"Abonos registrados: {Money(paidTotal)}  ·  Saldo pendiente activo: {Money(pendingTotal)}");

        AddHeading(document, "Reservas");
        var reservations = data.Reservations.OrderByDescending(r => r.CheckIn).ToList();
        if (reservations.Count == 0) AddLine(document, "Sin reservas registradas.");
        else
        {
            var table = NewTable(6);
            AddRow(table, true, "Huésped", "Hab.", "Entrada", "Salida", "Estado", "Total / Abonado");
            foreach (var reservation in reservations)
                AddRow(table, false, reservation.GuestName, reservation.RoomNumber,
                    reservation.CheckIn.ToString("dd/MM/yyyy", CostaRica), reservation.CheckOut.ToString("dd/MM/yyyy", CostaRica),
                    reservation.StatusLabel, $"{Money(Total(reservation))} / {Money(Paid(data, reservation))}");
            document.Blocks.Add(table);
        }

        AddHeading(document, "Abonos");
        var payments = data.Payments.OrderByDescending(p => p.RecordedAt).ToList();
        if (payments.Count == 0) AddLine(document, "Sin abonos registrados.");
        else
        {
            var table = NewTable(5);
            AddRow(table, true, "Fecha", "Huésped", "Hab.", "Método", "Monto");
            foreach (var payment in payments)
            {
                var reservation = data.Reservations.FirstOrDefault(r => r.Id == payment.ReservationId);
                AddRow(table, false, payment.RecordedAt.ToString("dd/MM/yyyy", CostaRica),
                    reservation?.GuestName ?? "Reserva eliminada", reservation?.RoomNumber ?? "—", payment.Method, Money(payment.Amount));
            }
            document.Blocks.Add(table);
        }
        return document;
    }

    private static decimal Total(Reservation reservation) => reservation.NightlyRate * Math.Max(0, (reservation.CheckOut.Date - reservation.CheckIn.Date).Days);
    private static decimal Paid(HotelData data, Reservation reservation) => data.Payments.Where(p => p.ReservationId == reservation.Id).Sum(p => p.Amount);
    private static string Money(decimal amount) => $"₡ {amount.ToString("N2", CostaRica)}";

    private static void AddHeading(FlowDocument document, string title) => document.Blocks.Add(new Paragraph(new Run(title))
    {
        FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 17, 0, 6)
    });

    private static void AddLine(FlowDocument document, string value) => document.Blocks.Add(new Paragraph(new Run(value))
    {
        Margin = new Thickness(0, 0, 0, 4)
    });

    private static Table NewTable(int columns)
    {
        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 1, 0, 0) };
        for (var i = 0; i < columns; i++) table.Columns.Add(new TableColumn());
        table.RowGroups.Add(new TableRowGroup());
        return table;
    }

    private static void AddRow(Table table, bool header, params string[] values)
    {
        var row = new TableRow();
        foreach (var value in values)
            row.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(0) })
            {
                Padding = new Thickness(3, 5, 3, 5),
                BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1),
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                Background = header ? Brushes.Gainsboro : Brushes.White
            });
        table.RowGroups[0].Rows.Add(row);
    }
}
