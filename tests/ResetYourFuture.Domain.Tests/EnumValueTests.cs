using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Web.Domain.Enums;
using ResetYourFuture.Web.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// Enums are persisted as ints and cast to/from ints in DTOs. These tests lock the
/// numeric values so a reorder or insert can't silently corrupt stored data or API output.
/// </summary>
public class EnumValueTests
{
    [Fact]
    public void SubscriptionTierEnum_HasStableValues()
    {
        ((int)SubscriptionTierEnum.Free).ShouldBe(0);
        ((int)SubscriptionTierEnum.Plus).ShouldBe(1);
        ((int)SubscriptionTierEnum.Pro).ShouldBe(2);
    }

    [Fact]
    public void SubscriptionTierEnum_OrdersFreeBelowPlusBelowPro()
    {
        // Upgrade/downgrade logic in SubscriptionService relies on this ordering.
        (SubscriptionTierEnum.Free < SubscriptionTierEnum.Plus).ShouldBeTrue();
        (SubscriptionTierEnum.Plus < SubscriptionTierEnum.Pro).ShouldBeTrue();
    }

    [Fact]
    public void BillingPeriod_HasStableValues()
    {
        ((int)BillingPeriod.Lifetime).ShouldBe(0);
        ((int)BillingPeriod.Monthly).ShouldBe(1);
        ((int)BillingPeriod.Quarterly).ShouldBe(3);
        ((int)BillingPeriod.Yearly).ShouldBe(12);
    }

    [Fact]
    public void ContentType_HasStableValues()
    {
        ((int)ContentType.Text).ShouldBe(1);
        ((int)ContentType.Video).ShouldBe(2);
        ((int)ContentType.Pdf).ShouldBe(3);
    }

    [Fact]
    public void EnrollmentStatus_HasStableValues()
    {
        ((int)EnrollmentStatus.Active).ShouldBe(1);
        ((int)EnrollmentStatus.Completed).ShouldBe(2);
        ((int)EnrollmentStatus.Dropped).ShouldBe(3);
    }

    [Fact]
    public void CertificateStatus_HasStableValues()
    {
        ((int)CertificateStatus.Active).ShouldBe(1);
        ((int)CertificateStatus.Revoked).ShouldBe(2);
    }

    [Fact]
    public void UserStatus_HasStableValues()
    {
        ((int)UserStatus.Unknown).ShouldBe(0);
        ((int)UserStatus.Student).ShouldBe(1);
        ((int)UserStatus.Graduate).ShouldBe(2);
        ((int)UserStatus.NEET).ShouldBe(3);
        ((int)UserStatus.Other).ShouldBe(99);
    }
}
