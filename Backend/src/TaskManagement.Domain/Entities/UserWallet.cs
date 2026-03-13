using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    public class UserWallet
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public int TotalPoints { get; set; }
        public int Level { get; set; }

        public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    }
}
