using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PastPort.API.Filters
{
    /// <summary>
    /// Action filter attribute that enforces subscription-based feature access control.
    /// When applied to a controller action, it checks whether the authenticated user's
    /// current subscription plan includes the specified feature slug. Returns HTTP 402
    /// (Payment Required) if the user's plan does not include the feature.
    /// </summary>
    /// <remarks>
    /// This attribute requires the user to be authenticated (JWT Bearer).
    /// It resolves <see cref="ISubscriptionService"/> from the request's DI container
    /// to perform the feature access check.
    /// </remarks>
    /// <example>
    /// <code>
    /// [HttpGet("artifacts/hidden")]
    /// [Authorize]
    /// [RequiresFeature("ExploreSecrets")]
    /// public IActionResult GetHiddenArtifacts()
    /// {
    ///     return Ok(new { message = "You found the hidden artifacts!" });
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequiresFeatureAttribute(string featureSlug) : Attribute, IAsyncActionFilter
    {
        /// <summary>
        /// Executes before the action method. Validates the user's identity and
        /// checks feature access through <see cref="ISubscriptionService.HasFeatureAccessAsync"/>.
        /// </summary>
        /// <param name="context">The action execution context.</param>
        /// <param name="next">The delegate to invoke the next filter or the action itself.</param>
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
}
