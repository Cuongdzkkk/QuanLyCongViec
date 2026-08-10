namespace TaskManagement.Application.Common;

public sealed class AiCreditsExhaustedException : Exception
{
    public int IncludedCredits { get; }
    public int UsedCredits { get; }
    public int RemainingCredits { get; }

    public AiCreditsExhaustedException(
        int includedCredits,
        int usedCredits,
        int remainingCredits)
        : base("Bạn đã sử dụng hết AI Credits trong tháng này.")
    {
        IncludedCredits = includedCredits;
        UsedCredits = usedCredits;
        RemainingCredits = remainingCredits;
    }
}
