namespace SmartHome.Domain.Entities
{
    public class Trigger
    {
        public int Id { get; set; }
        public int ScenarioId { get; set; }
        public int SensorId { get; set; }
        public string Operator { get; set; } = string.Empty; // >, <, ==, !=
        public double Threshold { get; set; }
        public string Type { get; set; } = string.Empty; // Sensor, Schedule
        public string? TimeCondition { get; set; }

        public bool CheckCondition(double currentValue)
        {
            return Operator switch
            {
                ">" => currentValue > Threshold,
                "<" => currentValue < Threshold,
                ">=" => currentValue >= Threshold,
                "<=" => currentValue <= Threshold,
                "==" => currentValue == Threshold,
                "!=" => currentValue != Threshold,
                _ => false
            };
        }
    }
}