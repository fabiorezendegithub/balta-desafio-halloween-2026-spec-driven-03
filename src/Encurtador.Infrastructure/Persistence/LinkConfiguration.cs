using Encurtador.Application.Links;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Encurtador.Infrastructure.Persistence;

public sealed class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("Links");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.Code).HasMaxLength(20).IsRequired();
        builder.Property(l => l.CodeKey).HasMaxLength(20).IsRequired();
        builder.Property(l => l.TargetUrl).HasMaxLength(2048).IsRequired();
        builder.Property(l => l.CreatedAtUtc).IsRequired();
        // SQLite não traduz comparações de DateTimeOffset em LINQ. Guardar o instante UTC como ticks (long)
        // mantém a comparação "ExpiresAtUtc > agora" dentro do banco, na mesma instrução do contador (D2, D9).
        builder.Property(l => l.ExpiresAtUtc).HasConversion(
            new ValueConverter<DateTimeOffset?, long?>(
                value => value.HasValue ? value.Value.UtcTicks : null,
                ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : null));
        builder.Property(l => l.ClickCount).HasDefaultValue(0L).IsRequired();
        builder.Property(l => l.ManagementKeyHash).HasMaxLength(64).IsRequired();

        // D1: unicidade garantida pelo banco, mesmo sob concorrência.
        builder.HasIndex(l => l.CodeKey)
            .IsUnique()
            .HasDatabaseName("UQ_Link_CodeKey");

        // REQ-012: o contador nunca fica negativo.
        builder.ToTable(t => t.HasCheckConstraint("CK_Link_ClickCount_NonNegative", "\"ClickCount\" >= 0"));
    }
}
