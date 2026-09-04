using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManagement.Domain.Entities
{
    public class LevelConfig
    {
        [Key]
        public Guid Id { get; set; }
        
        public Guid ProjectId { get; set; } // Workspace root

        [Required]
        public int Level { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        public int RequiredXpPerLevel { get; set; }

        public Guid? RewardId { get; set; }

        [ForeignKey("ProjectId")]
        public Project? Project { get; set; }

        [ForeignKey("RewardId")]
        public RewardDefinition? Reward { get; set; }
    }
}
