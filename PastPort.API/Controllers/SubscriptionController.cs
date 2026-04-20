using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using System.Security.Claims;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
        => _subscriptionService = subscriptionService;

    // ── Helpers ──────────────────────────────────────────────
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    // ── GET /api/subscriptions/plans ─────────────────────────
    /// <summary>Returns all active, public subscription plans.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<PlanDto>), 200)]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _subscriptionService.GetActivePlansAsync(ct);
        return Ok(plans);
    }

    // ── GET /api/subscriptions/plans/{id} ─────────────────────
    /// <summary>Returns a single plan's details including features.</summary>
    [HttpGet("plans/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlanDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken ct)
    {
        var plan = await _subscriptionService.GetPlanByIdAsync(id, ct);
        return plan is null ? NotFound() : Ok(plan);
    }

    // ── GET /api/subscriptions/me ─────────────────────────────
    /// <summary>Returns the authenticated user's current subscription, if any.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserSubscriptionDto), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> GetMySubscription(CancellationToken ct)
    {
        var sub = await _subscriptionService.GetActiveSubscriptionAsync(UserId, ct);
        return sub is null ? NoContent() : Ok(sub);
    }

    // ── POST /api/subscriptions/checkout ─────────────────────
    /// <summary>
    /// Step 1: Initiates a checkout session.
    /// Returns a payment URL the frontend must redirect the user to.
    /// The subscription is NOT active until the payment gateway webhook fires.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize]
    [ProducesResponseType(typeof(InitiateCheckoutResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> InitiateCheckout(
        [FromBody] InitiateCheckoutRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _subscriptionService.InitiateCheckoutAsync(UserId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already has an active"))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Subscription conflict",
                Detail = ex.Message,
                Status = 409
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Checkout failed",
                Detail = ex.Message,
                Status = 400
            });
        }
    }

    // ── POST /api/subscriptions/change-plan ──────────────────
    /// <summary>
    /// Upgrades or downgrades the user's current subscription plan.
    /// Applies proration by default.
    /// </summary>
    [HttpPost("change-plan")]
    [Authorize]
    [ProducesResponseType(typeof(UserSubscriptionDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] UpgradePlanRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _subscriptionService.ChangePlanAsync(UserId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Plan change failed",
                Detail = ex.Message,
                Status = 400
            });
        }
    }

    // ── POST /api/subscriptions/cancel ───────────────────────
    /// <summary>
    /// Cancels auto-renewal. User retains access until CurrentPeriodEnd.
    /// </summary>
    [HttpPost("cancel")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct)
    {
        try
        {
            await _subscriptionService.CancelSubscriptionAsync(UserId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Cancellation failed",
                Detail = ex.Message,
                Status = 400
            });
        }
    }

    // ── GET /api/subscriptions/features/{slug} ───────────────
    /// <summary>
    /// Checks whether the authenticated user's plan includes a specific feature.
    /// Use this as a lightweight gate for frontend conditional rendering.
    /// </summary>
    [HttpGet("features/{slug}")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureAccessResult), 200)]
    public async Task<IActionResult> CheckFeatureAccess(string slug, CancellationToken ct)
    {
        var hasAccess = await _subscriptionService.HasFeatureAccessAsync(UserId, slug, ct);
        return Ok(new FeatureAccessResult(slug, hasAccess));
    }
}

// Small response type for feature checks
public record FeatureAccessResult(string FeatureSlug, bool HasAccess);