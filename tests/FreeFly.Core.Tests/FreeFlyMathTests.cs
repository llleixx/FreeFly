using FreeFly.Core;
using NUnit.Framework;

namespace FreeFly.Core.Tests;

[TestFixture]
public sealed class FreeFlyMathTests
{
    [Test]
    public void BaseSpeedIsUnchangedWithoutModifiers()
    {
        Assert.That(FreeFlyMath.ApplySpeedModifiers(100f, false, false, 2f, 0.35f), Is.EqualTo(100f));
    }

    [Test]
    public void SpeedUpAndSlowDownAreTemporaryMultipliers()
    {
        Assert.That(FreeFlyMath.ApplySpeedModifiers(100f, true, false, 2f, 0.35f), Is.EqualTo(200f));
        Assert.That(FreeFlyMath.ApplySpeedModifiers(100f, false, true, 2f, 0.35f), Is.EqualTo(35f));
        Assert.That(FreeFlyMath.ApplySpeedModifiers(100f, true, true, 2f, 0.35f), Is.EqualTo(70f));
    }

    [Test]
    public void InvalidSpeedInputFallsBackToZero()
    {
        Assert.That(FreeFlyMath.ApplySpeedModifiers(float.NaN, false, false, 2f, 0.35f), Is.EqualTo(0f));
        Assert.That(FreeFlyMath.ApplySpeedModifiers(100f, false, false, float.PositiveInfinity, 0.35f), Is.EqualTo(0f));
    }
}
