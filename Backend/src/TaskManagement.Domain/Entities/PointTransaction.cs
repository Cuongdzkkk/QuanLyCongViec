using System;

namespace TaskManagement.Domain.Entities
{
    public class PointTransaction
    {
        public Guid Id { get; set; }
        public Guid WalletId { get; set; }
        public UserWallet Wallet { get; set; } = null!;
        public int Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
