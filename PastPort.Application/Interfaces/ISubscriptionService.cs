// PastPort.Application/Interfaces/ISubscriptionService.cs
// WHY: The old interface had duplicate methods with different return types,
// forcing implementations to throw NotImplementedException. We unify the
// payment flow to use concrete DTOs, removing ambiguity.

using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Domain.Enums;

public interface ISubscriptionService
{
    Task<SubscriptionResponseDto> CreateSubscriptionAsync(string userId, CreateSubscriptionRequestDto request);
    Task<SubscriptionResponseDto?> GetActiveSubscriptionAsync(string userId);
    Task<List<SubscriptionResponseDto>> GetUserSubscriptionsAsync(string userId);
    Task<bool> CancelSubscriptionAsync(string userId);
    Task<List<SubscriptionPlanInfoDto>> GetAvailablePlansAsync();
    Task<bool> CheckSubscriptionAccessAsync(string userId, SubscriptionPlan requiredPlan);

    // WHY: Single, concrete payment methods — no more object? returns
    Task<PayPalPaymentResponseDto> InitiatePaymentAsync(
        string userId,
        CreateSubscriptionRequestDto subscriptionRequest,
        PayPalPaymentRequestDto paymentRequest);

    Task<ApiResponseDto> CompletePaymentAsync(
        string userId,
        string orderId,
        CreateSubscriptionRequestDto subscriptionRequest);
}