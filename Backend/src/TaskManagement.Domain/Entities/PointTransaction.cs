using System;

namespace TaskManagement.Domain.Entities
{
    public class PointTransaction
    {
        public Guid Id { get; set; }
        public Guid UserWalletUserId { get; set; }
        public UserWallet UserWallet { get; set; } = null!;
        public Guid? WorkTaskId { get; set; }
        public WorkTask? WorkTask { get; set; }
        public int Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "Reward";
        public Guid? RewardEventId { get; set; }
        public string? IdempotencyKey { get; set; }
        public string? RewardRuleVersion { get; set; }
        public Guid? ReversalOfTransactionId { get; set; }
        public PointTransaction? ReversalOfTransaction { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
