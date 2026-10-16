using System.Linq;
using WalkGame.Application.Content;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>D-086: every Region 1 discovery has authored body and origin copy, and nothing extra.</summary>
public sealed class DiscoveryCopyTests
{
    [Fact]
    public void EveryDiscovery_HasBodyAndOriginCopy_WithoutKeysOrPlaceholders()
    {
        var region = Region1Catalog.Create();
        Assert.NotEmpty(region.Discoveries);
        foreach (var d in region.Discoveries)
        {
            foreach (var key in new[] { d.BodyKey, d.ProvenanceKey })
            {
                var text = Region1DiscoveryCopy.Text(key);
                Assert.False(string.IsNullOrWhiteSpace(text), "missing copy for " + key);
                Assert.DoesNotContain("disc.", text);
                Assert.DoesNotContain("TODO", text);
                Assert.InRange(text!.Length, 40, 260);
                Assert.EndsWith(".", text.TrimEnd('"'));
            }
        }
    }

    [Fact]
    public void NoOrphanCopy_EveryAuthoredKeyBelongsToADiscovery()
    {
        var keys = Region1Catalog.Create().Discoveries
            .SelectMany(d => new[] { d.BodyKey, d.ProvenanceKey }).ToHashSet();
        Assert.All(Region1DiscoveryCopy.Keys, k => Assert.Contains(k, keys));
        Assert.Equal(keys.Count, Region1DiscoveryCopy.Keys.Count());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("disc.unknown.body")] [InlineData("DISC.OLD-MILLSTONE.BODY")]
    public void UnknownKeys_ReturnNull(string? key) => Assert.Null(Region1DiscoveryCopy.Text(key));
}
