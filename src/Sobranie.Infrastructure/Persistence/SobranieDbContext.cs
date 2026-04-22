using Microsoft.EntityFrameworkCore;
using Sobranie.Domain;

namespace Sobranie.Infrastructure.Persistence;

public sealed class SobranieDbContext(DbContextOptions<SobranieDbContext> options) : DbContext(options)
{
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<MPProfile> MPs => Set<MPProfile>();
    public DbSet<SignatureMove> SignatureMoves => Set<SignatureMove>();
    public DbSet<ChorusLine> ChorusLines => Set<ChorusLine>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Speech> Speeches => Set<Speech>();
    public DbSet<BeefEdge> BeefEdges => Set<BeefEdge>();
    public DbSet<LlmCallLog> LlmCallLogs => Set<LlmCallLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Party>(p =>
        {
            p.HasKey(x => x.PartyId);
            p.Property(x => x.PartyId).HasMaxLength(32);
            p.Property(x => x.DisplayName).HasMaxLength(256);
            p.Property(x => x.ShortName).HasMaxLength(32);
            p.Property(x => x.ColorHex).HasMaxLength(9);
        });

        modelBuilder.Entity<MPProfile>(m =>
        {
            m.HasKey(x => x.MPId);
            m.Property(x => x.MPId).HasMaxLength(64);
            m.Property(x => x.DisplayName).HasMaxLength(256);
            m.Property(x => x.PersonaSystemPrompt).HasMaxLength(4096);
            m.HasOne(x => x.Party).WithMany(p => p.Members).HasForeignKey(x => x.PartyId);
            m.HasIndex(x => new { x.Tier, x.PartyId });
        });

        modelBuilder.Entity<SignatureMove>(s =>
        {
            s.HasKey(x => x.Id);
            s.Property(x => x.Label).HasMaxLength(128);
            s.Property(x => x.Exemplar).HasMaxLength(1024);
            s.HasOne(x => x.MP).WithMany(m => m.SignatureMoves).HasForeignKey(x => x.MPId);
        });

        modelBuilder.Entity<ChorusLine>(c =>
        {
            c.HasKey(x => x.Id);
            c.Property(x => x.Text).HasMaxLength(512);
            c.Property(x => x.TopicTag).HasMaxLength(64);
            c.HasOne(x => x.Party).WithMany(p => p.ChorusLines).HasForeignKey(x => x.PartyId);
            c.HasIndex(x => new { x.PartyId, x.TopicTag });
        });

        modelBuilder.Entity<Proposal>(p =>
        {
            p.HasKey(x => x.Id);
            p.Property(x => x.SourceUrl).HasMaxLength(2048);
            p.Property(x => x.Source).HasMaxLength(128);
            p.Property(x => x.Headline).HasMaxLength(1024);
            p.Property(x => x.RewrittenAsProposal).HasMaxLength(2048);
            p.HasIndex(x => x.Status);
            p.HasIndex(x => x.FetchedAt);
        });

        modelBuilder.Entity<Speech>(s =>
        {
            s.HasKey(x => x.Id);
            s.Property(x => x.Content).HasMaxLength(8192);
            s.HasOne(x => x.MP).WithMany().HasForeignKey(x => x.MPId);
            s.HasOne(x => x.Proposal).WithMany().HasForeignKey(x => x.ProposalId);
        s.HasIndex(x => x.UtteredAt);
        s.HasIndex(x => new { x.MPId, x.UtteredAt });
        });

        modelBuilder.Entity<BeefEdge>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.FromMPId).HasMaxLength(64);
            b.Property(x => x.ToMPId).HasMaxLength(64);
            b.HasIndex(x => new { x.FromMPId, x.ToMPId }).IsUnique();
        });

        modelBuilder.Entity<LlmCallLog>(l =>
        {
            l.HasKey(x => x.Id);
            l.Property(x => x.PromptHash).HasMaxLength(64);
            l.Property(x => x.PromptPreview).HasMaxLength(512);
            l.Property(x => x.Purpose).HasMaxLength(64);
            l.HasIndex(x => x.CalledAt);
        });
    }
}
