namespace Claims.Tests;

// MYT-3108: tests for the product rules in FINDING.md and for the injection paths.
// Seed data (Db.CreateSeeded): member 1 owns claims 101-105, member 2 owns claim 201.
public class MyTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());

    private static long[] Ids(IEnumerable<Claim> claims) => claims.Select(c => c.Id).Order().ToArray();

    // --- Search text ---

    [Theory]
    [InlineData("dental")]   // lower case, partial
    [InlineData("DENTAL")]   // upper case
    [InlineData("Smiles")]   // matches the provider, not the description
    public void Search_FindsPartialMatches_InDescriptionOrProvider_AnyCase(string text)
    {
        Assert.Equal(new long[] { 101, 103 }, Ids(Repo().Search(1, text)));
    }

    [Fact]
    public void Search_NameWithApostrophe_IsFound()
    {
        Assert.Equal(new long[] { 102 }, Ids(Repo().Search(1, "O'Brien")));
    }

    [Fact]
    public void Search_PercentInText_IsLiteral_NotAWildcard()
    {
        // "100%" must match "Copay 100% covered" (104) only, not "Copay 100 dollars" (105).
        Assert.Equal(new long[] { 104 }, Ids(Repo().Search(1, "100%")));
    }

    [Fact]
    public void Search_UnderscoreInText_IsLiteral_NotAWildcard()
    {
        // "_" would match any single character if passed straight to LIKE.
        Assert.Empty(Repo().Search(1, "Eye_exam"));
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("%")]
    [InlineData("Dental")]   // member 2's claim 201 is "Dental crown"
    public void Search_NeverReturnsAnotherMembersClaims(string text)
    {
        Assert.DoesNotContain(201L, Repo().Search(1, text).Select(c => c.Id));
    }

    // --- Sorting ---

    [Theory]
    [InlineData("date", "asc", new long[] { 104, 105 })]     // 2026-10-05, 2026-10-20
    [InlineData("date", "desc", new long[] { 105, 104 })]
    [InlineData("amount", "asc", new long[] { 104, 105 })]   // 0.00, 100.00
    [InlineData("amount", "DESC", new long[] { 105, 104 })]  // direction is case-insensitive
    [InlineData("status", "asc", new long[] { 105, 104 })]   // denied, paid
    public void Search_SortsByAllowedOptions(string sortBy, string sortDir, long[] expected)
    {
        var ids = Repo().Search(1, "City Pharmacy", sortBy, sortDir).Select(c => c.Id);
        Assert.Equal(expected, ids);
    }

    [Theory]
    [InlineData("provider")]   // a real column, but not an allowed option
    [InlineData("submitted_at")]
    [InlineData("bogus")]
    [InlineData("amount --")]
    [InlineData("(CASE WHEN (SELECT max(amount) FROM claims WHERE member_id=2)>500 THEN amount ELSE -amount END)")]
    public void Search_RejectsSortOptionsOutsideTheAllowList(string sortBy)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "City Pharmacy", sortBy, "asc"));
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("asc; DROP TABLE claims")]
    public void Search_RejectsSortDirectionOtherThanAscOrDesc(string sortDir)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "City Pharmacy", "amount", sortDir));
    }
}
