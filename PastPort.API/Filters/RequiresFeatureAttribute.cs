using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PastPort.API.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequiresFeatureAttribute(string featureSlug) : Attribute, IAsyncActionFilter
    {
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

            var hasAccess = await subscriptionService.HasFeatureAccessAsync(userId, featureSlug);

            if (!hasAccess)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Subscription required",
                    Detail = $"Your current plan does not include the '{featureSlug}' feature. Please upgrade your subscription.",
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
    // =======================================================
}
