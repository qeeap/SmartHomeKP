namespace SmartHome.Domain.Enums
{
    public enum UserRole { Guest, System, Owner }

    public enum DeviceStatus { Inactive, Active, Error, Decommissioned }

    public enum ScenarioStatus { Draft, Active, Inactivate }

    public enum EventType { Info, Warning, Error, Alert }

    public enum SensorType { Temperature, Humidity, Motion, Smoke, Light }

    public enum PermissionStatus { Active, Suspended, Expired, Revoked }
}