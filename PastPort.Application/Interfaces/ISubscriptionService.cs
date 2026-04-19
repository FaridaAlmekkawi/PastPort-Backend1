// ✅ FIXED: ISubscriptionService.cs
// المشكلة الأصلية: الـ interface كان فيه methods مكررة بـ return types مختلفة
// (object? و CompletePaymentResponseDto) اللي كانت بتخلي الـ implementation
// يرمي NotImplementedException في كل مرة بيتعمل فيها call.
// الحل: وحّدنا الـ interface بـ return types واضحة ومحددة.

using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Domain.Enums;

namespace PastPort.Application.Interfaces;

public interface ISubscriptionService
{
    Task<SubscriptionResponseDto> CreateSubscriptionAsync(
        string userId,
        CreateSubscriptionRequestDto request);

    Task<SubscriptionResponseDto?> GetActiveSubscriptionAsync(string userId);

    Task<List<SubscriptionResponseDto>> GetUserSubscriptionsAsync(string userId);

    Task<bool> CancelSubscriptionAsync(string userId);

    Task<List<SubscriptionPlanInfoDto>> GetAvailablePlansAsync();

    Task<bool> CheckSubscriptionAccessAsync(string userId, SubscriptionPlan requiredPlan);

    // ✅ FIXED: بدل object? بقى PayPalPaymentResponseDto — return type واضح
    Task<PayPalPaymentResponseDto> InitiatePaymentAsync(
        string userId,
        CreateSubscriptionRequestDto subscriptionRequest,
        PayPalPaymentRequestDto paymentRequest);

    // ✅ FIXED: بدل object? بقى ApiResponseDto — return type واضح
    Task<ApiResponseDto> CompletePaymentAsync(
        string userId,
        string orderId,
        CreateSubscriptionRequestDto subscriptionRequest);
}
