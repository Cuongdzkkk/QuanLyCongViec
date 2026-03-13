using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    public class Comment
    {
        public Guid Id { get; set; }
        public Guid WorkTaskId { get; set; }
        public WorkTask WorkTask { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string Content { get; set; } = string.Empty;
        public Guid? ParentCommentId { get; set; }
        public Comment? ParentComment { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Comment> ChildComments { get; set; } = new List<Comment>();
    }
}
