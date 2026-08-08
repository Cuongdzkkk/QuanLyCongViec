using System.Security.Cryptography;
using System.Text;

namespace LocalCollaborationFixture;

internal sealed record FixtureIdentity(
    string Prefix,
    Guid UserAId,
    Guid UserBId,
    Guid UserCId,
    Guid WorkspaceAId,
    Guid ProjectAId,
    Guid ChannelAId,
    Guid ConversationAbId)
{
    public IReadOnlyList<Guid> UserIds => [UserAId, UserBId, UserCId];

    public static FixtureIdentity For(string prefix) => new(
        prefix,
        StableGuid(prefix, "user-a"),
        StableGuid(prefix, "user-b"),
        StableGuid(prefix, "user-c"),
        StableGuid(prefix, "workspace-a"),
        StableGuid(prefix, "project-a"),
        StableGuid(prefix, "channel-a"),
        StableGuid(prefix, "conversation-ab"));

    public string Email(string user) => $"{Prefix}-{user.ToLowerInvariant()}@local.test";

    public static (Guid Low, Guid High) Pair(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);

    private static Guid StableGuid(string prefix, string label)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{prefix}:{label}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
