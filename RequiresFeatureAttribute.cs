// ============================================================
//  RequiresFeatureAttribute.cs — Action Filter for Feature Gating
//
//  Usage on a controller action:
//    [RequiresFeature("ExploreSecrets")]
//    public IActionResult GetHiddenArtifacts() { ... }
//
//  This attribute checks the user's subscription features BEFORE
//  the action body runs, returning 402 Payment Required if access
//  is denied — keeping your controllers clean of access-check boilerplate.
// ============================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using SubscriptionPayment.Application.Interfaces;

namespace SubscriptionPayment.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresFeatureAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _featureSlug;

    public RequiresFeatureAttribute(string featureSlug)
        => _featureSlug = featureSlug;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var subscriptionService = context.HttpContext.RequestServices
            .GetRequiredService<ISubscriptionService>();

        var hasAccess = await subscriptionService.HasFeatureAccessAsync(userId, _featureSlug);

        if (!hasAccess)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "Subscription required",
                Detail = $"Your current plan does not include the '{_featureSlug}' feature. Please upgrade your subscription.",
                Status = 402
            })
            { StatusCode = 402 };
            return;
        }

        await next();
    }
}

// ============================================================
//  USAGE EXAMPLE — In any controller in your project:
//
//  [HttpGet("artifacts/hidden")]
//  [Authorize]
//  [RequiresFeature("ExploreSecrets")]       ← blocks if not on plan
//  public IActionResult GetHiddenArtifacts()
//  {
//      return Ok(new { message = "You found the hidden artifacts!" });
//  }
//
//  [HttpGet("scenarios")]
//  [Authorize]
//  [RequiresFeature("UnlimitedScenarios")]   ← blocks free tier
//  public IActionResult GetAllScenarios() { ... }
// ============================================================