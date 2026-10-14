# Los Krotos Hotel — prototipo WPF

Prototipo de escritorio para Windows, desarrollado en C# con WPF y .NET 8. La interfaz usa una paleta verde inspirada en las referencias compartidas.

## Ejecutar

```powershell
dotnet run
```

La primera ejecución muestra 24 habitaciones, cuatro huéspedes y reservas de ejemplo con fechas relativas al día actual. Los cambios se guardan en `%LOCALAPPDATA%\LosKrotos\hotel-data.json`.

## Funciones incluidas

- Panel con indicadores, gráfica de estancias próximas, estado de habitaciones y llegadas.
- Reservas con búsqueda, calendario semanal de habitaciones, validación de capacidad y fechas, control de solapamientos y cancelación.
- Mostrador de check-in y check-out con listas de llegadas y huéspedes en casa.
- Inventario de habitaciones en tarjetas con colores por estado y filtros de estado, piso y tipo; crear, editar y eliminar habitaciones sin historial.
- Directorio de huéspedes: crear, editar e inspeccionar historial de reservas.
- Limpieza y mantenimiento: cambiar estados sin alterar habitaciones ocupadas.
- Reportes operativos y próximas salidas. No hay pagos.

El prototipo es local y de un solo equipo. WPF no se ejecuta dentro de un navegador. Para ofrecer acceso web más adelante, se puede compartir la lógica y los datos mediante una API ASP.NET Core y crear una interfaz web separada.
