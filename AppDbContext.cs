// ============================================================
//  AppDbContext.cs — EF Core DbContext
//  Includes Fluent API configurations for all entities
// ============================================================
using Microsoft.EntityFrameworkCore;
using SubscriptionPayment.Domain.Entities;

namespace SubscriptionPayment.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Plan ────────────────────────────────────────────
        modelBuilder.Entity<Plan>(e =>
        {
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Price).HasPrecision(18, 2);
        });

        // ── Feature slug must be unique ─────────────────────
        modelBuilder.Entity<Feature>(e =>
        {
            e.HasIndex(f => f.Slug).IsUnique();
        });

        // ── PlanFeature: composite unique constraint ─────────
        modelBuilder.Entity<PlanFeature>(e =>
        {
            e.HasIndex(pf => new { pf.PlanId, pf.FeatureId }).IsUnique();
            e.HasOne(pf => pf.Plan)
             .WithMany(p => p.PlanFeatures)
             .HasForeignKey(pf => pf.PlanId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pf => pf.Feature)
             .WithMany(f => f.PlanFeatures)
             .HasForeignKey(pf => pf.FeatureId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserSubscription ────────────────────────────────
        modelBuilder.Entity<UserSubscription>(e =>
        {
            e.HasIndex(us => new { us.UserId, us.Status });
            e.HasOne(us => us.Plan)
             .WithMany(p => p.UserSubscriptions)
             .HasForeignKey(us => us.PlanId)
             .OnDelete(DeleteBehavior.Restrict); // Don't cascade-delete subs on plan delete
        });

        // ── PaymentTransaction ──────────────────────────────
        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.HasIndex(t => t.GatewayTransactionId);
            e.HasIndex(t => t.IdempotencyKey).IsUnique();
            e.HasOne(t => t.UserSubscription)
             .WithMany(us => us.Transactions)
             .HasForeignKey(t => t.UserSubscriptionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Invoice ─────────────────────────────────────────
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.HasOne(i => i.PaymentTransaction)
             .WithOne(t => t.Invoice)
             .HasForeignKey<Invoice>(i => i.PaymentTransactionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── WebhookLog: idempotency key ─────────────────────
        modelBuilder.Entity<WebhookLog>(e =>
        {
            e.HasIndex(w => new { w.Gateway, w.GatewayEventId }).IsUnique();
        });
    }
}