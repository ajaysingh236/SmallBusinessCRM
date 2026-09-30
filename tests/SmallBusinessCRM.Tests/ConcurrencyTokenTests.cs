using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class ConcurrencyTokenTests
{
    private class DummyCurrentUserService : ICurrentUserService
    {
        public int? UserId => 1;
        public IReadOnlySet<string> Roles => new HashSet<string> { RolesCatalog.Admin };
        public bool IsAuthenticated => true;
        public bool IsAdmin => true;
        public bool IsManager => false;
        public bool IsManagerOrAdmin => true;
    }

    [Fact]
    public void ModelBuilder_MapsRowVersion_AsConcurrencyTokenOnLeadAndApplicationUser()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new CrmDbContext(options, new DummyCurrentUserService());
        var model = db.Model;

        var leadEntity = model.FindEntityType(typeof(Lead));
        Assert.NotNull(leadEntity);
        var leadRowVersion = leadEntity.FindProperty(nameof(Lead.RowVersion));
        Assert.NotNull(leadRowVersion);
        Assert.True(leadRowVersion.IsConcurrencyToken);

        var userEntity = model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(userEntity);
        var userRowVersion = userEntity.FindProperty(nameof(ApplicationUser.RowVersion));
        Assert.NotNull(userRowVersion);
        Assert.True(userRowVersion.IsConcurrencyToken);
    }
}
