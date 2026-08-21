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

    [TestCase(null, "")]
    [TestCase("  <Gamepad>/leftShoulder  ", "<Gamepad>/leftShoulder")]
    [TestCase("None", "")]
    [TestCase(" none ", "")]
    public void BindingPathsAreNormalized(string? path, string expected)
    {
        Assert.That(FreeFlyInputRules.NormalizeBindingPath(path), Is.EqualTo(expected));
    }

    [Test]
    public void SelectionIndexStaysWithinAvailableOptions()
    {
        Assert.That(FreeFlyInputRules.ClampSelection(-1, 3), Is.EqualTo(0));
        Assert.That(FreeFlyInputRules.ClampSelection(1, 3), Is.EqualTo(1));
        Assert.That(FreeFlyInputRules.ClampSelection(99, 3), Is.EqualTo(2));
        Assert.That(FreeFlyInputRules.ClampSelection(4, 0), Is.EqualTo(0));
    }

    [Test]
    public void FiniteCheckRejectsNonFiniteValues()
    {
        Assert.That(FreeFlyMath.IsFinite(1.5f), Is.True);
        Assert.That(FreeFlyMath.IsFinite(float.NaN), Is.False);
        Assert.That(FreeFlyMath.IsFinite(float.NegativeInfinity), Is.False);
    }
}
