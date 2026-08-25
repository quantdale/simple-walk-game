using System;
using WalkGame.Domain.Common;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Domain.Tests;

public class IdsAndResultsTests
{
    [Fact]
    public void Id_ConstructorRejectsNullEmptyAndWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new ProjectId(null!));
        Assert.Throws<ArgumentException>(() => new ProjectId(""));
        Assert.Throws<ArgumentException>(() => new ProjectId("   "));
    }

    [Fact]
    public void Id_EqualityIsValueBasedAndOrdinal()
    {
        var a = new ProjectId("Gate");
        var b = new ProjectId("Gate");
        var lowerCase = new ProjectId("gate");

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a == lowerCase);
        Assert.True(a != lowerCase);
        Assert.NotEqual(a, lowerCase);
        Assert.False(a.Equals((object?)null));
    }

    [Fact]
    public void Id_CompareToUsesOrdinalCaseSensitiveOrdering()
    {
        Assert.True(new ProjectId("alpha").CompareTo(new ProjectId("Alpha")) > 0);
        Assert.True(new ProjectId("a").CompareTo(new ProjectId("b")) < 0);
        Assert.Equal(0, new ProjectId("same").CompareTo(new ProjectId("same")));
    }

    [Fact]
    public void Id_IsValidReflectsPresenceOfValue()
    {
        Assert.True(new ProjectId("valid").IsValid);
        Assert.False(default(ProjectId).IsValid);
    }

    [Fact]
    public void Id_FromGuidIsStableCanonicalNFormat()
    {
        var guid = new Guid("00000000-0000-0000-0000-000000000042");

        var id = RewardTransactionId.FromGuid(guid);

        Assert.Equal("00000000000000000000000000000042", id.Value);
        Assert.Equal(id, RewardTransactionId.FromGuid(guid));
        Assert.Equal(guid, new Guid(id.Value));
        Assert.True(id == RewardTransactionId.FromGuid(new Guid(id.Value)));
        Assert.Equal(id.Value, id.ToString());
    }

    [Fact]
    public void DomainResult_OkHasNoError()
    {
        var ok = DomainResult.Ok();

        Assert.True(ok.IsSuccess);
        Assert.Null(ok.Error);
    }

    [Fact]
    public void DomainResult_FailCarriesErrorCodeAndMessage()
    {
        var fail = DomainResult.Fail(ErrorCodes.NotQueued, "queue is empty");

        Assert.False(fail.IsSuccess);
        Assert.NotNull(fail.Error);
        Assert.Equal(ErrorCodes.NotQueued, fail.Error!.Code);
        Assert.Equal("queue is empty", fail.Error!.Message);
        Assert.Equal("project.not-queued: queue is empty", fail.Error!.ToString());
    }

    [Fact]
    public void DomainResult_FromMapsSuccessFlagToResult()
    {
        Assert.True(DomainResult.From(true, ErrorCodes.InvalidArgument, "ignored").IsSuccess);

        var failed = DomainResult.From(false, ErrorCodes.QueueEmpty, "empty");
        Assert.False(failed.IsSuccess);
        Assert.Equal(ErrorCodes.QueueEmpty, failed.Error!.Code);
    }

    [Fact]
    public void DomainError_NullPartsDefaultToEmptyString()
    {
        var error = new DomainError(null!, null!);

        Assert.Equal(string.Empty, error.Code);
        Assert.Equal(string.Empty, error.Message);
    }

    [Fact]
    public void DomainResultT_HoldsValueOnSuccessAndErrorOnFailure()
    {
        var ok = DomainResult<long>.Ok(7L);
        Assert.True(ok.IsSuccess);
        Assert.Equal(7L, ok.Value);
        Assert.Null(ok.Error);

        var fail = DomainResult<long>.Fail(ErrorCodes.UnknownProject, "missing");
        Assert.False(fail.IsSuccess);
        Assert.Equal(0L, fail.Value);
        Assert.Equal(ErrorCodes.UnknownProject, fail.Error!.Code);
    }
}
