namespace TaskManagement.Application.DTOs.StarredRecent
{
    public readonly record struct PersonalEntityKey(string EntityType, Guid EntityId);

    public sealed class PersonalEntityReferenceDto
    {
        public string EntityType { get; init; } = string.Empty;
        public Guid EntityId { get; init; }
        public Guid WorkspaceId { get; init; }
        public Guid? ProjectId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string Url { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; }
    }

    public sealed class PersonalCollectionPageDto<T>
    {
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public IReadOnlyList<T> Items { get; init; } = [];
    }

    public sealed class StarredItemDto
    {
        public Guid Id { get; init; }
        public string ItemType { get; init; } = string.Empty;
        public Guid ItemId { get; init; }
        public Guid WorkspaceId { get; init; }
        public Guid? ProjectId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string Url { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public sealed class StarredItemMutationDto
    {
        public string Status { get; init; } = string.Empty;
        public StarredItemDto? Item { get; init; }
    }

    public sealed class StarredItemRequestDto
    {
        public string ItemType { get; set; } = string.Empty;
        public Guid ItemId { get; set; }
    }

    public sealed class RecentViewDto
    {
        public Guid Id { get; init; }
        public string EntityType { get; init; } = string.Empty;
        public Guid EntityId { get; init; }
        public Guid WorkspaceId { get; init; }
        public Guid? ProjectId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string Url { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public DateTime ViewedAt { get; init; }
    }

    public sealed class RecentViewRequestDto
    {
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }

        // Retained for request compatibility. Display metadata is resolved server-side.
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? Url { get; set; }
        public string? Icon { get; set; }
    }
}
