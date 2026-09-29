using System;
using System.Collections.Generic;

namespace Farm360.Application.Subscriptions.DTOs;

public sealed record TenantSubscriptionStatusDto(
    Guid TenantId,
    string TenantName,
    string Tier,
    string BillingCycle,
    string Status,
    bool IsTrial,
    int? TrialDays,
    DateTime? TrialEndsAtUtc,
    int TrialDaysRemaining,
    DateTime? SubscriptionExpiresAtUtc,
    int? DaysRemaining,
    bool HasUsedTrial,
    int MaxUsers,
    int CurrentUsers,
    int MaxFarms,
    int CurrentFarms,
    int MaxAnimals,
    int CurrentAnimals);

public sealed record SubscriptionPlanDto(
    string Tier,
    string Name,
    string Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    decimal OneTimePrice,
    int MaxUsers,
    int MaxFarms,
    int MaxAnimals,
    IReadOnlyList<string> Features,
    bool IsPopular = false);

public sealed record TrialOptionDto(
    int Days,
    string Title,
    string Description);

public sealed record TenantSubscriptionRecordDto(
    Guid Id,
    string Tier,
    string BillingCycle,
    decimal Amount,
    string Currency,
    DateTime StartedAtUtc,
    DateTime? ExpiresAtUtc,
    string PaymentMethod,
    string? PaymentReference,
    string Status,
    string InvoiceNumber,
    string? Notes,
    DateTime CreatedAtUtc);
