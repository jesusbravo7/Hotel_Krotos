# Los Krotos Hotel — prototipo WPF

Prototipo de escritorio para Windows, desarrollado en C# con WPF y .NET 8. La interfaz usa una paleta oscura de azules.

## Ejecutar

```powershell
dotnet run -c Release
```

La primera ejecución muestra 24 habitaciones, cuatro huéspedes y reservas de ejemplo con fechas relativas al día actual. Los cambios se guardan en `%LOCALAPPDATA%\LosKrotos\hotel-data.json`.

## Funciones incluidas

- Panel con indicadores, gráfica de estancias próximas, estado de habitaciones y llegadas.
- Reservas con búsqueda, calendario semanal en sección propia, validación de capacidad y fechas desde el día actual, control de solapamientos y cancelación.
- Registro de abonos por reserva con monto, fecha, método y nota; consulta de total abonado y saldo pendiente. Es un registro local, no procesa transacciones.
- Mostrador de check-in y check-out con listas de llegadas y huéspedes en casa.
- Inventario de habitaciones en cuadros de color por estado y filtros de estado; fotografías ilustrativas en el detalle y editor, además de crear, editar y eliminar habitaciones sin historial.
- Directorio de huéspedes: crear, editar, seleccionar tipo de documento e inspeccionar historial de reservas.
- Limpieza y mantenimiento: proteger habitaciones ocupadas y evitar mantenimiento cuando existe una reserva confirmada.
- Reportes operativos, abonos registrados, saldos pendientes y próximas salidas.
- Impresión del reporte operativo, las reservas y los abonos mediante el cuadro de impresión de Windows; se puede elegir una impresora o «Microsoft Print to PDF» si está instalada.

Las fotografías de demostración están en `Assets/` (estándar, deluxe y suite). Las habitaciones familiares usan la foto de suite hasta que se agregue una imagen específica.

Los datos de muestra nuevos usan una sola planta. Si ya existe un archivo de datos local, se conservan sus números de habitación y reservas.

Los importes se muestran en colones costarricenses (₡) y las nuevas muestras usan tarifas ilustrativas de Costa Rica. El archivo local existente conserva sus cifras originales; la aplicación no convierte automáticamente importes antiguos.

El prototipo es local y de un solo equipo. WPF no se ejecuta dentro de un navegador. Para ofrecer acceso web más adelante, se puede compartir la lógica y los datos mediante una API ASP.NET Core y crear una interfaz web separada.
