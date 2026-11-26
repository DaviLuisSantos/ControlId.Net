using System;
using ControlId.Access;
using Xunit;

namespace ControlId.Tests;

public class CardNumberTests
{
    [Theory]
    [InlineData(0, 45678)]
    [InlineData(0, 12345678)]
    [InlineData(123, 45678)]
    [InlineData(123, 12345678)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void IdaEVolta(int facility, int number)
    {
        var value = CardNumber.FromFacility(facility, number);
        Assert.Equal((facility, number), CardNumber.ToFacility(value));
        Assert.Equal(value, CardNumber.Parse(CardNumber.Format(value)));
    }

    [Fact]
    public void FromFacility_UsaFacilityVezes2Elevado32()
    {
        Assert.Equal(123L * 4294967296L + 45678, CardNumber.FromFacility(123, 45678));
        Assert.Equal("123,45678", CardNumber.Format(CardNumber.FromFacility(123, 45678)));
    }

    [Theory]
    [InlineData("123,45678")]
    [InlineData("123.45678")]
    [InlineData("123-45678")]
    [InlineData(" 123,45678 ")]
    public void Parse_AceitaSeparadores(string text)
    {
        Assert.Equal(CardNumber.FromFacility(123, 45678), CardNumber.Parse(text));
    }

    [Fact]
    public void Parse_SoNumero_TemFacilityZero()
    {
        Assert.Equal(12345678L, CardNumber.Parse("12345678"));
        Assert.Equal((0, 12345678), CardNumber.ToFacility(12345678L));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("123,")]
    [InlineData(",456")]
    [InlineData("1,2,3")]
    [InlineData("-5")]
    [InlineData("123,4x")]
    [InlineData("99999999999,1")]
    [InlineData("1,99999999999")]
    public void TryParse_Invalido(string text)
    {
        Assert.False(CardNumber.TryParse(text, out _));
        Assert.Throws<FormatException>(() => CardNumber.Parse(text));
    }

    [Fact]
    public void TryParse_Nulo()
    {
        Assert.False(CardNumber.TryParse(null, out _));
        Assert.Throws<ArgumentNullException>(() => CardNumber.Parse(null!));
    }

    [Fact]
    public void ForaDeFaixa_Lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CardNumber.FromFacility(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardNumber.FromFacility(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardNumber.ToFacility(-1));
    }
}
