using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using System;

namespace PastPort.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // --- Existing DbSets (Core) ---
    public DbSet<HistoricalScene> HistoricalScenes => Set<HistoricalScene>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Asset> Assets => Set<Asset>();

    // --- New Payment & Subscription DbSets ---
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<SavedPaymentMethod> SavedPaymentMethods => Set<SavedPaymentMethod>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 1. Existing Configurations
        builder.Entity<HistoricalScene>()
            .HasMany(s => s.Characters)
            .WithOne(c => c.Scene)
            .HasForeignKey(c => c.SceneId);

        builder.Entity<Asset>()
            .HasIndex(a => a.FileName);

        // ✅ FIX 4 (From JwtTokenService): Add unique index for RefreshToken to prevent Table Scans
        builder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        // ✅ FIX 3: Add missing indexes for Conversations to optimize queries
        builder.Entity<Conversation>(e =>
        {
            e.HasIndex(c => c.CharacterId);
            e.HasIndex(c => new { c.UserId, c.CharacterId }); // Composite index for specific queries
        });

        // 2. Plan & Features
        builder.Entity<Plan>(e =>
        {
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Price).HasPrecision(18, 2);
        });

        builder.Entity<Feature>(e =>
        {
            e.HasIndex(f => f.Slug).IsUnique();
        });

        builder.Entity<PlanFeature>(e =>
        {
            e.HasIndex(pf => new { pf.PlanId, pf.FeatureId }).IsUnique();
            e.HasOne(pf => pf.Plan).WithMany(p => p.PlanFeatures).HasForeignKey(pf => pf.PlanId);
        });

        // 3. Subscriptions & Transactions
        builder.Entity<UserSubscription>(e =>
        {
            e.HasIndex(us => new { us.UserId, us.Status });
            e.HasOne(us => us.Plan).WithMany(p => p.UserSubscriptions).OnDelete(DeleteBehavior.Restrict);

            // ✅ FIX 1 & 2: Explicitly mapping User relationship to prevent Shadow Properties
            e.HasOne(us => us.User)
             .WithMany()
             .HasForeignKey(us => us.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PaymentTransaction>(e =>
        {
            e.HasIndex(t => t.GatewayTransactionId);
            e.HasIndex(t => t.IdempotencyKey).IsUnique();
            e.Property(t => t.Amount).HasPrecision(18, 2);

            // ✅ FIX 1 & 2: Explicitly map relationships for PaymentTransaction to prevent Shadow Properties (Like UserSubscriptionId1)
            e.HasOne(pt => pt.UserSubscription)
             .WithMany() // Assuming UserSubscription doesn't have a list of transactions. If it does, use .WithMany(us => us.PaymentTransactions)
             .HasForeignKey(pt => pt.UserSubscriptionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(pt => pt.User)
             .WithMany()
             .HasForeignKey(pt => pt.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.Property(i => i.TotalAmount).HasPrecision(18, 2);
            e.Property(i => i.SubTotal).HasPrecision(18, 2); // Explicit precision for SubTotal

            e.HasOne(i => i.PaymentTransaction).WithOne(t => t.Invoice)
             .HasForeignKey<Invoice>(i => i.PaymentTransactionId);
        });

        builder.Entity<WebhookLog>(e =>
        {
            e.HasIndex(w => new { w.Gateway, w.GatewayEventId }).IsUnique();
        });
    }
}