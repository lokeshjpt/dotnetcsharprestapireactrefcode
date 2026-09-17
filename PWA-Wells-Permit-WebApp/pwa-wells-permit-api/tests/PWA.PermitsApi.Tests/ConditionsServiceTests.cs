using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Services;

namespace PWA.PermitsApi.Tests;

public sealed class ConditionsServiceTests
{
    private static ConditionsService CreateService(FakeConditionsRepository repo) =>
        new(repo, NullLogger<ConditionsService>.Instance);

    private static void SeedWorkType(FakeConditionsRepository repo, string category, string type, params ReferenceItemDto[] items)
        => repo.AvailableByWorkType[$"{category}|{type}"] = items.ToList();

    [Fact]
    public async Task GetConditionsAsync_ReturnsOnePanelPerWork_WithWorkTypeScopedMasterList()
    {
        var repo = new FakeConditionsRepository();
        repo.Works.Add(new WorkForConditionsDto(1, "NEW", "DOM", "New - Domestic Well", "PEND"));
        repo.Works.Add(new WorkForConditionsDto(2, "DEST", "DEST", "Destruction", "PENDC"));
        SeedWorkType(repo, "NEW", "DOM",
            new ReferenceItemDto("grout", "Grout shall be placed by Tremie"),
            new ReferenceItemDto("other", "Other"));
        SeedWorkType(repo, "DEST", "DEST", new ReferenceItemDto("seal", "Seal per code"));
        // Work 1 already has persisted conditions; work 2 has none (nothing pre-checked).
        repo.Applied = new List<WorkConditionRowDto>
        {
            new(1, "grout", null),
            new(1, "other", "Provide 48-hr notice"),
        };
        var service = CreateService(repo);

        var result = await service.GetConditionsAsync("1234567890123");

        Assert.Equal(2, result.Works.Count);

        var work1 = result.Works.Single(w => w.WorkId == 1);
        Assert.Equal("PEND", work1.StatusCode);
        Assert.Equal(2, work1.Available.Count);
        Assert.Equal(2, work1.Selected.Count);
        Assert.Contains(work1.Selected, s => s.ConditionType == "other" && s.OtherDesc == "Provide 48-hr notice");

        var work2 = result.Works.Single(w => w.WorkId == 2);
        Assert.Equal("PENDC", work2.StatusCode);
        Assert.Single(work2.Available);
        // Nothing is pre-checked for a work with no persisted conditions (parity with legacy: the
        // work-type conditions are shown as UNCHECKED checkboxes until the reviewer applies them).
        Assert.Empty(work2.Selected);
    }

    [Fact]
    public async Task GetConditionsAsync_NoPersistedConditions_LeavesSelectionEmpty()
    {
        var repo = new FakeConditionsRepository();
        repo.Works.Add(new WorkForConditionsDto(1, "NEW", "DOM", "New - Domestic Well", "PENDC"));
        SeedWorkType(repo, "NEW", "DOM",
            new ReferenceItemDto("grout", "Grout"),
            new ReferenceItemDto("other", "Other"));
        var service = CreateService(repo);

        var result = await service.GetConditionsAsync("1234567890123");

        var work = result.Works.Single();
        Assert.Empty(work.Selected);
        // The full work-type master list is still available for the reviewer to check.
        Assert.Equal(2, work.Available.Count);
    }

    [Fact]
    public async Task UpdateWorkConditionsAsync_PersistsSelectionForWork_AndDropsBlankCodes()
    {
        var repo = new FakeConditionsRepository();
        repo.Works.Add(new WorkForConditionsDto(2, "NEW", "DOM", "New - Domestic Well", "PENDC"));
        SeedWorkType(repo, "NEW", "DOM", new ReferenceItemDto("grout", "Grout"));
        var service = CreateService(repo);

        var request = new UpdateWorkConditionsRequest
        {
            Conditions = new List<ConditionSelectionDto>
            {
                new("grout", null),
                new("", null),          // blank code must be dropped
                new("other", "Special note"),
            },
        };

        await service.UpdateWorkConditionsAsync("1234567890123", 2, request, "staff@acgov.org");

        Assert.NotNull(repo.LastReplace);
        Assert.Equal("1234567890123", repo.LastReplace!.Value.AppId);
        Assert.Equal(2, repo.LastReplace.Value.WorkId);
        Assert.Equal("staff@acgov.org", repo.LastReplace.Value.UpdatedBy);
        Assert.False(repo.LastReplace.Value.NoSpecials);
        Assert.Equal(2, repo.LastReplace.Value.Conditions.Count);
        Assert.DoesNotContain(repo.LastReplace.Value.Conditions, c => string.IsNullOrWhiteSpace(c.ConditionType));
    }

    [Fact]
    public async Task UpdateWorkConditionsAsync_NoSpecials_PassesFlagThrough()
    {
        var repo = new FakeConditionsRepository();
        repo.Works.Add(new WorkForConditionsDto(1, "NEW", "DOM", "New - Domestic Well", "PENDC"));
        SeedWorkType(repo, "NEW", "DOM", new ReferenceItemDto("grout", "Grout"));
        var service = CreateService(repo);

        var request = new UpdateWorkConditionsRequest
        {
            Conditions = new List<ConditionSelectionDto>(),
            NoSpecials = true,
        };

        await service.UpdateWorkConditionsAsync("1234567890123", 1, request, "staff@acgov.org");

        Assert.NotNull(repo.LastReplace);
        Assert.Equal(1, repo.LastReplace!.Value.WorkId);
        Assert.True(repo.LastReplace.Value.NoSpecials);
        Assert.Empty(repo.LastReplace.Value.Conditions);
    }

    [Fact]
    public async Task UpdateWorkConditionsAsync_OnlyTargetsRequestedWork()
    {
        var repo = new FakeConditionsRepository();
        repo.Works.Add(new WorkForConditionsDto(1, "NEW", "DOM", "New - Domestic Well", "PEND"));
        repo.Works.Add(new WorkForConditionsDto(2, "DEST", "DEST", "Destruction", "PENDC"));
        SeedWorkType(repo, "NEW", "DOM", new ReferenceItemDto("grout", "Grout"));
        SeedWorkType(repo, "DEST", "DEST", new ReferenceItemDto("seal", "Seal"));
        repo.Applied = new List<WorkConditionRowDto> { new(1, "grout", null) };
        var service = CreateService(repo);

        var request = new UpdateWorkConditionsRequest
        {
            Conditions = new List<ConditionSelectionDto> { new("seal", null) },
        };

        var result = await service.UpdateWorkConditionsAsync("1234567890123", 2, request, "staff@acgov.org");

        // Work 1's persisted conditions must be untouched.
        var work1 = result.Works.Single(w => w.WorkId == 1);
        Assert.Single(work1.Selected);
        Assert.Equal("grout", work1.Selected[0].ConditionType);

        var work2 = result.Works.Single(w => w.WorkId == 2);
        Assert.Single(work2.Selected);
        Assert.Equal("seal", work2.Selected[0].ConditionType);
    }
}
