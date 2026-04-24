using Microsoft.Extensions.Logging;
using PastPort.Application.DTOs;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Exceptions; 
using DomainTransactionStatus = PastPort.Domain.Enums.TransactionStatus;
using DomainPlan = PastPort.Domain.Entities.Plan;

namespace PastPort.Infrastructure.Identity
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _db;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            ApplicationDbContext db,
            IPaymentService paymentService,
            ILogger<SubscriptionService> logger)
        {
            _db = db;
            _paymentService = paymentService;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────
        // GET PLANS
        // ────────────────────────────────────────────────────────
        public async Task<IEnumerable<PlanDto>> GetActivePlansAsync(CancellationToken ct = default)
        {
            var plans = await _db.Plans
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsPublic)
                .Include(p => p.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync(ct);

            return plans.Select(MapPlanToDto);
        }

        public async Task<PlanDto?> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
        {
            var plan = await _db.Plans
                .AsNoTracking()
                .Include(p => p.PlanFeatures).ThenInclude(pf => pf.Feature)
                .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct);

            return plan is null ? null : MapPlanToDto(plan);
        }

        // ────────────────────────────────────────────────────────
        // GET ACTIVE SUBSCRIPTION
        // ────────────────────────────────────────────────────────
        public async Task<UserSubscriptionDto?> GetActiveSubscriptionAsync(
            string userId, CancellationToken ct = default)
        {
            var sub = await GetActiveSubscriptionEntityAsync(userId, ct);
            return sub is null ? null : MapSubscriptionToDto(sub);
        }

        // ────────────────────────────────────────────────────────
        // INITIATE CHECKOUT
        // ────────────────────────────────────────────────────────
        public async Task<InitiateCheckoutResponse> InitiateCheckoutAsync(
            string userId,
            InitiateCheckoutRequest request,
            CancellationToken ct = default)
        {
            var plan = await _db.Plans.FindAsync(new object[] { request.PlanId }, ct)
                ?? throw new NotFoundException("Plan", request.PlanId); // ✅ استخدام NotFoundException

            if (!plan.IsActive)
                throw new ValidationException("Selected plan is not available."); // ✅ استخدام ValidationException

            // ── If user already has an active sub, use ChangePlanAsync instead ──
            var existingSub = await GetActiveSubscriptionEntityAsync(userId, ct);
            if (existingSub is not null)
                throw new ValidationException( // ✅ منع تكرار الاشتراك واستخدام ValidationException
                    "User already has an active subscription. Use the upgrade/downgrade endpoint.");

            // ── Determine billing period dates ───────────────────
            var (periodStart, periodEnd) = CalculateBillingPeriod(plan.BillingCycle);
            DateTime? trialEnd = plan.TrialDays > 0
                ? DateTime.UtcNow.AddDays(plan.TrialDays)
                : null;

            // ── 1. Create the Pending subscription ───────────────
            var subscription = new UserSubscription
            {
                UserId = userId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.PendingPayment,
                CurrentPeriodStart = periodStart,
                CurrentPeriodEnd = periodEnd,
                TrialEnd = trialEnd,
                AutoRenew = true
            };
            _db.UserSubscriptions.Add(subscription);

            // ── 2. Create the Pending transaction ────────────────
            var chargeAmount = trialEnd.HasValue ? 0m : plan.Price;

            var transaction = new PaymentTransaction
            {
                UserSubscriptionId = subscription.Id,
                UserId = userId,
                Amount = chargeAmount,
                Currency = plan.Currency,
                Status = (DomainTransactionStatus)(System.Transactions.TransactionStatus)TransactionStatus.Pending,
                Gateway = request.Gateway
            };
            _db.PaymentTransactions.Add(transaction);

            // ── 3. Create a Draft invoice ─────────────────────────
            var invoice = CreateDraftInvoice(userId, subscription.Id, transaction.Id, plan);
            _db.Invoices.Add(invoice);

            await _db.SaveChangesAsync(ct);

            // ── 4. Call gateway to get a payment URL ─────────────
            string paymentUrl;
            string gatewayTxId;

            try
            {
                (paymentUrl, gatewayTxId) = await _paymentService.CreateCheckoutSessionAsync(
                    subscription.Id,
                    transaction.Id,
                    chargeAmount,
                    plan.Currency,
                    request.SuccessUrl,
                    request.CancelUrl,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gateway session creation failed for transaction {TxId}", transaction.Id);
                transaction.Status = (DomainTransactionStatus)(System.Transactions.TransactionStatus)TransactionStatus.Cancelled;
                transaction.FailureReason = "Gateway session creation failed.";
                subscription.Status = SubscriptionStatus.PendingPayment;
                await _db.SaveChangesAsync(ct);

                throw new ValidationException("Payment gateway unavailable. Please try again."); // ✅ 
            }

            // ── 5. Persist gateway IDs ────────────────────────────
            transaction.GatewayTransactionId = gatewayTxId;
            transaction.GatewayPaymentUrl = paymentUrl;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Checkout initiated. UserId={UserId} PlanId={PlanId} TxId={TxId}",
                userId, plan.Id, transaction.Id);

            return new InitiateCheckoutResponse(
                TransactionId: transaction.Id,
                SubscriptionId: subscription.Id,
                PaymentUrl: paymentUrl,
                Status: "PendingPayment"
            );
        }

        // ────────────────────────────────────────────────────────
        // ACTIVATE SUBSCRIPTION
        // ────────────────────────────────────────────────────────
        public async Task ActivateSubscriptionAsync(Guid transactionId, CancellationToken ct = default)
        {
            var tx = await _db.PaymentTransactions
                .Include(t => t.UserSubscription)
                .Include(t => t.Invoice)
                .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
                ?? throw new NotFoundException("Transaction", transactionId); // ✅ استخدام NotFoundException

            var sub = tx.UserSubscription;

            // Guard against re-processing
            if (sub.Status == SubscriptionStatus.Active)
            {
                _logger.LogWarning("Subscription {SubId} is already active. Skipping.", sub.Id);
                return;
            }

            // ── Activate the subscription ─────────────────────────
            sub.Status = SubscriptionStatus.Active;
            sub.UpdatedAt = DateTime.UtcNow;

            // ── Finalize the transaction ──────────────────────────
            tx.Status = (DomainTransactionStatus)(System.Transactions.TransactionStatus)TransactionStatus.Success;
            tx.ProcessedAt = DateTime.UtcNow;

            // ── Mark invoice as Paid ──────────────────────────────
            if (tx.Invoice is not null)
            {
                tx.Invoice.Status = InvoiceStatus.Paid;
                tx.Invoice.PaidAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Subscription activated. SubId={SubId} UserId={UserId}",
                sub.Id, sub.UserId);
        }

        // ────────────────────────────────────────────────────────
        // HANDLE FAILED PAYMENT
        // ────────────────────────────────────────────────────────
        public async Task HandleFailedPaymentAsync(
            Guid transactionId, string reason, CancellationToken ct = default)
        {
            var tx = await _db.PaymentTransactions
                .Include(t => t.UserSubscription)
                .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
                ?? throw new NotFoundException("Transaction", transactionId); // ✅ استخدام NotFoundException

            tx.Status = (DomainTransactionStatus)(System.Transactions.TransactionStatus)TransactionStatus.Failed;
            tx.FailureReason = reason;
            tx.ProcessedAt = DateTime.UtcNow;

            var sub = tx.UserSubscription;
            sub.Status = sub.Status == SubscriptionStatus.Active
                ? SubscriptionStatus.PastDue
                : SubscriptionStatus.PendingPayment;
            sub.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Payment failed. TxId={TxId} SubId={SubId} Reason={Reason}",
                transactionId, sub.Id, reason);
        }

        // ────────────────────────────────────────────────────────
        // CHANGE PLAN (Upgrade / Downgrade)
        // ────────────────────────────────────────────────────────
        public async Task<UserSubscriptionDto> ChangePlanAsync(
            string userId,
            UpgradePlanRequest request,
            CancellationToken ct = default)
        {
            var currentSub = await GetActiveSubscriptionEntityAsync(userId, ct, includePlan: true)
                ?? throw new ValidationException("No active subscription found."); // ✅ 

            if (currentSub.PlanId == request.NewPlanId)
                throw new ValidationException("User is already on this plan."); // ✅ 

            var newPlan = await _db.Plans.FindAsync(new object[] { request.NewPlanId }, ct)
                ?? throw new NotFoundException("Plan", request.NewPlanId); // ✅ 

            decimal prorationCredit = 0;
            if (request.ApplyProration)
            {
                var totalDays = (currentSub.CurrentPeriodEnd - currentSub.CurrentPeriodStart).TotalDays;
                var daysRemaining = (currentSub.CurrentPeriodEnd - DateTime.UtcNow).TotalDays;
                var dailyRate = currentSub.Plan.Price / (decimal)totalDays;
                prorationCredit = Math.Max(0, dailyRate * (decimal)daysRemaining);
            }

            // ── Record old plan for audit trail ──────────────────
            currentSub.PreviousPlanId = currentSub.PlanId;
            currentSub.PlanId = newPlan.Id;
            currentSub.ProrationCredit = prorationCredit;

            // Reset billing period to now
            var (newStart, newEnd) = CalculateBillingPeriod(newPlan.BillingCycle);
            currentSub.CurrentPeriodStart = newStart;
            currentSub.CurrentPeriodEnd = newEnd;
            currentSub.UpdatedAt = DateTime.UtcNow;

            // ── New transaction for the prorated charge ───────────
            var chargeAmount = Math.Max(0, newPlan.Price - prorationCredit);
            var newTx = new PaymentTransaction
            {
                UserSubscriptionId = currentSub.Id,
                UserId = userId,
                Amount = chargeAmount,
                Currency = newPlan.Currency,
                Status = (DomainTransactionStatus)(System.Transactions.TransactionStatus)TransactionStatus.Pending,
            };
            _db.PaymentTransactions.Add(newTx);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Plan changed. UserId={UserId} OldPlan={OldPlan} NewPlan={NewPlan} Credit={Credit}",
                userId, currentSub.PreviousPlanId, newPlan.Id, prorationCredit);

            return MapSubscriptionToDto(currentSub);
        }

        // ────────────────────────────────────────────────────────
        // CANCEL SUBSCRIPTION
        // ────────────────────────────────────────────────────────
        public async Task CancelSubscriptionAsync(string userId, CancellationToken ct = default)
        {
            var sub = await GetActiveSubscriptionEntityAsync(userId, ct)
                ?? throw new ValidationException("No active subscription to cancel."); // ✅ 

            sub.Status = SubscriptionStatus.Cancelled;
            sub.AutoRenew = false;
            sub.CancelledAt = DateTime.UtcNow;
            sub.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        // ────────────────────────────────────────────────────────
        // FEATURE ACCESS GATE
        // ────────────────────────────────────────────────────────
        public async Task<bool> HasFeatureAccessAsync(
            string userId, string featureSlug, CancellationToken ct = default)
        {
            var sub = await GetActiveSubscriptionEntityAsync(userId, ct);

            if (sub is null) return false;
            if (sub.CurrentPeriodEnd < DateTime.UtcNow) return false;

            return await _db.PlanFeatures
                .AsNoTracking()
                .AnyAsync(pf =>
                    pf.PlanId == sub.PlanId &&
                    pf.Feature.Slug == featureSlug &&
                    pf.IsEnabled,
                    ct);
        }

        // ──────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────

        private async Task<UserSubscription?> GetActiveSubscriptionEntityAsync(
            string userId, CancellationToken ct, bool includePlan = false)
        {
            var query = _db.UserSubscriptions
            .Where(s => s.UserId == userId &&
                            (s.Status == SubscriptionStatus.Active ||
             s.Status == SubscriptionStatus.Trialing ||
                              s.Status == SubscriptionStatus.Cancelled));

            if (includePlan)
                query = query.Include(s => s.Plan);

            return await query.FirstOrDefaultAsync(ct);
        }

        private static (DateTime Start, DateTime End) CalculateBillingPeriod(BillingCycle cycle)
        {
            var start = DateTime.UtcNow;
            var end = cycle switch
            {
                BillingCycle.Monthly => start.AddMonths(1),
                BillingCycle.Yearly => start.AddYears(1),
                BillingCycle.Weekly => start.AddDays(7),
                BillingCycle.OneTime => DateTime.MaxValue,
                _ => start.AddMonths(1)
            };
            return (start, end);
        }

        private static PastPort.Domain.Entities.Invoice CreateDraftInvoice(
            string userId, Guid subscriptionId, Guid transactionId, PastPort.Domain.Entities.Plan plan)
        {
            var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            return new PastPort.Domain.Entities.Invoice
            {
                InvoiceNumber = invoiceNumber,
                UserSubscriptionId = subscriptionId,
                PaymentTransactionId = transactionId,
                UserId = userId,
                SubTotal = plan.Price,
                TotalAmount = plan.Price,
                Currency = plan.Currency,
                Status = InvoiceStatus.Draft,
                DueDate = DateTime.UtcNow.AddDays(1)
            };
        }

        private static PlanDto MapPlanToDto(Domain.Entities.Plan plan) => new(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.Price,
            plan.Currency,
            plan.BillingCycle,
            plan.TrialDays,
            plan.DisplayOrder,
            plan.PlanFeatures
                .Where(pf => pf.Feature.IsActive)
                .Select(pf => new FeatureDto(
                    pf.Feature.Id,
                    pf.Feature.Name,
                    pf.Feature.Slug,
                    pf.Feature.Description,
                    pf.Limit,
                    pf.IsEnabled))
                .ToList()
        );

        private static UserSubscriptionDto MapSubscriptionToDto(UserSubscription sub) => new(
            sub.Id,
            MapPlanToDto(sub.Plan),
            sub.Status,
            sub.CurrentPeriodStart,
            sub.CurrentPeriodEnd,
            sub.TrialEnd,
            sub.AutoRenew,
            sub.CancelledAt
        );
    }
}