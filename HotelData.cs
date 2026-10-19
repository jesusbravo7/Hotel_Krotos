using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace LosKrotos.Desktop;

public sealed class Room
{
    public string Number { get; set; } = "";
    public string Type { get; set; } = "Estándar";
    public int Capacity { get; set; } = 2;
    public decimal Rate { get; set; } = 28000;
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Available";
    public string StatusLabel => Status switch
    {
        "Occupied" => "Ocupada",
        "Dirty" => "Por limpiar",
        "Maintenance" => "Mantenimiento",
        _ => "Disponible"
    };
    public override string ToString() => $"{Number} · {Type} · {Capacity} personas";
}

public sealed class Guest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string DocumentType { get; set; } = "Cédula de identidad";
    public string Document { get; set; } = "";
    public override string ToString() => FullName;
}

public sealed class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuestId { get; set; }
    public string GuestName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Adults { get; set; } = 2;
    public int Children { get; set; }
    public decimal NightlyRate { get; set; }
    public string Status { get; set; } = "Confirmed";
    public string StatusLabel => Status switch
    {
        "CheckedIn" => "En casa",
        "Completed" => "Finalizada",
        "Cancelled" => "Cancelada",
        _ => "Confirmada"
    };
}

public sealed class PaymentEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Efectivo";
    public string Note { get; set; } = "";
}

public sealed class HotelData
{
    public ObservableCollection<Room> Rooms { get; set; } = [];
    public ObservableCollection<Guest> Guests { get; set; } = [];
    public ObservableCollection<Reservation> Reservations { get; set; } = [];
    public ObservableCollection<PaymentEntry> Payments { get; set; } = [];
}

public static class HotelStore
{
    public static readonly string DataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LosKrotos", "hotel-data.json");

    public static HotelData Load()
    {
        if (File.Exists(DataPath))
        {
            try
            {
                var data = JsonSerializer.Deserialize<HotelData>(File.ReadAllText(DataPath)) ?? CreateSample();
                data.Payments ??= [];
                foreach (var reservation in data.Reservations.Where(r => r.NightlyRate <= 0))
                    reservation.NightlyRate = data.Rooms.FirstOrDefault(r => r.Number == reservation.RoomNumber)?.Rate ?? 0;
                return data;
            }
            catch (JsonException)
            {
                // Keep the damaged file intact so it can be inspected or recovered.
                return CreateSample();
            }
        }
        return CreateSample();
    }

    public static void Save(HotelData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        var temp = DataPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, DataPath, true);
    }

    private static HotelData CreateSample()
    {
        var data = new HotelData();
        var types = new[] { "Estándar", "Estándar", "Deluxe", "Deluxe", "Suite", "Familiar" };
        for (var i = 0; i < 24; i++)
        {
            var number = $"{i + 101}";
            var type = types[i % types.Length];
            data.Rooms.Add(new Room
            {
                Number = number,
                Type = type,
                Capacity = type == "Familiar" ? 4 : type == "Suite" ? 3 : 2,
                Rate = type switch { "Familiar" => 52000, "Suite" => 65000, "Deluxe" => 45000, _ => 28000 },
                Description = type == "Familiar" ? "Espacio amplio para familias." : "Habitación luminosa con vista al jardín."
            });
        }
        var names = new[] { "María González", "Carlos Ramírez", "Ana Torres", "Luis Mejía" };
        foreach (var name in names)
            data.Guests.Add(new Guest { FullName = name, Phone = "+506 2222 0101", Email = $"{name.Split(' ')[0].ToLowerInvariant()}@ejemplo.com" });

        var today = DateTime.Today;
        data.Reservations.Add(new Reservation { GuestId = data.Guests[0].Id, GuestName = names[0], RoomNumber = "101", CheckIn = today, CheckOut = today.AddDays(3), NightlyRate = 28000 });
        data.Reservations.Add(new Reservation { GuestId = data.Guests[1].Id, GuestName = names[1], RoomNumber = "103", CheckIn = today.AddDays(1), CheckOut = today.AddDays(4), NightlyRate = 45000 });
        data.Reservations.Add(new Reservation { GuestId = data.Guests[2].Id, GuestName = names[2], RoomNumber = "105", CheckIn = today.AddDays(2), CheckOut = today.AddDays(5), NightlyRate = 65000 });
        data.Reservations.Add(new Reservation { GuestId = data.Guests[3].Id, GuestName = names[3], RoomNumber = "106", CheckIn = today.AddDays(-1), CheckOut = today.AddDays(2), NightlyRate = 52000, Status = "CheckedIn" });
        data.Rooms.First(r => r.Number == "106").Status = "Occupied";
        data.Rooms.First(r => r.Number == "112").Status = "Dirty";
        data.Rooms.First(r => r.Number == "121").Status = "Maintenance";
        return data;
    }
}
