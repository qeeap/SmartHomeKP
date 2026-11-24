using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SmartHome.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using Action = SmartHome.Domain.Entities.Action; // Разрешение конфликта имен

namespace SmartHome.Domain.Entities
{
    public class Scenario
    {
        public int Id { get; set; }
        public int AuthorId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ScenarioStatus Status { get; set; } = ScenarioStatus.Draft;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Trigger> Triggers { get; set; } = new List<Trigger>();
        public ICollection<Action> Actions { get; set; } = new List<Action>();

        public void Activate() => Status = ScenarioStatus.Active;
        public void Deactivate() => Status = ScenarioStatus.Inactivate;

        public bool Validate()
        {
            return Triggers.Any() && Actions.Any() && !string.IsNullOrWhiteSpace(Name);
        }
    }
}