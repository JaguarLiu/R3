using R3.Common;
using R3.Models;

namespace R3.Tests;

public class ExpenseBuilderTests
{
    private static readonly List<string> Participants = new() { "醬瓜", "jessie" };

    private static SplitExpense Build(string day) =>
        ExpenseBuilder.FromParsedItem(
            tripId: 1,
            item: new BatchParseItem { Day = day, Item = "香蕉", Total = 100m, SinglePayer = "醬瓜" },
            participants: Participants);

    [Theory]
    [InlineData("第 1 天")]
    [InlineData("第 3 天")]
    [InlineData("第3天")]    // no spaces is still valid
    public void Keeps_valid_day(string day)
    {
        Assert.Equal(day, Build(day).Day);
    }

    [Theory]
    [InlineData("第 X 天")]   // the model's placeholder leaking through
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Day 1")]
    [InlineData("第一天")]    // Chinese numeral, not the "第 N 天" digit format
    public void Falls_back_to_day_one_for_invalid_day(string day)
    {
        Assert.Equal("第 1 天", Build(day).Day);
    }
}
