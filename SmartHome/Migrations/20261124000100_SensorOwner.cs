using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartHome.Migrations;

public partial class SensorOwner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "OwnerId", table: "Sensors", nullable: true);
        migrationBuilder.Sql("""
            UPDATE s SET OwnerId = r.OwnerId
            FROM Sensors s JOIN Rooms r ON r.Id = s.RoomId;

            UPDATE s SET OwnerId = origins.OwnerId
            FROM Sensors s JOIN (
                SELECT e.SensorId, MIN(e.UserId) AS OwnerId FROM EventLogs e
                JOIN Users u ON u.Id = e.UserId
                WHERE e.Source = 'SensorController' AND e.Description LIKE N'Добавлен датчик:%'
                  AND u.Role = 2
                GROUP BY e.SensorId HAVING COUNT(DISTINCT e.UserId) = 1
            ) origins ON origins.SensorId = s.Id
            WHERE s.OwnerId IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OwnerId", table: "Sensors");
    }
}
