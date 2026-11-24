namespace SmartHome.Domain.Entities
{
    public class Action
    {
        public int Id { get; set; }
        public int ScenarioId { get; set; }
        public int DeviceId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Parameters { get; set; }
        public int Order { get; set; }
    }
}